using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Analysis.Refactoring;

namespace StaticCodeAnalyzer.Analysis
{
    public class RefactoringEngine
    {
        private readonly List<IRefactoringRule> _rules;

        public RefactoringEngine()
        {
            _rules = new List<IRefactoringRule>
            {
                new RefactoringRule_NamingConvention(),
                new RefactoringRule_EmptyCatchBlock(),
                new RefactoringRule_RemoveDuplicates(),         // объединённое правило
                new RefactoringRule_RemoveGoto(),
                new RefactoringRule_AsyncAwait(),
                new RefactoringRule_UnusedVariable(),
                new RefactoringRule_DisposableFields(),
                new RefactoringRule_MagicNumbers(),
                new RefactoringRule_EncapsulateFields(),
                new RefactoringRule_RenameLocalVariables(),
                new RefactoringRule_RemoveDuplicateCalls(),
                new RefactoringRule_SeparateOutput(),
                new RefactoringRule_SplitMethodByResponsibility(),
                new RefactoringRule_FixUndefinedIdentifier(),
                new RefactoringRule_ThreadSafety()
            };
        }

        public HashSet<string> GetFixableIssueCodes()
        {
            return new HashSet<string>
            {
                "NAM001", "NAM002", "ERR001", "ASY001", "UNU001", "DISP001", "MAG001", "GOTO001", "DUPL001",
                "ENC001", "REN001", "EXT001", "SEP001", "SPL001", "DUP002", "UND001", "THR001"
            };
        }

        public async Task<string> ApplyRefactoringAsync(string code)
        {
            var workspace = new AdhocWorkspace();
            var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "TempProject",
                "TempProject",
                LanguageNames.CSharp)
                .WithMetadataReferences(GetDefaultMetadataReferences())
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var project = workspace.AddProject(projectInfo);
            var document = workspace.AddDocument(project.Id, "TempDocument.cs", SourceText.From(code));

            foreach (var rule in _rules)
            {
                try
                {
                    var newDocument = await rule.ApplyAsync(document).ConfigureAwait(false);
                    if (newDocument != document)
                    {
                        document = newDocument;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in {rule.GetType().Name}: {ex.Message}");
                }
            }

            var finalRoot = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            return finalRoot.ToFullString();
        }

        private static IEnumerable<MetadataReference> GetDefaultMetadataReferences()
        {
            return new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
            };
        }
    }
}