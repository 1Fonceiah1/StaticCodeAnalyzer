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
    public class RefactoringRule_SplitMethodByResponsibility : IRefactoringRule
    {
        private const int MaxStatements = 15;

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Body != null && m.Body.Statements.Count > MaxStatements)
                .ToList();

            foreach (var method in methods)
            {
                var statements = method.Body.Statements.ToList();
                var computation = new List<StatementSyntax>();
                var output = new List<StatementSyntax>();
                var other = new List<StatementSyntax>();

                foreach (var stmt in statements)
                {
                    if (ContainsConsoleWrite(stmt))
                        output.Add(stmt);
                    else if (ContainsComputation(stmt))
                        computation.Add(stmt);
                    else
                        other.Add(stmt);
                }

                if (computation.Any() || output.Any())
                {
                    var classDecl = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                    if (classDecl == null) continue;

                    // Создаём ComputeValues только если есть вычисления
                    if (computation.Any())
                    {
                        var computeMethod = SyntaxFactory.MethodDeclaration(
                                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                                "ComputeValues")
                            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
                            .WithBody(SyntaxFactory.Block(computation))
                            .NormalizeWhitespace();
                        editor.AddMember(classDecl, computeMethod);
                    }

                    // Создаём DisplayOutputs только если есть вывод
                    if (output.Any())
                    {
                        var displayMethod = SyntaxFactory.MethodDeclaration(
                                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                                "DisplayOutputs")
                            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
                            .WithBody(SyntaxFactory.Block(output))
                            .NormalizeWhitespace();
                        editor.AddMember(classDecl, displayMethod);
                    }

                    // Формируем новое тело: вызовы новых методов + остальные операторы
                    var newBodyStatements = new List<StatementSyntax>();
                    if (computation.Any())
                        newBodyStatements.Add(CreateInvocation("ComputeValues"));
                    if (output.Any())
                        newBodyStatements.Add(CreateInvocation("DisplayOutputs"));
                    newBodyStatements.AddRange(other);

                    var newMethod = method.WithBody(SyntaxFactory.Block(newBodyStatements));
                    editor.ReplaceNode(method, newMethod);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private bool ContainsConsoleWrite(StatementSyntax stmt) =>
            stmt.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                            ma.Expression is IdentifierNameSyntax { Identifier.Text: "Console" } &&
                            (ma.Name.Identifier.Text == "WriteLine" || ma.Name.Identifier.Text == "Write"));

        private bool ContainsComputation(StatementSyntax stmt) =>
            stmt.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any() ||
            stmt.DescendantNodes().OfType<BinaryExpressionSyntax>()
                .Any(b => b.IsKind(SyntaxKind.AddExpression) || b.IsKind(SyntaxKind.MultiplyExpression) || b.IsKind(SyntaxKind.SubtractExpression));

        private StatementSyntax CreateInvocation(string methodName) =>
            SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName(methodName),
                    SyntaxFactory.ArgumentList()));
    }
}