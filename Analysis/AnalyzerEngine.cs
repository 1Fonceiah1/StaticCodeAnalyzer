using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class AnalyzerEngine
    {
        private readonly List<IAnalyzerRule> _rules;

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
                new ConsoleOutputInBusinessLogicRule()
            };
        }

        public async Task<List<AnalysisIssue>> AnalyzeFileAsync(string filePath)
        {
            try
            {
                var code = await System.IO.File.ReadAllTextAsync(filePath);
                var tree = CSharpSyntaxTree.ParseText(code, path: filePath);
                var compilation = CSharpCompilation.Create("temp")
                    .AddSyntaxTrees(tree)
                    .AddReferences(
                        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));

                var semanticModel = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync();

                var issues = new List<AnalysisIssue>();

                foreach (var rule in _rules)
                {
                    try
                    {
                        var ruleIssues = await rule.AnalyzeAsync(root, semanticModel, filePath);
                        issues.AddRange(ruleIssues);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Rule {rule.GetType().Name} failed: {ex.Message}");
                    }
                }

                return issues;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AnalyzerEngine error: {ex.Message}");
                throw;
            }
        }
    }
}