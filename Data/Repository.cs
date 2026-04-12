using Microsoft.EntityFrameworkCore;
using StaticCodeAnalyzer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public async Task<Project> GetOrCreateProjectAsync(string projectPath, string projectName)
        {
            Project project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectPath == projectPath);
            if (project == null)
            {
                project = new Project
                {
                    ProjectName = projectName,
                    ProjectPath = projectPath,
                    CreatedAt = DateTime.Now,
                    Language = "C#"
                };
                _context.Projects.Add(project);
                await _context.SaveChangesAsync();
            }
            return project;
        }

        // Добавляет запись о сканировании
        public async Task AddScanAsync(Scan scan)
        {
            _context.Scans.Add(scan);
            await _context.SaveChangesAsync();
        }

        // Добавляет коллекцию результатов анализа
        public async Task AddAnalysisResultsAsync(IEnumerable<AnalysisResult> results)
        {
            await _context.AnalysisResults.AddRangeAsync(results);
        }

        // Сохраняет все изменения в базе данных
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}