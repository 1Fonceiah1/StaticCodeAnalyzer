using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public interface IAnalyzerRule
    {
        Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath);
    }
}