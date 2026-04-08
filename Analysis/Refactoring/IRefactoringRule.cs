using Microsoft.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public interface IRefactoringRule
    {
        Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken = default);
    }
}