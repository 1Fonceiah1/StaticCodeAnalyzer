using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class MagicNumbersRule : IAnalyzerRule
    {
        private static readonly HashSet<string> AllowedLiterals = new()
        {
            "0", "1", "-1", "0.0", "1.0", "0f", "1f", "-1f", "0.0f", "1.0f",
            "0d", "1d", "0m", "1m", "true", "false", "null"
        };

        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var literals = root.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(l => l.IsKind(SyntaxKind.NumericLiteralExpression))
                .Where(l => !IsAllowed(l) && !IsInConstContext(l) && !IsInNameOf(l));

            foreach (var literal in literals)
            {
                var location = literal.GetLocation();
                if (location != null)
                {
                    var lineSpan = location.GetLineSpan();
                    var containingMethod = literal.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                    var containingClass = literal.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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

        private bool IsAllowed(LiteralExpressionSyntax literal)
        {
            return AllowedLiterals.Contains(literal.Token.Text);
        }

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

        private bool IsInNameOf(LiteralExpressionSyntax literal)
        {
            var parent = literal.Parent;
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