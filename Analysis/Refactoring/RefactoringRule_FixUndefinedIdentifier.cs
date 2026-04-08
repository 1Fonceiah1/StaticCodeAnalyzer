using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_FixUndefinedIdentifier : IRefactoringRule
    {
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(id => !(id.Parent is MemberAccessExpressionSyntax) && !IsDeclared(id, semanticModel, cancellationToken))
                .ToList();

            foreach (var id in identifiers)
            {
                var containingClass = id.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (containingClass != null)
                {
                    var constValue = GuessConstantValue(id.Identifier.Text);
                    if (constValue.HasValue)
                    {
                        var constDecl = SyntaxFactory.FieldDeclaration(
                            SyntaxFactory.VariableDeclaration(
                                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)),
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.VariableDeclarator(id.Identifier.Text)
                                        .WithInitializer(SyntaxFactory.EqualsValueClause(
                                            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(constValue.Value)))))))
                            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ConstKeyword)))
                            .NormalizeWhitespace();

                        editor.AddMember(containingClass, constDecl);
                        changed = true;
                    }
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private bool IsDeclared(IdentifierNameSyntax id, SemanticModel model, CancellationToken token)
        {
            var symbol = model.GetSymbolInfo(id, token).Symbol;
            return symbol != null;
        }

        private int? GuessConstantValue(string name)
        {
            switch (name.ToLower())
            {
                case "magic": return 100;
                case "maxlimit": return 100;
                case "max": return 100;
                case "limit": return 10;
                case "step": return 2;
                case "count": return 5;
                default: return null;
            }
        }
    }
}