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
        public IEnumerable<string> TargetIssueCodes => new[] { "SPL001", "CPX001" };

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
                var classDecl = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                if (classDecl == null) continue;

                // Проверяем, есть ли уже методы с именами ComputeValues/DisplayOutputs в этом классе
                bool hasCompute = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Any(m => m.Identifier.Text == "ComputeValues");
                bool hasDisplay = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Any(m => m.Identifier.Text == "DisplayOutputs");

                // Проверяем, не вызывает ли уже метод ComputeValues() или DisplayOutputs()
                bool alreadyHasComputeCall = method.Body.Statements
                    .OfType<ExpressionStatementSyntax>()
                    .Any(stmt => stmt.Expression is InvocationExpressionSyntax inv &&
                                 inv.Expression is IdentifierNameSyntax id &&
                                 id.Identifier.Text == "ComputeValues");
                bool alreadyHasDisplayCall = method.Body.Statements
                    .OfType<ExpressionStatementSyntax>()
                    .Any(stmt => stmt.Expression is InvocationExpressionSyntax inv &&
                                 inv.Expression is IdentifierNameSyntax id &&
                                 id.Identifier.Text == "DisplayOutputs");

                // Если оба метода уже существуют в классе и уже вызываются, пропускаем
                if (hasCompute && hasDisplay && alreadyHasComputeCall && alreadyHasDisplayCall)
                    continue;

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

                // Если нет ни вычислений, ни вывода, пропускаем
                if (!computation.Any() && !output.Any())
                    continue;

                // Безопасность: если в методе есть локальные переменные, не выносим блоки
                // (потому что переменные могут быть нужны и в других частях метода)
                bool hasLocalVariables = method.Body.Statements
                    .OfType<LocalDeclarationStatementSyntax>()
                    .Any();
                if (hasLocalVariables && (computation.Any() || output.Any()))
                    continue;

                // Создаём ComputeValues, если есть вычисления и метод ещё не создан
                if (computation.Any() && !hasCompute && !alreadyHasComputeCall)
                {
                    var computeMethod = SyntaxFactory.MethodDeclaration(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                            "ComputeValues")
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                        .WithBody(SyntaxFactory.Block(computation))
                        .NormalizeWhitespace();
                    editor.AddMember(classDecl, computeMethod);
                    hasCompute = true;
                    changed = true;
                }

                // Создаём DisplayOutputs, если есть вывод и метод ещё не создан
                if (output.Any() && !hasDisplay && !alreadyHasDisplayCall)
                {
                    var displayMethod = SyntaxFactory.MethodDeclaration(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                            "DisplayOutputs")
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                        .WithBody(SyntaxFactory.Block(output))
                        .NormalizeWhitespace();
                    editor.AddMember(classDecl, displayMethod);
                    hasDisplay = true;
                    changed = true;
                }

                // Если хотя бы один метод был добавлен, перестраиваем тело исходного метода
                if ((hasCompute && computation.Any()) || (hasDisplay && output.Any()))
                {
                    var newBodyStatements = new List<StatementSyntax>();
                    if (hasCompute && computation.Any() && !alreadyHasComputeCall)
                        newBodyStatements.Add(CreateInvocation("ComputeValues"));
                    if (hasDisplay && output.Any() && !alreadyHasDisplayCall)
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