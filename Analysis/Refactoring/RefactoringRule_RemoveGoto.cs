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
        public async Task<Microsoft.CodeAnalysis.Document> ApplyAsync(Microsoft.CodeAnalysis.Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var gotoStatements = root.DescendantNodes()
                .OfType<GotoStatementSyntax>()
                .ToList();

            foreach (var gotoStmt in gotoStatements)
            {
                var labelName = gotoStmt.Expression?.ToString();
                if (string.IsNullOrEmpty(labelName)) continue;

                var method = gotoStmt.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (method?.Body == null) continue;

                var label = method.DescendantNodes()
                    .OfType<LabeledStatementSyntax>()
                    .FirstOrDefault(l => l.Identifier.Text == labelName);
                if (label == null) continue;

                var parentBlock = label.Parent as BlockSyntax;
                if (parentBlock == null) continue;

                // Простая и надёжная логика: удаляем goto и метку, код между ними остаётся
                var statements = parentBlock.Statements.ToList();
                var gotoIndex = statements.IndexOf(gotoStmt);
                var labelIndex = statements.IndexOf(label);

                if (gotoIndex >= 0 && labelIndex >= 0)
                {
                    var newStatements = new List<StatementSyntax>();
                    for (int i = 0; i < statements.Count; i++)
                    {
                        if (i == gotoIndex || i == labelIndex) continue;
                        newStatements.Add(statements[i]);
                    }

                    var newBlock = parentBlock.WithStatements(SyntaxFactory.List(newStatements));
                    editor.ReplaceNode(parentBlock, newBlock);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}