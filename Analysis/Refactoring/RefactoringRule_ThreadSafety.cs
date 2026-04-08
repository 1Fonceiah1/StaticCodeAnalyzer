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
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classes)
            {
                // Ищет изменяемые поля (не readonly, не const)
                var mutableFields = classDecl.Members
                    .OfType<FieldDeclarationSyntax>()
                    .Where(f => !f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) && !f.Modifiers.Any(SyntaxKind.ConstKeyword))
                    .SelectMany(f => f.Declaration.Variables)
                    .ToList();

                if (!mutableFields.Any()) continue;

                // Проверяет, есть ли уже поле _lock
                bool hasLockField = classDecl.Members
                    .OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables)
                    .Any(v => v.Identifier.Text == "_lock");

                if (!hasLockField)
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

                // Найходит публичные методы, которые используют изменяемые поля
                var methods = classDecl.Members.OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) && m.Body != null);

                foreach (var method in methods)
                {
                    bool usesMutableField = method.DescendantNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Any(id => mutableFields.Any(f => f.Identifier.Text == id.Identifier.Text));

                    if (usesMutableField && !IsAlreadyLocked(method))
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
            // Проверяет, не обёрнут ли уже метод в lock
            if (method.Body?.Statements.FirstOrDefault() is LockStatementSyntax)
                return true;
            return false;
        }
    }
}