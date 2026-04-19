using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_RemoveDuplicates : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "DUP001" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            bool changed = false;

            List<MethodDeclarationSyntax> methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Body != null)
                .ToList();

            for (int i = 0; i < methods.Count; i++)
            {
                for (int j = i + 1; j < methods.Count; j++)
                {
                    // Проверяет эквивалентность тел и сигнатур методов
                    if (AreBodiesEquivalent(methods[i].Body, methods[j].Body) && 
                        AreSignaturesEquivalent(methods[i], methods[j], semanticModel))
                    {
                        MethodDeclarationSyntax targetMethod = methods[j];
                        MethodDeclarationSyntax sourceMethod = methods[i];
                        
                        // Пропускает, если метод уже заменён вызовом другого метода
                        if (targetMethod.Body.Statements.Count == 1 && 
                            targetMethod.Body.Statements.First() is ExpressionStatementSyntax exprStmt &&
                            exprStmt.Expression is InvocationExpressionSyntax inv &&
                            inv.Expression is IdentifierNameSyntax invId &&
                            invId.Identifier.Text == sourceMethod.Identifier.Text)
                            continue;

                        // Получает символ целевого метода
                        IMethodSymbol? methodSymbol = semanticModel.GetDeclaredSymbol(targetMethod);
                        if (methodSymbol == null) continue;

                        // Формирует список аргументов из параметров целевого метода
                        ArgumentListSyntax arguments = SyntaxFactory.ArgumentList(
                            SyntaxFactory.SeparatedList(
                                targetMethod.ParameterList.Parameters.Select(p => 
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier)))));

                        SyntaxNode replacementStatement;
                        if (methodSymbol.ReturnsVoid)
                        {
                            replacementStatement = SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.IdentifierName(sourceMethod.Identifier.Text), 
                                    arguments));
                        }
                        else
                        {
                            string returnType = methodSymbol.ReturnType.ToDisplayString();
                            string varName = $"result_{sourceMethod.Identifier.Text}";
                            LocalDeclarationStatementSyntax decl = SyntaxFactory.LocalDeclarationStatement(
                                SyntaxFactory.VariableDeclaration(
                                    SyntaxFactory.ParseTypeName(returnType),
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.VariableDeclarator(varName)
                                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                                SyntaxFactory.InvocationExpression(
                                                    SyntaxFactory.IdentifierName(sourceMethod.Identifier.Text),
                                                    arguments))))));
                            replacementStatement = decl;
                        }

                        // Заменяет тело метода на вызов исходного метода
                        BlockSyntax newBody = SyntaxFactory.Block((StatementSyntax)replacementStatement);
                        MethodDeclarationSyntax newMethod = targetMethod.WithBody(newBody);
                        editor.ReplaceNode(targetMethod, newMethod);
                        changed = true;
                    }
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        // Сравнивает два блока кода на идентичность
        private bool AreBodiesEquivalent(BlockSyntax? body1, BlockSyntax? body2)
        {
            if (body1 == null && body2 == null) return true;
            if (body1 == null || body2 == null) return false;
            return body1.NormalizeWhitespace().ToFullString() == body2.NormalizeWhitespace().ToFullString();
        }

        // Сравнивает сигнатуры методов: возвращаемый тип, модификаторы, параметры
        private bool AreSignaturesEquivalent(MethodDeclarationSyntax m1, MethodDeclarationSyntax m2, SemanticModel model)
        {
            IMethodSymbol? symbol1 = model.GetDeclaredSymbol(m1);
            IMethodSymbol? symbol2 = model.GetDeclaredSymbol(m2);
            if (symbol1 == null || symbol2 == null) return false;

            // Сравнивает возвращаемый тип
            if (!SymbolEqualityComparer.Default.Equals(symbol1.ReturnType, symbol2.ReturnType))
                return false;

            // Сравнивает модификаторы (упрощённо)
            HashSet<string> mods1 = new HashSet<string>(m1.Modifiers.Select(m => m.Text));
            HashSet<string> mods2 = new HashSet<string>(m2.Modifiers.Select(m => m.Text));
            if (!mods1.SetEquals(mods2)) return false;

            // Сравнивает параметры
            if (symbol1.Parameters.Length != symbol2.Parameters.Length) return false;
            for (int i = 0; i < symbol1.Parameters.Length; i++)
            {
                if (!SymbolEqualityComparer.Default.Equals(symbol1.Parameters[i].Type, symbol2.Parameters[i].Type))
                    return false;
                if (symbol1.Parameters[i].RefKind != symbol2.Parameters[i].RefKind)
                    return false;
            }

            return true;
        }
    }
}