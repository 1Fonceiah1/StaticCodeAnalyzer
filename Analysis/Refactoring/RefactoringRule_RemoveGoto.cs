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
    public class RefactoringRule_RemoveGoto : IRefactoringRule
    {
        // Преобразует конструкции с goto в if-else без goto
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var gotoStatements = root.DescendantNodes().OfType<GotoStatementSyntax>().ToList();
            foreach (var gotoStmt in gotoStatements)
            {
                var labelName = gotoStmt.Expression?.ToString();
                if (string.IsNullOrEmpty(labelName)) continue;

                var method = gotoStmt.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (method == null) continue;

                var label = method.DescendantNodes().OfType<LabeledStatementSyntax>().FirstOrDefault(l => l.Identifier.Text == labelName);
                if (label == null) continue;

                // Находит окружающий if, который содержит этот goto
                var ifStmt = gotoStmt.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();
                if (ifStmt != null && (ifStmt.Statement == gotoStmt || (ifStmt.Statement is BlockSyntax block && block.Statements.Contains(gotoStmt))))
                {
                    // Инвертирует условие
                    var condition = ifStmt.Condition;
                    var invertedCondition = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SyntaxFactory.ParenthesizedExpression(condition));

                    // Собирает операторы между if и меткой
                    var statementsBeforeLabel = new List<SyntaxNode>();
                    var parent = label.Parent;
                    if (parent != null)
                    {
                        statementsBeforeLabel = parent.ChildNodes().TakeWhile(n => n != label).ToList();
                    }

                    var newBlock = SyntaxFactory.Block(statementsBeforeLabel.OfType<StatementSyntax>());
                    var elseBlock = SyntaxFactory.Block(label.Statement);

                    var newIf = SyntaxFactory.IfStatement(invertedCondition, newBlock, SyntaxFactory.ElseClause(elseBlock));

                    editor.ReplaceNode(ifStmt, newIf);
                    editor.RemoveNode(gotoStmt);
                    editor.RemoveNode(label);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}