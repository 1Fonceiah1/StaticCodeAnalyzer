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
        // Находит числовые литералы, кроме 0,1,-1 и их аналогов с плавающей точкой
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            var numbers = root.DescendantNodes().OfType<LiteralExpressionSyntax>()
                .Where(l => l.Kind() == SyntaxKind.NumericLiteralExpression)
                .Where(l => !IsAllowedMagicNumber(l));

            foreach (var num in numbers)
            {
                // Пропускает числа, объявленные внутри констант
                bool isConst = false;
                var parent = num.Parent;
                while (parent != null && !(parent is FieldDeclarationSyntax || parent is LocalDeclarationStatementSyntax))
                    parent = parent.Parent;

                if (parent is FieldDeclarationSyntax field && field.Modifiers.Any(SyntaxKind.ConstKeyword))
                    isConst = true;
                if (parent is LocalDeclarationStatementSyntax local && local.Declaration.Variables.Any(v => v.Initializer?.Value == num) && local.Modifiers.Any(SyntaxKind.ConstKeyword))
                    isConst = true;

                if (!isConst)
                {
                    var location = num.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Низкий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "запах кода",
                            Code = "MAG001",
                            Description = $"Найдено магическое число '{num.Token.Text}'. Жёстко заданные числа снижают читаемость и усложняют поддержку.",
                            Suggestion = "Замените на именованную константу.",
                            RuleName = "MagicNumbers"
                        });
                    }
                }
            }

            return issues;
        }

        // Возвращает true, если число относится к разрешённым (0,1,-1,0.0,1.0)
        private bool IsAllowedMagicNumber(LiteralExpressionSyntax literal)
        {
            var text = literal.Token.Text;
            if (text == "0" || text == "1" || text == "-1" || text == "0.0" || text == "1.0")
                return true;
            return false;
        }
    }
}