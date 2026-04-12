namespace StaticCodeAnalyzer.Models
{
    // Модель одной проблемы, найденной анализатором
    public class AnalysisIssue
    {
        public string Severity { get; set; }         // Важность (Критический, Высокий, Средний, Низкий)
        public string FilePath { get; set; }         // Путь к файлу
        public int LineNumber { get; set; }          // Номер строки
        public int ColumnNumber { get; set; }        // Номер колонки
        public string Type { get; set; }             // Тип (ошибка, предупреждение, запах кода)
        public string Code { get; set; }             // Код правила
        public string Description { get; set; }      // Описание проблемы
        public string Suggestion { get; set; }       // Предложение по исправлению
        public string RuleName { get; set; }         // Имя правила
        public string ContainingTypeName { get; set; } // Имя содержащего типа (класса)
        public string MethodName { get; set; }       // Имя метода (если применимо)
    }
}