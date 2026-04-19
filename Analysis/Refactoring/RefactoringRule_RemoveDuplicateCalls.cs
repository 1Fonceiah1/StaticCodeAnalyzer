using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Linq;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_RemoveDuplicateCalls : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "DUP002" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            bool changed = false;

            List<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Body != null).ToList();
            foreach (MethodDeclarationSyntax method in methods)
            {
                List<ExpressionStatementSyntax> statements = method.Body.Statements.OfType<ExpressionStatementSyntax>().ToList();
                Dictionary<string, List<InvocationExpressionSyntax>> groups = new Dictionary<string, List<InvocationExpressionSyntax>>();

                // Группирует вызовы методов по сигнатуре
                foreach (ExpressionStatementSyntax stmt in statements)
                {
                    foreach (InvocationExpressionSyntax inv in stmt.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        IMethodSymbol? symbol = semanticModel.GetSymbolInfo(inv).Symbol as IMethodSymbol;
                        if (symbol == null || symbol.ReturnsVoid) continue;

                        string sig = $"{symbol.ContainingType?.ToDisplayString()}.{symbol.Name}({string.Join(",", symbol.Parameters.Select(p => p.Type.ToDisplayString()))})";
                        if (!groups.TryGetValue(sig, out List<InvocationExpressionSyntax>? list))
                        {
                            list = new List<InvocationExpressionSyntax>();
                            groups[sig] = list;
                        }
                        list.Add(inv);
                    }
                }

                // Обрабатывает сигнатуры, встречающиеся более одного раза
                foreach (KeyValuePair<string, List<InvocationExpressionSyntax>> group in groups.Where(g => g.Value.Count > 1))
                {
                    InvocationExpressionSyntax first = group.Value.First();
                    StatementSyntax? firstStmt = first.FirstAncestorOrSelf<StatementSyntax>();
                    if (firstStmt == null) continue;

                    IMethodSymbol? methodSym = semanticModel.GetSymbolInfo(first).Symbol as IMethodSymbol;
                    if (methodSym == null) continue;

                    // Формирует уникальное имя для переменной кеша
                    string varName = $"cached_{methodSym.Name}{char.ToUpperInvariant(methodSym.Name.Length > 4 ? methodSym.Name[4] : 'V')}{methodSym.Name.Substring(1)}";
                    varName = EnsureUnique(varName, method);

                    // Создаёт объявление локальной переменной с результатом вызова
                    LocalDeclarationStatementSyntax decl = SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.ParseTypeName(methodSym.ReturnType.ToDisplayString()),
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator(varName)
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(first)))));

                    editor.InsertBefore(firstStmt, decl);

                    // Заменяет последующие вызовы на использование кешированной переменной
                    foreach (InvocationExpressionSyntax inv in group.Value.Skip(1))
                    {
                        editor.ReplaceNode(inv, SyntaxFactory.IdentifierName(varName).WithTriviaFrom(inv));
                    }
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        // Гарантирует уникальность имени переменной в пределах метода
        private string EnsureUnique(string baseName, MethodDeclarationSyntax method)
        {
            HashSet<string> existing = method.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Select(v => v.Identifier.Text)
                .ToHashSet();
            string candidate = baseName;
            int i = 1;
            while (existing.Contains(candidate))
            {
                candidate = $"{baseName}{i++}";
            }
            return candidate;
        }
    }
}