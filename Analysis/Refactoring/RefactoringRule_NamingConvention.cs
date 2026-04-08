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

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classes)
            {
                var symbol = semanticModel.GetDeclaredSymbol(classDecl, cancellationToken);
                if (symbol != null && !IsPascalCase(symbol.Name))
                {
                    var newName = ToPascalCase(symbol.Name);
                    solution = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName, cancellationToken);
                    changed = true;
                }
            }

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var symbol = semanticModel.GetDeclaredSymbol(method, cancellationToken);
                if (symbol != null && !IsPascalCase(symbol.Name) && !symbol.IsOverride && !symbol.ExplicitInterfaceImplementations.Any())
                {
                    var newName = ToPascalCase(symbol.Name);
                    solution = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName, cancellationToken);
                    changed = true;
                }
            }

            var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .Where(v => v.Parent?.Parent is FieldDeclarationSyntax fieldDecl && fieldDecl.Modifiers.Any(SyntaxKind.PrivateKeyword) && !fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword));
            foreach (var fieldVar in fields)
            {
                var symbol = semanticModel.GetDeclaredSymbol(fieldVar, cancellationToken);
                if (symbol != null && !IsCamelCaseWithUnderscore(symbol.Name))
                {
                    var newName = ToCamelCaseWithUnderscore(symbol.Name);
                    solution = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName, cancellationToken);
                    changed = true;
                }
            }

            return changed ? solution.GetDocument(document.Id) : document;
        }

        private bool IsPascalCase(string name) => name.Length > 0 && char.IsUpper(name[0]);
        private string ToPascalCase(string name) => char.ToUpperInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : "");
        private bool IsCamelCaseWithUnderscore(string name) => name.StartsWith("_") && name.Length > 1 && char.IsLower(name[1]);
        private string ToCamelCaseWithUnderscore(string name)
        {
            if (name.StartsWith("_")) return name;
            return "_" + char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : "");
        }
    }
}