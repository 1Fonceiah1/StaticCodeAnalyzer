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
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (method.Body == null) continue;
                var statements = method.Body.Statements.ToList();
                var invocationGroups = new Dictionary<string, List<InvocationExpressionSyntax>>();

                for (int i = 0; i < statements.Count; i++)
                {
                    var invocations = statements[i].DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();
                    foreach (var inv in invocations)
                    {
                        var signature = GetSimplifiedSignature(inv);
                        if (!invocationGroups.ContainsKey(signature))
                            invocationGroups[signature] = new List<InvocationExpressionSyntax>();
                        invocationGroups[signature].Add(inv);
                    }
                }

                foreach (var group in invocationGroups.Where(g => g.Value.Count > 1))
                {
                    var firstInv = group.Value.First();
                    var methodSymbol = semanticModel.GetSymbolInfo(firstInv, cancellationToken).Symbol as IMethodSymbol;
                    if (methodSymbol == null) continue;
                    if (methodSymbol.ReturnsVoid) continue;

                    var firstStatement = firstInv.FirstAncestorOrSelf<StatementSyntax>();
                    if (firstStatement == null) continue;

                    string baseName = methodSymbol.Name.ToLower();
                    if (baseName.StartsWith("get")) baseName = baseName.Substring(3);
                    string varName = $"cached{char.ToUpperInvariant(baseName[0])}{baseName.Substring(1)}";
                    varName = GetUniqueVariableName(varName, method);

                    var typeName = SyntaxFactory.ParseTypeName(methodSymbol.ReturnType.ToDisplayString());
                    var declarator = SyntaxFactory.VariableDeclarator(varName)
                        .WithInitializer(SyntaxFactory.EqualsValueClause(firstInv));
                    var declaration = SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(typeName, SyntaxFactory.SingletonSeparatedList(declarator)));
                    editor.InsertBefore(firstStatement, declaration);

                    foreach (var inv in group.Value.Skip(1))
                    {
                        var replacement = SyntaxFactory.IdentifierName(varName).WithTriviaFrom(inv);
                        editor.ReplaceNode(inv, replacement);
                    }
                    changed = true;
                }
            }
            return changed ? editor.GetChangedDocument() : document;
        }

        private string GetSimplifiedSignature(InvocationExpressionSyntax inv)
        {
            if (inv.Expression is IdentifierNameSyntax id)
                return id.Identifier.Text;
            if (inv.Expression is MemberAccessExpressionSyntax ma)
                return ma.Name.Identifier.Text;
            return inv.ToString();
        }

        private string GetUniqueVariableName(string baseName, MethodDeclarationSyntax method)
        {
            var existing = method.Body.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .Select(v => v.Identifier.Text)
                .Concat(method.Body.DescendantNodes().OfType<IdentifierNameSyntax>().Select(id => id.Identifier.Text))
                .ToHashSet();
            string candidate = baseName;
            int counter = 1;
            while (existing.Contains(candidate))
            {
                candidate = $"{baseName}{counter}";
                counter++;
            }
            return candidate;
        }
    }
}