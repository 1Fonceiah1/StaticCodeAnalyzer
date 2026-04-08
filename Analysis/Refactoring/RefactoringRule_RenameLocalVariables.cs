using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_RenameLocalVariables : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "NAM003", "REN001" };

        private static readonly HashSet<string> PoorNames = new()
        {
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
            "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
            "temp", "tmp", "data", "val", "arg", "obj", "var", "item"
        };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var solution = document.Project.Solution;
            bool changed = false;

            var localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Parent is VariableDeclarationSyntax decl && decl.Parent is LocalDeclarationStatementSyntax)
                .ToList();

            foreach (var variable in localVars)
            {
                var symbol = semanticModel.GetDeclaredSymbol(variable, cancellationToken);
                if (symbol == null) continue;

                var currentName = symbol.Name;
                if (!PoorNames.Contains(currentName)) continue;

                var newName = SuggestBetterName(variable, semanticModel, cancellationToken);
                if (string.IsNullOrEmpty(newName) || newName == currentName) continue;

                // Проверка: нет ли конфликта имён в текущей области
                var method = variable.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (method != null && HasNameConflict(newName, method, semanticModel, cancellationToken))
                    continue;

                solution = await Renamer.RenameSymbolAsync(
                    solution, 
                    symbol, 
                    new SymbolRenameOptions(), 
                    newName, 
                    cancellationToken);
                changed = true;
            }

            return changed ? solution.GetDocument(document.Id) : document;
        }

        private string SuggestBetterName(VariableDeclaratorSyntax variable, SemanticModel semanticModel, CancellationToken ct)
        {
            // Счётчик цикла
            if (variable.FirstAncestorOrSelf<ForStatementSyntax>() != null)
                return "index";

            // Элемент коллекции
            if (variable.FirstAncestorOrSelf<ForEachStatementSyntax>() != null)
                return "item";

            // По типу
            var decl = variable.Parent as VariableDeclarationSyntax;
            if (decl?.Type != null)
            {
                var typeName = decl.Type.ToString().ToLowerInvariant();
                if (typeName.Contains("int") || typeName.Contains("long")) return "number";
                if (typeName.Contains("string")) return "text";
                if (typeName.Contains("list") || typeName.Contains("array") || typeName.Contains("ienumerable")) return "items";
                if (typeName.Contains("bool")) return "flag";
                if (typeName.Contains("datetime")) return "timestamp";
            }

            // По инициализатору
            if (variable.Initializer?.Value is LiteralExpressionSyntax lit)
            {
                if (lit.Token.Value is int) return "value";
                if (lit.Token.Value is string) return "message";
            }

            // Результат вызова метода
            if (variable.Initializer?.Value is InvocationExpressionSyntax)
                return "result";

            return "local";
        }

        private bool HasNameConflict(string newName, MethodDeclarationSyntax method, SemanticModel model, CancellationToken ct)
        {
            return method.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Any(id => id.Identifier.Text == newName && model.GetSymbolInfo(id, ct).Symbol != null);
        }
    }
}