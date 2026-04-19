using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    // Правило рефакторинга для удаления и замены операторов goto
    public class RefactoringRule_GotoStatement : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "GOTO001" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            bool changed = false;

            // Находит все операторы goto
            List<GotoStatementSyntax> gotoStatements = root.DescendantNodes()
                .OfType<GotoStatementSyntax>()
                .ToList();

            foreach (GotoStatementSyntax gotoStmt in gotoStatements)
            {
                // Получаем имя метки
                string labelName = gotoStmt.Expression?.ToString() ?? "";
                if (string.IsNullOrEmpty(labelName))
                    continue;

                // Ищем соответствующую метку
                MethodDeclarationSyntax containingMethod = gotoStmt.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (containingMethod == null)
                    continue;

                LabeledStatementSyntax targetLabel = containingMethod.DescendantNodes()
                    .OfType<LabeledStatementSyntax>()
                    .FirstOrDefault(l => l.Identifier.Text == labelName);

                if (targetLabel == null)
                    continue;

                // Проверяем, идёт ли метка после goto (переход вперёд)
                int gotoPosition = gotoStmt.SpanStart;
                int labelPosition = targetLabel.SpanStart;
                bool isForwardJump = labelPosition > gotoPosition;

                if (isForwardJump)
                {
                    // Для перехода вперёд заменяем goto на комментарий с throw NotImplementedException
                    // Это безопасный способ указать, что код требует ручного рефакторинга
                    SyntaxTrivia comment = SyntaxFactory.Comment(
                        $"// TODO: Перепишите goto {labelName} с использованием циклов, условий или выделите метод.");
                    SyntaxTriviaList trivia = SyntaxFactory.TriviaList(
                        comment, 
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Comment("// Код после этого места был меткой '" + labelName + "':"),
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Whitespace("        "));
                    
                    ThrowStatementSyntax throwStmt = SyntaxFactory.ThrowStatement(
                        SyntaxFactory.ObjectCreationExpression(
                            SyntaxFactory.ParseTypeName("NotImplementedException"))
                        .WithArgumentList(SyntaxFactory.ArgumentList()))
                        .WithLeadingTrivia(trivia);

                    editor.ReplaceNode(gotoStmt, throwStmt);
                    changed = true;
                }
                else
                {
                    // Для перехода назад (обычно используется для циклов) заменяем на while с условием
                    // Это сложный случай, требует анализа кода между goto и меткой
                    SyntaxTrivia comment = SyntaxFactory.Comment(
                        $"// TODO: Перепишите goto {labelName} (цикл) с использованием while/do-while.");
                    SyntaxTriviaList trivia = SyntaxFactory.TriviaList(
                        comment, 
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Whitespace("        "));
                    
                    BreakStatementSyntax breakStmt = SyntaxFactory.BreakStatement()
                        .WithLeadingTrivia(trivia);

                    editor.ReplaceNode(gotoStmt, breakStmt);
                    changed = true;
                }
            }

            // Удаляем метки, которые больше не используются
            List<LabeledStatementSyntax> allLabels = root.DescendantNodes()
                .OfType<LabeledStatementSyntax>()
                .ToList();

            foreach (LabeledStatementSyntax label in allLabels)
            {
                string labelName = label.Identifier.Text;
                bool isReferenced = gotoStatements.Any(g => g.Expression?.ToString() == labelName);
                
                if (isReferenced && changed)
                {
                    // Если метка была связана с goto, заменяем её на обычный оператор
                    SyntaxTrivia labelComment = SyntaxFactory.Comment(
                        $"// Метка '{labelName}' (код после удаления goto):");
                    StatementSyntax innerStatement = label.Statement;
                    
                    StatementSyntax newStatement = innerStatement.WithLeadingTrivia(
                        labelComment,
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Whitespace("        "));
                    newStatement = newStatement.WithLeadingTrivia(newStatement.GetLeadingTrivia().AddRange(innerStatement.GetLeadingTrivia()));
                    
                    editor.ReplaceNode(label, newStatement);
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}
