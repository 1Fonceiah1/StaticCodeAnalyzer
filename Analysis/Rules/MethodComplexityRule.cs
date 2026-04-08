using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class MethodComplexityRule : IAnalyzerRule
    {
        // Рассчитывает цикломатическую сложность метода и выдаёт предупреждение, если она превышает 10
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                int complexity = CalculateCyclomaticComplexity(method);
                if (complexity > 10)
                {
                    var location = method.Identifier.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Высокий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "запах кода",
                            Code = "CPX001",
                            Description = $"Метод '{method.Identifier.Text}' имеет цикломатическую сложность {complexity}, что превышает рекомендуемый порог 10.",
                            Suggestion = "Разбейте метод на несколько более мелких.",
                            RuleName = "MethodComplexity"
                        });
                    }
                }
            }

            return issues;
        }

        // Подсчитывает количество точек ветвления: if, for, foreach, while, case, &&, ||
        private int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
        {
            var nodes = method.DescendantNodes();
            int count = 1;
            count += nodes.OfType<IfStatementSyntax>().Count();
            count += nodes.OfType<ForStatementSyntax>().Count();
            count += nodes.OfType<ForEachStatementSyntax>().Count();
            count += nodes.OfType<WhileStatementSyntax>().Count();
            count += nodes.OfType<CaseSwitchLabelSyntax>().Count();
            count += nodes.OfType<ConditionalAccessExpressionSyntax>().Count();
            count += nodes.OfType<BinaryExpressionSyntax>()
                .Where(b => b.Kind() == SyntaxKind.LogicalAndExpression || b.Kind() == SyntaxKind.LogicalOrExpression)
                .Count();
            return count;
        }
    }
}