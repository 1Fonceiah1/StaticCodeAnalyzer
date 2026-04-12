using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StaticCodeAnalyzer.Models;
using System.IO;

namespace StaticCodeAnalyzer.Data
{
    // Контекст базы данных для работы с проектами, сканированиями и результатами анализа
    public class AppDbContext : DbContext
    {
        public DbSet<Project> Projects { get; set; }
        public DbSet<Scan> Scans { get; set; }
        public DbSet<AnalysisResult> AnalysisResults { get; set; }
        public DbSet<AnalysisRule> AnalysisRules { get; set; }
        public DbSet<AnalysisExclusion> AnalysisExclusions { get; set; }
        public DbSet<ScannedFile> ScannedFiles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();
                string connectionString = configuration.GetConnectionString("DefaultConnection");
                optionsBuilder.UseNpgsql(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Настройка хранения параметров сканирования как JSONB
            modelBuilder.Entity<Scan>()
                .Property(s => s.Parameters)
                .HasColumnType("jsonb");

            // Настройка последовательностей для первичных ключей
            modelBuilder.Entity<Project>()
                .Property(p => p.ProjectId)
                .HasDefaultValueSql("nextval('seq_project_id')");
            modelBuilder.Entity<Scan>()
                .Property(s => s.ScanId)
                .HasDefaultValueSql("nextval('seq_scan_id')");
            modelBuilder.Entity<AnalysisResult>()
                .Property(ar => ar.ResultId)
                .HasDefaultValueSql("nextval('seq_result_id')");

            // Создание индексов для ускорения запросов
            modelBuilder.Entity<Scan>().HasIndex(s => s.ProjectId);
            modelBuilder.Entity<Scan>().HasIndex(s => s.StartTime);
            modelBuilder.Entity<AnalysisResult>().HasIndex(ar => ar.ScanId);
            modelBuilder.Entity<AnalysisResult>().HasIndex(ar => ar.ProjectId);
            modelBuilder.Entity<AnalysisResult>().HasIndex(ar => ar.RuleId);
            modelBuilder.Entity<AnalysisResult>().HasIndex(ar => ar.FilePath);
        }
    }
}