using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Models;
using StaticCodeAnalyzer.Services;
using System.Text.Json;

namespace StaticCodeAnalyzer.Cli
{
    // CLI анализатор для работы из командной строки
    public class CliAnalyzer
    {
        private readonly AnalysisService _analysisService;

        public CliAnalyzer()
        {
            _analysisService = new AnalysisService();
        }

        // Запускает анализ с указанными опциями и возвращает JSON результат
        public JsonOutput Analyze(CliOptions options)
        {
            var output = new JsonOutput();
            var allIssues = new List<AnalysisIssue>();
            var analyzedFiles = new List<string>();

            try
            {
                // Определяем цели для анализа
                var targets = GetAnalysisTargets(options.Path, options.Recursive);
                
                if (targets.Count == 0)
                {
                    Console.Error.WriteLine($"Не найдены .cs файлы по пути: {options.Path}");
                    return output;
                }

                // Группируем файлы по директориям для корректного анализа
                var filesByDirectory = targets.GroupBy(f => System.IO.Path.GetDirectoryName(f));

                foreach (var group in filesByDirectory)
                {
                    var issues = _analysisService.AnalyzeFiles(group.ToList());
                    allIssues.AddRange(issues);
                    analyzedFiles.AddRange(group);
                }

                // Фильтрация по серьезности
                if (!string.IsNullOrEmpty(options.SeverityFilter))
                {
                    allIssues = FilterBySeverity(allIssues, options.SeverityFilter);
                }

                // Формируем вывод
                output = CreateOutput(allIssues, analyzedFiles, _analysisService.Engine.LastErrors);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Ошибка анализа: {ex.Message}");
                Environment.ExitCode = 1;
            }

            return output;
        }

        // Получает список файлов для анализа
        private List<string> GetAnalysisTargets(string path, bool recursive)
        {
            var targets = new List<string>();

            if (File.Exists(path))
            {
                if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    targets.Add(path);
                }
            }
            else if (Directory.Exists(path))
            {
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                targets.AddRange(Directory.GetFiles(path, "*.cs", searchOption));
            }

            return targets;
        }

        // Фильтрует issues по серьезности
        private List<AnalysisIssue> FilterBySeverity(List<AnalysisIssue> issues, string severityFilter)
        {
            var filter = severityFilter.ToLower();
            return issues.Where(i => 
                i.Severity?.ToLower() == filter ||
                (filter == "critical" && i.Severity == "Критический") ||
                (filter == "high" && i.Severity == "Высокий") ||
                (filter == "medium" && i.Severity == "Средний") ||
                (filter == "low" && i.Severity == "Низкий")
            ).ToList();
        }

        // Создает JSON вывод из результатов анализа
        private JsonOutput CreateOutput(List<AnalysisIssue> issues, List<string> files, List<RuleError> errors)
        {
            var output = new JsonOutput();

            // Summary
            output.Summary.TotalFiles = files.Count;
            output.Summary.TotalIssues = issues.Count;
            output.Summary.CriticalCount = issues.Count(i => i.Severity == "Критический" || i.Severity?.ToLower() == "critical");
            output.Summary.HighCount = issues.Count(i => i.Severity == "Высокий" || i.Severity?.ToLower() == "high");
            output.Summary.MediumCount = issues.Count(i => i.Severity == "Средний" || i.Severity?.ToLower() == "medium");
            output.Summary.LowCount = issues.Count(i => i.Severity == "Низкий" || i.Severity?.ToLower() == "low");

            // Files
            output.Files = files.Select(f => new FileInfo
            {
                Path = f,
                IssuesCount = issues.Count(i => i.FilePath == f)
            }).ToList();

            // Issues
            output.Issues = issues.Select(IssueInfo.FromAnalysisIssue).ToList();

            // Errors
            output.Errors = errors.Select(e => new ErrorInfo
            {
                Rule = e.RuleName,
                File = e.FilePath,
                Message = e.ErrorMessage
            }).ToList();

            return output;
        }
    }
}
