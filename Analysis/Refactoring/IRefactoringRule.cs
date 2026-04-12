using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public interface IRefactoringRule
    {
        Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Возвращает коды проблем, которые умеет исправлять данное правило.
        /// Если правило не привязано к конкретному коду, возвращает пустую коллекцию.
        /// </summary>
        IEnumerable<string> TargetIssueCodes => new List<string>();
    }
}