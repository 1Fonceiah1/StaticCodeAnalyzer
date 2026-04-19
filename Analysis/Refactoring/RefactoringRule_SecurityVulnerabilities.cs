using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Linq;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    // Правило рефакторинга для исправления уязвимостей безопасности: SQL-инъекций и небезопасного Process.Start
    public class RefactoringRule_SecurityVulnerabilities : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "SEC001", "SEC002" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            bool changed = false;

            // Исправляем SQL-инъекции: добавляем комментарий с рекомендацией использовать параметризованные запросы
            List<BinaryExpressionSyntax> stringConcatenations = root.DescendantNodes()
                .OfType<BinaryExpressionSyntax>()
                .Where(b => b.IsKind(SyntaxKind.AddExpression) && ContainsSqlKeyword(b))
                .ToList();

            foreach (BinaryExpressionSyntax expr in stringConcatenations)
            {
                // Добавляем комментарий перед выражением с предупреждением
                SyntaxTrivia warningComment = SyntaxFactory.Comment(
                    "// SECURITY WARNING: Обнаружена возможная SQL-инъекция. Используйте параметризованные запросы:");
                SyntaxTrivia exampleComment = SyntaxFactory.Comment(
                    "// var cmd = new SqlCommand(\"SELECT * FROM Users WHERE Id = @id\"); cmd.Parameters.AddWithValue(\"@id\", value);");

                SyntaxTriviaList newTrivia = SyntaxFactory.TriviaList(
                    warningComment,
                    SyntaxFactory.CarriageReturnLineFeed,
                    exampleComment,
                    SyntaxFactory.CarriageReturnLineFeed,
                    SyntaxFactory.Whitespace("        "));

                BinaryExpressionSyntax newExpr = expr.WithLeadingTrivia(newTrivia);
                editor.ReplaceNode(expr, newExpr);
                changed = true;
            }

            // Исправляем небезопасный Process.Start: добавляем валидацию аргумента
            List<InvocationExpressionSyntax> processCalls = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsProcessStartCall)
                .ToList();

            foreach (InvocationExpressionSyntax call in processCalls)
            {
                ExpressionSyntax? firstArg = call.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                if (firstArg is not LiteralExpressionSyntax)
                {
                    // Добавляем комментарий с рекомендацией по валидации
                    SyntaxTrivia validationComment = SyntaxFactory.Comment(
                        "// SECURITY WARNING: Проверьте и валидируйте аргумент перед передачей в Process.Start:");
                    SyntaxTrivia exampleComment = SyntaxFactory.Comment(
                        "// if (string.IsNullOrWhiteSpace(input) || input.Contains(\"..\") || input.Contains(\"/\")) throw new ArgumentException();");

                    SyntaxTriviaList newTrivia = SyntaxFactory.TriviaList(
                        validationComment,
                        SyntaxFactory.CarriageReturnLineFeed,
                        exampleComment,
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Whitespace("        "));

                    InvocationExpressionSyntax newCall = call.WithLeadingTrivia(newTrivia);
                    editor.ReplaceNode(call, newCall);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        // Проверяет, содержит ли выражение ключевое слово SQL
        private bool ContainsSqlKeyword(BinaryExpressionSyntax expr)
        {
            string[] sqlKeywords = { "SELECT", "INSERT", "UPDATE", "DELETE", "FROM", "WHERE", "JOIN", "UNION" };
            string text = expr.ToString().ToUpperInvariant();
            return sqlKeywords.Any(k => text.Contains(k));
        }

        // Определяет, является ли вызов Process.Start
        private bool IsProcessStartCall(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax member)
            {
                return member.Name.Identifier.Text == "Start" &&
                       member.Expression.ToString().Contains("Process");
            }
            return false;
        }
    }
}
