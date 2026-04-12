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
    public class RefactoringRule_EncapsulateFields : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "ENC001" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            // Находит все публичные поля (кроме констант)
            List<FieldDeclarationSyntax> publicFields = root.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) && 
                            !f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                .ToList();

            foreach (FieldDeclarationSyntax fieldDecl in publicFields)
            {
                List<VariableDeclaratorSyntax> variables = fieldDecl.Declaration.Variables.ToList();
                if (!variables.Any()) continue;

                TypeDeclarationSyntax? typeDecl = fieldDecl.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                if (typeDecl == null) continue;

                foreach (VariableDeclaratorSyntax variable in variables)
                {
                    string originalName = variable.Identifier.Text;
                    string propertyName = ToPascalCase(originalName);
                    string fieldName = originalName.StartsWith("_") ? originalName : $"_{ToCamelCase(originalName)}";

                    // Проверяет, не существует ли уже свойства с таким именем
                    bool propertyExists = typeDecl.Members
                        .OfType<PropertyDeclarationSyntax>()
                        .Any(p => p.Identifier.Text.Equals(propertyName, System.StringComparison.OrdinalIgnoreCase));
                    if (propertyExists) continue;

                    // Проверяет, не существует ли уже поля с таким именем
                    bool fieldExists = typeDecl.Members
                        .OfType<FieldDeclarationSyntax>()
                        .SelectMany(f => f.Declaration.Variables)
                        .Any(v => v.Identifier.Text.Equals(fieldName, System.StringComparison.OrdinalIgnoreCase));
                    if (fieldExists) continue;

                    // Создаёт приватное поле вместо публичного
                    IEnumerable<SyntaxToken> newModifiers = fieldDecl.Modifiers
                        .Where(m => !m.IsKind(SyntaxKind.PublicKeyword))
                        .Append(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
                    
                    FieldDeclarationSyntax privateField = fieldDecl
                        .WithModifiers(SyntaxFactory.TokenList(newModifiers))
                        .WithDeclaration(fieldDecl.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(
                            variable.WithIdentifier(SyntaxFactory.Identifier(fieldName)))))
                        .NormalizeWhitespace();

                    // Создаёт автоматическое свойство для доступа к полю
                    PropertyDeclarationSyntax property = SyntaxFactory.PropertyDeclaration(
                            fieldDecl.Declaration.Type.WithTrailingTrivia(SyntaxFactory.Space),
                            SyntaxFactory.Identifier(propertyName))
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
                        .WithAccessorList(SyntaxFactory.AccessorList(
                            SyntaxFactory.List(new[]
                            {
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                            })))
                        .NormalizeWhitespace();

                    editor.ReplaceNode(fieldDecl, privateField);
                    editor.InsertAfter(privateField, property);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        // Преобразует имя в формат PascalCase
        private string ToPascalCase(string name) => string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : "");
        
        // Преобразует имя в формат camelCase
        private string ToCamelCase(string name) => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : "");
    }
}