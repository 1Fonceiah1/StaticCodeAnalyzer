using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StaticCodeAnalyzer.Analysis
{
    public class ProjectContext
    {
        public string ProjectPath { get; }
        public List<string> SourceFiles { get; }
        public CSharpCompilation Compilation { get; private set; }
        private readonly Dictionary<string, SemanticModel> _semanticModelCache = new();

        private ProjectContext(string projectPath, List<string> sourceFiles, CSharpCompilation compilation)
        {
            ProjectPath = projectPath;
            SourceFiles = sourceFiles;
            Compilation = compilation;
        }

        public static async Task<ProjectContext> CreateAsync(string projectPath, CancellationToken cancellationToken = default)
        {
            var sourceFiles = await Task.Run(() => Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories).ToList(), cancellationToken);
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var file in sourceFiles)
            {
                var code = await File.ReadAllTextAsync(file, cancellationToken);
                var tree = CSharpSyntaxTree.ParseText(code, path: file);
                syntaxTrees.Add(tree);
            }

            var compilation = CSharpCompilation.Create("ProjectCompilation")
                .AddSyntaxTrees(syntaxTrees)
                .AddReferences(GetDefaultMetadataReferences());

            return new ProjectContext(projectPath, sourceFiles, compilation);
        }

        public static ProjectContext CreateForTest(string projectPath, List<string> sourceFiles, CSharpCompilation compilation)
        {
            return new ProjectContext(projectPath, sourceFiles, compilation);
        }

        public async Task<SemanticModel> GetSemanticModelAsync(string filePath, CancellationToken ct = default)
        {
            lock (_semanticModelCache)
            {
                if (_semanticModelCache.TryGetValue(filePath, out var cached))
                    return cached;
            }

            var tree = Compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);
            if (tree == null)
                throw new FileNotFoundException($"SyntaxTree not found for {filePath}");

            var model = Compilation.GetSemanticModel(tree);
            lock (_semanticModelCache)
            {
                _semanticModelCache[filePath] = model;
            }
            return await Task.FromResult(model);
        }

        public async Task UpdateFileAsync(string filePath, string newCode, CancellationToken cancellationToken = default)
        {
            var newTree = CSharpSyntaxTree.ParseText(newCode, path: filePath);
            var oldTree = Compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);
            if (oldTree != null)
            {
                Compilation = Compilation.ReplaceSyntaxTree(oldTree, newTree);
                lock (_semanticModelCache)
                {
                    _semanticModelCache.Remove(filePath);
                }
            }
            else
            {
                Compilation = Compilation.AddSyntaxTrees(newTree);
                SourceFiles.Add(filePath);
            }
            await Task.CompletedTask;
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