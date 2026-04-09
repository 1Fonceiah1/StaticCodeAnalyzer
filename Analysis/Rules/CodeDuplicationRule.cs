using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class CodeDuplicationRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Body != null)
                .ToList();

            for (int i = 0; i < methods.Count; i++)
            {
                for (int j = i + 1; j < methods.Count; j++)
                {
                    if (AreBodiesEquivalent(methods[i].Body, methods[j].Body))
                    {
                        var location = methods[i].Identifier.GetLocation();
                        if (location != null)
                        {
                            var lineSpan = location.GetLineSpan();
                            issues.Add(new AnalysisIssue
                            {
                                Severity = "Средний",
                                FilePath = filePath,
                                LineNumber = lineSpan.StartLinePosition.Line + 1,
                                ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                                Type = "запах кода",
                                Code = "DUP001",
                                Description = $"Метод '{methods[i].Identifier.Text}' имеет идентичное тело с методом '{methods[j].Identifier.Text}'.",
                                Suggestion = "Объедините дублирующийся код в один метод или используйте наследование.",
                                RuleName = "CodeDuplication"
                            });
                        }
                    }
                }
            }

            return Task.FromResult(issues);
        }

        private bool AreBodiesEquivalent(BlockSyntax? body1, BlockSyntax? body2)
        {
            if (body1 == null && body2 == null) return true;
            if (body1 == null || body2 == null) return false;
            
            // Надёжное сравнение без SyntaxFactory: убирает пробелы/переносы и сравниваем текст
            return body1.NormalizeWhitespace().ToFullString() == body2.NormalizeWhitespace().ToFullString();
        }
    }
}