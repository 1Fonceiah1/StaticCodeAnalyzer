using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Выявляет потенциальные уязвимости безопасности: SQL-инъекции и небезопасный запуск процессов
    public class SecurityVulnerabilitiesRule : IAnalyzerRule
    {
        // Ключевые слова SQL для выявления конкатенации запросов
        private static readonly string[] SqlKeywords = { "SELECT", "INSERT", "UPDATE", "DELETE", "FROM", "WHERE", "JOIN", "UNION" };

        public List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();

            // Проверяет SQL-инъекции через конкатенацию строк
            IEnumerable<BinaryExpressionSyntax> stringConcatenations = root.DescendantNodes()
                .OfType<BinaryExpressionSyntax>()
                .Where(b => b.IsKind(SyntaxKind.AddExpression) && ContainsSqlKeyword(b));

            foreach (BinaryExpressionSyntax expr in stringConcatenations)
            {
                Microsoft.CodeAnalysis.Location? location = expr.GetLocation();
                if (location != null)
                {
                    FileLinePositionSpan lineSpan = location.GetLineSpan();
                    MethodDeclarationSyntax? containingMethod = expr.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                    ClassDeclarationSyntax? containingClass = expr.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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
                        RuleName = "SecurityVulnerabilities",
                        ContainingTypeName = containingClass?.Identifier.Text,
                        MethodName = containingMethod?.Identifier.Text
                    });
                }
            }

            // Проверяет вызовы Process.Start с непроверенными аргументами
            IEnumerable<InvocationExpressionSyntax> processCalls = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsProcessStartCall);

            foreach (InvocationExpressionSyntax call in processCalls)
            {
                ExpressionSyntax? firstArg = call.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                if (firstArg is not LiteralExpressionSyntax)
                {
                    Microsoft.CodeAnalysis.Location? location = call.GetLocation();
                    if (location != null)
                    {
                        FileLinePositionSpan lineSpan = location.GetLineSpan();
                        MethodDeclarationSyntax? containingMethod = call.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                        ClassDeclarationSyntax? containingClass = call.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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
                            RuleName = "SecurityVulnerabilities",
                            ContainingTypeName = containingClass?.Identifier.Text,
                            MethodName = containingMethod?.Identifier.Text
                        });
                    }
                }
            }

            return issues;
        }

        // Проверяет, содержит ли выражение ключевое слово SQL
        private bool ContainsSqlKeyword(BinaryExpressionSyntax expr)
        {
            string text = expr.ToString().ToUpperInvariant();
            return SqlKeywords.Any(k => text.Contains(k));
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