using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    // Проверяет, что классы, содержащие поля, реализующие IDisposable, сами реализуют IDisposable
    public class DisposableFieldsRule : IAnalyzerRule
    {
        public List<AnalysisIssue> Analyze(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            List<AnalysisIssue> issues = new List<AnalysisIssue>();
            IEnumerable<ClassDeclarationSyntax> classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (ClassDeclarationSyntax classDecl in classes)
            {
                List<VariableDeclaratorSyntax> disposableFields = new List<VariableDeclaratorSyntax>();

                foreach (FieldDeclarationSyntax fieldDecl in classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    ITypeSymbol? typeSymbol = semanticModel.GetTypeInfo(fieldDecl.Declaration.Type).Type;
                    if (typeSymbol != null && ImplementsIDisposable(typeSymbol))
                    {
                        disposableFields.AddRange(fieldDecl.Declaration.Variables);
                    }
                }

                if (!disposableFields.Any()) continue;

                bool implementsIDisposable = ClassImplementsIDisposable(classDecl, semanticModel);

                if (!implementsIDisposable)
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
                            Code = "DISP001",
                            Description = $"Класс '{classDecl.Identifier.Text}' содержит поля, реализующие IDisposable, но сам не реализует IDisposable.",
                            Suggestion = "Реализуйте интерфейс IDisposable для корректного освобождения ресурсов.",
                            RuleName = "DisposableFields",
                            ContainingTypeName = classDecl.Identifier.Text,
                            MethodName = null
                        });
                    }
                }
            }

            return issues;
        }

        // Проверяет, реализует ли тип IDisposable
        private bool ImplementsIDisposable(ITypeSymbol type)
        {
            if (type == null) return false;
            if (type.Name == "IDisposable" && type.ContainingNamespace?.ToDisplayString() == "System")
                return true;
            if (type.Interfaces.Any(i => i.Name == "IDisposable" && i.ContainingNamespace?.ToDisplayString() == "System"))
                return true;
            return false;
        }

        // Проверяет, реализует ли класс IDisposable в синтаксическом дереве
        private bool ClassImplementsIDisposable(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
        {
            if (classDecl.BaseList == null) return false;
            return classDecl.BaseList.Types.Any(t =>
            {
                string typeName = t.Type.ToString();
                return typeName == "IDisposable" || 
                       typeName == "System.IDisposable" ||
                       typeName.EndsWith(".IDisposable");
            });
        }
    }
}