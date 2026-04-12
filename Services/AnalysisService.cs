using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

        public async Task<List<AnalysisIssue>> AnalyzeFiles(List<string> filePaths, IProgress<int> progress = null, CancellationToken cancellationToken = default)
        {
            if (filePaths == null || filePaths.Count == 0)
                return new List<AnalysisIssue>();

            var rootDirectory = Path.GetDirectoryName(filePaths[0]);
            if (string.IsNullOrEmpty(rootDirectory))
                return new List<AnalysisIssue>();

            var context = await ProjectContext.CreateAsync(rootDirectory, cancellationToken);
            return await _engine.AnalyzeProjectAsync(context, filePaths, progress, cancellationToken);
        }
    }
}