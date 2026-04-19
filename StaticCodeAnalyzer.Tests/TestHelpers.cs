using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Analysis.Refactoring;
using StaticCodeAnalyzer.Models;
using MSProject = Microsoft.CodeAnalysis.Project;

namespace StaticCodeAnalyzer.Tests
{
    public static class TestHelpers
    {
        // Анализ
        public static List<AnalysisIssue> AnalyzeCode<T>(string code, string filePath = "Test.cs")
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
            SyntaxNode root = tree.GetRoot();
            T rule = new T();

            return rule.Analyze(root, semanticModel, filePath);
        }

        // Рефакторинг
        public static string ApplyRefactoring<T>(string code)
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

            Document resultDoc = rule.Apply(document);
            SyntaxNode root = resultDoc.GetSyntaxRootAsync().Result;
            return root?.NormalizeWhitespace().ToFullString() ?? code;
        }

        // Кросс-файловое тестирование
        public static ProjectContext CreateTestProjectContext(IEnumerable<(string FileName, string Code)> files)
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