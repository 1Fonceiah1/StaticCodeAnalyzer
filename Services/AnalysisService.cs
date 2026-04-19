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
        public List<AnalysisIssue> AnalyzeFiles(List<string> filePaths, IProgress<int> progress = null)
        {
            if (filePaths == null || filePaths.Count == 0)
                return new List<AnalysisIssue>();

            string rootDirectory = Path.GetDirectoryName(filePaths[0]);
            if (string.IsNullOrEmpty(rootDirectory))
                return new List<AnalysisIssue>();

            ProjectContext context = ProjectContext.Create(rootDirectory);
            return _engine.AnalyzeProject(context, filePaths, progress);
        }

        // Свойство для доступа к движку (требуется в MainWindow для получения ошибок)
        public AnalyzerEngine Engine => _engine;
    }
}