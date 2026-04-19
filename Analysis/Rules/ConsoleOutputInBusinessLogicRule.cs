using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Выявляет прямые вызовы Console.Write/WriteLine в методах бизнес-логики
    public class ConsoleOutputInBusinessLogicRule : IAnalyzerRule
    {
        // Имена методов, в которых вывод на консоль разрешён
        private static readonly HashSet<string> AllowedMethodNames = new HashSet<string>()
        {
            "DisplayOutput", "ShowOutput", "Print", "Log", "WriteToConsole", "Debug"
        };

        public List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<InvocationExpressionSyntax> consoleCalls = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsConsoleWriteCall);

            foreach (InvocationExpressionSyntax call in consoleCalls)
            {
                MethodDeclarationSyntax? containingMethod = call.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (containingMethod == null) continue;

                string methodName = containingMethod.Identifier.Text;
                if (AllowedMethodNames.Contains(methodName)) continue;

                Microsoft.CodeAnalysis.Location? location = call.GetLocation();
                if (location != null)
                {
                    FileLinePositionSpan lineSpan = location.GetLineSpan();
                    ClassDeclarationSyntax? containingClass = containingMethod.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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
                        RuleName = "ConsoleOutputInBusinessLogic",
                        ContainingTypeName = containingClass?.Identifier.Text,
                        MethodName = containingMethod.Identifier.Text
                    });
                }
            }

            return issues;
        }

        // Определяет, является ли вызов обращением к Console.Write/WriteLine
        private bool IsConsoleWriteCall(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax member)
            {
                if (member.Expression is IdentifierNameSyntax id && id.Identifier.Text == "Console")
                {
                    string name = member.Name.Identifier.Text;
                    return name == "WriteLine" || name == "Write";
                }
            }
            return false;
        }
    }
}