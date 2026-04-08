using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_EmptyCatchBlock : IRefactoringRule
    {
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var emptyCatches = root.DescendantNodes()
                .OfType<CatchClauseSyntax>()
                .Where(c => c.Block == null || c.Block.Statements.Count == 0)
                .ToList();

            foreach (var catchClause in emptyCatches)
            {
                var comment = SyntaxFactory.Comment("// TODO: Log or handle exception properly");
                var newBlock = SyntaxFactory.Block(SyntaxFactory.EmptyStatement().WithLeadingTrivia(comment));
                var newCatch = catchClause.WithBlock(newBlock);
                editor.ReplaceNode(catchClause, newCatch);
                changed = true;
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}