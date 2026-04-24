using CommandLine;

namespace StaticCodeAnalyzer.Cli
{
    // Опции командной строки для CLI режима анализатора
    public class CliOptions
    {
        [Option('a', "analyze", Required = true, HelpText = "Путь к файлу или директории для анализа")]
        public string? Path { get; set; }

        [Option('o', "output", Required = false, Default = "json", HelpText = "Формат вывода: json, console")]
        public string? OutputFormat { get; set; }

        [Option('r', "recursive", Required = false, Default = true, HelpText = "Рекурсивный анализ директории")]
        public bool Recursive { get; set; }

        [Option('s', "severity", Required = false, Default = null, HelpText = "Фильтр по серьезности: Critical, High, Medium, Low")]
        public string? SeverityFilter { get; set; }

        [Option('c', "config", Required = false, HelpText = "Путь к конфигурационному файлу")]
        public string? ConfigPath { get; set; }
    }
}
