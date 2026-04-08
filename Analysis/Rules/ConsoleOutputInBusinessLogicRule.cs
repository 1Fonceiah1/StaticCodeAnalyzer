using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class ConsoleOutputInBusinessLogicRule : IAnalyzerRule
    {
        // Находит прямые вызовы Console.WriteLine/Write в бизнес-методах (исключая специальные методы вывода)
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            var consoleInvocations = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                              ma.Expression.ToString() == "Console" &&
                              (ma.Name.Identifier.Text == "WriteLine" || ma.Name.Identifier.Text == "Write"));

            foreach (var invocation in consoleInvocations)
            {
                // Пропускает, если метод специально предназначен для вывода (по имени)
                var containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (containingMethod != null)
                {
                    var methodName = containingMethod.Identifier.Text;
                    if (methodName.Equals("DisplayOutput", System.StringComparison.OrdinalIgnoreCase) ||
                        methodName.Equals("ShowOutput", System.StringComparison.OrdinalIgnoreCase) ||
                        methodName.Equals("Print", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var location = invocation.GetLocation();
                if (location == null) continue;

                var lineSpan = location.GetLineSpan();
                issues.Add(new AnalysisIssue
                {
                    Severity = "Средний",
                    FilePath = filePath,
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                    Type = "запах кода",
                    Code = "SEP001",
                    Description = "Прямой вызов Console.WriteLine/Write обнаружен в бизнес-логике. Это смешивает вывод данных с вычислениями.",
                    Suggestion = "Вынесите вывод в отдельный метод (например, DisplayOutput) для улучшения тестируемости и разделения ответственности.",
                    RuleName = "ConsoleOutputInBusinessLogic"
                });
            }

            return issues;
        }
    }
}