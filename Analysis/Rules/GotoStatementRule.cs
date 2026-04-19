using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Предупреждает об использовании оператора goto, усложняющего понимание кода
    public class GotoStatementRule : IAnalyzerRule
    {
        public List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<GotoStatementSyntax> gotoStatements = root.DescendantNodes().OfType<GotoStatementSyntax>();

            foreach (GotoStatementSyntax gotoStmt in gotoStatements)
            {
                Microsoft.CodeAnalysis.Location? location = gotoStmt.GotoKeyword.GetLocation();
                if (location != null)
                {
                    FileLinePositionSpan lineSpan = location.GetLineSpan();
                    MethodDeclarationSyntax? containingMethod = gotoStmt.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                    ClassDeclarationSyntax? containingClass = gotoStmt.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                    issues.Add(new AnalysisIssue
                    {
                        Severity = "Средний",
                        FilePath = filePath,
                        LineNumber = lineSpan.StartLinePosition.Line + 1,
                        ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                        Type = "запах кода",
                        Code = "GOTO001",
                        Description = "Использование goto усложняет чтение и поддержку кода.",
                        Suggestion = "Перепишите логику с использованием циклов, условий или выделите метод.",
                        RuleName = "GotoStatement",
                        ContainingTypeName = containingClass?.Identifier.Text,
                        MethodName = containingMethod?.Identifier.Text
                    });
                }
            }

            return issues;
        }
    }
}