using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class EmptyCatchBlockRule : IAnalyzerRule
    {
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            var catchBlocks = root.DescendantNodes().OfType<CatchClauseSyntax>();
            foreach (var catchBlock in catchBlocks)
            {
                var block = catchBlock.Block;
                if (block == null || block.Statements.Count == 0)
                {
                    var location = catchBlock.GetLocation();
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
                            Code = "ERR001",
                            Description = "Пустой блок catch молча подавляет исключения.",
                            Suggestion = "Запишите исключение в лог или обработайте его.",
                            RuleName = "EmptyCatchBlocks"
                        });
                    }
                }
            }

            return issues;
        }
    }
}