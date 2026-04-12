using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Выявляет различные формы мёртвого и избыточного кода
    public class DeadCodeRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();

            // 1. Находит бесполезные присваивания (x = x + 0, x = x * 1, x = x)
            List<AssignmentExpressionSyntax> uselessAssignments = root.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(IsUselessAssignment)
                .ToList();
            foreach (AssignmentExpressionSyntax assign in uselessAssignments)
                AddIssue(assign, assign.GetLocation(), "DEAD001", "Бесполезное присваивание (например, x = x + 0, x = x * 1 или x = x).", issues, filePath);

            // 2. Находит тривиальные циклы for (одна итерация без побочных эффектов)
            List<ForStatementSyntax> trivialForLoops = root.DescendantNodes()
                .OfType<ForStatementSyntax>()
                .Where(IsTrivialForLoop)
                .ToList();
            foreach (ForStatementSyntax loop in trivialForLoops)
                AddIssue(loop, loop.ForKeyword.GetLocation(), "SIM001", "Цикл for с одной итерацией без побочных эффектов.", issues, filePath);

            // 3. Находит тривиальные циклы while (одна итерация без побочных эффектов)
            List<WhileStatementSyntax> trivialWhileLoops = root.DescendantNodes()
                .OfType<WhileStatementSyntax>()
                .Where(IsTrivialWhileLoop)
                .ToList();
            foreach (WhileStatementSyntax loop in trivialWhileLoops)
                AddIssue(loop, loop.WhileKeyword.GetLocation(), "SIM001", "Цикл while с одной итерацией без побочных эффектов.", issues, filePath);

            // 4. Находит бесполезные операторы-выражения (просто идентификатор)
            List<ExpressionStatementSyntax> uselessIdStatements = root.DescendantNodes()
                .OfType<ExpressionStatementSyntax>()
                .Where(stmt => stmt.Expression is IdentifierNameSyntax)
                .ToList();
            foreach (ExpressionStatementSyntax stmt in uselessIdStatements)
                AddIssue(stmt, stmt.GetLocation(), "DEAD002", "Бесполезный оператор (просто идентификатор, не имеющий побочных эффектов).", issues, filePath);

            // 5. Находит пустые блоки else
            List<ElseClauseSyntax> emptyElseBlocks = root.DescendantNodes()
                .OfType<IfStatementSyntax>()
                .Where(ifStmt => ifStmt.Else != null && IsEffectivelyEmpty(ifStmt.Else.Statement))
                .Select(ifStmt => ifStmt.Else!)
                .ToList();
            foreach (ElseClauseSyntax elseClause in emptyElseBlocks)
                AddIssue(elseClause, elseClause.ElseKeyword.GetLocation(), "DEAD003", "Пустой блок else (не содержит полезного кода).", issues, filePath);

            // 6. Находит пустые блоки if (без else)
            List<IfStatementSyntax> emptyIfBlocks = root.DescendantNodes()
                .OfType<IfStatementSyntax>()
                .Where(ifStmt => ifStmt.Else == null && IsEffectivelyEmpty(ifStmt.Statement))
                .ToList();
            foreach (IfStatementSyntax ifStmt in emptyIfBlocks)
                AddIssue(ifStmt, ifStmt.IfKeyword.GetLocation(), "DEAD003", "Пустой блок if (не содержит полезного кода).", issues, filePath);

            // 7. Находит дублирующиеся присваивания подряд
            List<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            foreach (MethodDeclarationSyntax method in methods)
            {
                if (method.Body == null) continue;
                List<StatementSyntax> statements = method.Body.Statements.ToList();
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

            // 8. Находит пары противоположных операций (x = x + 1; x = x - 1;)
            List<ExpressionStatementSyntax> oppositePairs = FindOppositeOperationPairs(root);
            foreach (ExpressionStatementSyntax stmt in oppositePairs)
                AddIssue(stmt, stmt.GetLocation(), "DEAD001", "Пара противоположных операций (например, x = x + 1; x = x - 1;).", issues, filePath);

            // 9. Находит бесполезные инкременты неиспользуемой переменной
            List<ExpressionStatementSyntax> uselessIncrements = FindUselessIncrements(root);
            foreach (ExpressionStatementSyntax stmt in uselessIncrements)
                AddIssue(stmt, stmt.GetLocation(), "DEAD001", "Бесполезный инкремент (переменная не используется).", issues, filePath);

            // 10. Находит бессмысленные if (x == false) { x = false; }
            List<IfStatementSyntax> selfAssignIfs = FindSelfAssignIf(root);
            foreach (IfStatementSyntax ifStmt in selfAssignIfs)
                AddIssue(ifStmt, ifStmt.IfKeyword.GetLocation(), "DEAD001", "Бессмысленный if, можно заменить на присваивание.", issues, filePath);

            return Task.FromResult(issues);
        }

        private void AddIssue(SyntaxNode node, Microsoft.CodeAnalysis.Location? location, string code, string description, List<AnalysisIssue> issues, string filePath)
        {
            if (location == null) return;
            FileLinePositionSpan lineSpan = location.GetLineSpan();
            MethodDeclarationSyntax? containingMethod = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            ClassDeclarationSyntax? containingClass = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
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

        // Проверяет, является ли присваивание бесполезным
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

        // Определяет тривиальный цикл for
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

        // Определяет тривиальный цикл while
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

        // Проверяет наличие побочных эффектов в операторе
        private bool HasSideEffects(StatementSyntax statement)
        {
            foreach (AssignmentExpressionSyntax assign in statement.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assign.Left is ElementAccessExpressionSyntax || assign.Left is MemberAccessExpressionSyntax)
                    return true;
            }
            return false;
        }

        // Проверяет, является ли выражение нулём
        private bool IsZero(ExpressionSyntax expr)
        {
            return expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.NumericLiteralExpression) &&
                   (lit.Token.Text == "0" || lit.Token.Text == "0.0" || lit.Token.Text == "0f" || lit.Token.Text == "0d" || lit.Token.Text == "0m");
        }

        // Проверяет, является ли выражение единицей
        private bool IsOne(ExpressionSyntax expr)
        {
            return expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.NumericLiteralExpression) &&
                   (lit.Token.Text == "1" || lit.Token.Text == "1.0" || lit.Token.Text == "1f" || lit.Token.Text == "1d" || lit.Token.Text == "1m");
        }

        // Проверяет, является ли блок или оператор фактически пустым
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

        // Определяет бесполезный оператор или объявление
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

        // Находит пары противоположных операций
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

        // Проверяет, являются ли два присваивания противоположными
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

        // Находит бесполезные инкременты
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

        // Находит конструкции if (x == false) { x = false; }
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