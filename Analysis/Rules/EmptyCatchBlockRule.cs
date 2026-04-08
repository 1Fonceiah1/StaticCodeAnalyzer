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
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var catchClauses = root.DescendantNodes().OfType<CatchClauseSyntax>();

            foreach (var catchClause in catchClauses)
            {
                if (catchClause.Block == null || catchClause.Block.Statements.Count == 0)
                {
                    var location = catchClause.CatchKeyword.GetLocation();
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
                            Description = "Пустой блок catch подавляет исключения без обработки.",
                            Suggestion = "Добавьте логирование, повторный выброс или корректную обработку исключения.",
                            RuleName = "EmptyCatchBlocks"
                        });
                    }
                }
            }

            return Task.FromResult(issues);
        }
    }
}