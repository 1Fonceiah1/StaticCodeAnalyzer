using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Находит методы с идентичными телами
    public class CodeDuplicationRule : IAnalyzerRule
    {
        public List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            List<MethodDeclarationSyntax> methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Body != null)
                .ToList();

            for (int i = 0; i < methods.Count; i++)
            {
                for (int j = i + 1; j < methods.Count; j++)
                {
                    if (AreBodiesEquivalent(methods[i].Body, methods[j].Body))
                    {
                        Microsoft.CodeAnalysis.Location? location = methods[i].Identifier.GetLocation();
                        if (location != null)
                        {
                            FileLinePositionSpan lineSpan = location.GetLineSpan();
                            ClassDeclarationSyntax? containingClass = methods[i].FirstAncestorOrSelf<ClassDeclarationSyntax>();
                            issues.Add(new AnalysisIssue
                            {
                                Severity = "Средний",
                                FilePath = filePath,
                                LineNumber = lineSpan.StartLinePosition.Line + 1,
                                ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                                Type = "запах кода",
                                Code = "DUP001",
                                Description = $"Метод '{methods[i].Identifier.Text}' имеет идентичное тело с методом '{methods[j].Identifier.Text}'.",
                                Suggestion = "Объедините дублирующийся код в один метод или используйте наследование.",
                                RuleName = "CodeDuplication",
                                ContainingTypeName = containingClass?.Identifier.Text,
                                MethodName = methods[i].Identifier.Text
                            });
                        }
                    }
                }
            }

            return issues;
        }

        // Сравнивает два блока кода на идентичность
        private bool AreBodiesEquivalent(BlockSyntax? body1, BlockSyntax? body2)
        {
            if (body1 == null && body2 == null) return true;
            if (body1 == null || body2 == null) return false;
            return body1.NormalizeWhitespace().ToFullString() == body2.NormalizeWhitespace().ToFullString();
        }
    }
}