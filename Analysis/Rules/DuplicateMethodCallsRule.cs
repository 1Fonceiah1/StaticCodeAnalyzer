using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class DuplicateMethodCallsRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var method in methods)
            {
                if (method.Body == null) continue;

                var statements = method.Body.Statements.OfType<ExpressionStatementSyntax>().ToList();
                for (int i = 0; i < statements.Count - 1; i++)
                {
                    var currentInvocation = GetInvocation(statements[i].Expression);
                    var nextInvocation = GetInvocation(statements[i + 1].Expression);
                    if (currentInvocation == null || nextInvocation == null) continue;

                    var currentSymbol = semanticModel.GetSymbolInfo(currentInvocation).Symbol as IMethodSymbol;
                    var nextSymbol = semanticModel.GetSymbolInfo(nextInvocation).Symbol as IMethodSymbol;
                    if (currentSymbol == null || nextSymbol == null) continue;

                    if (SymbolEqualityComparer.Default.Equals(currentSymbol, nextSymbol) &&
                        AreArgumentsEqual(currentInvocation.ArgumentList, nextInvocation.ArgumentList, semanticModel))
                    {
                        var location = statements[i + 1].GetLocation();
                        if (location != null)
                        {
                            var lineSpan = location.GetLineSpan();
                            var containingClass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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

            return Task.FromResult(issues);
        }

        private InvocationExpressionSyntax GetInvocation(ExpressionSyntax expr)
        {
            return expr as InvocationExpressionSyntax;
        }

        private bool AreArgumentsEqual(ArgumentListSyntax args1, ArgumentListSyntax args2, SemanticModel model)
        {
            if (args1 == null && args2 == null) return true;
            if (args1 == null || args2 == null) return false;
            if (args1.Arguments.Count != args2.Arguments.Count) return false;

            for (int i = 0; i < args1.Arguments.Count; i++)
            {
                var arg1 = args1.Arguments[i].Expression;
                var arg2 = args2.Arguments[i].Expression;
                var const1 = model.GetConstantValue(arg1);
                var const2 = model.GetConstantValue(arg2);
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