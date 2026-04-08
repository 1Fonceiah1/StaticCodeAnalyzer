using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class AsyncAwaitRule : IAnalyzerRule
    {
        // Находит асинхронные методы без оператора await
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            // Ищет все методы с модификатором async
            var asyncMethods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(SyntaxKind.AsyncKeyword));

            foreach (var method in asyncMethods)
            {
                // Проверяет, есть ли внутри метода await
                bool hasAwait = method.DescendantNodes().OfType<AwaitExpressionSyntax>().Any();
                if (!hasAwait)
                {
                    var location = method.Identifier.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Средний",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "предупреждение",
                            Code = "ASY001",
                            Description = $"Асинхронный метод '{method.Identifier.Text}' не содержит операторов await.",
                            Suggestion = "Удалите модификатор async, если метод не является асинхронным.",
                            RuleName = "AsyncAwaitUsage"
                        });
                    }
                }
            }

            return issues;
        }
    }
}