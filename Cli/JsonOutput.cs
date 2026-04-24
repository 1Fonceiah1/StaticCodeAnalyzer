using StaticCodeAnalyzer.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StaticCodeAnalyzer.Cli
{
    // JSON модель для вывода результатов анализа
    public class JsonOutput
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("summary")]
        public SummaryInfo Summary { get; set; } = new SummaryInfo();

        [JsonPropertyName("files")]
        public List<FileInfo> Files { get; set; } = new List<FileInfo>();

        [JsonPropertyName("issues")]
        public List<IssueInfo> Issues { get; set; } = new List<IssueInfo>();

        [JsonPropertyName("errors")]
        public List<ErrorInfo> Errors { get; set; } = new List<ErrorInfo>();
    }

    public class SummaryInfo
    {
        [JsonPropertyName("totalFiles")]
        public int TotalFiles { get; set; }

        [JsonPropertyName("totalIssues")]
        public int TotalIssues { get; set; }

        [JsonPropertyName("criticalCount")]
        public int CriticalCount { get; set; }

        [JsonPropertyName("highCount")]
        public int HighCount { get; set; }

        [JsonPropertyName("mediumCount")]
        public int MediumCount { get; set; }

        [JsonPropertyName("lowCount")]
        public int LowCount { get; set; }
    }

    public class FileInfo
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("issuesCount")]
        public int IssuesCount { get; set; }
    }

    public class IssueInfo
    {
        [JsonPropertyName("filePath")]
        public string FilePath { get; set; }

        [JsonPropertyName("line")]
        public int Line { get; set; }

        [JsonPropertyName("column")]
        public int Column { get; set; }

        [JsonPropertyName("severity")]
        public string Severity { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("rule")]
        public string Rule { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("suggestion")]
        public string Suggestion { get; set; }

        [JsonPropertyName("containingType")]
        public string ContainingType { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; }

        public static IssueInfo FromAnalysisIssue(AnalysisIssue issue)
        {
            return new IssueInfo
            {
                FilePath = issue.FilePath,
                Line = issue.LineNumber,
                Column = issue.ColumnNumber,
                Severity = MapSeverity(issue.Severity),
                Type = issue.Type,
                Code = issue.Code,
                Rule = issue.RuleName,
                Message = issue.Description,
                Suggestion = issue.Suggestion,
                ContainingType = issue.ContainingTypeName,
                Method = issue.MethodName
            };
        }

        private static string MapSeverity(string severity)
        {
            return severity?.ToLower() switch
            {
                "критический" => "critical",
                "высокий" => "high",
                "средний" => "medium",
                "низкий" => "low",
                _ => severity?.ToLower() ?? "medium"
            };
        }
    }

    public class ErrorInfo
    {
        [JsonPropertyName("rule")]
        public string Rule { get; set; }

        [JsonPropertyName("file")]
        public string File { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
