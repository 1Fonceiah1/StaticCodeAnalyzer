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

        static Logger()
        {
            // Создаём папку для логов, если её нет
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);

            // Удаляем старые файлы логов
            CleanOldLogs();
        }

        /// <summary>
        /// Записывает действие пользователя в лог-файл.
        /// </summary>
        /// <param name="action">Описание действия (например, "OpenFile", "Analyze")</param>
        /// <param name="details">Дополнительные детали (путь, количество найденных проблем и т.п.)</param>
        public static void Log(string action, string details = null)
        {
            try
            {
                string logFileName = $"log_{DateTime.Now:yyyy-MM-dd}.log";
                string logFilePath = Path.Combine(_logDirectory, logFileName);

                string userName = Environment.UserName;
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logEntry = $"[{timestamp}] Пользователь: {userName} | Действие: {action}";

                if (!string.IsNullOrWhiteSpace(details))
                    logEntry += $" | {details}";

                lock (_lock)
                {
                    File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // Нельзя допустить, чтобы ошибка логирования нарушила работу приложения
                System.Diagnostics.Debug.WriteLine($"Ошибка логирования: {ex.Message}");
            }
        }

        /// <summary>
        /// Удаляет файлы логов, созданные более <see cref="_maxDaysToKeep"/> дней назад.
        /// </summary>
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