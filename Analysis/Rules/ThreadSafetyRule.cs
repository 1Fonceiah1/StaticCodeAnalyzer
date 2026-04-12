using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class ThreadSafetyRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classes)
            {
                var mutableFields = classDecl.Members
                    .OfType<FieldDeclarationSyntax>()
                    .Where(f => !f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) && !f.Modifiers.Any(SyntaxKind.ConstKeyword))
                    .SelectMany(f => f.Declaration.Variables)
                    .Select(v => new
                    {
                        Syntax = v,
                        Symbol = semanticModel.GetDeclaredSymbol(v) as IFieldSymbol
                    })
                    .Where(x => x.Symbol != null)
                    .ToList();

                if (!mutableFields.Any()) continue;

                bool anyFieldMutated = false;
                foreach (var field in mutableFields)
                {
                    if (IsFieldWrittenAfterConstructor(classDecl, field.Symbol, semanticModel))
                    {
                        anyFieldMutated = true;
                        break;
                    }
                }

                if (!anyFieldMutated) continue;

                bool hasLock = classDecl.DescendantNodes().OfType<LockStatementSyntax>().Any();
                bool hasThreadSafeTypes = mutableFields.Any(f => IsThreadSafeType(f.Symbol.Type.ToDisplayString()));

                if (!hasLock && !hasThreadSafeTypes)
                {
                    var location = classDecl.Identifier.GetLocation();
                    if (location != null)
                    {
                        var lineSpan = location.GetLineSpan();
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

            return Task.FromResult(issues);
        }

        private bool IsFieldWrittenAfterConstructor(ClassDeclarationSyntax classDecl, IFieldSymbol field, SemanticModel model)
        {
            var members = classDecl.Members;
            foreach (var member in members)
            {
                if (member is ConstructorDeclarationSyntax) continue;
                if (member is MethodDeclarationSyntax method && method.Body != null)
                {
                    if (MethodWritesField(method, field, model)) return true;
                }
                if (member is PropertyDeclarationSyntax prop && prop.AccessorList != null)
                {
                    foreach (var accessor in prop.AccessorList.Accessors)
                    {
                        if (accessor.Body != null && MethodWritesField(accessor.Body, field, model))
                            return true;
                    }
                }
            }
            return false;
        }

        private bool MethodWritesField(MethodDeclarationSyntax method, IFieldSymbol field, SemanticModel model)
        {
            return MethodWritesField(method.Body, field, model);
        }

        private bool MethodWritesField(BlockSyntax body, IFieldSymbol field, SemanticModel model)
        {
            var assignments = body.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(a => a.IsKind(SyntaxKind.SimpleAssignmentExpression) ||
                            a.IsKind(SyntaxKind.AddAssignmentExpression) ||
                            a.IsKind(SyntaxKind.SubtractAssignmentExpression) ||
                            a.IsKind(SyntaxKind.MultiplyAssignmentExpression));

            foreach (var assign in assignments)
            {
                var left = assign.Left;
                if (left is IdentifierNameSyntax id)
                {
                    var symbol = model.GetSymbolInfo(id).Symbol;
                    if (SymbolEqualityComparer.Default.Equals(symbol, field))
                        return true;
                }
                if (left is MemberAccessExpressionSyntax member && member.Expression is ThisExpressionSyntax)
                {
                    var symbol = model.GetSymbolInfo(member).Symbol;
                    if (SymbolEqualityComparer.Default.Equals(symbol, field))
                        return true;
                }
            }
            return false;
        }

        private bool IsThreadSafeType(string typeName)
        {
            var safeTypes = new[] { "string", "int", "long", "bool", "decimal", "DateTime", 
                                   "ConcurrentDictionary", "ConcurrentQueue", "ImmutableArray", "ImmutableList" };
            return safeTypes.Any(t => typeName.Contains(t));
        }
    }
}