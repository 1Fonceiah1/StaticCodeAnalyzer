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
        public IEnumerable<string> TargetIssueCodes => new[] { "ASY001" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;
            bool needsTaskUsing = false;

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

            foreach (var method in methods)
            {
                var threadSleeps = method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                                  ma.Expression.ToString() == "Thread" &&
                                  ma.Name.Identifier.Text == "Sleep")
                    .ToList();

                if (threadSleeps.Any())
                {
                    needsTaskUsing = true;

                    // Пакетная замена всех Thread.Sleep → await Task.Delay
                    var newMethod = method.ReplaceNodes(threadSleeps, (original, _) =>
                    {
                        var arg = original.ArgumentList.Arguments.FirstOrDefault();
                        if (arg == null) return original;

                        var delay = SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("Task"),
                                    SyntaxFactory.IdentifierName("Delay")))
                            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(arg)));

                        return SyntaxFactory.AwaitExpression(delay).WithTriviaFrom(original);
                    });

                    // Добавляет async, если отсутствует
                    if (!newMethod.Modifiers.Any(SyntaxKind.AsyncKeyword))
                        newMethod = newMethod.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));

                    // Корректирует возвращаемый тип ТОЛЬКО если метод уже возвращает Task/Task<T> или void
                    var returnTypeSymbol = semanticModel.GetTypeInfo(method.ReturnType, cancellationToken).Type;
                    if (returnTypeSymbol?.SpecialType == SpecialType.System_Void)
                    {
                        // void → Task – меняем (это наименее опасно, но всё равно может сломать код)
                        newMethod = newMethod.WithReturnType(SyntaxFactory.ParseTypeName("Task"));
                    }
                    else if (returnTypeSymbol != null && 
                             returnTypeSymbol.Name != "Task" && 
                             !returnTypeSymbol.Name.StartsWith("Task`"))
                    {
                        // Не-Task и не-void – не меняем автоматически, только выдаём предупреждение через комментарий
                        var warningComment = SyntaxFactory.TriviaList(
                            SyntaxFactory.Comment("// ВНИМАНИЕ: метод содержит Thread.Sleep, но его возвращаемый тип не изменён автоматически. Рассмотрите смену на Task<T> вручную."),
                            SyntaxFactory.CarriageReturnLineFeed);
                        newMethod = newMethod.WithLeadingTrivia(warningComment);
                    }
                    else
                    {
                        // Уже Task или Task<T> – оборачиваем в async Task (если нужно)
                        if (returnTypeSymbol.Name.StartsWith("Task`"))
                        {
                            // Если Task<T>, оставляем как есть (await Task.Delay вернёт Task, но компилятор справится)
                        }
                    }

                    editor.ReplaceNode(method, newMethod.NormalizeWhitespace());
                    changed = true;
                }
                // Удаляет бесполезный async
                else if (method.Modifiers.Any(SyntaxKind.AsyncKeyword) &&
                         !method.DescendantNodes().OfType<AwaitExpressionSyntax>().Any())
                {
                    var newModifiers = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.AsyncKeyword));
                    var newMethod = method.WithModifiers(SyntaxFactory.TokenList(newModifiers));
                    editor.ReplaceNode(method, newMethod.NormalizeWhitespace());
                    changed = true;
                }
            }

            var resultDoc = changed ? editor.GetChangedDocument() : document;

            // Добавляет using System.Threading.Tasks только если он не был добавлен ранее
            if (needsTaskUsing)
            {
                var finalRoot = await resultDoc.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) as CompilationUnitSyntax;
                if (finalRoot != null && !finalRoot.Usings.Any(u => u.Name?.ToString() == "System.Threading.Tasks"))
                {
                    var usingTask = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks"));
                    var newRoot = finalRoot.AddUsings(usingTask).NormalizeWhitespace();
                    resultDoc = resultDoc.WithSyntaxRoot(newRoot);
                }
            }

            return resultDoc;
        }
    }
}