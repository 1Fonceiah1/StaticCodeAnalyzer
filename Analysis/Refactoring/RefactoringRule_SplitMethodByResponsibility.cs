using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_SplitMethodByResponsibility : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "SPL001", "CPX001" };

        private const int MaxStatements = 15;

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            bool changed = false;

            List<MethodDeclarationSyntax> methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Body != null && m.Body.Statements.Count > MaxStatements)
                .ToList();

            foreach (MethodDeclarationSyntax method in methods)
            {
                ClassDeclarationSyntax? classDecl = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                if (classDecl == null) continue;

                // Проверяет наличие уже существующих методов ComputeValues и DisplayOutputs в классе
                bool hasCompute = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Any(m => m.Identifier.Text == "ComputeValues");
                bool hasDisplay = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Any(m => m.Identifier.Text == "DisplayOutputs");

                // Проверяет, не вызывает ли уже метод ComputeValues() или DisplayOutputs()
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

                // Пропускает, если оба метода уже существуют и вызываются
                if (hasCompute && hasDisplay && alreadyHasComputeCall && alreadyHasDisplayCall)
                    continue;

                List<StatementSyntax> statements = method.Body.Statements.ToList();
                List<StatementSyntax> computation = new List<StatementSyntax>();
                List<StatementSyntax> output = new List<StatementSyntax>();
                List<StatementSyntax> other = new List<StatementSyntax>();

                foreach (StatementSyntax stmt in statements)
                {
                    if (ContainsConsoleWrite(stmt))
                        output.Add(stmt);
                    else if (ContainsComputation(stmt))
                        computation.Add(stmt);
                    else
                        other.Add(stmt);
                }

                // Пропускает, если нет ни вычислений, ни вывода
                if (!computation.Any() && !output.Any())
                    continue;

                // Пропускает методы, содержащие локальные переменные (для безопасности)
                bool hasLocalVariables = method.Body.Statements
                    .OfType<LocalDeclarationStatementSyntax>()
                    .Any();
                if (hasLocalVariables && (computation.Any() || output.Any()))
                    continue;

                // Создаёт метод ComputeValues, если есть вычисления и метод ещё не создан
                if (computation.Any() && !hasCompute && !alreadyHasComputeCall)
                {
                    MethodDeclarationSyntax computeMethod = SyntaxFactory.MethodDeclaration(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                            "ComputeValues")
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                        .WithBody(SyntaxFactory.Block(computation))
                        .NormalizeWhitespace();
                    editor.AddMember(classDecl, computeMethod);
                    hasCompute = true;
                    changed = true;
                }

                // Создаёт метод DisplayOutputs, если есть вывод и метод ещё не создан
                if (output.Any() && !hasDisplay && !alreadyHasDisplayCall)
                {
                    MethodDeclarationSyntax displayMethod = SyntaxFactory.MethodDeclaration(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                            "DisplayOutputs")
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                        .WithBody(SyntaxFactory.Block(output))
                        .NormalizeWhitespace();
                    editor.AddMember(classDecl, displayMethod);
                    hasDisplay = true;
                    changed = true;
                }

                // Если хотя бы один метод добавлен, перестраивает тело исходного метода
                if ((hasCompute && computation.Any()) || (hasDisplay && output.Any()))
                {
                    List<StatementSyntax> newBodyStatements = new List<StatementSyntax>();
                    if (hasCompute && computation.Any() && !alreadyHasComputeCall)
                        newBodyStatements.Add(CreateInvocation("ComputeValues"));
                    if (hasDisplay && output.Any() && !alreadyHasDisplayCall)
                        newBodyStatements.Add(CreateInvocation("DisplayOutputs"));
                    newBodyStatements.AddRange(other);

                    MethodDeclarationSyntax newMethod = method.WithBody(SyntaxFactory.Block(newBodyStatements));
                    editor.ReplaceNode(method, newMethod);
                    changed = true;
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        // Проверяет, содержит ли оператор вызов Console.Write/WriteLine
        private bool ContainsConsoleWrite(StatementSyntax stmt)
        {
            return stmt.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                            ma.Expression is IdentifierNameSyntax { Identifier.Text: "Console" } &&
                            (ma.Name.Identifier.Text == "WriteLine" || ma.Name.Identifier.Text == "Write"));
        }

        // Проверяет, содержит ли оператор вычислительные операции (присваивания или арифметику)
        private bool ContainsComputation(StatementSyntax stmt)
        {
            return stmt.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any() ||
                   stmt.DescendantNodes().OfType<BinaryExpressionSyntax>()
                       .Any(b => b.IsKind(SyntaxKind.AddExpression) || 
                                 b.IsKind(SyntaxKind.MultiplyExpression) || 
                                 b.IsKind(SyntaxKind.SubtractExpression));
        }

        // Создаёт оператор вызова метода без аргументов
        private StatementSyntax CreateInvocation(string methodName)
        {
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName(methodName),
                    SyntaxFactory.ArgumentList()));
        }
    }
}