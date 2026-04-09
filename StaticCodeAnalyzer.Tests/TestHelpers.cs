using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Analysis.Refactoring; // ← Добавлено
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Tests
{
    public static class TestHelpers
    {
        // Анализирует код с помощью указанного правила и возвращает найденные проблемы
        public static async Task<List<AnalysisIssue>> AnalyzeCodeAsync<T>(string code, string filePath = "Test.cs") 
            where T : IAnalyzerRule, new()
        {
            var tree = CSharpSyntaxTree.ParseText(code, path: filePath);
            var compilation = CSharpCompilation.Create("TestAssembly")
                .AddSyntaxTrees(tree)
                .AddReferences(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location));

            var semanticModel = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync();
            var rule = new T();
            
            return await rule.AnalyzeAsync(root, semanticModel, filePath);
        }

        // Применяет правило рефакторинга к коду и возвращает результат
        public static async Task<string> ApplyRefactoringAsync<T>(string code) 
            where T : IRefactoringRule, new() // ← Исправлено ограничение
        {
            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
                .WithMetadataReferences(new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location)
                });
            
            var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(code));
            var rule = new T();
            
            // Передаёт токен отмены
            var resultDoc = await rule.ApplyAsync(document, CancellationToken.None);
            var root = await resultDoc.GetSyntaxRootAsync();
            return root?.NormalizeWhitespace().ToFullString() ?? code;
        }
    }
}