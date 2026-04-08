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
    public class RefactoringRule_EmptyCatchBlock : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "ERR001" };

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
                var exceptionVar = catchClause.Declaration?.Identifier.Text ?? "ex";
                
                // Создаём комментарий безопасным способом
                var comment = SyntaxFactory.Comment($"// TODO: Обработайте исключение или запишите в лог. Переменная: {exceptionVar}");
                var trivia = SyntaxFactory.TriviaList(comment, SyntaxFactory.CarriageReturnLineFeed);
                
                // Создаём оператор throw с комментарием
                var throwStmt = SyntaxFactory.ThrowStatement().WithLeadingTrivia(trivia);
                var newBlock = SyntaxFactory.Block(throwStmt);
                
                var newCatch = catchClause.WithBlock(newBlock);
                
                // Безопасная замена узла
                if (catchClause.SyntaxTree == root.SyntaxTree)
                {
                    editor.ReplaceNode(catchClause, newCatch);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}