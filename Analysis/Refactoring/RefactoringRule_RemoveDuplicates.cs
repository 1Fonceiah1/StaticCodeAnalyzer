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
    public class RefactoringRule_RemoveDuplicates : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "DUP001" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Body != null)
                .ToList();

            for (int i = 0; i < methods.Count; i++)
            {
                for (int j = i + 1; j < methods.Count; j++)
                {
                    // Сравниваем не только тела, но и сигнатуры
                    if (AreBodiesEquivalent(methods[i].Body, methods[j].Body) && 
                        AreSignaturesEquivalent(methods[i], methods[j], semanticModel, cancellationToken))
                    {
                        var targetMethod = methods[j];
                        var sourceMethod = methods[i];
                        
                        // Проверяет, не является ли метод уже заменённым
                        if (targetMethod.Body.Statements.Count == 1 && 
                            targetMethod.Body.Statements.First() is ExpressionStatementSyntax exprStmt &&
                            exprStmt.Expression is InvocationExpressionSyntax inv &&
                            inv.Expression is IdentifierNameSyntax invId &&
                            invId.Identifier.Text == sourceMethod.Identifier.Text)
                            continue;

                        // Получает символ метода для корректной генерации вызова
                        var methodSymbol = semanticModel.GetDeclaredSymbol(targetMethod, cancellationToken) as IMethodSymbol;
                        if (methodSymbol == null) continue;

                        // Формирует список аргументов из параметров целевого метода
                        var arguments = SyntaxFactory.ArgumentList(
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
                            var returnType = methodSymbol.ReturnType.ToDisplayString();
                            var varName = $"result_{sourceMethod.Identifier.Text}";
                            var decl = SyntaxFactory.LocalDeclarationStatement(
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

                        var newBody = SyntaxFactory.Block((StatementSyntax)replacementStatement);
                        var newMethod = targetMethod.WithBody(newBody);
                        editor.ReplaceNode(targetMethod, newMethod);
                        changed = true;
                    }
                }
            }

            return changed ? editor.GetChangedDocument() : document;
        }

        private bool AreBodiesEquivalent(BlockSyntax? body1, BlockSyntax? body2)
        {
            if (body1 == null && body2 == null) return true;
            if (body1 == null || body2 == null) return false;
            return body1.NormalizeWhitespace().ToFullString() == body2.NormalizeWhitespace().ToFullString();
        }

        /// <summary>
        /// Сравнивает сигнатуры методов: возвращаемый тип, модификаторы, параметры.
        /// </summary>
        private bool AreSignaturesEquivalent(MethodDeclarationSyntax m1, MethodDeclarationSyntax m2, SemanticModel model, CancellationToken ct)
        {
            var symbol1 = model.GetDeclaredSymbol(m1, ct);
            var symbol2 = model.GetDeclaredSymbol(m2, ct);
            if (symbol1 == null || symbol2 == null) return false;

            // Сравнение возвращаемого типа
            if (!SymbolEqualityComparer.Default.Equals(symbol1.ReturnType, symbol2.ReturnType))
                return false;

            // Сравнение модификаторов (public, static, async, etc.) – упрощённо
            var mods1 = new HashSet<string>(m1.Modifiers.Select(m => m.Text));
            var mods2 = new HashSet<string>(m2.Modifiers.Select(m => m.Text));
            if (!mods1.SetEquals(mods2)) return false;

            // Сравнение параметров
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