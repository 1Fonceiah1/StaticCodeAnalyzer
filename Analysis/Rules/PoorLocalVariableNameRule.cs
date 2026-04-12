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
            "temp", "tmp", "data", "val", "arg", "obj", "var", "item", "x1", "y1", "z1",
            "foo", "bar", "baz", "qux"
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
                if (!PoorNames.Contains(name)) continue;

                if (IsAcceptableInContext(variable, name))
                    continue;

                var location = variable.Identifier.GetLocation();
                if (location != null)
                {
                    var lineSpan = location.GetLineSpan();
                    var containingMethod = variable.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                    var containingClass = variable.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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
                        RuleName = "PoorLocalVariableName",
                        ContainingTypeName = containingClass?.Identifier.Text,
                        MethodName = containingMethod?.Identifier.Text
                    });
                }
            }

            return Task.FromResult(issues);
        }

        private bool IsLocalVariable(VariableDeclaratorSyntax variable)
        {
            return variable.Parent is VariableDeclarationSyntax decl &&
                   decl.Parent is LocalDeclarationStatementSyntax;
        }

        private bool IsAcceptableInContext(VariableDeclaratorSyntax variable, string name)
        {
            if (variable.Parent is VariableDeclarationSyntax decl && decl.Parent is ForStatementSyntax)
            {
                if (name == "i" || name == "j" || name == "k" || name == "idx")
                    return true;
            }

            if (variable.Parent is VariableDeclarationSyntax foreachDecl && foreachDecl.Parent is ForEachStatementSyntax)
            {
                if (name == "item" || name == "x" || name == "elem")
                    return true;
            }

            if (variable.Parent is VariableDeclarationSyntax lambdaDecl)
            {
                var parent = lambdaDecl.Parent;
                if (parent is SimpleLambdaExpressionSyntax || parent is ParenthesizedLambdaExpressionSyntax)
                {
                    if (name.Length == 1)
                        return true;
                }
            }

            return false;
        }
    }
}