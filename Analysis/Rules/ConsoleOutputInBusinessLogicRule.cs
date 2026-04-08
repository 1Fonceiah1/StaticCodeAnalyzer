using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class ConsoleOutputInBusinessLogicRule : IAnalyzerRule
    {
        private static readonly HashSet<string> AllowedMethodNames = new()
        {
            "DisplayOutput", "ShowOutput", "Print", "Log", "WriteToConsole", "Debug"
        };

        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var consoleCalls = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsConsoleWriteCall);

            foreach (var call in consoleCalls)
            {
                var containingMethod = call.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (containingMethod == null) continue;

                var methodName = containingMethod.Identifier.Text;
                if (AllowedMethodNames.Contains(methodName)) continue;

                var location = call.GetLocation();
                if (location != null)
                {
                    var lineSpan = location.GetLineSpan();
                    issues.Add(new AnalysisIssue
                    {
                        Severity = "Средний",
                        FilePath = filePath,
                        LineNumber = lineSpan.StartLinePosition.Line + 1,
                        ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                        Type = "запах кода",
                        Code = "SEP001",
                        Description = "Прямой вызов Console.WriteLine/Write в бизнес-логике нарушает принцип разделения ответственности.",
                        Suggestion = "Вынесите вывод в отдельный сервис или метод (например, через ILogger).",
                        RuleName = "ConsoleOutputInBusinessLogic"
                    });
                }
            }

            return Task.FromResult(issues);
        }

        private bool IsConsoleWriteCall(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax member)
            {
                if (member.Expression is IdentifierNameSyntax id && id.Identifier.Text == "Console")
                {
                    var name = member.Name.Identifier.Text;
                    return name == "WriteLine" || name == "Write";
                }
            }
            return false;
        }
    }
}