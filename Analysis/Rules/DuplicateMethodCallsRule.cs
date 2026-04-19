using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Выявляет повторяющиеся вызовы методов подряд с одинаковыми аргументами
    public class DuplicateMethodCallsRule : IAnalyzerRule
    {
        public List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (MethodDeclarationSyntax method in methods)
            {
                if (method.Body == null) continue;

                List<ExpressionStatementSyntax> statements = method.Body.Statements.OfType<ExpressionStatementSyntax>().ToList();
                for (int i = 0; i < statements.Count - 1; i++)
                {
                    InvocationExpressionSyntax? currentInvocation = GetInvocation(statements[i].Expression);
                    InvocationExpressionSyntax? nextInvocation = GetInvocation(statements[i + 1].Expression);
                    if (currentInvocation == null || nextInvocation == null) continue;

                    IMethodSymbol? currentSymbol = semanticModel.GetSymbolInfo(currentInvocation).Symbol as IMethodSymbol;
                    IMethodSymbol? nextSymbol = semanticModel.GetSymbolInfo(nextInvocation).Symbol as IMethodSymbol;
                    if (currentSymbol == null || nextSymbol == null) continue;

                    if (SymbolEqualityComparer.Default.Equals(currentSymbol, nextSymbol) &&
                        AreArgumentsEqual(currentInvocation.ArgumentList, nextInvocation.ArgumentList, semanticModel))
                    {
                        Microsoft.CodeAnalysis.Location? location = statements[i + 1].GetLocation();
                        if (location != null)
                        {
                            FileLinePositionSpan lineSpan = location.GetLineSpan();
                            ClassDeclarationSyntax? containingClass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                            issues.Add(new AnalysisIssue
                            {
                                Severity = "Низкий",
                                FilePath = filePath,
                                LineNumber = lineSpan.StartLinePosition.Line + 1,
                                ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                                Type = "запах кода",
                                Code = "DUP002",
                                Description = $"Повторяющийся вызов метода '{currentSymbol.Name}' подряд. Возможно, это избыточно.",
                                Suggestion = "Сохраните результат в переменную или удалите дублирующий вызов.",
                                RuleName = "DuplicateMethodCalls",
                                ContainingTypeName = containingClass?.Identifier.Text,
                                MethodName = method.Identifier.Text
                            });
                        }
                    }
                }
            }

            return issues;
        }

        // Извлекает выражение вызова метода
        private InvocationExpressionSyntax? GetInvocation(ExpressionSyntax expr)
        {
            return expr as InvocationExpressionSyntax;
        }

        // Сравнивает списки аргументов двух вызовов
        private bool AreArgumentsEqual(ArgumentListSyntax args1, ArgumentListSyntax args2, SemanticModel model)
        {
            if (args1 == null && args2 == null) return true;
            if (args1 == null || args2 == null) return false;
            if (args1.Arguments.Count != args2.Arguments.Count) return false;

            for (int i = 0; i < args1.Arguments.Count; i++)
            {
                ExpressionSyntax arg1 = args1.Arguments[i].Expression;
                ExpressionSyntax arg2 = args2.Arguments[i].Expression;
                Optional<object> const1 = model.GetConstantValue(arg1);
                Optional<object> const2 = model.GetConstantValue(arg2);
                if (const1.HasValue && const2.HasValue)
                {
                    if (!Equals(const1.Value, const2.Value))
                        return false;
                }
                else
                {
                    if (arg1.ToString() != arg2.ToString())
                        return false;
                }
            }
            return true;
        }
    }
}