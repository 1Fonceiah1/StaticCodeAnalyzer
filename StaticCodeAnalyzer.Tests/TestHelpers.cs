using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Analysis.Refactoring;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Tests
{
    public static class TestHelpers
    {
        // === Существующие методы (не изменены) ===

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
            where T : IRefactoringRule, new()
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
            
            var resultDoc = await rule.ApplyAsync(document, CancellationToken.None);
            var root = await resultDoc.GetSyntaxRootAsync();
            return root?.NormalizeWhitespace().ToFullString() ?? code;
        }

        // === Новые методы для этапа 1 (кросс-файловое тестирование) ===

        /// <summary>
        /// Создаёт контекст проекта для тестов с несколькими файлами.
        /// </summary>
        public static async Task<ProjectContext> CreateTestProjectContextAsync(IEnumerable<(string FileName, string Code)> files)
        {
            var syntaxTrees = new List<SyntaxTree>();
            var filePaths = new List<string>();
            foreach (var (fileName, code) in files)
            {
                var tree = CSharpSyntaxTree.ParseText(code, path: fileName);
                syntaxTrees.Add(tree);
                filePaths.Add(fileName);
            }
            var compilation = CSharpCompilation.Create("TestProject")
                .AddSyntaxTrees(syntaxTrees)
                .AddReferences(GetDefaultMetadataReferences());

            return ProjectContext.CreateForTest("TestRoot", filePaths, compilation);
        }

        /// <summary>
        /// Возвращает стандартный набор мета-ссылок для тестов.
        /// </summary>
        private static IEnumerable<MetadataReference> GetDefaultMetadataReferences()
        {
            return new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.IEnumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Exception).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location)
            };
        }
    }
}