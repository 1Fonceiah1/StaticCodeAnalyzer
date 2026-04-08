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
        private static readonly HashSet<string> PoorNames = new()
        {
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
            "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
            "temp", "tmp", "data", "val", "arg", "obj", "var", "item", "x", "y", "z"
        };

        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => IsLocalVariable(v));

            foreach (var variable in localVars)
            {
                var name = variable.Identifier.Text;
                if (PoorNames.Contains(name))
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
                            Code = "NAM003",
                            Description = $"Имя переменной '{name}' неинформативно и снижает читаемость кода.",
                            Suggestion = "Используйте осмысленное имя, отражающее назначение переменной (например, 'userIndex', 'totalAmount').",
                            RuleName = "PoorLocalVariableName"
                        });
                    }
                }
            }

            return Task.FromResult(issues);
        }

        private bool IsLocalVariable(VariableDeclaratorSyntax variable)
        {
            return variable.Parent is VariableDeclarationSyntax decl &&
                   decl.Parent is LocalDeclarationStatementSyntax;
        }
    }
}