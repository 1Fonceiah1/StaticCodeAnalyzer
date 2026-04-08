using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class UnusedVariableRule : IAnalyzerRule
    {
        // Находит локальные переменные, которые объявлены, но нигде не используются
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            var variables = root.DescendantNodes().OfType<VariableDeclaratorSyntax>();

            foreach (var variable in variables)
            {
                var symbol = semanticModel.GetDeclaredSymbol(variable);
                if (symbol == null) continue;

                // Рассматривает только локальные переменные (не поля)
                if (variable.Parent is VariableDeclarationSyntax decl && decl.Parent is LocalDeclarationStatementSyntax)
                {
                    // Ищет ссылки на символ
                    var references = await SymbolFinder.FindReferencesAsync(symbol, null);
                    if (references.Count() <= 1)    // только объявление
                    {
                        var location = variable.Identifier.GetLocation();
                        if (location == null) continue;

                        var lineSpan = location.GetLineSpan();
                        if (lineSpan.StartLinePosition.Line < 0) continue;

                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Низкий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "запах кода",
                            Code = "UNU001",
                            Description = $"Переменная '{variable.Identifier.Text}' объявлена, но не используется.",
                            Suggestion = "Удалите неиспользуемую переменную.",
                            RuleName = "UnusedVariables"
                        });
                    }
                }
            }
            return issues;
        }
    }
}