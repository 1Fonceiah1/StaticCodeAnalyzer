using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_UnusedVariable : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "UNU001" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Parent is VariableDeclarationSyntax decl && decl.Parent is LocalDeclarationStatementSyntax)
                .ToList();

            foreach (var variable in localVars)
            {
                var symbol = semanticModel.GetDeclaredSymbol(variable, cancellationToken);
                if (symbol == null) continue;

                var references = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution, cancellationToken).ConfigureAwait(false);
                
                int usageCount = references
                    .SelectMany(r => r.Locations)
                    .Count(loc => !loc.IsImplicit && loc.Location.SourceTree == root.SyntaxTree);

                if (usageCount == 0)
                {
                    var declaration = variable.Parent as VariableDeclarationSyntax;
                    var statement = declaration?.Parent as LocalDeclarationStatementSyntax;
                    if (statement == null) continue;

                    if (declaration.Variables.Count == 1)
                    {
                        editor.RemoveNode(statement);
                        changed = true;
                    }
                    else
                    {
                        var newVariables = declaration.Variables.Where(v => v != variable).ToArray();
                        if (newVariables.Length == 0)
                        {
                            editor.RemoveNode(statement);
                        }
                        else
                        {
                            var newDeclaration = declaration.WithVariables(SyntaxFactory.SeparatedList(newVariables));
                            editor.ReplaceNode(declaration, newDeclaration);
                        }
                        changed = true;
                    }
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}