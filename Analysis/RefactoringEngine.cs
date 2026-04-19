using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using StaticCodeAnalyzer.Analysis.Refactoring;
using StaticCodeAnalyzer.Services;

namespace StaticCodeAnalyzer.Analysis
{
    // Движок рефакторинга, последовательно применяет правила из Analysis/Refactoring к документу
    public class RefactoringEngine
    {
        private readonly List<IRefactoringRule> _rules;
        private readonly AdhocWorkspace _workspace;
        private readonly object _workspaceLock = new object();

        public RefactoringEngine()
        {
            _rules = new List<IRefactoringRule>
            {
                new RefactoringRule_EmptyCatchBlock(),
                new RefactoringRule_MagicNumbers(),
                new RefactoringRule_SimplifyDeadCode(),
                new RefactoringRule_NamingConvention(),
                new RefactoringRule_RenameLocalVariables(),
                new RefactoringRule_EncapsulateFields(),
                new RefactoringRule_DisposableFields(),
                new RefactoringRule_ThreadSafety(),
                new RefactoringRule_RemoveDuplicates(),
                new RefactoringRule_RemoveDuplicateCalls(),
                new RefactoringRule_SeparateOutput(),
                new RefactoringRule_SplitMethodByResponsibility(),
                new RefactoringRule_AsyncAwait(),
                new RefactoringRule_UnusedVariable(),
                new RefactoringRule_GotoStatement(),
                new RefactoringRule_SecurityVulnerabilities()
            };

            _workspace = new AdhocWorkspace();
            ProjectInfo projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "RefactoringProject",
                "RefactoringProject",
                LanguageNames.CSharp)
                .WithMetadataReferences(GetDefaultMetadataReferences())
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            _workspace.AddProject(projectInfo);
        }

        // Возвращает коды проблем, которые могут быть исправлены хотя бы одним правилом
        public HashSet<string> GetFixableIssueCodes()
        {
            return new HashSet<string>
            {
                "NAM001", "NAM002", "NAM003",
                "ERR001",
                "ASY001",
                "UNU001",
                "DISP001",
                "MAG001",
                "DUP001", "DUP002",
                "ENC001",
                "REN001",
                "SEP001",
                "SPL001",
                "THR001",
                "CPX001",
                "DEAD001", "SIM001", "DEAD002", "DEAD003", "DEAD004",
                "GOTO001",
                "SEC001", "SEC002"
            };
        }

        // Применяет рефакторинг к строке кода (упрощённая версия без выбора правил)
        public string ApplyRefactoring(string code)
        {
            (string newCode, bool success, List<string> errors) = ApplyRefactoringWithRollback(code, null);
            return newCode;
        }

        // Основной метод: применяет рефакторинг с возможностью отката, возвращает изменённый код и ошибки
        public (string NewCode, bool Success, List<string> Errors) ApplyRefactoringWithRollback(
            string code,
            HashSet<string> allowedRuleCodes = null,
            IProgress<int> progress = null)
        {
            lock (_workspaceLock)
            {
                Project project = _workspace.CurrentSolution.Projects.First();
                Document existingDoc = project.Documents.FirstOrDefault();
                if (existingDoc != null)
                {
                    Solution newSolution = _workspace.CurrentSolution.RemoveDocument(existingDoc.Id);
                    _workspace.TryApplyChanges(newSolution);
                }
            }

            Document document = _workspace.AddDocument(_workspace.CurrentSolution.Projects.First().Id, "TempDocument.cs", SourceText.From(code));
            List<string> errors = new List<string>();
            bool anyChange = false;

            List<IRefactoringRule> rulesToApply = _rules;
            if (allowedRuleCodes != null && allowedRuleCodes.Any())
            {
                rulesToApply = _rules.Where(r => r.TargetIssueCodes.Any(c => allowedRuleCodes.Contains(c))).ToList();
            }

            int total = rulesToApply.Count;
            int processed = 0;

            foreach (IRefactoringRule rule in rulesToApply)
            {
                try
                {
                    Document newDocument = rule.Apply(document);
                    if (newDocument != document)
                    {
                        document = newDocument;
                        anyChange = true;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Ошибка в {rule.GetType().Name}: {ex.Message}");
                    Logger.Log("RefactoringError", $"{rule.GetType().Name}: {ex.Message}", Logger.LogLevel.Error);
                }

                processed++;
                progress?.Report((processed * 100) / total);
            }

            if (!anyChange && errors.Any())
                return (code, false, errors);

            SyntaxNode finalRoot = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            if (finalRoot != null)
            {
                SyntaxNode normalized = finalRoot.NormalizeWhitespace(indentation: "    ", elasticTrivia: true, eol: "\n");
                return (normalized.ToFullString(), true, errors);
            }

            return (code, false, errors);
        }

        // Предоставляет метаданные для компиляции в рабочей области
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