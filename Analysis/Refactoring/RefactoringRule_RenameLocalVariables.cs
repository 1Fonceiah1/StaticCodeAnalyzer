using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using System.Collections.Generic;
using System.Linq;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_RenameLocalVariables : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "REN001" };

        // Список неинформативных имён переменных
        private static readonly HashSet<string> PoorNames = new HashSet<string>()
        {
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
            "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
            "temp", "tmp", "data", "val", "arg", "obj", "var", "item"
        };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            Solution solution = document.Project.Solution;
            bool changed = false;

            // Собирает все локальные переменные с неудачными именами
            List<VariableDeclaratorSyntax> localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Parent is VariableDeclarationSyntax decl && decl.Parent is LocalDeclarationStatementSyntax)
                .ToList();

            // Фильтрует по неинформативным именам
            List<(VariableDeclaratorSyntax Syntax, ISymbol Symbol)> poorVars = localVars
                .Select(v => (Syntax: v, Symbol: semanticModel.GetDeclaredSymbol(v)))
                .Where(x => x.Symbol != null && PoorNames.Contains(x.Symbol.Name))
                .ToList();

            // Группирует по методу для обработки каждого метода отдельно
            var methodGroups = poorVars.GroupBy(x => x.Syntax.FirstAncestorOrSelf<MethodDeclarationSyntax>());

            foreach (var group in methodGroups)
            {
                MethodDeclarationSyntax? method = group.Key;
                if (method == null) continue;

                // Собирает все занятые имена в этом методе (параметры + локальные переменные)
                HashSet<string> usedNames = new HashSet<string>();
                foreach (ParameterSyntax p in method.ParameterList.Parameters)
                    usedNames.Add(p.Identifier.Text);
                foreach (VariableDeclaratorSyntax v in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    ISymbol? sym = semanticModel.GetDeclaredSymbol(v);
                    if (sym != null)
                        usedNames.Add(sym.Name);
                }

                // Создаёт карту переименований для переменных в группе
                Dictionary<ISymbol, string> renameMap = new Dictionary<ISymbol, string>();
                foreach (var item in group)
                {
                    string suggested = SuggestBetterName(item.Syntax, semanticModel);
                    if (string.IsNullOrEmpty(suggested)) continue;

                    string unique = suggested;
                    int counter = 1;
                    while (usedNames.Contains(unique))
                    {
                        unique = suggested + counter;
                        counter++;
                    }
                    renameMap[item.Symbol] = unique;
                    usedNames.Add(unique);
                }

                // Применяет переименования
                foreach (KeyValuePair<ISymbol, string> kv in renameMap)
                {
                    solution = Renamer.RenameSymbolAsync(solution, kv.Key, new SymbolRenameOptions(), kv.Value).GetAwaiter().GetResult();
                    changed = true;
                }
            }

            return changed ? solution.GetDocument(document.Id) : document;
        }

        // Предлагает осмысленное имя на основе контекста переменной
        private string SuggestBetterName(VariableDeclaratorSyntax variable, SemanticModel model)
        {
            string name = variable.Identifier.Text;
            VariableDeclarationSyntax? declaration = variable.Parent as VariableDeclarationSyntax;
            TypeSyntax? typeSyntax = declaration?.Type;

            // Для счётчика цикла for
            if (declaration?.Parent is ForStatementSyntax)
                return "index";

            // Для итератора foreach
            if (declaration?.Parent is ForEachStatementSyntax)
                return "item";

            // По имени переменной
            if (name == "i" || name == "j" || name == "k")
                return "counter";
            if (name == "s")
                return "sum";
            if (name == "len")
                return "length";

            // По типу переменной
            if (typeSyntax != null)
            {
                TypeInfo typeInfo = model.GetTypeInfo(typeSyntax);
                ITypeSymbol? type = typeInfo.Type;
                if (type != null)
                {
                    if (type.SpecialType == SpecialType.System_Int32 ||
                        type.SpecialType == SpecialType.System_Int64)
                        return "number";
                    if (type.SpecialType == SpecialType.System_String)
                        return "text";
                    if (type.TypeKind == TypeKind.Array)
                        return "array";
                }
            }

            // По инициализатору
            if (variable.Initializer?.Value is LiteralExpressionSyntax lit)
            {
                if (lit.Token.Value is int) return "value";
                if (lit.Token.Value is string) return "message";
            }

            return "localVar";
        }
    }
}