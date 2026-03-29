namespace StaticCodeAnalyzer.Models
{
    public class AnalysisIssue
    {
        public string Severity { get; set; }
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
        public string Type { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public string Suggestion { get; set; }
        public string RuleName { get; set; }
    }
}