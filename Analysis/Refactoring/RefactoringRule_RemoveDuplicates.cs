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
        // Устраняет дублирование: одинаковые блоки if-else, дублирующиеся методы, повторяющиеся блоки внутри метода
        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            // 1. Обработка дублирующихся if-else с одинаковыми блоками
            changed |= RemoveIdenticalIfElseBlocks(root, editor);

            // 2. Обработка дублирующихся методов (одинаковые тела)
            changed |= await RemoveDuplicateMethodsAsync(root, editor, cancellationToken).ConfigureAwait(false);

            // 3. Обработка дублирующихся блоков внутри одного метода
            changed |= RemoveDuplicateBlocksInsideMethods(root, editor);

            return changed ? editor.GetChangedDocument() : document;
        }

        // Находит if-else с идентичными блоками и заменяет на один блок
        private bool RemoveIdenticalIfElseBlocks(SyntaxNode root, DocumentEditor editor)
        {
            bool changed = false;
            var ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>()
                .Where(ifStmt => ifStmt.Else != null && ifStmt.Else.Statement is BlockSyntax elseBlock &&
                                 ifStmt.Statement is BlockSyntax thenBlock &&
                                 thenBlock.Statements.SequenceEqual(elseBlock.Statements, new SyntaxNodeComparer()))
                .ToList();

            foreach (var ifStmt in ifStatements)
            {
                editor.ReplaceNode(ifStmt, ifStmt.Statement);
                changed = true;
            }
            return changed;
        }

        // Находит методы с одинаковым телом и заменяет второй метод вызовом первого (с кэшированием, если есть возвращаемое значение)
        private async Task<bool> RemoveDuplicateMethodsAsync(SyntaxNode root, DocumentEditor editor, CancellationToken cancellationToken)
        {
            bool changed = false;
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            var semanticModel = await editor.OriginalDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            for (int i = 0; i < methods.Count; i++)
            {
                for (int j = i + 1; j < methods.Count; j++)
                {
                    if (AreEquivalent(methods[i].Body, methods[j].Body))
                    {
                        // Проверяет, что метод не возвращает void (иначе просто вызывает)
                        var methodSymbol = semanticModel?.GetDeclaredSymbol(methods[j], cancellationToken) as IMethodSymbol;
                        if (methodSymbol != null && !methodSymbol.ReturnsVoid)
                        {
                            // Сохраняет результат в переменную
                            var returnType = methodSymbol.ReturnType.ToDisplayString();
                            var varName = $"cachedResult_{methods[i].Identifier.Text}";
                            var declaration = SyntaxFactory.LocalDeclarationStatement(
                                SyntaxFactory.VariableDeclaration(
                                    SyntaxFactory.ParseTypeName(returnType),
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.VariableDeclarator(varName)
                                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                                SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(methods[i].Identifier.Text))
                                            )))));
                            var callStatement = SyntaxFactory.ExpressionStatement(SyntaxFactory.IdentifierName(varName));
                            var newBody = SyntaxFactory.Block(declaration, callStatement);
                            var newMethod = methods[j].WithBody(newBody);
                            editor.ReplaceNode(methods[j], newMethod);
                        }
                        else
                        {
                            var call = SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(methods[i].Identifier.Text)));
                            var newBody = SyntaxFactory.Block(call);
                            var newMethod = methods[j].WithBody(newBody);
                            editor.ReplaceNode(methods[j], newMethod);
                        }
                        changed = true;
                    }
                }
            }
            return changed;
        }

        // Находит дублирующиеся блоки кода внутри одного метода и выносит их в локальные функции
        private bool RemoveDuplicateBlocksInsideMethods(SyntaxNode root, DocumentEditor editor)
        {
            bool changed = false;
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var method in methods)
            {
                if (method.Body == null) continue;
                var blocks = method.Body.DescendantNodes().OfType<BlockSyntax>().ToList();
                var duplicates = FindDuplicateBlocks(blocks);
                foreach (var dup in duplicates)
                {
                    var extractedName = $"Duplicated_{method.Identifier.Text}_{dup.GetHashCode()}";
                    var localFunction = SyntaxFactory.LocalFunctionStatement(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), extractedName)
                        .WithBody(dup)
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.StaticKeyword)));
                    var callStatement = SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(extractedName)));
                    var newMethodBody = method.Body.ReplaceNode(dup, callStatement);
                    newMethodBody = newMethodBody.InsertNodesBefore(newMethodBody.Statements.First(), new[] { localFunction });
                    var newMethod = method.WithBody(newMethodBody);
                    editor.ReplaceNode(method, newMethod);
                    changed = true;
                }
            }
            return changed;
        }

        // Сравнивает два синтаксических узла на эквивалентность
        private bool AreEquivalent(SyntaxNode node1, SyntaxNode node2)
        {
            if (node1 == null || node2 == null) return false;
            return SyntaxFactory.AreEquivalent(node1, node2);
        }

        // Находит одинаковые блоки в списке
        private List<BlockSyntax> FindDuplicateBlocks(List<BlockSyntax> blocks)
        {
            var result = new List<BlockSyntax>();
            for (int i = 0; i < blocks.Count; i++)
            {
                for (int j = i + 1; j < blocks.Count; j++)
                {
                    if (AreEquivalent(blocks[i], blocks[j]))
                    {
                        result.Add(blocks[i]);
                        break;
                    }
                }
            }
            return result;
        }

        private class SyntaxNodeComparer : IEqualityComparer<SyntaxNode>
        {
            public bool Equals(SyntaxNode x, SyntaxNode y) => SyntaxFactory.AreEquivalent(x, y);
            public int GetHashCode(SyntaxNode obj) => obj.GetHashCode();
        }
    }
}