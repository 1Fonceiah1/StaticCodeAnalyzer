using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using System.Collections.Generic;
using System.Linq;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_NamingConvention : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "NAM001", "NAM002" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            Solution solution = document.Project.Solution;
            bool changed = false;

            // Переименовывает методы в PascalCase
            List<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            foreach (MethodDeclarationSyntax method in methods)
            {
                IMethodSymbol? symbol = semanticModel.GetDeclaredSymbol(method);
                if (symbol == null) continue;

                // Пропускает переопределённые методы и явные реализации интерфейсов
                if (symbol.IsOverride || symbol.ExplicitInterfaceImplementations.Any()) continue;

                string current = symbol.Name;
                if (!IsPascalCase(current))
                {
                    string target = ToPascalCase(current);
                    TypeDeclarationSyntax? containingType = method.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                    if (containingType != null && HasNameConflict(target, containingType))
                        continue;

                    solution = Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), target).GetAwaiter().GetResult();
                    changed = true;
                }
            }

            // Переименовывает приватные поля в _camelCase
            List<VariableDeclaratorSyntax> fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)) && !f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                .SelectMany(f => f.Declaration.Variables)
                .ToList();

            foreach (VariableDeclaratorSyntax varDecl in fields)
            {
                ISymbol? symbol = semanticModel.GetDeclaredSymbol(varDecl);
                if (symbol == null) continue;

                string current = symbol.Name;
                if (!IsPrivateFieldConvention(current))
                {
                    string target = ToPrivateFieldConvention(current);
                    TypeDeclarationSyntax? containingType = varDecl.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                    if (containingType != null && HasNameConflict(target, containingType))
                        continue;

                    solution = Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), target).GetAwaiter().GetResult();
                    changed = true;
                }
            }

            return changed ? solution.GetDocument(document.Id) : document;
        }

        // Проверяет, соответствует ли имя соглашению PascalCase
        private bool IsPascalCase(string n) => n.Length > 0 && char.IsUpper(n[0]) && !n.Contains('_');

        // Преобразует имя в PascalCase
        private string ToPascalCase(string n) => char.ToUpperInvariant(n[0]) + (n.Length > 1 ? n.Substring(1) : "");

        // Проверяет соглашение для приватных полей: начинается с "_" и далее строчная буква
        private bool IsPrivateFieldConvention(string n) => n.StartsWith("_") && n.Length > 1 && char.IsLower(n[1]);

        // Преобразует имя в формат _camelCase
        private string ToPrivateFieldConvention(string n) => n.StartsWith("_") ? n : "_" + char.ToLowerInvariant(n[0]) + (n.Length > 1 ? n.Substring(1) : "");

        // Проверяет, существует ли уже член с заданным именем в типе
        private bool HasNameConflict(string newName, TypeDeclarationSyntax typeDecl)
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