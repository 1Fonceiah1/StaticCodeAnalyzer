using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Services
{
    public static class FileService
    {
        public static async Task<string> ReadFileAsync(string path)
        {
            return await File.ReadAllTextAsync(path);
        }

        public static IEnumerable<string> GetCsFiles(string directory)
        {
            return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
        }
    }
}