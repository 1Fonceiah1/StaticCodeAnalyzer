using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class SecurityVulnerabilitiesRule : IAnalyzerRule
    {
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            // 1. Поиск SQL-инъекций: строки, содержащие SQL-ключевые слова и конкатенацию
            var stringExpressions = root.DescendantNodes().OfType<BinaryExpressionSyntax>()
                .Where(b => b.Kind() == SyntaxKind.AddExpression)
                .Where(b => IsSqlString(b, semanticModel));

            foreach (var expr in stringExpressions)
            {
                var location = expr.GetLocation();
                if (location != null)
                {
                    var lineSpan = location.GetLineSpan();
                    issues.Add(new AnalysisIssue
                    {
                        Severity = "Критический",
                        FilePath = filePath,
                        LineNumber = lineSpan.StartLinePosition.Line + 1,
                        ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                        Type = "ошибка",
                        Code = "SEC001",
                        Description = "Обнаружена конкатенация строк в SQL-запросе. Это может привести к SQL-инъекции.",
                        Suggestion = "Используйте параметризованные запросы (SqlCommand.Parameters) или ORM с защитой от инъекций.",
                        RuleName = "SecurityVulnerabilities"
                    });
                }
            }

            // 2. Поиск вызовов Process.Start с потенциально опасными аргументами
            var processInvocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression is MemberAccessExpressionSyntax member &&
                            member.Name.Identifier.Text == "Start" &&
                            member.Expression.ToString().Contains("Process"));

            foreach (var invocation in processInvocations)
            {
                // Проверяем, является ли аргумент строковым литералом
                bool isSafe = false;
                if (invocation.ArgumentList.Arguments.Count > 0)
                {
                    var firstArg = invocation.ArgumentList.Arguments[0].Expression;
                    if (firstArg is LiteralExpressionSyntax literal && literal.Kind() == SyntaxKind.StringLiteralExpression)
                        isSafe = true;
                }

                if (!isSafe)
                {
                    var location = invocation.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Высокий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "ошибка",
                            Code = "SEC002",
                            Description = "Вызов Process.Start с аргументом, не являющимся строковым литералом. Это может привести к выполнению произвольного кода.",
                            Suggestion = "Проверяйте и валидируйте входные данные перед передачей в Process.Start.",
                            RuleName = "SecurityVulnerabilities"
                        });
                    }
                }
            }

            return issues;
        }

        private bool IsSqlString(BinaryExpressionSyntax expr, SemanticModel semanticModel)
        {
            // Простейшая эвристика: ищем строки, содержащие SQL-команды
            var sqlKeywords = new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "FROM", "WHERE" };
            var left = expr.Left.ToString().ToUpperInvariant();
            var right = expr.Right.ToString().ToUpperInvariant();
            return sqlKeywords.Any(k => left.Contains(k) || right.Contains(k));
        }
    }
}