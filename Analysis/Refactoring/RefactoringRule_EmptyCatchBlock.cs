using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Linq;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_EmptyCatchBlock : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "ERR001" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            bool changed = false;

            List<CatchClauseSyntax> emptyCatches = root.DescendantNodes()
                .OfType<CatchClauseSyntax>()
                .Where(c => c.Block == null || c.Block.Statements.Count == 0)
                .ToList();

            foreach (CatchClauseSyntax catchClause in emptyCatches)
            {
                string exceptionVar = catchClause.Declaration?.Identifier.Text ?? "ex";
                
                // Добавляет комментарий с пояснением
                SyntaxTrivia comment = SyntaxFactory.Comment($"// TODO: Обработайте исключение или запишите в лог. Переменная: {exceptionVar}");
                SyntaxTriviaList trivia = SyntaxFactory.TriviaList(comment, SyntaxFactory.CarriageReturnLineFeed);
                
                // Создаёт оператор throw
                ThrowStatementSyntax throwStmt = SyntaxFactory.ThrowStatement().WithLeadingTrivia(trivia);
                BlockSyntax newBlock = SyntaxFactory.Block(throwStmt);
                
                CatchClauseSyntax newCatch = catchClause.WithBlock(newBlock);
                
                // Выполняет безопасную замену узла
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