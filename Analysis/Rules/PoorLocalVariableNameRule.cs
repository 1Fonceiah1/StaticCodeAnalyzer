using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class PoorLocalVariableNameRule : IAnalyzerRule
    {
        // Список «плохих» имён, которые не несут смысловой нагрузки
        private static readonly HashSet<string> PoorNames = new HashSet<string>
        {
            "a", "b", "c", "d", "e", "f", "x", "y", "z", "temp", "tmp", "data", "val", "arg"
        };

        // Находит локальные переменные с такими именами
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            var localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Ancestors().OfType<LocalDeclarationStatementSyntax>().Any() ||
                            v.Ancestors().OfType<ForEachStatementSyntax>().Any() ||
                            v.Ancestors().OfType<CatchDeclarationSyntax>().Any());

            foreach (var varDecl in localVars)
            {
                var name = varDecl.Identifier.Text;
                if (PoorNames.Contains(name))
                {
                    var location = varDecl.Identifier.GetLocation();
                    if (location == null) continue;

                    var lineSpan = location.GetLineSpan();
                    issues.Add(new AnalysisIssue
                    {
                        Severity = "Низкий",
                        FilePath = filePath,
                        LineNumber = lineSpan.StartLinePosition.Line + 1,
                        ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                        Type = "запах кода",
                        Code = "NAM003",
                        Description = $"Имя локальной переменной '{name}' неинформативно. Короткие или общие имена (a, b, tmp, data) снижают читаемость.",
                        Suggestion = "Дайте переменной осмысленное имя, отражающее её назначение (например, 'index', 'item', 'result').",
                        RuleName = "PoorLocalVariableName"
                    });
                }
            }

            return issues;
        }
    }
}