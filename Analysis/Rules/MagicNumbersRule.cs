using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Выявляет магические числа, которые следует заменять именованными константами
    public class MagicNumbersRule : IAnalyzerRule
    {
        // Разрешённые литералы (0, 1, -1, 0.0, 1.0 и т.д.)
        private static readonly HashSet<string> AllowedLiterals = new HashSet<string>()
        {
            "0", "1", "-1", "0.0", "1.0", "0f", "1f", "-1f", "0.0f", "1.0f",
            "0d", "1d", "0m", "1m", "true", "false", "null"
        };

        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<LiteralExpressionSyntax> literals = root.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(l => l.IsKind(SyntaxKind.NumericLiteralExpression))
                .Where(l => !IsAllowed(l) && !IsInConstContext(l) && !IsInNameOf(l));

            foreach (LiteralExpressionSyntax literal in literals)
            {
                Microsoft.CodeAnalysis.Location? location = literal.GetLocation();
                if (location != null)
                {
                    FileLinePositionSpan lineSpan = location.GetLineSpan();
                    MethodDeclarationSyntax? containingMethod = literal.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                    ClassDeclarationSyntax? containingClass = literal.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                    issues.Add(new AnalysisIssue
                    {
                        Severity = "Низкий",
                        FilePath = filePath,
                        LineNumber = lineSpan.StartLinePosition.Line + 1,
                        ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                        Type = "запах кода",
                        Code = "MAG001",
                        Description = $"Магическое число '{literal.Token.Text}' снижает читаемость кода.",
                        Suggestion = "Замените на именованную константу с понятным именем.",
                        RuleName = "MagicNumbers",
                        ContainingTypeName = containingClass?.Identifier.Text,
                        MethodName = containingMethod?.Identifier.Text
                    });
                }
            }

            return Task.FromResult(issues);
        }

        // Проверяет, разрешён ли литерал
        private bool IsAllowed(LiteralExpressionSyntax literal)
        {
            return AllowedLiterals.Contains(literal.Token.Text);
        }

        // Определяет, находится ли литерал в контексте, где константа не требуется
        private bool IsInConstContext(LiteralExpressionSyntax literal)
        {
            return literal.Ancestors().Any(a =>
                a is FieldDeclarationSyntax f && f.Modifiers.Any(SyntaxKind.ConstKeyword) ||
                a is LocalDeclarationStatementSyntax l && l.Modifiers.Any(SyntaxKind.ConstKeyword) ||
                a is EnumMemberDeclarationSyntax ||
                a is AttributeSyntax ||
                (a is ParameterSyntax p && p.Default?.Value == literal) ||
                (a is EqualsValueClauseSyntax eq && eq.Parent is VariableDeclaratorSyntax v &&
                 v.Parent?.Parent is FieldDeclarationSyntax field && field.Modifiers.Any(SyntaxKind.ConstKeyword)));
        }

        // Проверяет, находится ли литерал внутри вызова nameof
        private bool IsInNameOf(LiteralExpressionSyntax literal)
        {
            SyntaxNode? parent = literal.Parent;
            while (parent != null)
            {
                if (parent is InvocationExpressionSyntax inv && inv.Expression is IdentifierNameSyntax id && id.Identifier.Text == "nameof")
                    return true;
                parent = parent.Parent;
            }
            return false;
        }
    }
}