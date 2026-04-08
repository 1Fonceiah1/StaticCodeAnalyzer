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
    public class RefactoringRule_SeparateOutput : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "SEP001" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var consoleWrites = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                              ma.Expression is IdentifierNameSyntax { Identifier.Text: "Console" } &&
                              (ma.Name.Identifier.Text == "WriteLine" || ma.Name.Identifier.Text == "Write"))
                .ToList();

            if (!consoleWrites.Any()) return document;

            var classDecl = consoleWrites.First().FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (classDecl == null) return document;

            // Проверяем, есть ли уже метод с таким именем
            var existingMethods = classDecl.Members.OfType<MethodDeclarationSyntax>();
            if (existingMethods.Any(m => m.Identifier.Text == "DisplayOutput"))
                return document;

            // Создаём метод DisplayOutput
            var outputMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    "DisplayOutput")
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("message"))
                            .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword))))))
                .WithBody(SyntaxFactory.Block(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("Console"),
                                SyntaxFactory.IdentifierName("WriteLine")),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("message"))))))))
                .NormalizeWhitespace();

            editor.AddMember(classDecl, outputMethod);
            changed = true;

            // Заменяем вызовы Console.WriteLine на DisplayOutput
            foreach (var write in consoleWrites)
            {
                var arg = write.ArgumentList.Arguments.FirstOrDefault();
                if (arg != null)
                {
                    var newCall = SyntaxFactory.InvocationExpression(
                            SyntaxFactory.IdentifierName("DisplayOutput"),
                            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(arg)))
                        .WithTriviaFrom(write);
                    editor.ReplaceNode(write, newCall);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}