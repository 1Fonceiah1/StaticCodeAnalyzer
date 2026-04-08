using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class DuplicateMethodCallsRule : IAnalyzerRule
    {
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (method.Body == null) continue;

                var statements = method.Body.Statements.ToList();
                // Ищет подряд идущие одинаковые вызовы (простое дублирование)
                for (int i = 0; i < statements.Count - 1; i++)
                {
                    if (statements[i] is ExpressionStatementSyntax firstStmt &&
                        statements[i + 1] is ExpressionStatementSyntax secondStmt &&
                        firstStmt.Expression.ToString() == secondStmt.Expression.ToString())
                    {
                        var location = secondStmt.GetLocation();
                        if (location == null) continue;

                        var lineSpan = location.GetLineSpan();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Низкий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "запах кода",
                            Code = "DUP002",
                            Description = $"Обнаружен повторяющийся вызов метода '{firstStmt.Expression.ToString()}' (дважды подряд).",
                            Suggestion = "Удалите лишний вызов или сохраните результат в переменную, если метод имеет побочные эффекты и должен быть вызван дважды.",
                            RuleName = "DuplicateMethodCalls"
                        });
                    }
                }
            }

            return issues;
        }
    }
}