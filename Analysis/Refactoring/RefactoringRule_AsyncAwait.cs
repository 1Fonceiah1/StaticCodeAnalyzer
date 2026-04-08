using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_AsyncAwait : IRefactoringRule
    {
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

            foreach (var method in methods)
            {
                var threadSleeps = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                                  ma.Expression.ToString() == "Thread" &&
                                  ma.Name.Identifier.Text == "Sleep")
                    .ToList();

                if (threadSleeps.Any())
                {
                    var compilationUnit = root as CompilationUnitSyntax ?? method.FirstAncestorOrSelf<CompilationUnitSyntax>();
                    if (compilationUnit != null && !compilationUnit.Usings.Any(u => u.Name.ToString() == "System.Threading.Tasks"))
                    {
                        var usingTask = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks"));
                        var newCompilationUnit = compilationUnit.AddUsings(usingTask);
                        editor.ReplaceNode(compilationUnit, newCompilationUnit);
                        root = newCompilationUnit;
                    }

                    var newMethod = method;
                    foreach (var sleep in threadSleeps)
                    {
                        var arg = sleep.ArgumentList.Arguments.FirstOrDefault();
                        if (arg == null) continue;
                        var delay = SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("Task"),
                                SyntaxFactory.IdentifierName("Delay")))
                            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(arg)));
                        var awaitDelay = SyntaxFactory.AwaitExpression(delay);
                        newMethod = newMethod.ReplaceNode(sleep, awaitDelay);
                    }

                    if (!newMethod.Modifiers.Any(SyntaxKind.AsyncKeyword))
                        newMethod = newMethod.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));

                    var returnType = semanticModel.GetTypeInfo(method.ReturnType, cancellationToken).Type;
                    if (returnType?.SpecialType == SpecialType.System_Void)
                        newMethod = newMethod.WithReturnType(SyntaxFactory.ParseTypeName("Task"));
                    else if (returnType != null && returnType.Name != "Task" && !returnType.Name.StartsWith("Task`"))
                        newMethod = newMethod.WithReturnType(
                            SyntaxFactory.GenericName(
                                SyntaxFactory.Identifier("Task"),
                                SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(method.ReturnType))));

                    editor.ReplaceNode(method, newMethod);
                    changed = true;
                }
                else if (method.Modifiers.Any(SyntaxKind.AsyncKeyword) && !method.DescendantNodes().OfType<AwaitExpressionSyntax>().Any())
                {
                    var newModifiers = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.AsyncKeyword));
                    var newMethod = method.WithModifiers(SyntaxFactory.TokenList(newModifiers));
                    editor.ReplaceNode(method, newMethod);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}