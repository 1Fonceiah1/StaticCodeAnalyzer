using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Проверяет соглашения об именовании: методы в PascalCase, приватные поля в _camelCase
    public class NamingConventionRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();

            // Проверяет методы
            IEnumerable<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (MethodDeclarationSyntax method in methods)
            {
                string name = method.Identifier.Text;
                if (!string.IsNullOrEmpty(name) && !IsPascalCase(name))
                {
                    Microsoft.CodeAnalysis.Location? location = method.Identifier.GetLocation();
                    if (location != null)
                    {
                        FileLinePositionSpan lineSpan = location.GetLineSpan();
                        ClassDeclarationSyntax? containingClass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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

            // Проверяет приватные поля
            IEnumerable<FieldDeclarationSyntax> fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.Any(SyntaxKind.PrivateKeyword) && !f.Modifiers.Any(SyntaxKind.ConstKeyword));
            foreach (FieldDeclarationSyntax field in fields)
            {
                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                {
                    string name = variable.Identifier.Text;
                    if (!string.IsNullOrEmpty(name) && !IsPrivateFieldConvention(name))
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

        // Проверяет, соответствует ли имя соглашению PascalCase
        private bool IsPascalCase(string name) => !string.IsNullOrEmpty(name) && char.IsUpper(name[0]) && !name.Contains('_');
        
        // Преобразует имя в PascalCase
        private string ToPascalCase(string name) => string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
        
        // Проверяет соглашение для приватных полей: начинается с "_" и далее строчная буква
        private bool IsPrivateFieldConvention(string name) => name.StartsWith("_") && name.Length > 1 && char.IsLower(name[1]);
        
        // Преобразует имя в camelCase
        private string ToCamelCase(string name) => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : "");
    }
}