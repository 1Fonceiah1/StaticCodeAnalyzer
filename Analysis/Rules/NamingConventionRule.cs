using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class NamingConventionRule : IAnalyzerRule
    {
        // Проверяет имена методов (должны быть PascalCase) и полей (должны быть camelCase или _camelCase)
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            // Проверка методов
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var name = method.Identifier.Text;
                if (!string.IsNullOrEmpty(name) && !char.IsUpper(name[0]))
                {
                    var location = method.Identifier.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Средний",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "запах кода",
                            Code = "NAM001",
                            Description = $"Метод '{name}' должен начинаться с заглавной буквы (PascalCase).",
                            Suggestion = $"Переименуйте в '{char.ToUpper(name[0]) + name.Substring(1)}'.",
                            RuleName = "NamingConvention"
                        });
                    }
                }
            }

            // Проверка полей (приватные должны быть _camelCase или camelCase)
            var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
            foreach (var field in fields)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    var name = variable.Identifier.Text;
                    if (string.IsNullOrEmpty(name)) continue;

                    if (name.StartsWith("_") && name.Length > 1 && char.IsLower(name[1]))
                        continue;

                    if (!char.IsLower(name[0]))
                    {
                        var location = variable.Identifier.GetLocation();
                        if (location != null)
                        {
                            var lineSpan = location.GetLineSpan();
                            issues.Add(new AnalysisIssue
                            {
                                Severity = "Средний",
                                FilePath = filePath,
                                LineNumber = lineSpan.StartLinePosition.Line + 1,
                                ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                                Type = "запах кода",
                                Code = "NAM002",
                                Description = $"Поле '{name}' должно начинаться с маленькой буквы (camelCase).",
                                Suggestion = $"Переименуйте в '{char.ToLower(name[0]) + name.Substring(1)}'.",
                                RuleName = "NamingConvention"
                            });
                        }
                    }
                }
            }

            return issues;
        }
    }
}