using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class NamingConventionRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var name = method.Identifier.Text;
                if (!string.IsNullOrEmpty(name) && !IsPascalCase(name))
                {
                    var location = method.Identifier.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
                        var containingClass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Средний",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "запах кода",
                            Code = "NAM001",
                            Description = $"Метод '{name}' нарушает соглашение об именовании (должен быть PascalCase).",
                            Suggestion = $"Переименуйте в '{ToPascalCase(name)}'.",
                            RuleName = "NamingConvention",
                            ContainingTypeName = containingClass?.Identifier.Text,
                            MethodName = name
                        });
                    }
                }
            }

            var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.Any(SyntaxKind.PrivateKeyword) && !f.Modifiers.Any(SyntaxKind.ConstKeyword));
            foreach (var field in fields)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    var name = variable.Identifier.Text;
                    if (!string.IsNullOrEmpty(name) && !IsPrivateFieldConvention(name))
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
                                Code = "NAM002",
                                Description = $"Поле '{name}' должно следовать соглашению _camelCase для приватных полей.",
                                Suggestion = $"Переименуйте в '_{ToCamelCase(name)}'.",
                                RuleName = "NamingConvention",
                                ContainingTypeName = containingClass?.Identifier.Text,
                                MethodName = null
                            });
                        }
                    }
                }
            }

            return Task.FromResult(issues);
        }

        private bool IsPascalCase(string name) => !string.IsNullOrEmpty(name) && char.IsUpper(name[0]) && !name.Contains('_');
        private string ToPascalCase(string name) => string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
        private bool IsPrivateFieldConvention(string name) => name.StartsWith("_") && name.Length > 1 && char.IsLower(name[1]);
        private string ToCamelCase(string name) => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : "");
    }
}