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
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var gotoStatements = root.DescendantNodes().OfType<GotoStatementSyntax>();

            foreach (var gotoStmt in gotoStatements)
            {
                var location = gotoStmt.GotoKeyword.GetLocation();
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
                        Code = "GOTO001",
                        Description = "Использование goto усложняет чтение и поддержку кода.",
                        Suggestion = "Перепишите логику с использованием циклов, условий или выделите метод.",
                        RuleName = "GotoStatement"
                    });
                }
            }

            return Task.FromResult(issues);
        }
    }
}