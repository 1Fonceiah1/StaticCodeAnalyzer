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
                var threadSleeps = method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                                  ma.Expression is IdentifierNameSyntax { Identifier.Text: "Thread" } &&
                                  ma.Name.Identifier.Text == "Sleep")
                    .ToList();

                if (threadSleeps.Any())
                {
                    // Добавляем using System.Threading.Tasks
                    var compUnit = root as CompilationUnitSyntax ?? method.FirstAncestorOrSelf<CompilationUnitSyntax>();
                    if (compUnit != null && !compUnit.Usings.Any(u => u.Name?.ToString() == "System.Threading.Tasks"))
                    {
                        var usingTask = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks"));
                        var newCompUnit = compUnit.AddUsings(usingTask).NormalizeWhitespace();
                        editor.ReplaceNode(compUnit, newCompUnit);
                        root = newCompUnit;
                    }

                    var newMethod = method;
                    foreach (var sleep in threadSleeps)
                    {
                        var arg = sleep.ArgumentList.Arguments.FirstOrDefault();
                        if (arg == null) continue;

                        var delayCall = SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("Task"),
                                    SyntaxFactory.IdentifierName("Delay")))
                            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(arg)));

                        // Ключевой фикс: AwaitExpression автоматически добавляет пробел после await
                        var awaitDelay = SyntaxFactory.AwaitExpression(delayCall);
                        newMethod = newMethod.ReplaceNode(sleep, awaitDelay);
                    }

                    // Добавляем async с правильным пробелом
                    if (!newMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
                    {
                        // Вставляем async ПЕРЕД первым модификатором или перед типом возврата
                        var newModifiers = newMethod.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword).WithTrailingTrivia(SyntaxFactory.Space));
                        newMethod = newMethod.WithModifiers(newModifiers);
                    }

                    // Корректируем возвращаемый тип
                    var returnTypeSymbol = semanticModel.GetTypeInfo(method.ReturnType, cancellationToken).Type;
                    if (returnTypeSymbol != null)
                    {
                        if (returnTypeSymbol.SpecialType == SpecialType.System_Void)
                            newMethod = newMethod.WithReturnType(SyntaxFactory.ParseTypeName("Task").WithTrailingTrivia(SyntaxFactory.Space));
                        else if (returnTypeSymbol.Name != "Task" && (returnTypeSymbol.OriginalDefinition?.Name ?? "") != "Task`1")
                        {
                            var genericTask = SyntaxFactory.GenericName("Task")
                                .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(method.ReturnType)));
                            newMethod = newMethod.WithReturnType(genericTask.WithTrailingTrivia(SyntaxFactory.Space));
                        }
                    }

                    editor.ReplaceNode(method, newMethod.NormalizeWhitespace());
                    changed = true;
                }
                // Удаляем бесполезный async
                else if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)) && 
                         !method.DescendantNodes().OfType<AwaitExpressionSyntax>().Any())
                {
                    var newModifiers = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.AsyncKeyword));
                    var newMethod = method.WithModifiers(SyntaxFactory.TokenList(newModifiers));
                    editor.ReplaceNode(method, newMethod);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument().WithSyntaxRoot(await editor.GetChangedDocument().GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)) : document;
        }
    }
}