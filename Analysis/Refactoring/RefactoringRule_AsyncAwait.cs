using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

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
            bool needsTaskUsing = false;

            // Снимок методов из исходного дерева
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

                    // Пакетная замена всех Thread.Sleep → await Task.Delay за один проход
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
                    var returnType = semanticModel.GetTypeInfo(method.ReturnType, cancellationToken).Type;
                    if (returnType?.SpecialType == SpecialType.System_Void)
                        newMethod = newMethod.WithReturnType(SyntaxFactory.ParseTypeName("Task"));
                    else if (returnType != null && returnType.Name != "Task" && !returnType.Name.StartsWith("Task`"))
                        newMethod = newMethod.WithReturnType(
                            SyntaxFactory.GenericName(
                                SyntaxFactory.Identifier("Task"),
                                SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(method.ReturnType))));

                    // Безопасная замена через редактор (использует узел method)
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