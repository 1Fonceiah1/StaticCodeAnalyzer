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
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var method in methods)
            {
                if (method.Body == null) continue;

                var statements = method.Body.Statements.OfType<ExpressionStatementSyntax>().ToList();
                for (int i = 0; i < statements.Count - 1; i++)
                {
                    var expr1 = statements[i].Expression.ToString();
                    var expr2 = statements[i + 1].Expression.ToString();

                    if (expr1 == expr2 && !IsSideEffectFree(expr1))
                    {
                        var location = statements[i + 1].GetLocation();
                        if (location != null)
                        {
                            var lineSpan = location.GetLineSpan();
                            issues.Add(new AnalysisIssue
                            {
                                Severity = "Низкий",
                                FilePath = filePath,
                                LineNumber = lineSpan.StartLinePosition.Line + 1,
                                ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                                Type = "запах кода",
                                Code = "DUP002",
                                Description = $"Повторяющийся вызов '{expr1}' подряд. Возможно, это избыточно.",
                                Suggestion = "Сохраните результат в переменную или удалите дублирующий вызов.",
                                RuleName = "DuplicateMethodCalls"
                            });
                        }
                    }
                }
            }

            return Task.FromResult(issues);
        }

        private bool IsSideEffectFree(string expression)
        {
            var pureMethods = new[] { "ToString", "GetHashCode", "Equals", "CompareTo" };
            return pureMethods.Any(m => expression.Contains(m));
        }
    }
}