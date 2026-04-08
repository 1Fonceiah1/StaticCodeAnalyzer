using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Analysis
{
    public class CodeDuplicationRule : IAnalyzerRule
    {
        // Сравнивает все методы в файле и находит пары с идентичным телом
        public async Task<List<AnalysisIssue>> AnalyzeAsync(SyntaxNode root, SemanticModel semanticModel, string filePath)
        {
            var issues = new List<AnalysisIssue>();

            // Ищет все методы в файле
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            if (methods.Count < 2) return issues; // не с чем сравнивать

            // Сравнивает каждую пару методов на идентичность тела (без учёта отступов и имён переменных)
            for (int i = 0; i < methods.Count; i++)
            {
                for (int j = i + 1; j < methods.Count; j++)
                {
                    var method1 = methods[i];
                    var method2 = methods[j];
                    if (AreBodiesIdentical(method1.Body, method2.Body))
                    {
                        // Сообщает о дублировании
                        var loc1 = method1.Identifier.GetLocation();
                        var loc2 = method2.Identifier.GetLocation();
                        if (loc1 != null && loc2 != null)
                        {
                            var lineSpan1 = loc1.GetLineSpan();
                            var lineSpan2 = loc2.GetLineSpan();
                            issues.Add(new AnalysisIssue
                            {
                                Severity = "Средний",
                                FilePath = filePath,
                                LineNumber = lineSpan1.StartLinePosition.Line + 1,
                                ColumnNumber = lineSpan1.StartLinePosition.Character + 1,
                                Type = "запах кода",
                                Code = "DUP001",
                                Description = $"Метод '{method1.Identifier.Text}' дублирует код метода '{method2.Identifier.Text}'.",
                                Suggestion = "Вынесите общий код в отдельный метод или используйте наследование/композицию.",
                                RuleName = "CodeDuplication"
                            });
                        }
                    }
                }
            }

            return issues;
        }

        private bool AreBodiesIdentical(BlockSyntax? body1, BlockSyntax? body2)
        {
            if (body1 == null && body2 == null) return true;
            if (body1 == null || body2 == null) return false;

            // Сравнивает строковые представления, игнорируя пробелы и переводы строк
            string code1 = body1.NormalizeWhitespace().ToFullString();
            string code2 = body2.NormalizeWhitespace().ToFullString();
            return code1 == code2;
        }
    }
}