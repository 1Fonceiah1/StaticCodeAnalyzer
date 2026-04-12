using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Предупреждает о публичных полях, нарушающих инкапсуляцию
    public class PublicFieldsRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<FieldDeclarationSyntax> publicFields = root.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.Any(SyntaxKind.PublicKeyword) && !f.Modifiers.Any(SyntaxKind.ConstKeyword));

            foreach (FieldDeclarationSyntax field in publicFields)
            {
                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                {
                    Microsoft.CodeAnalysis.Location? location = variable.Identifier.GetLocation();
                    if (location != null)
                    {
                        FileLinePositionSpan lineSpan = location.GetLineSpan();
                        ClassDeclarationSyntax? containingClass = field.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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