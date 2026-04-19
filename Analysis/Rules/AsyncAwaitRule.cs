using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Выявляет методы с модификатором async, не содержащие оператора await
    public class AsyncAwaitRule : IAnalyzerRule
    {
        public List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<MethodDeclarationSyntax> asyncMethods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(SyntaxKind.AsyncKeyword));

            foreach (MethodDeclarationSyntax method in asyncMethods)
            {
                bool hasAwait = method.DescendantNodes()
                    .OfType<AwaitExpressionSyntax>()
                    .Any();

                if (!hasAwait)
                {
                    Microsoft.CodeAnalysis.Location? location = method.Identifier.GetLocation();
                    if (location != null)
                    {
                        FileLinePositionSpan lineSpan = location.GetLineSpan();
                        ClassDeclarationSyntax? containingClass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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

            return issues;
        }
    }
}