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
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classes)
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, cancellationToken);
                if (classSymbol == null) continue;

                var disposableFields = classDecl.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables)
                    .Select(v => new { Variable = v, Symbol = semanticModel.GetDeclaredSymbol(v, cancellationToken) as IFieldSymbol })
                    .Where(x => x.Symbol != null && ImplementsIDisposable(x.Symbol.Type))
                    .ToList();

                if (!disposableFields.Any()) continue;

                if (classSymbol.AllInterfaces.Any(i => i.Name == "IDisposable")) continue;

                var baseList = classDecl.BaseList ?? SyntaxFactory.BaseList();
                var disposableBase = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("System.IDisposable"));
                if (!baseList.Types.Any(t => t.Type.ToString().Contains("IDisposable")))
                {
                    baseList = baseList.AddTypes(disposableBase);
                    var newClassDecl = classDecl.WithBaseList(baseList);
                    editor.ReplaceNode(classDecl, newClassDecl);
                    changed = true;
                }

                var disposeMethod = classSymbol.GetMembers("Dispose").OfType<IMethodSymbol>().FirstOrDefault(m => m.Parameters.Length == 0);
                if (disposeMethod != null) continue;

                var disposeBody = SyntaxFactory.Block();
                foreach (var field in disposableFields)
                {
                    var ifNullCheck = SyntaxFactory.IfStatement(
                        SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression, SyntaxFactory.IdentifierName(field.Variable.Identifier), SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                        SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName(field.Variable.Identifier),
                                    SyntaxFactory.IdentifierName("Dispose")))
                        )));
                    disposeBody = disposeBody.AddStatements(ifNullCheck);
                }
                disposeBody = disposeBody.AddStatements(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("GC"),
                                SyntaxFactory.IdentifierName("SuppressFinalize")))
                        .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.ThisExpression()))))));

                var newDisposeMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Dispose")
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(disposeBody)
                    .NormalizeWhitespace();

                editor.AddMember(classDecl, newDisposeMethod);
                changed = true;
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private bool ImplementsIDisposable(ITypeSymbol type)
        {
            return type.AllInterfaces.Any(i => i.Name == "IDisposable") || type.Name == "IDisposable";
        }
    }
}