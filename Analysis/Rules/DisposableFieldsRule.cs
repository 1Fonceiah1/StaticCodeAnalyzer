using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class DisposableFieldsRule : IAnalyzerRule
    {
        public Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classes)
            {
                var disposableFields = new List<VariableDeclaratorSyntax>();

                foreach (var fieldDecl in classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    var typeSymbol = semanticModel.GetTypeInfo(fieldDecl.Declaration.Type).Type;
                    if (typeSymbol != null && ImplementsIDisposable(typeSymbol))
                    {
                        disposableFields.AddRange(fieldDecl.Declaration.Variables);
                    }
                }

                if (!disposableFields.Any()) continue;

                bool implementsIDisposable = ClassImplementsIDisposable(classDecl, semanticModel);

                if (!implementsIDisposable)
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
                            Code = "DISP001",
                            Description = $"Класс '{classDecl.Identifier.Text}' содержит поля, реализующие IDisposable, но сам не реализует IDisposable.",
                            Suggestion = "Реализуйте интерфейс IDisposable для корректного освобождения ресурсов.",
                            RuleName = "DisposableFields"
                        });
                    }
                }
            }

            return Task.FromResult(issues);
        }

        private bool ImplementsIDisposable(ITypeSymbol type)
        {
            if (type == null) return false;
            if (type.Name == "IDisposable" && type.ContainingNamespace?.ToDisplayString() == "System")
                return true;
            if (type.Interfaces.Any(i => i.Name == "IDisposable" && i.ContainingNamespace?.ToDisplayString() == "System"))
                return true;
            return false;
        }

        private bool ClassImplementsIDisposable(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
        {
            if (classDecl.BaseList == null) return false;
            return classDecl.BaseList.Types.Any(t =>
            {
                var typeName = t.Type.ToString();
                return typeName == "IDisposable" || 
                       typeName == "System.IDisposable" ||
                       typeName.EndsWith(".IDisposable");
            });
        }
    }
}