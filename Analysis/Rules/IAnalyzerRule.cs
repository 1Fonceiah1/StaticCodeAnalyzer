using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Определяет контракт для правил статического анализа
    public interface IAnalyzerRule
    {
        // Выполняет анализ синтаксического дерева и возвращает список найденных проблем
        Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath);
    }
}