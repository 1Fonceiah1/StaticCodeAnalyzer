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
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            bool anyChange;
            do
            {
                anyChange = false;
                root = await editor.GetChangedDocument().GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                editor = await DocumentEditor.CreateAsync(editor.GetChangedDocument(), cancellationToken).ConfigureAwait(false);

                // Упрощает бесполезные арифметические присваивания (x = x + 0 → x)
                foreach (AssignmentExpressionSyntax assign in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    ExpressionSyntax simplified = SimplifyAssignment(assign);
                    if (simplified != assign)
                    {
                        editor.ReplaceNode(assign, simplified);
                        anyChange = true;
                    }
                }

                // Удаляет присваивания вида x = x;
                List<ExpressionStatementSyntax> uselessAssignments = root.DescendantNodes()
                    .OfType<ExpressionStatementSyntax>()
                    .Where(stmt => stmt.Expression is AssignmentExpressionSyntax a && a.Left.ToString() == a.Right.ToString())
                    .ToList();
                foreach (ExpressionStatementSyntax stmt in uselessAssignments)
                {
                    editor.RemoveNode(stmt);
                    anyChange = true;
                }

                // Удаляет операторы, состоящие из одного идентификатора
                List<ExpressionStatementSyntax> uselessIds = root.DescendantNodes()
                    .OfType<ExpressionStatementSyntax>()
                    .Where(stmt => stmt.Expression is IdentifierNameSyntax)
                    .ToList();
                foreach (ExpressionStatementSyntax stmt in uselessIds)
                {
                    editor.RemoveNode(stmt);
                    anyChange = true;
                }

                // Удаляет тривиальные циклы for (одна итерация без побочных эффектов)
                foreach (ForStatementSyntax loop in root.DescendantNodes().OfType<ForStatementSyntax>().Where(IsTrivialForLoop))
                {
                    editor.RemoveNode(loop);
                    anyChange = true;
                }

                // Удаляет тривиальные циклы while (одна итерация без побочных эффектов)
                foreach (WhileStatementSyntax loop in root.DescendantNodes().OfType<WhileStatementSyntax>().Where(IsTrivialWhileLoop))
                {
                    editor.RemoveNode(loop);
                    anyChange = true;
                }

                // Удаляет пустые блоки else
                foreach (IfStatementSyntax ifStmt in root.DescendantNodes().OfType<IfStatementSyntax>())
                {
                    if (ifStmt.Else != null && IsEffectivelyEmpty(ifStmt.Else.Statement))
                    {
                        editor.ReplaceNode(ifStmt, ifStmt.WithElse(null));
                        anyChange = true;
                    }
                }

                // Удаляет пустые блоки if (без else)
                foreach (IfStatementSyntax ifStmt in root.DescendantNodes().OfType<IfStatementSyntax>())
                {
                    if (ifStmt.Else == null && IsEffectivelyEmpty(ifStmt.Statement))
                    {
                        editor.RemoveNode(ifStmt);
                        anyChange = true;
                    }
                }

                // Удаляет дублирующиеся присваивания, идущие подряд
                List<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
                foreach (MethodDeclarationSyntax method in methods)
                {
                    if (method.Body == null) continue;
                    List<StatementSyntax> statements = method.Body.Statements.ToList();
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

                // Удаляет пары противоположных операций (x = x + 1; x = x - 1;)
                List<ExpressionStatementSyntax> oppositePairs = FindOppositeOperationPairs(root);
                foreach (ExpressionStatementSyntax stmt in oppositePairs)
                {
                    editor.RemoveNode(stmt);
                    anyChange = true;
                }

                // Удаляет бесполезные инкременты неиспользуемых переменных
                List<ExpressionStatementSyntax> uselessIncrements = FindUselessIncrements(root);
                foreach (ExpressionStatementSyntax stmt in uselessIncrements)
                {
                    editor.RemoveNode(stmt);
                    anyChange = true;
                }

                // Преобразует if (x == false) { x = false; } → x = false;
                List<IfStatementSyntax> selfAssignIfs = FindSelfAssignIf(root);
                foreach (IfStatementSyntax ifStmt in selfAssignIfs)
                {
                    AssignmentExpressionSyntax assign = (AssignmentExpressionSyntax)((ExpressionStatementSyntax)((BlockSyntax)ifStmt.Statement).Statements[0]).Expression;
                    editor.ReplaceNode(ifStmt, SyntaxFactory.ExpressionStatement(assign));
                    anyChange = true;
                }

            } while (anyChange);

            return editor.GetChangedDocument();
        }

        // Упрощает присваивание, заменяя x = x + 0 на x, x = x * 1 на x и т.д.
        private ExpressionSyntax SimplifyAssignment(AssignmentExpressionSyntax assign)
        {
            if (!assign.IsKind(SyntaxKind.SimpleAssignmentExpression)) return assign;
            ExpressionSyntax right = assign.Right;
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

        // Проверяет идентичность двух выражений по текстовому представлению
        private bool AreIdentical(ExpressionSyntax left, ExpressionSyntax right) => left.ToString() == right.ToString();

        // Проверяет, является ли выражение числовым нулём
        private bool IsZero(ExpressionSyntax expr)
        {
            return expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.NumericLiteralExpression) &&
                   (lit.Token.Text == "0" || lit.Token.Text == "0.0" || lit.Token.Text == "0f" || lit.Token.Text == "0d" || lit.Token.Text == "0m");
        }

        // Проверяет, является ли выражение числовой единицей
        private bool IsOne(ExpressionSyntax expr)
        {
            return expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.NumericLiteralExpression) &&
                   (lit.Token.Text == "1" || lit.Token.Text == "1.0" || lit.Token.Text == "1f" || lit.Token.Text == "1d" || lit.Token.Text == "1m");
        }

        // Определяет, является ли цикл for тривиальным (одна итерация без побочных эффектов)
        private bool IsTrivialForLoop(ForStatementSyntax forLoop)
        {
            if (forLoop.Declaration == null || forLoop.Condition == null || forLoop.Incrementors.Count == 0) return false;
            VariableDeclaratorSyntax? declarator = forLoop.Declaration.Variables.FirstOrDefault();
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

        // Определяет, является ли цикл while тривиальным (одна итерация без побочных эффектов)
        private bool IsTrivialWhileLoop(WhileStatementSyntax whileLoop)
        {
            if (whileLoop.Condition is BinaryExpressionSyntax cond &&
                cond.IsKind(SyntaxKind.LessThanExpression) &&
                cond.Right is LiteralExpressionSyntax rightLit && IsOne(rightLit))
            {
                IEnumerable<AssignmentExpressionSyntax> increments = whileLoop.Statement.DescendantNodes()
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

        // Проверяет наличие побочных эффектов (изменение элементов массива или свойств)
        private bool HasSideEffects(StatementSyntax statement)
        {
            foreach (AssignmentExpressionSyntax assign in statement.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assign.Left is ElementAccessExpressionSyntax || assign.Left is MemberAccessExpressionSyntax)
                    return true;
            }
            return false;
        }

        // Проверяет, является ли блок или оператор фактически пустым (не содержит полезного кода)
        private bool IsEffectivelyEmpty(StatementSyntax statement)
        {
            if (statement is BlockSyntax block)
            {
                foreach (StatementSyntax stmt in block.Statements)
                    if (!IsUselessOrDeclaration(stmt)) return false;
                return true;
            }
            return IsUselessOrDeclaration(statement);
        }

        // Определяет, является ли оператор бесполезным или объявлением (не влияющим на логику)
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

        // Находит пары противоположных операций (x = x + 1; x = x - 1;)
        private List<ExpressionStatementSyntax> FindOppositeOperationPairs(SyntaxNode root)
        {
            List<ExpressionStatementSyntax> pairs = new List<ExpressionStatementSyntax>();
            List<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            foreach (MethodDeclarationSyntax method in methods)
            {
                if (method.Body == null) continue;
                List<StatementSyntax> statements = method.Body.Statements.ToList();
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

        // Проверяет, являются ли два присваивания противоположными (x = x + 1 и x = x - 1)
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

        // Находит бесполезные инкременты (переменная инкрементируется, но не используется)
        private List<ExpressionStatementSyntax> FindUselessIncrements(SyntaxNode root)
        {
            List<ExpressionStatementSyntax> useless = new List<ExpressionStatementSyntax>();
            List<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            foreach (MethodDeclarationSyntax method in methods)
            {
                if (method.Body == null) continue;
                List<string> allIdentifiers = method.Body.DescendantNodes().OfType<IdentifierNameSyntax>().Select(id => id.Identifier.Text).ToList();
                HashSet<string> declaredVars = method.Body.DescendantNodes()
                    .OfType<VariableDeclaratorSyntax>()
                    .Select(v => v.Identifier.Text)
                    .ToHashSet();

                List<ExpressionStatementSyntax> increments = method.Body.DescendantNodes()
                    .OfType<ExpressionStatementSyntax>()
                    .Where(stmt => stmt.Expression is AssignmentExpressionSyntax assign &&
                                   (assign.IsKind(SyntaxKind.AddAssignmentExpression) ||
                                    (assign.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                                     assign.Right is BinaryExpressionSyntax bin &&
                                     bin.IsKind(SyntaxKind.AddExpression) && IsOne(bin.Right))))
                    .ToList();

                foreach (ExpressionStatementSyntax inc in increments)
                {
                    string left = ((AssignmentExpressionSyntax)inc.Expression).Left.ToString();
                    int usageCount = allIdentifiers.Count(id => id == left);
                    if (usageCount <= 1 && declaredVars.Contains(left))
                        useless.Add(inc);
                }
            }
            return useless;
        }

        // Находит конструкции if (x == false) { x = false; } для замены на прямое присваивание
        private List<IfStatementSyntax> FindSelfAssignIf(SyntaxNode root)
        {
            List<IfStatementSyntax> result = new List<IfStatementSyntax>();
            IEnumerable<IfStatementSyntax> ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>();
            foreach (IfStatementSyntax ifStmt in ifStatements)
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