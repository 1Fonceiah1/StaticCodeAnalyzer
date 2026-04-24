using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Data
{
    // Репозиторий для работы с базой данных (CRUD-операции)
    public class Repository : IDisposable
    {
        private readonly AppDbContext _context;

        public Repository(AppDbContext context)
        {
            _context = context;
        }

        // Возвращает существующий проект по пути или создаёт новый
        public Project GetOrCreateProject(string projectPath, string projectName)
        {
            Project project = _context.Projects.FirstOrDefault(p => p.ProjectPath == projectPath);
            if (project == null)
            {
                project = new Project
                {
                    ProjectName = projectName,
                    ProjectPath = projectPath,
                    CreatedAt = DateTime.UtcNow,
                    Language = "C#"
                };
                _context.Projects.Add(project);
                _context.SaveChanges();
            }
            return project;
        }

        // Обновляет дату последнего сканирования проекта
        public void UpdateProjectLastScanned(int projectId)
        {
            Project project = _context.Projects.Find(projectId);
            if (project != null)
            {
                project.LastScanned = DateTime.UtcNow;
                _context.SaveChanges();
            }
        }

        // Добавляет запись о сканировании
        public void AddScan(Scan scan)
        {
            _context.Scans.Add(scan);
            _context.SaveChanges();
        }

        // Добавляет коллекцию результатов анализа
        public void AddAnalysisResults(IEnumerable<AnalysisResult> results)
        {
            _context.AnalysisResults.AddRange(results);
        }

        // Добавляет коллекцию отсканированных файлов
        public void AddScannedFiles(IEnumerable<ScannedFile> files)
        {
            _context.ScannedFiles.AddRange(files);
        }

        // Возвращает существующее правило по коду или создаёт новое
        public AnalysisRule GetOrCreateRule(string ruleCode, string ruleName, string category, string severity, string description)
        {
            AnalysisRule rule = _context.AnalysisRules.FirstOrDefault(r => r.RuleName == ruleCode);
            if (rule == null)
            {
                rule = new AnalysisRule
                {
                    RuleName = ruleCode,
                    RuleCategory = category,
                    RuleSeverity = severity,
                    IsActive = true,
                    RuleDescription = description,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AnalysisRules.Add(rule);
                _context.SaveChanges();
            }
            return rule;
        }

        // Добавляет коллекцию исключений для проекта
        public void AddExclusions(IEnumerable<AnalysisExclusion> exclusions)
        {
            _context.AnalysisExclusions.AddRange(exclusions);
        }

        // Сохраняет все изменения в базе данных
        public void SaveChanges()
        {
            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}