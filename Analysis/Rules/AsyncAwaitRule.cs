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
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var asyncMethods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(SyntaxKind.AsyncKeyword));

            foreach (var method in asyncMethods)
            {
                bool hasAwait = method.DescendantNodes()
                    .OfType<AwaitExpressionSyntax>()
                    .Any();

                if (!hasAwait)
                {
                    var location = method.Identifier.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
                        var containingClass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Средний",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "предупреждение",
                            Code = "ASY001",
                            Description = $"Асинхронный метод '{method.Identifier.Text}' не содержит операторов await.",
                            Suggestion = "Удалите модификатор async или добавьте await для асинхронных операций.",
                            RuleName = "AsyncAwaitUsage",
                            ContainingTypeName = containingClass?.Identifier.Text,
                            MethodName = method.Identifier.Text
                        });
                    }
                }
            }

            return Task.FromResult(issues);
        }
    }
}