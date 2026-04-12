using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
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
            var solution = document.Project.Solution;

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

                    // Корректирует возвращаемый тип
                    var returnTypeSymbol = semanticModel.GetTypeInfo(method.ReturnType, cancellationToken).Type;
                    if (returnTypeSymbol?.SpecialType == SpecialType.System_Void)
                    {
                        // Проверяем, можно ли безопасно изменить void → Task
                        bool hasExternalReferences = await HasExternalReferencesAsync(method, semanticModel, solution, cancellationToken).ConfigureAwait(false);
                        if (!hasExternalReferences)
                        {
                            newMethod = newMethod.WithReturnType(SyntaxFactory.ParseTypeName("Task"));
                        }
                        else
                        {
                            var warningComment = SyntaxFactory.TriviaList(
                                SyntaxFactory.Comment("// ВНИМАНИЕ: метод содержит Thread.Sleep, но его возвращаемый тип не изменён из-за внешних вызовов. Измените вручную на Task."),
                                SyntaxFactory.CarriageReturnLineFeed);
                            newMethod = newMethod.WithLeadingTrivia(warningComment);
                        }
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
                    // Если уже Task или Task<T> – оставляем как есть

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

        private async Task<bool> HasExternalReferencesAsync(MethodDeclarationSyntax method, SemanticModel semanticModel, Solution solution, CancellationToken ct)
        {
            var methodSymbol = semanticModel.GetDeclaredSymbol(method, ct);
            if (methodSymbol == null) return false;

            var references = await SymbolFinder.FindReferencesAsync(methodSymbol, solution, ct).ConfigureAwait(false);
            // Исключаем само объявление метода
            var referencingLocations = references.SelectMany(r => r.Locations)
                                                  .Where(loc => !loc.IsImplicit && loc.Location.SourceSpan != method.Span);
            return referencingLocations.Any();
        }
    }
}