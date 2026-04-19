namespace StaticCodeAnalyzer.Services
{
    // Вспомогательный сервис для работы с файлами
    public static class FileService
    {
        // Читает содержимое файла
        public static string ReadFile(string path)
        {
            return File.ReadAllText(path);
        }

        // Возвращает список всех .cs-файлов в директории (включая поддиректории)
        public static IEnumerable<string> GetCsFiles(string directory)
        {
            return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
        }
    }
}