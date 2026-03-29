using System;
using System.Collections.Generic;

namespace StaticCodeAnalyzer.Models
{
    public class Project
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ProjectPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastScanned { get; set; }
        public string Language { get; set; }

        public ICollection<Scan> Scans { get; set; }
        public ICollection<AnalysisResult> AnalysisResults { get; set; }
        public ICollection<AnalysisExclusion> Exclusions { get; set; }
    }

    public class Scan
    {
        public int ScanId { get; set; }
        public int ProjectId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Parameters { get; set; } // JSONB
        public string UserName { get; set; }
        public string ExternalLogPath { get; set; }
        public int TotalFilesScanned { get; set; }
        public int TotalIssuesFound { get; set; }

        public Project Project { get; set; }
        public ICollection<AnalysisResult> AnalysisResults { get; set; }
        public ICollection<ScannedFile> ScannedFiles { get; set; }
    }

    public class AnalysisResult
    {
        public int ResultId { get; set; }
        public int ScanId { get; set; }
        public int ProjectId { get; set; }
        public int? RuleId { get; set; }
        public string FilePath { get; set; }
        public int? LineNumber { get; set; }
        public int? ColumnNumber { get; set; }
        public string IssueType { get; set; }
        public string IssueSeverity { get; set; }
        public string IssueCode { get; set; }
        public string IssueDescription { get; set; }
        public string SuggestedFix { get; set; }
        public DateTime CreatedAt { get; set; }

        public Scan Scan { get; set; }
        public Project Project { get; set; }
        public AnalysisRule Rule { get; set; }
    }

    public class AnalysisRule
    {
        public int RuleId { get; set; }
        public string RuleName { get; set; }
        public string RuleCategory { get; set; }
        public string RuleSeverity { get; set; }
        public bool IsActive { get; set; }
        public string RuleDescription { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<AnalysisResult> AnalysisResults { get; set; }
    }

    public class AnalysisExclusion
    {
        public int ExclusionId { get; set; }
        public int ProjectId { get; set; }
        public string ExclusionPattern { get; set; }
        public string ExclusionType { get; set; }
        public DateTime CreatedAt { get; set; }

        public Project Project { get; set; }
    }

    public class ScannedFile
    {
        public int ScannedFileId { get; set; }
        public int ScanId { get; set; }
        public string FilePath { get; set; }
        public string FileExtension { get; set; }
        public int LinesCount { get; set; }
        public int IssuesCount { get; set; }

        public Scan Scan { get; set; }
    }
}