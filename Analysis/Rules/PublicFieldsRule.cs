using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class PublicFieldsRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var publicFields = root.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.Any(SyntaxKind.PublicKeyword) && !f.Modifiers.Any(SyntaxKind.ConstKeyword));

            foreach (var field in publicFields)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    var location = variable.Identifier.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
                        var containingClass = field.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Средний",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "запах кода",
                            Code = "ENC001",
                            Description = $"Публичное поле '{variable.Identifier.Text}' нарушает инкапсуляцию.",
                            Suggestion = "Сделайте поле приватным и предоставьте доступ через свойство (property).",
                            RuleName = "PublicFields",
                            ContainingTypeName = containingClass?.Identifier.Text,
                            MethodName = null
                        });
                    }
                }
            }

            return Task.FromResult(issues);
        }
    }
}