using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Tests;

namespace StaticCodeAnalyzer.Tests.Integration
{
    public class CrossFileAnalysisTests
    {
        [Fact]
        public async Task AnalyzeProject_ShouldResolveSymbolsAcrossFiles()
        {
            // Arrange
            var code1 = @"
                namespace TestNamespace
                {
                    public class ClassA
                    {
                        public int GetValue() => 42;
                    }
                }";
            var code2 = @"
                namespace TestNamespace
                {
                    public class ClassB
                    {
                        public void UseClassA()
                        {
                            var a = new ClassA();
                            int x = a.GetValue();
                        }
                    }
                }";
            var files = new[] { ("file1.cs", code1), ("file2.cs", code2) };
            var context = await TestHelpers.CreateTestProjectContextAsync(files);

            var engine = new AnalyzerEngine();
            var issues = await engine.AnalyzeProjectAsync(context);

            // Assert: нет ошибок "undefined identifier" для ClassA и GetValue
            issues.Should().NotContain(i => i.Code == "UND001" && i.Description.Contains("ClassA"));
            issues.Should().NotContain(i => i.Code == "UND001" && i.Description.Contains("GetValue"));
        }
    }
}