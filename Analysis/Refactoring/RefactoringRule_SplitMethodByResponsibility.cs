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
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Body != null && m.Body.Statements.Count > 15)
                .ToList();

            foreach (var method in methods)
            {
                var statements = method.Body.Statements.ToList();
                var computationStatements = new List<StatementSyntax>();
                var outputStatements = new List<StatementSyntax>();
                var otherStatements = new List<StatementSyntax>();

                foreach (var stmt in statements)
                {
                    if (ContainsConsoleWrite(stmt))
                        outputStatements.Add(stmt);
                    else if (ContainsArithmeticOrAssignment(stmt))
                        computationStatements.Add(stmt);
                    else
                        otherStatements.Add(stmt);
                }

                if (computationStatements.Any() && outputStatements.Any())
                {
                    var className = method.Parent as TypeDeclarationSyntax;
                    if (className != null)
                    {
                        if (computationStatements.Any())
                        {
                            var computeMethod = CreateComputeMethod(computationStatements, method);
                            editor.AddMember(className, computeMethod);
                            changed = true;
                        }
                        if (outputStatements.Any())
                        {
                            var outputMethod = CreateOutputMethod(outputStatements, method);
                            editor.AddMember(className, outputMethod);
                            changed = true;
                        }

                        var newBodyStatements = new List<StatementSyntax>();
                        if (computationStatements.Any())
                            newBodyStatements.Add(CreateInvocationStatement("ComputeValues"));
                        if (outputStatements.Any())
                            newBodyStatements.Add(CreateInvocationStatement("DisplayOutputs"));
                        newBodyStatements.AddRange(otherStatements);

                        var newMethod = method.WithBody(SyntaxFactory.Block(newBodyStatements));
                        editor.ReplaceNode(method, newMethod);
                        changed = true;
                    }
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private bool ContainsConsoleWrite(StatementSyntax stmt)
        {
            return stmt.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                            ma.Expression.ToString() == "Console" &&
                            (ma.Name.Identifier.Text == "WriteLine" || ma.Name.Identifier.Text == "Write"));
        }

        private bool ContainsArithmeticOrAssignment(StatementSyntax stmt)
        {
            return stmt.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any() ||
                   stmt.DescendantNodes().OfType<BinaryExpressionSyntax>().Any(b =>
                       b.IsKind(SyntaxKind.AddExpression) ||
                       b.IsKind(SyntaxKind.MultiplyExpression) ||
                       b.IsKind(SyntaxKind.SubtractExpression));
        }

        private MethodDeclarationSyntax CreateComputeMethod(List<StatementSyntax> statements, MethodDeclarationSyntax original)
        {
            var body = SyntaxFactory.Block(statements);
            return SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    "ComputeValues")
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                .WithBody(body)
                .NormalizeWhitespace();
        }

        private MethodDeclarationSyntax CreateOutputMethod(List<StatementSyntax> statements, MethodDeclarationSyntax original)
        {
            var body = SyntaxFactory.Block(statements);
            return SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    "DisplayOutputs")
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                .WithBody(body)
                .NormalizeWhitespace();
        }

        private StatementSyntax CreateInvocationStatement(string methodName)
        {
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName(methodName),
                    SyntaxFactory.ArgumentList()));
        }
    }
}