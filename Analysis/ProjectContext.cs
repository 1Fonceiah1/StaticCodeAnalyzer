using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StaticCodeAnalyzer.Analysis
{
    // Представляет контекст проекта: список исходных файлов, синтаксические деревья и компиляцию
    public class ProjectContext
    {
        public string ProjectPath { get; }
        public List<string> SourceFiles { get; }
        public CSharpCompilation Compilation { get; private set; }
        private readonly Dictionary<string, SemanticModel> _semanticModelCache = new Dictionary<string, SemanticModel>();

        private ProjectContext(string projectPath, List<string> sourceFiles, CSharpCompilation compilation)
        {
            ProjectPath = projectPath;
            SourceFiles = sourceFiles;
            Compilation = compilation;
        }

        // Создаёт контекст асинхронно, сканируя все .cs-файлы в указанной директории
        public static async Task<ProjectContext> CreateAsync(string projectPath, CancellationToken cancellationToken = default)
        {
            List<string> sourceFiles = await Task.Run(() => Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories).ToList(), cancellationToken);
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
            foreach (string file in sourceFiles)
            {
                string code = await File.ReadAllTextAsync(file, cancellationToken);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(code, path: file);
                syntaxTrees.Add(tree);
            }

            CSharpCompilation compilation = CSharpCompilation.Create("ProjectCompilation")
                .AddSyntaxTrees(syntaxTrees)
                .AddReferences(GetDefaultMetadataReferences());

            return new ProjectContext(projectPath, sourceFiles, compilation);
        }

        // Фабричный метод для создания контекста в тестах (позволяет передать готовую компиляцию)
        public static ProjectContext CreateForTest(string projectPath, List<string> sourceFiles, CSharpCompilation compilation)
        {
            return new ProjectContext(projectPath, sourceFiles, compilation);
        }

        // Возвращает семантическую модель для указанного файла (с кэшированием)
        public async Task<SemanticModel> GetSemanticModelAsync(string filePath, CancellationToken ct = default)
        {
            lock (_semanticModelCache)
            {
                if (_semanticModelCache.TryGetValue(filePath, out SemanticModel cached))
                    return cached;
            }

            SyntaxTree tree = Compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);
            if (tree == null)
                throw new FileNotFoundException($"SyntaxTree not found for {filePath}");

            SemanticModel model = Compilation.GetSemanticModel(tree);
            lock (_semanticModelCache)
            {
                _semanticModelCache[filePath] = model;
            }
            return await Task.FromResult(model);
        }

        // Обновляет содержимое файла в контексте компиляции
        public async Task UpdateFileAsync(string filePath, string newCode, CancellationToken cancellationToken = default)
        {
            SyntaxTree newTree = CSharpSyntaxTree.ParseText(newCode, path: filePath);
            SyntaxTree oldTree = Compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);
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

        // Возвращает стандартный набор метаданных, необходимых для компиляции
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