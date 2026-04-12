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

                // Уже реализует IDisposable?
                bool alreadyImplements = classSymbol.Interfaces.Any(i => i.Name == "IDisposable") ||
                                         classSymbol.AllInterfaces.Any(i => i.Name == "IDisposable");
                if (alreadyImplements) continue;

                // Собираем поля, реализующие IDisposable
                var disposableFieldsInfo = new List<DisposableFieldInfo>();
                foreach (var fieldDecl in classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    foreach (var variable in fieldDecl.Declaration.Variables)
                    {
                        if (IsDisposable(variable, semanticModel, cancellationToken))
                        {
                            disposableFieldsInfo.Add(new DisposableFieldInfo
                            {
                                FieldDeclaration = fieldDecl,
                                Variable = variable
                            });
                        }
                    }
                }

                if (!disposableFieldsInfo.Any()) continue;

                // Проверяем, есть ли уже метод Dispose()
                if (classSymbol.GetMembers("Dispose").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0))
                    continue;

                // Создаём новый класс со всеми изменениями
                ClassDeclarationSyntax newClass = classDecl;

                // 1. Добавляем IDisposable к базовым типам
                if (newClass.BaseList == null || !newClass.BaseList.Types.Any(t => t.Type.ToString().Contains("IDisposable")))
                {
                    var baseList = newClass.BaseList ?? SyntaxFactory.BaseList();
                    baseList = baseList.AddTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("System.IDisposable")));
                    newClass = newClass.WithBaseList(baseList);
                }

                // 2. Добавляем поле _disposed
                var disposedField = SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.ParseTypeName("bool"),
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator("_disposed")
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression))))))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                    .NormalizeWhitespace();
                newClass = newClass.AddMembers(disposedField);

                // 3. Генерируем Dispose(bool disposing)
                var disposeBoolBody = GenerateDisposeBoolBody(disposableFieldsInfo);
                var disposeBoolMethod = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Dispose")
                    .WithModifiers(SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                        SyntaxFactory.Token(SyntaxKind.VirtualKeyword)))
                    .WithParameterList(SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("disposing"))
                                .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword))))))
                    .WithBody(disposeBoolBody)
                    .NormalizeWhitespace();
                newClass = newClass.AddMembers(disposeBoolMethod);

                // 4. Генерируем публичный Dispose()
                var publicDisposeBody = SyntaxFactory.Block(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.IdentifierName("Dispose"),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)))))),
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("GC"),
                                SyntaxFactory.IdentifierName("SuppressFinalize")),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(SyntaxFactory.ThisExpression()))))));
                var publicDisposeMethod = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Dispose")
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(publicDisposeBody)
                    .NormalizeWhitespace();
                newClass = newClass.AddMembers(publicDisposeMethod);

                // Заменяем старый класс новым (одна операция)
                editor.ReplaceNode(classDecl, newClass);
                changed = true;
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private BlockSyntax GenerateDisposeBoolBody(List<DisposableFieldInfo> disposableFields)
        {
            var statements = new List<StatementSyntax>();

            // if (_disposed) return;
            statements.Add(SyntaxFactory.IfStatement(
                SyntaxFactory.IdentifierName("_disposed"),
                SyntaxFactory.ReturnStatement()));

            // if (disposing) { ... }
            var disposingBlockStatements = new List<StatementSyntax>();
            foreach (var field in disposableFields)
            {
                disposingBlockStatements.Add(SyntaxFactory.IfStatement(
                    SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression,
                        SyntaxFactory.IdentifierName(field.Variable.Identifier),
                        SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    SyntaxFactory.Block(
                        SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName(field.Variable.Identifier),
                                    SyntaxFactory.IdentifierName("Dispose")))))));
            }
            statements.Add(SyntaxFactory.IfStatement(
                SyntaxFactory.IdentifierName("disposing"),
                SyntaxFactory.Block(disposingBlockStatements)));

            // _disposed = true;
            statements.Add(SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName("_disposed"),
                    SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))));

            return SyntaxFactory.Block(statements);
        }

        private bool IsDisposable(VariableDeclaratorSyntax variable, SemanticModel model, CancellationToken ct)
        {
            var symbol = model.GetDeclaredSymbol(variable, ct) as IFieldSymbol;
            if (symbol?.Type == null) return false;
            return symbol.Type.SpecialType == SpecialType.System_IDisposable ||
                   symbol.Type.Interfaces.Any(i => i.Name == "IDisposable");
        }

        private class DisposableFieldInfo
        {
            public FieldDeclarationSyntax FieldDeclaration { get; set; }
            public VariableDeclaratorSyntax Variable { get; set; }
        }
    }
}