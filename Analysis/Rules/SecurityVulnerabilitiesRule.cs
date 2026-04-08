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
        private static readonly string[] SqlKeywords = { "SELECT", "INSERT", "UPDATE", "DELETE", "FROM", "WHERE", "JOIN", "UNION" };

        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            // SQL-инъекции: конкатенация строк в запросах
            var stringConcatenations = root.DescendantNodes()
                .OfType<BinaryExpressionSyntax>()
                .Where(b => b.IsKind(SyntaxKind.AddExpression) && ContainsSqlKeyword(b));

            foreach (var expr in stringConcatenations)
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
                        Description = "Возможная уязвимость SQL-инъекции: конкатенация строк в запросе.",
                        Suggestion = "Используйте параметризованные запросы или ORM с защитой от инъекций.",
                        RuleName = "SecurityVulnerabilities"
                    });
                }
            }

            // Process.Start с непроверенными аргументами
            var processCalls = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsProcessStartCall);

            foreach (var call in processCalls)
            {
                var firstArg = call.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                if (firstArg is not LiteralExpressionSyntax)
                {
                    var location = call.GetLocation();
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
                            Description = "Process.Start вызван с непроверенным аргументом — риск выполнения произвольного кода.",
                            Suggestion = "Валидируйте входные данные перед передачей в Process.Start.",
                            RuleName = "SecurityVulnerabilities"
                        });
                    }
                }
            }

            return Task.FromResult(issues);
        }

        private bool ContainsSqlKeyword(BinaryExpressionSyntax expr)
        {
            var text = expr.ToString().ToUpperInvariant();
            return SqlKeywords.Any(k => text.Contains(k));
        }

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