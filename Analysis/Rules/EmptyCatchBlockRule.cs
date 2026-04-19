using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Выявляет пустые блоки catch, которые подавляют исключения
    public class EmptyCatchBlockRule : IAnalyzerRule
    {
        public List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<CatchClauseSyntax> catchClauses = root.DescendantNodes().OfType<CatchClauseSyntax>();

            foreach (CatchClauseSyntax catchClause in catchClauses)
            {
                if (catchClause.Block == null || catchClause.Block.Statements.Count == 0)
                {
                    Microsoft.CodeAnalysis.Location? location = catchClause.CatchKeyword.GetLocation();
                    if (location != null)
                    {
                        FileLinePositionSpan lineSpan = location.GetLineSpan();
                        MethodDeclarationSyntax? containingMethod = catchClause.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                        ClassDeclarationSyntax? containingClass = catchClause.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Высокий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "ошибка",
                            Code = "ERR001",
                            Description = "Пустой блок catch подавляет исключения без обработки.",
                            Suggestion = "Добавьте логирование, повторный выброс или корректную обработку исключения.",
                            RuleName = "EmptyCatchBlocks",
                            ContainingTypeName = containingClass?.Identifier.Text,
                            MethodName = containingMethod?.Identifier.Text
                        });
                    }
                }
            }

            return issues;
        }
    }
}