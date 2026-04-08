using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class GotoStatementRule : IAnalyzerRule
    {
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            var gotoStatements = root.DescendantNodes().OfType<GotoStatementSyntax>();
            foreach (var gotoStmt in gotoStatements)
            {
                var location = gotoStmt.GotoKeyword.GetLocation();
                if (location == null) continue;

                var lineSpan = location.GetLineSpan();
                issues.Add(new AnalysisIssue
                {
                    Severity = "Средний",
                    FilePath = filePath,
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                    Type = "запах кода",
                    Code = "GOTO001",
                    Description = "Обнаружен оператор goto. Использование goto усложняет понимание потока выполнения и делает код менее поддерживаемым.",
                    Suggestion = "Перепишите логику с использованием циклов, условных операторов или вызовов методов.",
                    RuleName = "GotoStatement"
                });
            }

            return issues;
        }
    }
}