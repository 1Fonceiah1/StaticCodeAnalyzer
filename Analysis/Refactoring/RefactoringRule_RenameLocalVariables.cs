using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_RenameLocalVariables : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "NAM003", "REN001" };

        private static readonly HashSet<string> PoorNames = new()
        {
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
            "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
            "temp", "tmp", "data", "val", "arg", "obj", "var", "item"
        };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var solution = document.Project.Solution;
            bool changed = false;

            // Сначала собираем все переменные, которые требуют переименования
            var localVars = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Parent is VariableDeclarationSyntax decl && decl.Parent is LocalDeclarationStatementSyntax)
                .Select(v => new { Syntax = v, Symbol = semanticModel.GetDeclaredSymbol(v, cancellationToken) })
                .Where(x => x.Symbol != null && PoorNames.Contains(x.Symbol.Name))
                .ToList();

            // Группируем по методу, чтобы обрабатывать каждый метод отдельно
            var methodGroups = localVars.GroupBy(x => x.Syntax.FirstAncestorOrSelf<MethodDeclarationSyntax>());

            foreach (var group in methodGroups)
            {
                var method = group.Key;
                if (method == null) continue;

                // Собираем все имена, которые уже заняты в этом методе (параметры + локальные переменные)
                var usedNames = new HashSet<string>();
                // Параметры метода
                foreach (var p in method.ParameterList.Parameters)
                    usedNames.Add(p.Identifier.Text);
                // Все локальные переменные (включая те, которые не будем переименовывать)
                foreach (var v in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    var sym = semanticModel.GetDeclaredSymbol(v, cancellationToken);
                    if (sym != null)
                        usedNames.Add(sym.Name);
                }

                // Для каждой переменной в группе генерируем уникальное имя
                var renameMap = new Dictionary<ISymbol, string>();
                foreach (var item in group)
                {
                    string suggested = SuggestBetterName(item.Syntax, semanticModel, cancellationToken);
                    if (string.IsNullOrEmpty(suggested)) continue;

                    string unique = suggested;
                    int counter = 1;
                    while (usedNames.Contains(unique))
                    {
                        unique = suggested + counter;
                        counter++;
                    }
                    renameMap[item.Symbol] = unique;
                    usedNames.Add(unique); // резервируем имя для следующих переменных
                }

                // Применяем переименования
                foreach (var kv in renameMap)
                {
                    solution = await Renamer.RenameSymbolAsync(solution, kv.Key, new SymbolRenameOptions(), kv.Value, cancellationToken);
                    changed = true;
                }
            }

            return changed ? solution.GetDocument(document.Id) : document;
        }

        private string SuggestBetterName(VariableDeclaratorSyntax variable, SemanticModel model, CancellationToken ct)
        {
            string name = variable.Identifier.Text;
            var declaration = variable.Parent as VariableDeclarationSyntax;
            var typeSyntax = declaration?.Type;

            // Счётчик цикла for
            if (declaration?.Parent is ForStatementSyntax)
                return "index";

            // Итератор foreach
            if (declaration?.Parent is ForEachStatementSyntax)
                return "item";

            // По имени переменной
            if (name == "i" || name == "j" || name == "k")
                return "counter";
            if (name == "s")
                return "sum";
            if (name == "len")
                return "length";

            // По типу
            if (typeSyntax != null)
            {
                var typeInfo = model.GetTypeInfo(typeSyntax, ct);
                var type = typeInfo.Type;
                if (type != null)
                {
                    if (type.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_Int32 ||
                        type.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_Int64)
                        return "number";
                    if (type.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String)
                        return "text";
                    if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Array)
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