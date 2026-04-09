using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_DisposableFields : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "DISP001" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            foreach (var classDecl in classes)
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, cancellationToken);
                if (classSymbol == null) continue;

                // Проверка: уже реализует IDisposable?
                bool alreadyImplements = classSymbol.Interfaces.Any(i => i.Name == "IDisposable") ||
                                         classSymbol.AllInterfaces.Any(i => i.Name == "IDisposable");
                if (alreadyImplements) continue;

                var disposableFields = classDecl.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables.Select(v => new { Field = f, Variable = v }))
                    .Where(x => IsDisposable(x.Variable, semanticModel, cancellationToken))
                    .ToList();

                if (!disposableFields.Any()) continue;

                // Добавляет IDisposable в базовые типы
                if (classDecl.BaseList == null || !classDecl.BaseList.Types.Any(t => t.Type.ToString().Contains("IDisposable")))
                {
                    var baseList = classDecl.BaseList ?? SyntaxFactory.BaseList();
                    baseList = baseList.AddTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("System.IDisposable")));
                    editor.ReplaceNode(classDecl, classDecl.WithBaseList(baseList));
                    changed = true;
                }

                // Генерирует Dispose, если его нет
                if (classSymbol.GetMembers("Dispose").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0))
                    continue;

                var disposeBody = SyntaxFactory.Block();
                foreach (var field in disposableFields)
                {
                    var nullCheck = SyntaxFactory.IfStatement(
                        SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression,
                            SyntaxFactory.IdentifierName(field.Variable.Identifier),
                            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                        SyntaxFactory.Block(
                            SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(field.Variable.Identifier),
                                        SyntaxFactory.IdentifierName("Dispose"))))));
                    disposeBody = disposeBody.AddStatements(nullCheck);
                }

                var suppressFinalize = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("GC"),
                            SyntaxFactory.IdentifierName("SuppressFinalize")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(SyntaxFactory.ThisExpression())))));

                var disposeMethod = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Dispose")
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(disposeBody.AddStatements(suppressFinalize))
                    .NormalizeWhitespace();

                editor.AddMember(classDecl, disposeMethod);
                changed = true;
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private bool IsDisposable(VariableDeclaratorSyntax variable, SemanticModel model, CancellationToken ct)
        {
            var symbol = model.GetDeclaredSymbol(variable, ct) as IFieldSymbol;
            if (symbol?.Type == null) return false;
            return symbol.Type.SpecialType == SpecialType.System_IDisposable ||
                   symbol.Type.Interfaces.Any(i => i.Name == "IDisposable");
        }
    }
}