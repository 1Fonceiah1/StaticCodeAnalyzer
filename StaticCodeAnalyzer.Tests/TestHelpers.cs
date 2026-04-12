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
using MSProject = Microsoft.CodeAnalysis.Project; // alias для устранения конфликта

namespace StaticCodeAnalyzer.Tests
{
    public static class TestHelpers
    {
        // Анализ
        public static async Task<List<AnalysisIssue>> AnalyzeCodeAsync<T>(string code, string filePath = "Test.cs") 
            where T : IAnalyzerRule, new()
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code, path: filePath);
            CSharpCompilation compilation = CSharpCompilation.Create("TestAssembly")
                .AddSyntaxTrees(tree)
                .AddReferences(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location));

            SemanticModel semanticModel = compilation.GetSemanticModel(tree);
            SyntaxNode root = await tree.GetRootAsync();
            T rule = new T();
            
            return await rule.AnalyzeAsync(root, semanticModel, filePath);
        }

        // Рефакторинг
        public static async Task<string> ApplyRefactoringAsync<T>(string code) 
            where T : IRefactoringRule, new()
        {
            AdhocWorkspace workspace = new AdhocWorkspace();
            MSProject project = workspace.AddProject("TestProject", LanguageNames.CSharp)
                .WithMetadataReferences(new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location)
                });
            
            Document document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(code));
            T rule = new T();
            
            Document resultDoc = await rule.ApplyAsync(document, CancellationToken.None);
            SyntaxNode root = await resultDoc.GetSyntaxRootAsync();
            return root?.NormalizeWhitespace().ToFullString() ?? code;
        }

        // Кросс-файловое тестирование
        public static async Task<ProjectContext> CreateTestProjectContextAsync(IEnumerable<(string FileName, string Code)> files)
        {
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
            List<string> filePaths = new List<string>();
            foreach ((string fileName, string code) in files)
            {
                SyntaxTree tree = CSharpSyntaxTree.ParseText(code, path: fileName);
                syntaxTrees.Add(tree);
                filePaths.Add(fileName);
            }
            CSharpCompilation compilation = CSharpCompilation.Create("TestProject")
                .AddSyntaxTrees(syntaxTrees)
                .AddReferences(GetDefaultMetadataReferences());

            return ProjectContext.CreateForTest("TestRoot", filePaths, compilation);
        }

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