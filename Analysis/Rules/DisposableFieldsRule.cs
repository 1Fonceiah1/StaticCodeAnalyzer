using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class DisposableFieldsRule : IAnalyzerRule
    {
        // Находит классы, содержащие поля, реализующие IDisposable, но сам класс не реализует IDisposable
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classes)
            {
                // Собирает поля, чей тип реализует IDisposable
                var disposableFields = classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables)
                    .Where(v =>
                    {
                        var typeSymbol = semanticModel.GetTypeInfo(v.Initializer?.Value ?? (SyntaxNode)v.Parent?.Parent!).Type;
                        if (typeSymbol != null && typeSymbol.AllInterfaces.Any(i => i.Name == "IDisposable"))
                            return true;
                        return false;
                    }).ToList();

                // Проверяет, реализует ли сам класс IDisposable
                bool implementsIDisposable = classDecl.BaseList?.Types.Any(t => t.Type.ToString().Contains("IDisposable")) ?? false;
                if (disposableFields.Any() && !implementsIDisposable)
                {
                    var location = classDecl.Identifier.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Высокий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "предупреждение",
                            Code = "DISP001",
                            Description = $"Класс '{classDecl.Identifier.Text}' содержит поля, реализующие IDisposable, но сам не реализует IDisposable.",
                            Suggestion = "Реализуйте IDisposable для корректного освобождения ресурсов.",
                            RuleName = "DisposableFields"
                        });
                    }
                }
            }

            return issues;
        }
    }
}