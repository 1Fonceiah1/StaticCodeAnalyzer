using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Collections.Generic;
using System.Linq;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_UnusedVariable : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "UNU001" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            bool changed = false;

            // Находит все объявления локальных переменных
            List<VariableDeclaratorSyntax> localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Parent is VariableDeclarationSyntax decl && decl.Parent is LocalDeclarationStatementSyntax)
                .ToList();

            foreach (VariableDeclaratorSyntax variable in localVars)
            {
                ISymbol? symbol = semanticModel.GetDeclaredSymbol(variable);
                if (symbol == null) continue;

                // Ищет все ссылки на символ переменной
                IEnumerable<ReferencedSymbol> references = SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution).GetAwaiter().GetResult();
                
                int usageCount = references
                    .SelectMany(r => r.Locations)
                    .Count(loc => !loc.IsImplicit && loc.Location.SourceTree == root.SyntaxTree);

                // Удаляет переменную, если она не используется
                if (usageCount == 0)
                {
                    VariableDeclarationSyntax? declaration = variable.Parent as VariableDeclarationSyntax;
                    LocalDeclarationStatementSyntax? statement = declaration?.Parent as LocalDeclarationStatementSyntax;
                    if (statement == null) continue;

                    if (declaration.Variables.Count == 1)
                    {
                        editor.RemoveNode(statement);
                        changed = true;
                    }
                    else
                    {
                        // Удаляет только конкретную переменную из объявления нескольких переменных
                        List<VariableDeclaratorSyntax> newVariables = declaration.Variables.Where(v => v != variable).ToList();
                        if (newVariables.Count == 0)
                        {
                            editor.RemoveNode(statement);
                        }
                        else
                        {
                            VariableDeclarationSyntax newDeclaration = declaration.WithVariables(SyntaxFactory.SeparatedList(newVariables));
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