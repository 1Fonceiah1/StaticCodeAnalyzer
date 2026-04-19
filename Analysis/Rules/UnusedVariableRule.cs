using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Выявляет неиспользуемые локальные переменные
    public class UnusedVariableRule : IAnalyzerRule
    {
        public List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<VariableDeclaratorSyntax> localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Parent is VariableDeclarationSyntax decl && decl.Parent is LocalDeclarationStatementSyntax);

            foreach (VariableDeclaratorSyntax variable in localVars)
            {
                ISymbol? symbol = semanticModel.GetDeclaredSymbol(variable);
                if (symbol == null) continue;

                MethodDeclarationSyntax? method = variable.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (method == null) continue;

                // Подсчитывает использования символа в методе
                int usageCount = method.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Count(id => semanticModel.GetSymbolInfo(id).Symbol != null &&
                                 SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(id).Symbol, symbol));

                if (usageCount <= 1)
                {
                    Microsoft.CodeAnalysis.Location? location = variable.Identifier.GetLocation();
                    if (location != null)
                    {
                        FileLinePositionSpan lineSpan = location.GetLineSpan();
                        ClassDeclarationSyntax? containingClass = variable.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Низкий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "запах кода",
                            Code = "UNU001",
                            Description = $"Переменная '{variable.Identifier.Text}' объявлена, но не используется.",
                            Suggestion = "Удалите неиспользуемую переменную или используйте её в коде.",
                            RuleName = "UnusedVariables",
                            ContainingTypeName = containingClass?.Identifier.Text,
                            MethodName = method?.Identifier.Text
                        });
                    }
                }
            }

            return issues;
        }
    }
}