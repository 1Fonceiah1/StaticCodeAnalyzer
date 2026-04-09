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
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var magicLiterals = root.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(l => l.IsKind(SyntaxKind.NumericLiteralExpression) && !IsAllowed(l) && !IsInConstContext(l))
                .ToList();

            if (!magicLiterals.Any()) return document;

            var groups = magicLiterals.GroupBy(l => l.Token.Text);
            foreach (var group in groups)
            {
                var literalText = group.Key;
                var typeName = GetNumericTypeName(literalText); // ← ИСПРАВЛЕНО: корректное определение типа
                var constName = GenerateConstName(literalText);
                var first = group.First();
                var containingType = first.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                if (containingType == null) continue;

                var typeSymbol = semanticModel.GetDeclaredSymbol(containingType, cancellationToken);
                if (typeSymbol?.GetMembers(constName).Any() == true) continue;

                var constField = SyntaxFactory.FieldDeclaration(
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

                foreach (var lit in group)
                {
                    var replacement = SyntaxFactory.IdentifierName(constName).WithTriviaFrom(lit);
                    editor.ReplaceNode(lit, replacement);
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private bool IsAllowed(LiteralExpressionSyntax literal)
        {
            var text = literal.Token.Text;
            return text == "0" || text == "1" || text == "-1" || text == "0.0" || text == "1.0" ||
                   text == "0f" || text == "1f" || text == "-1f" || text == "0.0f" || text == "1.0f";
        }

        private bool IsInConstContext(LiteralExpressionSyntax literal) =>
            literal.Ancestors().Any(a => a is AttributeSyntax || a is EnumMemberDeclarationSyntax ||
                (a is ParameterSyntax p && p.Default?.Value == literal) ||
                (a is VariableDeclaratorSyntax v && v.Parent?.Parent is FieldDeclarationSyntax f && 
                 f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword))));

        // Корректное определение типа по суффиксу и наличию точки
        private string GetNumericTypeName(string text)
        {
            if (text.Contains("f") || text.Contains("F")) return "float";
            if (text.Contains("m") || text.Contains("M")) return "decimal";
            if (text.Contains("d") || text.Contains("D")) return "double";
            if (text.Contains("L") || text.Contains("l")) return "long";
            if (text.Contains("U") || text.Contains("u")) return text.Contains("L") ? "ulong" : "uint";
            // Если есть точка и нет суффикса - это double по умолчанию в C#
            if (text.Contains(".")) return "double";
            return "int";
        }

        private string GenerateConstName(string text)
        {
            var clean = Regex.Replace(text, "[^a-zA-Z0-9]", "_");
            return $"Const_{clean}";
        }
    }
}