using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Analysis.Refactoring;
using StaticCodeAnalyzer.Services;

namespace StaticCodeAnalyzer.Analysis
{
    public class RefactoringEngine
    {
        private readonly List<IRefactoringRule> _rules;
        private readonly AdhocWorkspace _workspace;
        private readonly object _workspaceLock = new();

        public RefactoringEngine()
        {
            _rules = new List<IRefactoringRule>
            {
                new RefactoringRule_EmptyCatchBlock(),
                new RefactoringRule_MagicNumbers(),
                new RefactoringRule_SimplifyDeadCode(),
                new RefactoringRule_FixUndefinedIdentifier(),
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
                new RefactoringRule_UnusedVariable()
            };

            _workspace = new AdhocWorkspace();
            var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "RefactoringProject",
                "RefactoringProject",
                LanguageNames.CSharp)
                .WithMetadataReferences(GetDefaultMetadataReferences())
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            _workspace.AddProject(projectInfo);
        }

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
                "UND001",
                "THR001",
                "CPX001",
                "DEAD001", "SIM001", "DEAD002", "DEAD003", "DEAD004"
            };
        }

        public async Task<string> ApplyRefactoringAsync(string code)
        {
            var result = await ApplyRefactoringWithRollbackAsync(code, null, CancellationToken.None);
            return result.NewCode;
        }

        public async Task<(string NewCode, bool Success, List<string> Errors)> ApplyRefactoringWithRollbackAsync(
            string code,
            HashSet<string> allowedRuleCodes = null,
            CancellationToken cancellationToken = default,
            IProgress<int> progress = null)
        {
            lock (_workspaceLock)
            {
                var project = _workspace.CurrentSolution.Projects.First();
                var existingDoc = project.Documents.FirstOrDefault();
                if (existingDoc != null)
                {
                    var newSolution = _workspace.CurrentSolution.RemoveDocument(existingDoc.Id);
                    _workspace.TryApplyChanges(newSolution);
                }
            }

            var document = _workspace.AddDocument(_workspace.CurrentSolution.Projects.First().Id, "TempDocument.cs", SourceText.From(code));
            var errors = new List<string>();
            bool anyChange = false;

            var rulesToApply = _rules;
            if (allowedRuleCodes != null && allowedRuleCodes.Any())
            {
                rulesToApply = _rules.Where(r => r.TargetIssueCodes.Any(c => allowedRuleCodes.Contains(c))).ToList();
            }

            int total = rulesToApply.Count;
            int processed = 0;

            foreach (var rule in rulesToApply)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var newDocument = await rule.ApplyAsync(document, cancellationToken).ConfigureAwait(false);
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

            var finalRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (finalRoot != null)
            {
                var normalized = finalRoot.NormalizeWhitespace(indentation: "    ", elasticTrivia: true, eol: "\n");
                return (normalized.ToFullString(), true, errors);
            }

            return (code, false, errors);
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