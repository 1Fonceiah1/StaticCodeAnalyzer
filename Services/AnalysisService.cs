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
    // Сервис для выполнения анализа над набором файлов
    public class AnalysisService
    {
        private readonly AnalyzerEngine _engine;

        public AnalysisService()
        {
            _engine = new AnalyzerEngine();
        }

        // Запускает анализ для списка файлов и возвращает найденные проблемы
        public async Task<List<AnalysisIssue>> AnalyzeFiles(List<string> filePaths, IProgress<int> progress = null, CancellationToken cancellationToken = default)
        {
            if (filePaths == null || filePaths.Count == 0)
                return new List<AnalysisIssue>();

            string rootDirectory = Path.GetDirectoryName(filePaths[0]);
            if (string.IsNullOrEmpty(rootDirectory))
                return new List<AnalysisIssue>();

            ProjectContext context = await ProjectContext.CreateAsync(rootDirectory, cancellationToken);
            return await _engine.AnalyzeProjectAsync(context, filePaths, progress, cancellationToken);
        }

        // Свойство для доступа к движку (требуется в MainWindow для получения ошибок)
        public AnalyzerEngine Engine => _engine;
    }
}