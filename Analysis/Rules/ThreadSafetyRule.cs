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
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classes)
            {
                var mutableFields = classDecl.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .Where(f => !f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) && !f.Modifiers.Any(SyntaxKind.ConstKeyword))
                    .SelectMany(f => f.Declaration.Variables)
                    .ToList();

                if (!mutableFields.Any()) continue;

                bool hasLock = classDecl.DescendantNodes().OfType<LockStatementSyntax>().Any();
                bool hasThreadSafeTypes = classDecl.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .Any(f => IsThreadSafeType(f.Declaration.Type.ToString()));

                if (!hasLock && !hasThreadSafeTypes)
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
                            Description = $"Класс '{classDecl.Identifier.Text}' содержит изменяемые поля без синхронизации.",
                            Suggestion = "Добавьте lock, используйте Concurrent-коллекции или Immutable-типы.",
                            RuleName = "ThreadSafety"
                        });
                    }
                }
            }

            return Task.FromResult(issues);
        }

        private bool IsThreadSafeType(string typeName)
        {
            var safeTypes = new[] { "string", "int", "long", "bool", "decimal", "DateTime", 
                                   "ConcurrentDictionary", "ConcurrentQueue", "ImmutableArray", "ImmutableList" };
            return safeTypes.Any(t => typeName.Contains(t));
        }
    }
}