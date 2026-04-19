using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public interface IRefactoringRule
    {
        Document Apply(Document document);

        /// Возвращает коды проблем, которые умеет исправлять данное правило.
        /// Если правило не привязано к конкретному коду, возвращает пустую коллекцию.
        IEnumerable<string> TargetIssueCodes => new List<string>();
    }
}