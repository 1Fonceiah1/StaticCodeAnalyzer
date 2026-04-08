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
        private static readonly HashSet<string> BadNames = new HashSet<string>
        {
            "a", "b", "c", "d", "e", "f", "x", "y", "z", "temp", "tmp", "data", "val", "arg"
        };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var solution = document.Project.Solution;
            bool changed = false;

            var localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Ancestors().OfType<LocalDeclarationStatementSyntax>().Any() ||
                            v.Ancestors().OfType<ForEachStatementSyntax>().Any() ||
                            v.Ancestors().OfType<CatchDeclarationSyntax>().Any())
                .ToList();

            foreach (var variable in localVars)
            {
                var symbol = semanticModel.GetDeclaredSymbol(variable, cancellationToken);
                if (symbol == null) continue;
                if (!BadNames.Contains(symbol.Name)) continue;

                string newName = SuggestBetterName(variable, semanticModel, cancellationToken);
                if (string.IsNullOrEmpty(newName) || newName == symbol.Name) continue;

                solution = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName, cancellationToken);
                changed = true;
            }

            return changed ? solution.GetDocument(document.Id) : document;
        }

        private string SuggestBetterName(VariableDeclaratorSyntax variable, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            var forLoop = variable.FirstAncestorOrSelf<ForStatementSyntax>();
            if (forLoop != null && forLoop.Declaration?.Variables.Any(v => v.Identifier.Text == variable.Identifier.Text) == true)
                return "index";

            var forEach = variable.FirstAncestorOrSelf<ForEachStatementSyntax>();
            if (forEach != null && forEach.Identifier.Text == variable.Identifier.Text)
                return "item";

            var declaration = variable.Parent as VariableDeclarationSyntax;
            if (declaration?.Type != null)
            {
                string typeName = declaration.Type.ToString().ToLower();
                if (typeName.Contains("int") || typeName.Contains("long") || typeName.Contains("double") || typeName.Contains("decimal"))
                    return "number";
                if (typeName.Contains("string"))
                    return "text";
                if (typeName.Contains("list") || typeName.Contains("ienumerable") || typeName.Contains("array"))
                    return "items";
                if (typeName.Contains("bool"))
                    return "flag";
            }

            if (variable.Initializer?.Value is LiteralExpressionSyntax literal)
            {
                if (literal.Token.Value is int)
                    return "value";
                if (literal.Token.Value is string)
                    return "message";
            }

            if (variable.Initializer?.Value is InvocationExpressionSyntax)
                return "result";

            var usage = variable.FirstAncestorOrSelf<MethodDeclarationSyntax>()?
                .DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(id => id.Identifier.Text == variable.Identifier.Text)
                .FirstOrDefault();
            if (usage != null && usage.Parent is BinaryExpressionSyntax binary && (binary.IsKind(SyntaxKind.AddExpression) || binary.IsKind(SyntaxKind.MultiplyExpression)))
                return "accumulator";

            return "local";
        }
    }
}