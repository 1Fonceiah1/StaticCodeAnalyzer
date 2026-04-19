using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Предупреждает о классах с изменяемыми полями, которые реально изменяются, но не имеют синхронизации
    public class ThreadSafetyRule : IAnalyzerRule
    {
        public List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<ClassDeclarationSyntax> classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (ClassDeclarationSyntax classDecl in classes)
            {
                // Собирает изменяемые поля (не readonly и не const)
                List<VariableDeclaratorSyntax> mutableFields = classDecl.Members
                    .OfType<FieldDeclarationSyntax>()
                    .Where(f => !f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) && !f.Modifiers.Any(SyntaxKind.ConstKeyword))
                    .SelectMany(f => f.Declaration.Variables)
                    .ToList();

                // Преобразует в список с символами
                var mutableFieldsWithSymbols = mutableFields
                    .Select(v => new
                    {
                        Syntax = v,
                        Symbol = semanticModel.GetDeclaredSymbol(v) as IFieldSymbol
                    })
                    .Where(x => x.Symbol != null)
                    .ToList();

                if (!mutableFieldsWithSymbols.Any()) continue;

                // Проверяет, действительно ли поля изменяются после конструктора
                bool anyFieldMutated = false;
                foreach (var field in mutableFieldsWithSymbols)
                {
                    if (IsFieldWrittenAfterConstructor(classDecl, field.Symbol!, semanticModel))
                    {
                        anyFieldMutated = true;
                        break;
                    }
                }

                if (!anyFieldMutated) continue;

                // Проверяет наличие синхронизации
                bool hasLock = classDecl.DescendantNodes().OfType<LockStatementSyntax>().Any();
                bool hasThreadSafeTypes = mutableFieldsWithSymbols.Any(f => IsThreadSafeType(f.Symbol!.Type.ToDisplayString()));

                if (!hasLock && !hasThreadSafeTypes)
                {
                    Microsoft.CodeAnalysis.Location? location = classDecl.Identifier.GetLocation();
                    if (location != null)
                    {
                        FileLinePositionSpan lineSpan = location.GetLineSpan();
                        issues.Add(new AnalysisIssue
                        {
                            Severity = "Высокий",
                            FilePath = filePath,
                            LineNumber = lineSpan.StartLinePosition.Line + 1,
                            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
                            Type = "предупреждение",
                            Code = "THR001",
                            Description = $"Класс '{classDecl.Identifier.Text}' содержит изменяемые поля, которые реально изменяются после конструктора, без синхронизации.",
                            Suggestion = "Добавьте lock, используйте Concurrent-коллекции или Immutable-типы.",
                            RuleName = "ThreadSafety",
                            ContainingTypeName = classDecl.Identifier.Text,
                            MethodName = null
                        });
                    }
                }
            }

            return issues;
        }

        // Определяет, изменяется ли поле после конструктора
        private bool IsFieldWrittenAfterConstructor(ClassDeclarationSyntax classDecl, IFieldSymbol field, SemanticModel model)
        {
            IEnumerable<MemberDeclarationSyntax> members = classDecl.Members;
            foreach (MemberDeclarationSyntax member in members)
            {
                if (member is ConstructorDeclarationSyntax) continue;
                if (member is MethodDeclarationSyntax method && method.Body != null)
                {
                    if (MethodWritesField(method, field, model)) return true;
                }
                if (member is PropertyDeclarationSyntax prop && prop.AccessorList != null)
                {
                    foreach (AccessorDeclarationSyntax accessor in prop.AccessorList.Accessors)
                    {
                        if (accessor.Body != null && MethodWritesField(accessor.Body, field, model))
                            return true;
                    }
                }
            }
            return false;
        }

        // Проверяет, записывает ли метод в поле
        private bool MethodWritesField(MethodDeclarationSyntax method, IFieldSymbol field, SemanticModel model)
        {
            return MethodWritesField(method.Body!, field, model);
        }

        private bool MethodWritesField(BlockSyntax body, IFieldSymbol field, SemanticModel model)
        {
            IEnumerable<AssignmentExpressionSyntax> assignments = body.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(a => a.IsKind(SyntaxKind.SimpleAssignmentExpression) ||
                            a.IsKind(SyntaxKind.AddAssignmentExpression) ||
                            a.IsKind(SyntaxKind.SubtractAssignmentExpression) ||
                            a.IsKind(SyntaxKind.MultiplyAssignmentExpression));

            foreach (AssignmentExpressionSyntax assign in assignments)
            {
                ExpressionSyntax left = assign.Left;
                if (left is IdentifierNameSyntax id)
                {
                    ISymbol? symbol = model.GetSymbolInfo(id).Symbol;
                    if (SymbolEqualityComparer.Default.Equals(symbol, field))
                        return true;
                }
                if (left is MemberAccessExpressionSyntax member && member.Expression is ThisExpressionSyntax)
                {
                    ISymbol? symbol = model.GetSymbolInfo(member).Symbol;
                    if (SymbolEqualityComparer.Default.Equals(symbol, field))
                        return true;
                }
            }
            return false;
        }

        // Проверяет, является ли тип потокобезопасным (упрощённо)
        private bool IsThreadSafeType(string typeName)
        {
            string[] safeTypes = new[] { "string", "int", "long", "bool", "decimal", "DateTime", 
                                       "ConcurrentDictionary", "ConcurrentQueue", "ImmutableArray", "ImmutableList" };
            return safeTypes.Any(t => typeName.Contains(t));
        }
    }
}