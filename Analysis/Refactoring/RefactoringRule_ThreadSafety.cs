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
    public class RefactoringRule_ThreadSafety : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "THR001" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            foreach (var classDecl in classes)
            {
                // Находит изменяемые поля
                var mutableFields = classDecl.Members
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
                    var lockField = SyntaxFactory.FieldDeclaration(
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
                var methods = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                               m.Body != null &&
                               !m.Modifiers.Any(SyntaxKind.AsyncKeyword));

                foreach (var method in methods)
                {
                    bool usesMutable = method.DescendantNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Any(id => mutableFields.Any(f => f.Identifier.Text == id.Identifier.Text));

                    if (usesMutable && !IsAlreadyLocked(method))
                    {
                        var newBody = SyntaxFactory.Block(
                            SyntaxFactory.LockStatement(
                                SyntaxFactory.IdentifierName("_lock"),
                                method.Body));

                        var newMethod = method.WithBody(newBody);
                        editor.ReplaceNode(method, newMethod);
                        changed = true;
                    }
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private bool IsAlreadyLocked(MethodDeclarationSyntax method)
        {
            if (method.Body == null) return false;

            // Проверяем наличие любого lock-оператора в теле
            var lockNodes = method.Body.DescendantNodes().OfType<LockStatementSyntax>();
            if (lockNodes.Any())
                return true;

            // Проверяем вызовы Monitor.Enter и подобные
            var monitorCalls = method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                              ma.Expression.ToString() == "Monitor" &&
                              (ma.Name.Identifier.Text == "Enter" || ma.Name.Identifier.Text == "TryEnter"));
            if (monitorCalls.Any())
                return true;

            return false;
        }
    }
}