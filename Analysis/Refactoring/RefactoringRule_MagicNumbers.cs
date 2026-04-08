using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_MagicNumbers : IRefactoringRule
    {
        // Заменяет числовые литералы (кроме разрешённых) на именованные константы
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            // Находит все числовые литералы, исключая разрешённые и контексты констант
            var numericLiterals = root.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(l => l.IsKind(SyntaxKind.NumericLiteralExpression) && !IsAllowed(l) && !IsInConstContext(l, semanticModel))
                .ToList();

            if (!numericLiterals.Any()) return document;

            var groups = numericLiterals.GroupBy(l => l.Token.Text);
            var replacements = new Dictionary<LiteralExpressionSyntax, string>();

            foreach (var group in groups)
            {
                var literalText = group.Key;
                var constName = GenerateConstName(literalText);
                var first = group.First();
                var containingType = first.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                if (containingType == null) continue;

                // Проверяет, нет ли уже константы с таким именем
                var typeSymbol = semanticModel.GetDeclaredSymbol(containingType, cancellationToken);
                if (typeSymbol != null && typeSymbol.GetMembers(constName).Any())
                    continue;

                var typeName = GetNumericTypeName(first);
                // Создаёт приватную константу
                var constField = SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.ParseTypeName(typeName),
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(constName)
                                .WithInitializer(SyntaxFactory.EqualsValueClause(first.WithoutTrivia()))
                        ))
                )
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ConstKeyword)))
                .NormalizeWhitespace();

                editor.AddMember(containingType, constField);
                foreach (var lit in group)
                    replacements[lit] = constName;
            }

            // Заменяет литералы на идентификаторы констант
            foreach (var (literal, constName) in replacements)
            {
                var newIdentifier = SyntaxFactory.IdentifierName(constName).WithTriviaFrom(literal);
                editor.ReplaceNode(literal, newIdentifier);
            }

            return editor.GetChangedDocument();
        }

        // Разрешённые числа (0,1,-1,0.0,1.0)
        private bool IsAllowed(LiteralExpressionSyntax literal)
        {
            var text = literal.Token.Text;
            return text == "0" || text == "1" || text == "-1" || text == "0.0" || text == "1.0" ||
                   text == "0f" || text == "1f" || text == "-1f" || text == "0.0f" || text == "1.0f";
        }

        // Пропускает литералы внутри атрибутов, параметров по умолчанию, enum и констант
        private bool IsInConstContext(LiteralExpressionSyntax literal, SemanticModel model)
        {
            return literal.Ancestors().Any(a => a is AttributeSyntax || a is EnumMemberDeclarationSyntax ||
                                               (a is ParameterSyntax p && p.Default?.Value == literal) ||
                                               (a is VariableDeclaratorSyntax v && v.Parent?.Parent is FieldDeclarationSyntax f && f.Modifiers.Any(SyntaxKind.ConstKeyword)));
        }

        // Генерирует имя константы вида magic_100, magic_3_14
        private string GenerateConstName(string literalText)
        {
            var sanitized = literalText.Replace(".", "_").Replace("-", "minus");
            return $"magic_{sanitized}";
        }

        // Определяет тип числа (int, float, double, decimal, long и т.д.)
        private string GetNumericTypeName(LiteralExpressionSyntax literal)
        {
            var text = literal.Token.Text;
            if (text.Contains("f") || text.Contains("F")) return "float";
            if (text.Contains("d") || text.Contains("D")) return "double";
            if (text.Contains("m") || text.Contains("M")) return "decimal";
            if (text.Contains("l") || text.Contains("L")) return "long";
            if (text.Contains("ul") || text.Contains("UL")) return "ulong";
            if (text.Contains("u") || text.Contains("U")) return "uint";
            return "int";
        }
    }
}