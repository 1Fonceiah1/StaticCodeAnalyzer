using System;
using System.IO;
using System.Threading;

namespace StaticCodeAnalyzer.Services
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly int _maxDaysToKeep = 30;

        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        static Logger()
        {
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
            CleanOldLogs();
        }

        public static void Log(string action, string details = null, LogLevel level = LogLevel.Info)
        {
            try
            {
                string logFileName = $"log_{DateTime.Now:yyyy-MM-dd}.log";
                string logFilePath = Path.Combine(_logDirectory, logFileName);

                string userName = Environment.UserName;
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logEntry = $"[{timestamp}] {level} | Пользователь: {userName} | Действие: {action}";

                if (!string.IsNullOrWhiteSpace(details))
                    logEntry += $" | {details}";

                lock (_lock)
                {
                    File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка логирования: {ex.Message}");
            }
        }

        private static void CleanOldLogs()
        {
            try
            {
                var files = Directory.GetFiles(_logDirectory, "log_*.log");
                DateTime cutoff = DateTime.Now.AddDays(-_maxDaysToKeep);

                foreach (string file in files)
                {
                    if (File.GetCreationTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка очистки логов: {ex.Message}");
            }
        }
    }
}