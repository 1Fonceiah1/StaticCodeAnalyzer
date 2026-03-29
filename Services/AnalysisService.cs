using System.Collections.Generic;
using System.Threading.Tasks;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Services
{
    public class AnalysisService
    {
        private readonly AnalyzerEngine _engine;

        public AnalysisService()
        {
            _engine = new AnalyzerEngine();
        }

        public async Task<List<AnalysisIssue>> AnalyzeFiles(List<string> filePaths)
        {
            var allIssues = new List<AnalysisIssue>();
            foreach (var file in filePaths)
            {
                var issues = await _engine.AnalyzeFileAsync(file);
                allIssues.AddRange(issues);
            }
            return allIssues;
        }
    }
}