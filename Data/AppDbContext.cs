using Microsoft.EntityFrameworkCore;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Data
{
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
            optionsBuilder.UseNpgsql("Host=localhost;Database=StaticAnalyzer;Username=postgres;Password=3457");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Scan>()
                .Property(s => s.Parameters)
                .HasColumnType("jsonb");

            modelBuilder.Entity<Project>()
                .Property(p => p.ProjectId)
                .HasDefaultValueSql("nextval('seq_project_id')");
            modelBuilder.Entity<Scan>()
                .Property(s => s.ScanId)
                .HasDefaultValueSql("nextval('seq_scan_id')");
            modelBuilder.Entity<AnalysisResult>()
                .Property(ar => ar.ResultId)
                .HasDefaultValueSql("nextval('seq_result_id')");

            modelBuilder.Entity<Scan>().HasIndex(s => s.ProjectId);
            modelBuilder.Entity<Scan>().HasIndex(s => s.StartTime);
            modelBuilder.Entity<AnalysisResult>().HasIndex(ar => ar.ScanId);
            modelBuilder.Entity<AnalysisResult>().HasIndex(ar => ar.ProjectId);
            modelBuilder.Entity<AnalysisResult>().HasIndex(ar => ar.RuleId);
            modelBuilder.Entity<AnalysisResult>().HasIndex(ar => ar.FilePath);
        }
    }
}