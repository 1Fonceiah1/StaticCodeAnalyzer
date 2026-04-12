using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Проверяет цикломатическую сложность методов, предупреждает о превышении порога
    public class MethodComplexityRule : IAnalyzerRule
    {
        private const int MaxComplexity = 10;

        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (MethodDeclarationSyntax method in methods)
            {
                int complexity = CalculateCyclomaticComplexity(method);
                if (complexity > MaxComplexity)
                {
                    Microsoft.CodeAnalysis.Location? location = method.Identifier.GetLocation();
                    if (location != null)
                    {
                        FileLinePositionSpan lineSpan = location.GetLineSpan();
                        ClassDeclarationSyntax? containingClass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Высокий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "запах кода",
                            Code = "CPX001",
                            Description = $"Метод '{method.Identifier.Text}' имеет цикломатическую сложность {complexity} (порог: {MaxComplexity}).",
                            Suggestion = "Разбейте метод на несколько меньших или выделите вспомогательные методы.",
                            RuleName = "MethodComplexity",
                            ContainingTypeName = containingClass?.Identifier.Text,
                            MethodName = method.Identifier.Text
                        });
                    }
                }
            }

            return Task.FromResult(issues);
        }

        // Вычисляет цикломатическую сложность метода
        private int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
        {
            IEnumerable<SyntaxNode> nodes = method.DescendantNodes();
            int complexity = 1;

            complexity += nodes.OfType<IfStatementSyntax>().Count();
            complexity += nodes.OfType<ForStatementSyntax>().Count();
            complexity += nodes.OfType<ForEachStatementSyntax>().Count();
            complexity += nodes.OfType<WhileStatementSyntax>().Count();
            complexity += nodes.OfType<DoStatementSyntax>().Count();
            complexity += nodes.OfType<CaseSwitchLabelSyntax>().Count();
            complexity += nodes.OfType<CasePatternSwitchLabelSyntax>().Count();
            complexity += nodes.OfType<ConditionalExpressionSyntax>().Count();
            complexity += nodes.OfType<BinaryExpressionSyntax>()
                .Count(b => b.IsKind(SyntaxKind.LogicalAndExpression) || b.IsKind(SyntaxKind.LogicalOrExpression));
            complexity += nodes.OfType<CatchClauseSyntax>().Count();

            return complexity;
        }
    }
}