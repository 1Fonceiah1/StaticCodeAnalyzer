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

        // Содержит предопределённые константы для часто встречающихся неопределённых идентификаторов
        private static readonly Dictionary<string, (string Value, string Type)> CommonConstants = new Dictionary<string, (string, string)>()
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
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            // Выявляет неразрешённые идентификаторы, не являющиеся частью обращения к члену или using-директивы
            List<IGrouping<string, IdentifierNameSyntax>> undefinedIdentifiers = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(id => !(id.Parent is MemberAccessExpressionSyntax) &&
                             !(id.Parent is QualifiedNameSyntax) &&
                             !(id.Parent is UsingDirectiveSyntax) &&
                             semanticModel.GetSymbolInfo(id, cancellationToken).Symbol == null)
                .GroupBy(id => id.Identifier.Text)
                .ToList();

            foreach (IGrouping<string, IdentifierNameSyntax> group in undefinedIdentifiers)
            {
                string name = group.Key;
                IdentifierNameSyntax firstOccurrence = group.First();
                
                // Определяет класс, содержащий неопределённый идентификатор
                ClassDeclarationSyntax? classDecl = firstOccurrence.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                if (classDecl == null) continue;

                // Проверяет, не объявлена ли уже константа с таким именем
                HashSet<string> existingMembers = classDecl.Members
                    .OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables)
                    .Select(v => v.Identifier.Text)
                    .ToHashSet();
                
                if (existingMembers.Contains(name)) continue;

                // Ищет предопределённое значение для данного имени
                if (!CommonConstants.TryGetValue(name.ToLowerInvariant(), out (string Value, string Type) constantInfo)) continue;

                (string value, string typeName) = constantInfo;

                // Создаёт литеральное выражение в зависимости от типа
                ExpressionSyntax literalValue = typeName switch
                {
                    "int" => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(int.Parse(value))),
                    "long" => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(long.Parse(value) + "L")),
                    "double" => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(double.Parse(value))),
                    "string" => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(value)),
                    "bool" => SyntaxFactory.LiteralExpression(value.ToLowerInvariant() == "true" ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
                    _ => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(int.Parse(value)))
                };

                // Формирует приватную константу
                FieldDeclarationSyntax constField = SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.ParseTypeName(typeName),
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator(name)
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(literalValue)))))
                    .WithModifiers(SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ConstKeyword)))
                    .NormalizeWhitespace();

                // Добавляет константу в класс
                editor.AddMember(classDecl, constField);
                changed = true;
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}