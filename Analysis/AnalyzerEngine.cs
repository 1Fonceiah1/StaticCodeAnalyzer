using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;
using StaticCodeAnalyzer.Services;

namespace StaticCodeAnalyzer.Analysis
{
    public class AnalyzerEngine
    {
        private readonly List<IAnalyzerRule> _rules;
        public List<RuleError> LastErrors { get; private set; } = new();

        public AnalyzerEngine()
        {
            _rules = new List<IAnalyzerRule>
            {
                new NamingConventionRule(),
                new MethodComplexityRule(),
                new UnusedVariableRule(),
                new MagicNumbersRule(),
                new EmptyCatchBlockRule(),
                new AsyncAwaitRule(),
                new DisposableFieldsRule(),
                new ThreadSafetyRule(),
                new SecurityVulnerabilitiesRule(),
                new CodeDuplicationRule(),
                new GotoStatementRule(),
                new PublicFieldsRule(),
                new PoorLocalVariableNameRule(),
                new DuplicateMethodCallsRule(),
                new ConsoleOutputInBusinessLogicRule(),
                new DeadCodeRule()
            };
        }

        public async Task<List<AnalysisIssue>> AnalyzeFileAsync(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("Не удалось определить директорию файла.");

            var context = await ProjectContext.CreateAsync(directory);
            return await AnalyzeProjectAsync(context, new[] { filePath });
        }

        public async Task<List<AnalysisIssue>> AnalyzeProjectAsync(
            ProjectContext context,
            IEnumerable<string> filesToAnalyze = null,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            var issues = new List<AnalysisIssue>();
            var files = filesToAnalyze?.ToList() ?? context.SourceFiles;
            var compilation = context.Compilation;
            LastErrors.Clear();

            int total = files.Count;
            int processed = 0;

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var syntaxTree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);
                if (syntaxTree == null) continue;

                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync(cancellationToken);

                foreach (var rule in _rules)
                {
                    try
                    {
                        var ruleIssues = await rule.AnalyzeAsync(root, semanticModel, filePath);
                        issues.AddRange(ruleIssues);
                    }
                    catch (Exception ex)
                    {
                        LastErrors.Add(new RuleError(rule.GetType().Name, filePath, ex.Message));
                        Logger.Log("RuleError", $"{rule.GetType().Name} on {filePath}: {ex.Message}", Logger.LogLevel.Error);
                    }
                }

                processed++;
                progress?.Report((processed * 100) / total);
            }
            return issues;
        }
    }

    public record RuleError(string RuleName, string FilePath, string ErrorMessage);
}