using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Linq;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_ThreadSafety : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "THR001" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            bool changed = false;

            List<ClassDeclarationSyntax> classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            foreach (ClassDeclarationSyntax classDecl in classes)
            {
                // Находит изменяемые поля (не readonly и не const)
                List<VariableDeclaratorSyntax> mutableFields = classDecl.Members
                    .OfType<FieldDeclarationSyntax>()
                    .Where(f => !f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) && !f.Modifiers.Any(SyntaxKind.ConstKeyword))
                    .SelectMany(f => f.Declaration.Variables)
                    .ToList();

                if (!mutableFields.Any()) continue;

                // Проверяет наличие поля _lock
                bool hasLock = classDecl.Members
                    .OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables)
                    .Any(v => v.Identifier.Text == "_lock");

                if (!hasLock)
                {
                    // Добавляет приватное поле _lock
                    FieldDeclarationSyntax lockField = SyntaxFactory.FieldDeclaration(
                            SyntaxFactory.VariableDeclaration(
                                SyntaxFactory.ParseTypeName("object"),
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.VariableDeclarator("_lock")
                                        .WithInitializer(SyntaxFactory.EqualsValueClause(
                                            SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName("object"))
                                                .WithArgumentList(SyntaxFactory.ArgumentList()))))))
                        .WithModifiers(SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                            SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
                        .NormalizeWhitespace();

                    editor.AddMember(classDecl, lockField);
                    changed = true;
                }

                // Находит публичные не-async методы, использующие изменяемые поля
                List<MethodDeclarationSyntax> methods = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                               m.Body != null &&
                               !m.Modifiers.Any(SyntaxKind.AsyncKeyword))
                    .ToList();

                foreach (MethodDeclarationSyntax method in methods)
                {
                    bool usesMutable = method.DescendantNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Any(id => mutableFields.Any(f => f.Identifier.Text == id.Identifier.Text));

                    if (usesMutable && !IsAlreadyLocked(method))
                    {
                        // Оборачивает тело метода в lock(_lock)
                        BlockSyntax newBody = SyntaxFactory.Block(
                            SyntaxFactory.LockStatement(
                                SyntaxFactory.IdentifierName("_lock"),
                                method.Body));

                        MethodDeclarationSyntax newMethod = method.WithBody(newBody);
                        editor.ReplaceNode(method, newMethod);
                        changed = true;
                    }
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        // Проверяет, содержит ли метод уже механизмы синхронизации (lock или Monitor)
        private bool IsAlreadyLocked(MethodDeclarationSyntax method)
        {
            if (method.Body == null) return false;

            // Проверяет наличие lock-оператора
            IEnumerable<LockStatementSyntax> lockNodes = method.Body.DescendantNodes().OfType<LockStatementSyntax>();
            if (lockNodes.Any())
                return true;

            // Проверяет вызовы Monitor.Enter и подобные
            IEnumerable<InvocationExpressionSyntax> monitorCalls = method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                              ma.Expression.ToString() == "Monitor" &&
                              (ma.Name.Identifier.Text == "Enter" || ma.Name.Identifier.Text == "TryEnter"));
            if (monitorCalls.Any())
                return true;

            return false;
        }
    }
}