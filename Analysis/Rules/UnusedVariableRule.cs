using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class UnusedVariableRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Parent is VariableDeclarationSyntax decl && decl.Parent is LocalDeclarationStatementSyntax);

            foreach (var variable in localVars)
            {
                var symbol = semanticModel.GetDeclaredSymbol(variable);
                if (symbol == null) continue;

                var method = variable.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (method == null) continue;

                int usageCount = method.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Count(id => semanticModel.GetSymbolInfo(id).Symbol != null &&
                                 SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(id).Symbol, symbol));

                if (usageCount <= 1) // только объявление
                {
                    var location = variable.Identifier.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Низкий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "запах кода",
                            Code = "UNU001",
                            Description = $"Переменная '{variable.Identifier.Text}' объявлена, но не используется.",
                            Suggestion = "Удалите неиспользуемую переменную или используйте её в коде.",
                            RuleName = "UnusedVariables"
                        });
                    }
                }
            }

            return Task.FromResult(issues);
        }
    }
}