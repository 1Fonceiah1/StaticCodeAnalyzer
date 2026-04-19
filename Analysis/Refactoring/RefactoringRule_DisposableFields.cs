using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_DisposableFields : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "DISP001" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            bool changed = false;

            List<ClassDeclarationSyntax> classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            foreach (ClassDeclarationSyntax classDecl in classes)
            {
                INamedTypeSymbol? classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
                if (classSymbol == null) continue;

                // Проверяет, реализует ли класс уже IDisposable
                bool alreadyImplements = classSymbol.Interfaces.Any(i => i.Name == "IDisposable") ||
                                         classSymbol.AllInterfaces.Any(i => i.Name == "IDisposable");
                if (alreadyImplements) continue;

                // Собирает поля, реализующие IDisposable
                List<DisposableFieldInfo> disposableFieldsInfo = new List<DisposableFieldInfo>();
                foreach (FieldDeclarationSyntax fieldDecl in classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    foreach (VariableDeclaratorSyntax variable in fieldDecl.Declaration.Variables)
                    {
                        if (IsDisposable(variable, semanticModel))
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

                // Проверяет наличие метода Dispose() без параметров
                if (classSymbol.GetMembers("Dispose").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0))
                    continue;

                // Создаёт изменённую версию класса
                ClassDeclarationSyntax newClass = classDecl;

                // 1. Добавляет IDisposable в список базовых типов
                if (newClass.BaseList == null || !newClass.BaseList.Types.Any(t => t.Type.ToString().Contains("IDisposable")))
                {
                    BaseListSyntax baseList = newClass.BaseList ?? SyntaxFactory.BaseList();
                    baseList = baseList.AddTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("System.IDisposable")));
                    newClass = newClass.WithBaseList(baseList);
                }

                // 2. Добавляет поле _disposed
                FieldDeclarationSyntax disposedField = SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.ParseTypeName("bool"),
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator("_disposed")
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression))))))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                    .NormalizeWhitespace();
                newClass = newClass.AddMembers(disposedField);

                // 3. Генерирует метод Dispose(bool disposing)
                BlockSyntax disposeBoolBody = GenerateDisposeBoolBody(disposableFieldsInfo);
                MethodDeclarationSyntax disposeBoolMethod = SyntaxFactory.MethodDeclaration(
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

                // 4. Генерирует публичный метод Dispose()
                BlockSyntax publicDisposeBody = SyntaxFactory.Block(
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
                MethodDeclarationSyntax publicDisposeMethod = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Dispose")
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(publicDisposeBody)
                    .NormalizeWhitespace();
                newClass = newClass.AddMembers(publicDisposeMethod);

                // Заменяет старый класс новым
                editor.ReplaceNode(classDecl, newClass);
                changed = true;
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private BlockSyntax GenerateDisposeBoolBody(List<DisposableFieldInfo> disposableFields)
        {
            List<StatementSyntax> statements = new List<StatementSyntax>();

            // Добавляет проверку _disposed
            statements.Add(SyntaxFactory.IfStatement(
                SyntaxFactory.IdentifierName("_disposed"),
                SyntaxFactory.ReturnStatement()));

            // Генерирует блок для управляемых ресурсов
            List<StatementSyntax> disposingBlockStatements = new List<StatementSyntax>();
            foreach (DisposableFieldInfo field in disposableFields)
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

            // Устанавливает флаг disposed
            statements.Add(SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName("_disposed"),
                    SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))));

            return SyntaxFactory.Block(statements);
        }

        private bool IsDisposable(VariableDeclaratorSyntax variable, SemanticModel model)
        {
            IFieldSymbol? symbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;
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