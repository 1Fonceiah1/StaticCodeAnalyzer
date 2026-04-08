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
    public class RefactoringRule_FixUndefinedIdentifier : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "UND001" };

        private static readonly Dictionary<string, (string Value, string Type)> CommonConstants = new()
        {
            { "max", ("100", "int") },
            { "limit", ("50", "int") },
            { "count", ("10", "int") },
            { "step", ("1", "int") },
            { "magic", ("42", "int") },
            { "defaultvalue", ("0", "int") },
            { "timeout", ("30", "int") },
            { "buffer", ("1024", "int") }
        };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            // Находим все идентификаторы, которые не являются частью обращения к члену и не разрешены
            var undefinedIdentifiers = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(id => !(id.Parent is MemberAccessExpressionSyntax) &&
                             !(id.Parent is QualifiedNameSyntax) &&
                             !(id.Parent is UsingDirectiveSyntax) &&
                             semanticModel.GetSymbolInfo(id, cancellationToken).Symbol == null)
                .GroupBy(id => id.Identifier.Text)
                .ToList();

            foreach (var group in undefinedIdentifiers)
            {
                var name = group.Key;
                var firstOccurrence = group.First();
                
                // Ищем содержащий класс
                var classDecl = firstOccurrence.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                if (classDecl == null) continue;

                // Проверяем, не объявлена ли уже константа с таким именем в этом классе
                var existingMembers = classDecl.Members
                    .OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables)
                    .Select(v => v.Identifier.Text);
                
                if (existingMembers.Contains(name)) continue;

                // Проверяем, есть ли предопределённое значение для этого имени
                if (!CommonConstants.TryGetValue(name.ToLowerInvariant(), out var constantInfo)) continue;

                var (value, typeName) = constantInfo;

                // Генерируем литерал правильного типа
                ExpressionSyntax literalValue = typeName switch
                {
                    "int" => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(int.Parse(value))),
                    "long" => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(long.Parse(value) + "L")),
                    "double" => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(double.Parse(value))),
                    "string" => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(value)),
                    "bool" => SyntaxFactory.LiteralExpression(value.ToLowerInvariant() == "true" ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
                    _ => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(int.Parse(value)))
                };

                // Создаём приватную константу
                var constField = SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.ParseTypeName(typeName),
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator(name)
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(literalValue)))))
                    .WithModifiers(SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ConstKeyword)))
                    .NormalizeWhitespace();

                // Добавляем константу в класс
                editor.AddMember(classDecl, constField);
                changed = true;
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}