using Microsoft.CodeAnalysis;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Определяет контракт для правил статического анализа
    public interface IAnalyzerRule
    {
        // Выполняет анализ синтаксического дерева и возвращает список найденных проблем
        List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath);
    }
}