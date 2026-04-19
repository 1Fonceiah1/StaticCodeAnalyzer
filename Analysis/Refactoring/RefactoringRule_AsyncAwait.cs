using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_AsyncAwait : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "ASY001" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            bool changed = false;
            bool needsTaskUsing = false;
            Solution solution = document.Project.Solution;

            List<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

            foreach (MethodDeclarationSyntax method in methods)
            {
                List<InvocationExpressionSyntax> threadSleeps = method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                                  ma.Expression.ToString() == "Thread" &&
                                  ma.Name.Identifier.Text == "Sleep")
                    .ToList();

                if (threadSleeps.Any())
                {
                    needsTaskUsing = true;

                    // Заменяет пакетно все вызовы Thread.Sleep на await Task.Delay
                    MethodDeclarationSyntax newMethod = method.ReplaceNodes(threadSleeps, (original, _) =>
                    {
                        ArgumentSyntax? arg = original.ArgumentList.Arguments.FirstOrDefault();
                        if (arg == null) return original;

                        InvocationExpressionSyntax delay = SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("Task"),
                                    SyntaxFactory.IdentifierName("Delay")))
                            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(arg)));

                        return SyntaxFactory.AwaitExpression(delay).WithTriviaFrom(original);
                    });

                    // Добавляет модификатор async, если отсутствует
                    if (!newMethod.Modifiers.Any(SyntaxKind.AsyncKeyword))
                        newMethod = newMethod.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));

                    // Корректирует возвращаемый тип
                    ITypeSymbol? returnTypeSymbol = semanticModel.GetTypeInfo(method.ReturnType).Type;
                    if (returnTypeSymbol?.SpecialType == SpecialType.System_Void)
                    {
                        // Проверяет возможность безопасной замены void на Task
                        bool hasExternalReferences = HasExternalReferences(method, semanticModel, solution);
                        if (!hasExternalReferences)
                        {
                            newMethod = newMethod.WithReturnType(SyntaxFactory.ParseTypeName("Task"));
                        }
                        else
                        {
                            SyntaxTriviaList warningComment = SyntaxFactory.TriviaList(
                                SyntaxFactory.Comment("// ВНИМАНИЕ: метод содержит Thread.Sleep, но его возвращаемый тип не изменён из-за внешних вызовов. Измените вручную на Task."),
                                SyntaxFactory.CarriageReturnLineFeed);
                            newMethod = newMethod.WithLeadingTrivia(warningComment);
                        }
                    }
                    else if (returnTypeSymbol != null &&
                             returnTypeSymbol.Name != "Task" &&
                             !returnTypeSymbol.Name.StartsWith("Task`"))
                    {
                        // Не изменяет автоматически для типов, отличных от Task и void; добавляет предупреждающий комментарий
                        SyntaxTriviaList warningComment = SyntaxFactory.TriviaList(
                            SyntaxFactory.Comment("// ВНИМАНИЕ: метод содержит Thread.Sleep, но его возвращаемый тип не изменён автоматически. Рассмотрите смену на Task<T> вручную."),
                            SyntaxFactory.CarriageReturnLineFeed);
                        newMethod = newMethod.WithLeadingTrivia(warningComment);
                    }
                    // Оставляет без изменений, если возвращаемый тип уже Task или Task<T>

                    editor.ReplaceNode(method, newMethod.NormalizeWhitespace());
                    changed = true;
                }
                // Удаляет бесполезный модификатор async
                else if (method.Modifiers.Any(SyntaxKind.AsyncKeyword) &&
                         !method.DescendantNodes().OfType<AwaitExpressionSyntax>().Any())
                {
                    IEnumerable<SyntaxToken> newModifiers = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.AsyncKeyword));
                    MethodDeclarationSyntax newMethod = method.WithModifiers(SyntaxFactory.TokenList(newModifiers));
                    editor.ReplaceNode(method, newMethod.NormalizeWhitespace());
                    changed = true;
                }
            }

            Document resultDoc = changed ? editor.GetChangedDocument() : document;

            // Добавляет директиву using System.Threading.Tasks, если она отсутствует
            if (needsTaskUsing)
            {
                CompilationUnitSyntax? finalRoot = resultDoc.GetSyntaxRootAsync().GetAwaiter().GetResult() as CompilationUnitSyntax;
                if (finalRoot != null && !finalRoot.Usings.Any(u => u.Name?.ToString() == "System.Threading.Tasks"))
                {
                    UsingDirectiveSyntax usingTask = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks"));
                    CompilationUnitSyntax newRoot = finalRoot.AddUsings(usingTask).NormalizeWhitespace();
                    resultDoc = resultDoc.WithSyntaxRoot(newRoot);
                }
            }

            return resultDoc;
        }

        private bool HasExternalReferences(MethodDeclarationSyntax method, SemanticModel semanticModel, Solution solution)
        {
            IMethodSymbol? methodSymbol = semanticModel.GetDeclaredSymbol(method);
            if (methodSymbol == null) return false;

            IEnumerable<ReferencedSymbol> references = SymbolFinder.FindReferencesAsync(methodSymbol, solution).GetAwaiter().GetResult();
            // Исключает объявление самого метода
            IEnumerable<ReferenceLocation> referencingLocations = references.SelectMany(r => r.Locations)
                                                  .Where(loc => !loc.IsImplicit && loc.Location.SourceSpan != method.Span);
            return referencingLocations.Any();
        }
    }
}