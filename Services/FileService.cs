using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Services
{
    // Вспомогательный сервис для работы с файлами
    public static class FileService
    {
        // Асинхронно читает содержимое файла
        public static async Task<string> ReadFileAsync(string path)
        {
            return await File.ReadAllTextAsync(path);
        }

        // Возвращает список всех .cs-файлов в директории (включая поддиректории)
        public static IEnumerable<string> GetCsFiles(string directory)
        {
            return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
        }
    }
}