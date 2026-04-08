using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_UnusedVariable : IRefactoringRule
    {
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Ancestors().OfType<LocalDeclarationStatementSyntax>().Any() || v.Ancestors().OfType<ForEachStatementSyntax>().Any())
                .ToList();

            foreach (var variable in localVars)
            {
                var symbol = semanticModel.GetDeclaredSymbol(variable, cancellationToken);
                if (symbol == null) continue;

                var references = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution, cancellationToken).ConfigureAwait(false);
                int refCount = references.SelectMany(r => r.Locations).Count(loc => !loc.IsImplicit);

                if (refCount == 1) // only the declaration itself
                {
                    var declaration = variable.Parent as VariableDeclarationSyntax;
                    var statement = declaration?.Parent as LocalDeclarationStatementSyntax;
                    if (statement != null && declaration.Variables.Count == 1)
                    {
                        editor.RemoveNode(statement);
                        changed = true;
                    }
                    else if (declaration != null && declaration.Variables.Count > 1)
                    {
                        var newDecl = declaration.RemoveNode(variable, SyntaxRemoveOptions.KeepNoTrivia);
                        if (newDecl is VariableDeclarationSyntax newVarDecl && newVarDecl.Variables.Count == 0)
                        {
                            editor.RemoveNode(statement);
                        }
                        else
                        {
                            editor.ReplaceNode(declaration, newDecl);
                        }
                        changed = true;
                    }
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}