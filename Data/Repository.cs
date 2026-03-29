using Microsoft.EntityFrameworkCore;
using StaticCodeAnalyzer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Data
{
    public class Repository : IDisposable
    {
        private readonly AppDbContext _context;

        public Repository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Project> GetOrCreateProjectAsync(string projectPath, string projectName)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectPath == projectPath);
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

        public async Task AddScanAsync(Scan scan)
        {
            _context.Scans.Add(scan);
            await _context.SaveChangesAsync();
        }

        public async Task AddAnalysisResultsAsync(IEnumerable<AnalysisResult> results)
        {
            await _context.AnalysisResults.AddRangeAsync(results);
        }

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