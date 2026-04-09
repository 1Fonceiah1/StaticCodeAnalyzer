using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Rename;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_NamingConvention : IRefactoringRule
    {
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var solution = document.Project.Solution;
            bool changed = false;

            // Переименование методов в PascalCase
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var symbol = semanticModel.GetDeclaredSymbol(method, cancellationToken);
                if (symbol == null) continue;
                
                // Пропускает переопределённые методы и явные реализации интерфейсов
                if (symbol.IsOverride || symbol.ExplicitInterfaceImplementations.Any()) continue;
                
                string current = symbol.Name;
                if (!IsPascalCase(current))
                {
                    string target = ToPascalCase(current);
                    // Проверяет, нет ли конфликта имён
                    var containingType = method.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                    if (containingType != null && HasNameConflict(target, containingType, semanticModel, cancellationToken))
                        continue;
                    
                    solution = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), target, cancellationToken);
                    changed = true;
                }
            }

            // Переименование приватных полей в _camelCase
            var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)) && !f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                .SelectMany(f => f.Declaration.Variables);

            foreach (var varDecl in fields)
            {
                var symbol = semanticModel.GetDeclaredSymbol(varDecl, cancellationToken);
                if (symbol == null) continue;

                string current = symbol.Name;
                if (!IsPrivateFieldConvention(current))
                {
                    string target = ToPrivateFieldConvention(current);
                    var containingType = varDecl.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                    if (containingType != null && HasNameConflict(target, containingType, semanticModel, cancellationToken))
                        continue;
                    
                    solution = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), target, cancellationToken);
                    changed = true;
                }
            }

            return changed ? solution.GetDocument(document.Id) : document;
        }

        private bool IsPascalCase(string n) => n.Length > 0 && char.IsUpper(n[0]) && !n.Contains('_');
        private string ToPascalCase(string n) => char.ToUpperInvariant(n[0]) + (n.Length > 1 ? n.Substring(1) : "");
        private bool IsPrivateFieldConvention(string n) => n.StartsWith("_") && n.Length > 1 && char.IsLower(n[1]);
        private string ToPrivateFieldConvention(string n) => n.StartsWith("_") ? n : "_" + char.ToLowerInvariant(n[0]) + (n.Length > 1 ? n.Substring(1) : "");
        
        private bool HasNameConflict(string newName, TypeDeclarationSyntax typeDecl, SemanticModel model, CancellationToken ct)
        {
            return typeDecl.Members
                .OfType<MemberDeclarationSyntax>()
                .Any(m => m is BaseTypeDeclarationSyntax baseType && baseType.Identifier.Text == newName ||
                          m is FieldDeclarationSyntax field && field.Declaration.Variables.Any(v => v.Identifier.Text == newName) ||
                          m is PropertyDeclarationSyntax prop && prop.Identifier.Text == newName ||
                          m is MethodDeclarationSyntax method && method.Identifier.Text == newName);
        }
    }
}