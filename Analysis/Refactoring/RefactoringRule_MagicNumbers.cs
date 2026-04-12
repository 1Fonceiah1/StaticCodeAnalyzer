using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_MagicNumbers : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "MAG001" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            // Находит все числовые литералы, не являющиеся разрешёнными или находящиеся в особых контекстах
            List<LiteralExpressionSyntax> magicLiterals = root.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(l => l.IsKind(SyntaxKind.NumericLiteralExpression))
                .Where(l => !IsAllowed(l) && !IsInConstContext(l) && !IsInArrayIndexOrSize(l) && !IsInAttribute(l))
                .ToList();

            if (!magicLiterals.Any()) return document;

            // Группирует одинаковые литералы для замены одной константой
            IEnumerable<IGrouping<string, LiteralExpressionSyntax>> groups = magicLiterals.GroupBy(l => l.Token.Text);
            foreach (IGrouping<string, LiteralExpressionSyntax> group in groups)
            {
                string literalText = group.Key;
                string typeName = GetNumericTypeName(literalText);
                string constName = GenerateConstName(literalText);
                LiteralExpressionSyntax first = group.First();
                TypeDeclarationSyntax? containingType = first.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                if (containingType == null) continue;

                // Проверяет, не определена ли уже константа с таким именем
                INamedTypeSymbol? typeSymbol = semanticModel.GetDeclaredSymbol(containingType, cancellationToken);
                if (typeSymbol?.GetMembers(constName).Any() == true) continue;

                // Создаёт приватную константу
                FieldDeclarationSyntax constField = SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.ParseTypeName(typeName),
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator(constName)
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(first.WithoutTrivia())))))
                    .WithModifiers(SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ConstKeyword)))
                    .NormalizeWhitespace();

                editor.AddMember(containingType, constField);
                changed = true;

                // Заменяет все вхождения литерала на имя константы
                foreach (LiteralExpressionSyntax lit in group)
                {
                    IdentifierNameSyntax replacement = SyntaxFactory.IdentifierName(constName).WithTriviaFrom(lit);
                    editor.ReplaceNode(lit, replacement);
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        // Проверяет, является ли литерал разрешённым (0, 1, -1, и т.д.)
        private bool IsAllowed(LiteralExpressionSyntax literal)
        {
            string text = literal.Token.Text;
            return text == "0" || text == "1" || text == "-1" || text == "0.0" || text == "1.0" ||
                   text == "0f" || text == "1f" || text == "-1f" || text == "0.0f" || text == "1.0f";
        }

        // Определяет, находится ли литерал в контексте, где константа не требуется (атрибут, enum, параметр по умолчанию)
        private bool IsInConstContext(LiteralExpressionSyntax literal)
        {
            return literal.Ancestors().Any(a => a is AttributeSyntax || a is EnumMemberDeclarationSyntax ||
                (a is ParameterSyntax p && p.Default?.Value == literal) ||
                (a is VariableDeclaratorSyntax v && v.Parent?.Parent is FieldDeclarationSyntax f &&
                 f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword))));
        }

        // Проверяет, используется ли литерал как индекс массива или размер
        private bool IsInArrayIndexOrSize(LiteralExpressionSyntax literal)
        {
            SyntaxNode? parent = literal.Parent;
            while (parent != null)
            {
                if (parent is BracketedArgumentListSyntax || parent is ElementAccessExpressionSyntax)
                    return true;
                if (parent is ArrayRankSpecifierSyntax)
                    return true;
                parent = parent.Parent;
            }
            return false;
        }

        // Проверяет, находится ли литерал внутри атрибута
        private bool IsInAttribute(LiteralExpressionSyntax literal)
        {
            return literal.Ancestors().OfType<AttributeSyntax>().Any();
        }

        // Определяет тип числа по суффиксу
        private string GetNumericTypeName(string text)
        {
            if (text.Contains("f") || text.Contains("F")) return "float";
            if (text.Contains("m") || text.Contains("M")) return "decimal";
            if (text.Contains("d") || text.Contains("D")) return "double";
            if (text.Contains("L") || text.Contains("l")) return "long";
            if (text.Contains("U") || text.Contains("u")) return text.Contains("L") ? "ulong" : "uint";
            if (text.Contains(".")) return "double";
            return "int";
        }

        // Генерирует имя константы на основе числового литерала
        private string GenerateConstName(string text)
        {
            string clean = Regex.Replace(text, "[^a-zA-Z0-9]", "_");
            return $"Const_{clean}";
        }
    }
}