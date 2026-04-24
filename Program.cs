using CommandLine;
using StaticCodeAnalyzer.Cli;
using System.Text.Json;

namespace StaticCodeAnalyzer
{
    // Точка входа для CLI режима анализатора
    // Запуск: StaticCodeAnalyzer.exe --analyze <path> --output json
    public class Program
    {
        public static void Main(string[] args)
        {
            // Если запущено без аргументов, запускаем WPF приложение
            if (args.Length == 0)
            {
                StartWpfApp();
                return;
            }

            // Парсим аргументы командной строки
            Parser.Default.ParseArguments<CliOptions>(args)
                .WithParsed(RunCliMode)
                .WithNotParsed(HandleParseError);
        }

        // Запускает WPF приложение
        private static void StartWpfApp()
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        // Выполняет CLI режим анализа
        private static void RunCliMode(CliOptions options)
        {
            try
            {
                var analyzer = new CliAnalyzer();
                var result = analyzer.Analyze(options);

                // Выводим результат
                if (options.OutputFormat.ToLower() == "json")
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
                }
                else
                {
                    PrintConsoleOutput(result);
                }

                // Выходной код 1 если есть критические ошибки
                if (result.Summary.CriticalCount > 0)
                {
                    Environment.ExitCode = 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Ошибка: {ex.Message}");
                Environment.ExitCode = 2;
            }
        }

        // Выводит результаты в консольном формате
        private static void PrintConsoleOutput(JsonOutput result)
        {
            Console.WriteLine("=== Static Code Analyzer Results ===");
            Console.WriteLine($"Files analyzed: {result.Summary.TotalFiles}");
            Console.WriteLine($"Total issues: {result.Summary.TotalIssues}");
            Console.WriteLine($"  Critical: {result.Summary.CriticalCount}");
            Console.WriteLine($"  High: {result.Summary.HighCount}");
            Console.WriteLine($"  Medium: {result.Summary.MediumCount}");
            Console.WriteLine($"  Low: {result.Summary.LowCount}");
            Console.WriteLine();

            if (result.Issues.Count > 0)
            {
                Console.WriteLine("Issues:");
                foreach (var issue in result.Issues)
                {
                    Console.WriteLine($"  [{issue.Severity.ToUpper()}] {issue.FilePath}:{issue.Line}:{issue.Column}");
                    Console.WriteLine($"    {issue.Rule}: {issue.Message}");
                    if (!string.IsNullOrEmpty(issue.Suggestion))
                    {
                        Console.WriteLine($"    Suggestion: {issue.Suggestion}");
                    }
                    Console.WriteLine();
                }
            }

            if (result.Errors.Count > 0)
            {
                Console.WriteLine("Errors during analysis:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  {error.Rule} on {error.File}: {error.Message}");
                }
            }
        }

        // Обрабатывает ошибки парсинга аргументов
        private static void HandleParseError(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                if (error is not HelpRequestedError && error is not VersionRequestedError)
                {
                    Console.Error.WriteLine($"Ошибка аргументов: {error}");
                }
            }
            Environment.ExitCode = 2;
        }
    }
}
