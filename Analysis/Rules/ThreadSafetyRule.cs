using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class ThreadSafetyRule : IAnalyzerRule
    {
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            // Ищем все классы
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classes)
            {
                // 1. Находим не-readonly поля, которые могут изменяться
                var mutableFields = classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>()
                    .Where(f => !f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) &&
                                !f.Modifiers.Any(SyntaxKind.ConstKeyword))
                    .SelectMany(f => f.Declaration.Variables)
                    .ToList();

                if (!mutableFields.Any())
                    continue; // нет изменяемых полей — безопасно

                // 2. Проверяем, есть ли в классе хоть один оператор lock
                bool hasLock = classDecl.DescendantNodes().OfType<LockStatementSyntax>().Any();

                if (!hasLock)
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
                            Code = "THR001",
                            Description = $"Класс '{classDecl.Identifier.Text}' содержит изменяемые поля, но не использует блокировки (lock). Может быть небезопасен в многопоточной среде.",
                            Suggestion = "Добавьте синхронизацию (lock, Monitor, Mutex) для полей, доступных из разных потоков.",
                            RuleName = "ThreadSafety"
                        });
                    }
                }
            }

            return issues;
        }
    }
}