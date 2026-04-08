using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_EncapsulateFields : IRefactoringRule
    {
        // Заменяет публичные поля на приватные поля + автоматические свойства
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.Any(SyntaxKind.PublicKeyword) && !f.Modifiers.Any(SyntaxKind.ConstKeyword))
                .ToList();

            foreach (var fieldDecl in fields)
            {
                var variables = fieldDecl.Declaration.Variables.ToList();
                if (!variables.Any()) continue;

                foreach (var variable in variables)
                {
                    string fieldName = variable.Identifier.Text;
                    string propertyName = ToPascalCase(fieldName);
                    string fieldType = fieldDecl.Declaration.Type.ToString();

                    // Делает поле приватным
                    var newModifiers = fieldDecl.Modifiers.Where(m => !m.IsKind(SyntaxKind.PublicKeyword));
                    if (!newModifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
                        newModifiers = newModifiers.Append(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
                    var newFieldDecl = fieldDecl.WithModifiers(SyntaxFactory.TokenList(newModifiers))
                        .WithDeclaration(fieldDecl.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(variable)))
                        .NormalizeWhitespace();

                    editor.ReplaceNode(fieldDecl, newFieldDecl);

                    // Создаёт публичное автоматическое свойство
                    var property = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(fieldType), propertyName)
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                        .WithAccessorList(SyntaxFactory.AccessorList(
                            SyntaxFactory.List(new[]
                            {
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                            })))
                        .NormalizeWhitespace();

                    editor.AddMember(fieldDecl.Parent as TypeDeclarationSyntax, property);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        // Преобразует имя поля в PascalCase для свойства
        private static string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.StartsWith("_") && name.Length > 1)
                name = name.Substring(1);
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
    }
}