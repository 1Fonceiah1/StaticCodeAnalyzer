using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class DeadCodeRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            // 1. Бесполезные присваивания (x = x + 0, x = x * 1, x = x)
            var uselessAssignments = root.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(IsUselessAssignment)
                .ToList();
            foreach (var assign in uselessAssignments)
                AddIssue(assign, assign.GetLocation(), "DEAD001", "Бесполезное присваивание (например, x = x + 0, x = x * 1 или x = x).", issues, filePath);

            // 2. Пустые циклы for (одна итерация)
            var trivialForLoops = root.DescendantNodes()
                .OfType<ForStatementSyntax>()
                .Where(IsTrivialForLoop)
                .ToList();
            foreach (var loop in trivialForLoops)
                AddIssue(loop, loop.ForKeyword.GetLocation(), "SIM001", "Цикл for с одной итерацией без побочных эффектов.", issues, filePath);

            // 3. Пустые циклы while (одна итерация)
            var trivialWhileLoops = root.DescendantNodes()
                .OfType<WhileStatementSyntax>()
                .Where(IsTrivialWhileLoop)
                .ToList();
            foreach (var loop in trivialWhileLoops)
                AddIssue(loop, loop.WhileKeyword.GetLocation(), "SIM001", "Цикл while с одной итерацией без побочных эффектов.", issues, filePath);

            // 4. Бесполезные операторы-выражения (просто идентификатор)
            var uselessIdStatements = root.DescendantNodes()
                .OfType<ExpressionStatementSyntax>()
                .Where(stmt => stmt.Expression is IdentifierNameSyntax)
                .ToList();
            foreach (var stmt in uselessIdStatements)
                AddIssue(stmt, stmt.GetLocation(), "DEAD002", "Бесполезный оператор (просто идентификатор, не имеющий побочных эффектов).", issues, filePath);

            // 5. Пустые блоки else
            var emptyElseBlocks = root.DescendantNodes()
                .OfType<IfStatementSyntax>()
                .Where(ifStmt => ifStmt.Else != null && IsEffectivelyEmpty(ifStmt.Else.Statement))
                .Select(ifStmt => ifStmt.Else)
                .ToList();
            foreach (var elseClause in emptyElseBlocks)
                AddIssue(elseClause, elseClause.ElseKeyword.GetLocation(), "DEAD003", "Пустой блок else (не содержит полезного кода).", issues, filePath);

            // 6. Пустые блоки if (без else)
            var emptyIfBlocks = root.DescendantNodes()
                .OfType<IfStatementSyntax>()
                .Where(ifStmt => ifStmt.Else == null && IsEffectivelyEmpty(ifStmt.Statement))
                .ToList();
            foreach (var ifStmt in emptyIfBlocks)
                AddIssue(ifStmt, ifStmt.IfKeyword.GetLocation(), "DEAD003", "Пустой блок if (не содержит полезного кода).", issues, filePath);

            // 7. Дублирующиеся присваивания подряд
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            foreach (var method in methods)
            {
                if (method.Body == null) continue;
                var statements = method.Body.Statements.ToList();
                for (int i = 0; i < statements.Count - 1; i++)
                {
                    if (statements[i] is ExpressionStatementSyntax current &&
                        statements[i + 1] is ExpressionStatementSyntax next &&
                        current.Expression is AssignmentExpressionSyntax a1 &&
                        next.Expression is AssignmentExpressionSyntax a2 &&
                        a1.Left.ToString() == a2.Left.ToString() &&
                        a1.Right.ToString() == a2.Right.ToString())
                    {
                        AddIssue(next, next.GetLocation(), "DEAD004", "Дублирующееся присваивание подряд.", issues, filePath);
                    }
                }
            }

            // 8. Пары противоположных операций (x = x + 1; x = x - 1;)
            var oppositePairs = FindOppositeOperationPairs(root);
            foreach (var stmt in oppositePairs)
                AddIssue(stmt, stmt.GetLocation(), "DEAD001", "Пара противоположных операций (например, x = x + 1; x = x - 1;).", issues, filePath);

            // 9. Бесполезные инкременты неиспользуемой переменной
            var uselessIncrements = FindUselessIncrements(root);
            foreach (var stmt in uselessIncrements)
                AddIssue(stmt, stmt.GetLocation(), "DEAD001", "Бесполезный инкремент (переменная не используется).", issues, filePath);

            // 10. Бессмысленные if (x == false) { x = false; }
            var selfAssignIfs = FindSelfAssignIf(root);
            foreach (var ifStmt in selfAssignIfs)
                AddIssue(ifStmt, ifStmt.IfKeyword.GetLocation(), "DEAD001", "Бессмысленный if, можно заменить на присваивание.", issues, filePath);

            return Task.FromResult(issues);
        }

        private void AddIssue(SyntaxNode node, Location location, string code, string description, List<AnalysisIssue> issues, string filePath)
        {
            if (location == null) return;
            var lineSpan = location.GetLineSpan();
            var containingMethod = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            var containingClass = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            issues.Add(new AnalysisIssue
            {
                Severity = "Низкий",
                FilePath = filePath,
                LineNumber = lineSpan.StartLinePosition.Line + 1,
                ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                Type = "запах кода",
                Code = code,
                Description = description,
                Suggestion = "Удалите или упростите выражение.",
                RuleName = "DeadCode",
                ContainingTypeName = containingClass?.Identifier.Text,
                MethodName = containingMethod?.Identifier.Text
            });
        }

        private bool IsUselessAssignment(AssignmentExpressionSyntax assign)
        {
            if (!assign.IsKind(SyntaxKind.SimpleAssignmentExpression)) return false;
            if (assign.Left.ToString() == assign.Right.ToString()) return true;
            if (assign.Right is BinaryExpressionSyntax binary)
            {
                if ((binary.IsKind(SyntaxKind.AddExpression) || binary.IsKind(SyntaxKind.SubtractExpression)) &&
                    IsZero(binary.Right) && assign.Left.ToString() == binary.Left.ToString()) return true;
                if (binary.IsKind(SyntaxKind.AddExpression) && IsZero(binary.Left) && assign.Left.ToString() == binary.Right.ToString()) return true;
                if ((binary.IsKind(SyntaxKind.MultiplyExpression) || binary.IsKind(SyntaxKind.DivideExpression)) &&
                    IsOne(binary.Right) && assign.Left.ToString() == binary.Left.ToString()) return true;
                if (binary.IsKind(SyntaxKind.MultiplyExpression) && IsOne(binary.Left) && assign.Left.ToString() == binary.Right.ToString()) return true;
            }
            return false;
        }

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

        private bool IsZero(ExpressionSyntax expr) =>
            expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.NumericLiteralExpression) &&
            (lit.Token.Text == "0" || lit.Token.Text == "0.0" || lit.Token.Text == "0f" || lit.Token.Text == "0d" || lit.Token.Text == "0m");

        private bool IsOne(ExpressionSyntax expr) =>
            expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.NumericLiteralExpression) &&
            (lit.Token.Text == "1" || lit.Token.Text == "1.0" || lit.Token.Text == "1f" || lit.Token.Text == "1d" || lit.Token.Text == "1m");

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