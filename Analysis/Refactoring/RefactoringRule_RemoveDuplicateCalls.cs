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
    public class RefactoringRule_RemoveDuplicateCalls : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "DUP002" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Body != null);
            foreach (var method in methods)
            {
                var statements = method.Body.Statements.OfType<ExpressionStatementSyntax>().ToList();
                var groups = new Dictionary<string, List<InvocationExpressionSyntax>>();

                foreach (var stmt in statements)
                {
                    foreach (var inv in stmt.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        var symbol = semanticModel.GetSymbolInfo(inv, cancellationToken).Symbol as IMethodSymbol;
                        if (symbol == null || symbol.ReturnsVoid) continue;

                        var sig = $"{symbol.ContainingType?.ToDisplayString()}.{symbol.Name}({string.Join(",", symbol.Parameters.Select(p => p.Type.ToDisplayString()))})";
                        if (!groups.TryGetValue(sig, out var list)) groups[sig] = list = new List<InvocationExpressionSyntax>();
                        list.Add(inv);
                    }
                }

                foreach (var group in groups.Where(g => g.Value.Count > 1))
                {
                    var first = group.Value.First();
                    var firstStmt = first.FirstAncestorOrSelf<StatementSyntax>();
                    if (firstStmt == null) continue;

                    var methodSym = semanticModel.GetSymbolInfo(first, cancellationToken).Symbol as IMethodSymbol;
                    if (methodSym == null) continue;

                    string varName = $"cached_{methodSym.Name}{char.ToUpperInvariant(methodSym.Name.Length > 4 ? methodSym.Name[4] : 'V')}{methodSym.Name.Substring(1)}";
                    varName = EnsureUnique(varName, method);

                    var decl = SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.ParseTypeName(methodSym.ReturnType.ToDisplayString()),
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator(varName)
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(first)))));

                    editor.InsertBefore(firstStmt, decl);

                    foreach (var inv in group.Value.Skip(1))
                    {
                        editor.ReplaceNode(inv, SyntaxFactory.IdentifierName(varName).WithTriviaFrom(inv));
                    }
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private string EnsureUnique(string baseName, MethodDeclarationSyntax method)
        {
            var existing = method.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Select(v => v.Identifier.Text)
                .ToHashSet();
            string candidate = baseName;
            int i = 1;
            while (existing.Contains(candidate)) candidate = $"{baseName}{i++}";
            return candidate;
        }
    }
}