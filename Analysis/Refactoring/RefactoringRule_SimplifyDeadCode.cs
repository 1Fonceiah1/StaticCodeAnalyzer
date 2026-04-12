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
    public class RefactoringRule_SimplifyDeadCode : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "DEAD001", "SIM001", "DEAD002", "DEAD003", "DEAD004" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            bool anyChange;
            do
            {
                anyChange = false;
                root = await editor.GetChangedDocument().GetSyntaxRootAsync(cancellationToken);
                editor = await DocumentEditor.CreateAsync(editor.GetChangedDocument(), cancellationToken);

                // 1. Упрощаем присваивания
                foreach (var assign in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    var simplified = SimplifyAssignment(assign);
                    if (simplified != assign)
                    {
                        editor.ReplaceNode(assign, simplified);
                        anyChange = true;
                    }
                }

                // 2. Удаляем x = x;
                var uselessAssignments = root.DescendantNodes()
                    .OfType<ExpressionStatementSyntax>()
                    .Where(stmt => stmt.Expression is AssignmentExpressionSyntax a && a.Left.ToString() == a.Right.ToString())
                    .ToList();
                foreach (var stmt in uselessAssignments)
                {
                    editor.RemoveNode(stmt);
                    anyChange = true;
                }

                // 3. Удаляем просто идентификатор;
                var uselessIds = root.DescendantNodes()
                    .OfType<ExpressionStatementSyntax>()
                    .Where(stmt => stmt.Expression is IdentifierNameSyntax)
                    .ToList();
                foreach (var stmt in uselessIds)
                {
                    editor.RemoveNode(stmt);
                    anyChange = true;
                }

                // 4. Удаляем пустые циклы for
                foreach (var loop in root.DescendantNodes().OfType<ForStatementSyntax>().Where(IsTrivialForLoop))
                {
                    editor.RemoveNode(loop);
                    anyChange = true;
                }

                // 5. Удаляем пустые циклы while
                foreach (var loop in root.DescendantNodes().OfType<WhileStatementSyntax>().Where(IsTrivialWhileLoop))
                {
                    editor.RemoveNode(loop);
                    anyChange = true;
                }

                // 6. Удаляем пустые else
                foreach (var ifStmt in root.DescendantNodes().OfType<IfStatementSyntax>())
                {
                    if (ifStmt.Else != null && IsEffectivelyEmpty(ifStmt.Else.Statement))
                    {
                        editor.ReplaceNode(ifStmt, ifStmt.WithElse(null));
                        anyChange = true;
                    }
                }

                // 7. Удаляем пустые блоки if
                foreach (var ifStmt in root.DescendantNodes().OfType<IfStatementSyntax>())
                {
                    if (ifStmt.Else == null && IsEffectivelyEmpty(ifStmt.Statement))
                    {
                        editor.RemoveNode(ifStmt);
                        anyChange = true;
                    }
                }

                // 8. Удаляем дублирующиеся присваивания подряд
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methods)
                {
                    if (method.Body == null) continue;
                    var statements = method.Body.Statements.ToList();
                    for (int i = 0; i < statements.Count - 1; i++)
                    {
                        if (statements[i] is ExpressionStatementSyntax cur && statements[i+1] is ExpressionStatementSyntax nxt &&
                            cur.Expression is AssignmentExpressionSyntax a1 && nxt.Expression is AssignmentExpressionSyntax a2 &&
                            a1.Left.ToString() == a2.Left.ToString() && a1.Right.ToString() == a2.Right.ToString())
                        {
                            editor.RemoveNode(nxt);
                            anyChange = true;
                            break;
                        }
                    }
                }

                // 9. Удаляем пары противоположных операций
                var oppositePairs = FindOppositeOperationPairs(root);
                foreach (var stmt in oppositePairs)
                {
                    editor.RemoveNode(stmt);
                    anyChange = true;
                }

                // 10. Удаляем бесполезные инкременты
                var uselessIncrements = FindUselessIncrements(root);
                foreach (var stmt in uselessIncrements)
                {
                    editor.RemoveNode(stmt);
                    anyChange = true;
                }

                // 11. Преобразуем if (x == false) { x = false; } → x = false;
                var selfAssignIfs = FindSelfAssignIf(root);
                foreach (var ifStmt in selfAssignIfs)
                {
                    var assign = ((ExpressionStatementSyntax)((BlockSyntax)ifStmt.Statement).Statements[0]).Expression;
                    editor.ReplaceNode(ifStmt, SyntaxFactory.ExpressionStatement(assign));
                    anyChange = true;
                }

            } while (anyChange);

            return editor.GetChangedDocument();
        }

        private ExpressionSyntax SimplifyAssignment(AssignmentExpressionSyntax assign)
        {
            if (!assign.IsKind(SyntaxKind.SimpleAssignmentExpression)) return assign;
            var right = assign.Right;
            if (right is BinaryExpressionSyntax binary)
            {
                if ((binary.IsKind(SyntaxKind.AddExpression) || binary.IsKind(SyntaxKind.SubtractExpression)) &&
                    IsZero(binary.Right) && AreIdentical(assign.Left, binary.Left))
                    return assign.Left;
                if (binary.IsKind(SyntaxKind.AddExpression) && IsZero(binary.Left) && AreIdentical(assign.Left, binary.Right))
                    return assign.Left;
                if ((binary.IsKind(SyntaxKind.MultiplyExpression) || binary.IsKind(SyntaxKind.DivideExpression)) &&
                    IsOne(binary.Right) && AreIdentical(assign.Left, binary.Left))
                    return assign.Left;
                if (binary.IsKind(SyntaxKind.MultiplyExpression) && IsOne(binary.Left) && AreIdentical(assign.Left, binary.Right))
                    return assign.Left;
            }
            return assign;
        }

        private bool AreIdentical(ExpressionSyntax left, ExpressionSyntax right) => left.ToString() == right.ToString();

        private bool IsZero(ExpressionSyntax expr) =>
            expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.NumericLiteralExpression) &&
            (lit.Token.Text == "0" || lit.Token.Text == "0.0" || lit.Token.Text == "0f" || lit.Token.Text == "0d" || lit.Token.Text == "0m");

        private bool IsOne(ExpressionSyntax expr) =>
            expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.NumericLiteralExpression) &&
            (lit.Token.Text == "1" || lit.Token.Text == "1.0" || lit.Token.Text == "1f" || lit.Token.Text == "1d" || lit.Token.Text == "1m");

        private bool IsTrivialForLoop(ForStatementSyntax forLoop)
        {
            if (forLoop.Declaration == null || forLoop.Condition == null || forLoop.Incrementors.Count == 0) return false;
            var declarator = forLoop.Declaration.Variables.FirstOrDefault();
            if (declarator?.Initializer?.Value is not LiteralExpressionSyntax initLit) return false;
            if (!IsZero(initLit)) return false;
            if (forLoop.Condition is BinaryExpressionSyntax cond &&
                cond.IsKind(SyntaxKind.LessThanExpression) &&
                cond.Right is LiteralExpressionSyntax rightLit && IsOne(rightLit))
            {
                return !HasSideEffects(forLoop.Statement);
            }
            return false;
        }

        private bool IsTrivialWhileLoop(WhileStatementSyntax whileLoop)
        {
            if (whileLoop.Condition is BinaryExpressionSyntax cond &&
                cond.IsKind(SyntaxKind.LessThanExpression) &&
                cond.Right is LiteralExpressionSyntax rightLit && IsOne(rightLit))
            {
                var increments = whileLoop.Statement.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Where(a => a.IsKind(SyntaxKind.AddAssignmentExpression) ||
                                (a.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                                 a.Right is BinaryExpressionSyntax bin &&
                                 bin.IsKind(SyntaxKind.AddExpression) && IsOne(bin.Right)));
                if (increments.Any())
                    return !HasSideEffects(whileLoop.Statement);
            }
            return false;
        }

        private bool HasSideEffects(StatementSyntax statement)
        {
            foreach (var assign in statement.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assign.Left is ElementAccessExpressionSyntax || assign.Left is MemberAccessExpressionSyntax)
                    return true;
            }
            return false;
        }

        private bool IsEffectivelyEmpty(StatementSyntax statement)
        {
            if (statement is BlockSyntax block)
            {
                foreach (var stmt in block.Statements)
                    if (!IsUselessOrDeclaration(stmt)) return false;
                return true;
            }
            return IsUselessOrDeclaration(statement);
        }

        private bool IsUselessOrDeclaration(StatementSyntax stmt)
        {
            if (stmt is EmptyStatementSyntax) return true;
            if (stmt is ExpressionStatementSyntax expr)
            {
                if (expr.Expression is IdentifierNameSyntax) return true;
                if (expr.Expression is AssignmentExpressionSyntax assign && assign.Left.ToString() == assign.Right.ToString()) return true;
            }
            if (stmt is LocalDeclarationStatementSyntax) return true;
            return false;
        }

        private List<ExpressionStatementSyntax> FindOppositeOperationPairs(SyntaxNode root)
        {
            var pairs = new List<ExpressionStatementSyntax>();
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (method.Body == null) continue;
                var statements = method.Body.Statements.ToList();
                for (int i = 0; i < statements.Count - 1; i++)
                {
                    if (statements[i] is ExpressionStatementSyntax stmt1 &&
                        statements[i + 1] is ExpressionStatementSyntax stmt2 &&
                        stmt1.Expression is AssignmentExpressionSyntax a1 &&
                        stmt2.Expression is AssignmentExpressionSyntax a2 &&
                        IsOpposite(a1, a2))
                    {
                        pairs.Add(stmt2);
                    }
                }
            }
            return pairs;
        }

        private bool IsOpposite(AssignmentExpressionSyntax a1, AssignmentExpressionSyntax a2)
        {
            if (!a1.IsKind(SyntaxKind.SimpleAssignmentExpression) || !a2.IsKind(SyntaxKind.SimpleAssignmentExpression))
                return false;
            if (a1.Left.ToString() != a2.Left.ToString()) return false;
            if (a1.Right is BinaryExpressionSyntax b1 && a2.Right is BinaryExpressionSyntax b2)
            {
                if (b1.IsKind(SyntaxKind.AddExpression) && b2.IsKind(SyntaxKind.SubtractExpression) &&
                    IsOne(b1.Right) && IsOne(b2.Right) &&
                    a1.Left.ToString() == b1.Left.ToString() && a2.Left.ToString() == b2.Left.ToString())
                    return true;
                if (b1.IsKind(SyntaxKind.SubtractExpression) && b2.IsKind(SyntaxKind.AddExpression) &&
                    IsOne(b1.Right) && IsOne(b2.Right) &&
                    a1.Left.ToString() == b1.Left.ToString() && a2.Left.ToString() == b2.Left.ToString())
                    return true;
            }
            return false;
        }

        private List<ExpressionStatementSyntax> FindUselessIncrements(SyntaxNode root)
        {
            var useless = new List<ExpressionStatementSyntax>();
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (method.Body == null) continue;
                var allIdentifiers = method.Body.DescendantNodes().OfType<IdentifierNameSyntax>().Select(id => id.Identifier.Text).ToList();
                var declaredVars = method.Body.DescendantNodes()
                    .OfType<VariableDeclaratorSyntax>()
                    .Select(v => v.Identifier.Text)
                    .ToHashSet();

                var increments = method.Body.DescendantNodes()
                    .OfType<ExpressionStatementSyntax>()
                    .Where(stmt => stmt.Expression is AssignmentExpressionSyntax assign &&
                                   (assign.IsKind(SyntaxKind.AddAssignmentExpression) ||
                                    (assign.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                                     assign.Right is BinaryExpressionSyntax bin &&
                                     bin.IsKind(SyntaxKind.AddExpression) && IsOne(bin.Right))))
                    .ToList();

                foreach (var inc in increments)
                {
                    var left = ((AssignmentExpressionSyntax)inc.Expression).Left.ToString();
                    int usageCount = allIdentifiers.Count(id => id == left);
                    if (usageCount <= 1 && declaredVars.Contains(left))
                        useless.Add(inc);
                }
            }
            return useless;
        }

        private List<IfStatementSyntax> FindSelfAssignIf(SyntaxNode root)
        {
            var result = new List<IfStatementSyntax>();
            var ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>();
            foreach (var ifStmt in ifStatements)
            {
                if (ifStmt.Condition is BinaryExpressionSyntax bin && bin.IsKind(SyntaxKind.EqualsExpression) &&
                    bin.Right is LiteralExpressionSyntax lit && lit.Token.Text == "false" &&
                    ifStmt.Statement is BlockSyntax block && block.Statements.Count == 1 &&
                    block.Statements[0] is ExpressionStatementSyntax expr &&
                    expr.Expression is AssignmentExpressionSyntax assign &&
                    assign.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                    assign.Right is LiteralExpressionSyntax lit2 && lit2.Token.Text == "false" &&
                    assign.Left.ToString() == bin.Left.ToString())
                {
                    result.Add(ifStmt);
                }
            }
            return result;
        }
    }
}