using Microsoft.EntityFrameworkCore;
using StaticCodeAnalyzer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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
                    CreatedAt = DateTime.Now,
                    Language = "C#"
                };
                _context.Projects.Add(project);
                _context.SaveChanges();
            }
            return project;
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
            _context.SaveChanges();
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