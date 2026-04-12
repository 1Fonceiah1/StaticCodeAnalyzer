using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Tests.Integration
{
    public class CrossFileAnalysisTests
    {
        [Fact]
        public async Task AnalyzeProject_ShouldResolveSymbolsAcrossFiles()
        {
            string code1 = @"
                namespace TestNamespace
                {
                    public class ClassA
                    {
                        public int GetValue() => 42;
                    }
                }";
            string code2 = @"
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
            ProjectContext context = await TestHelpers.CreateTestProjectContextAsync(files);

            AnalyzerEngine engine = new AnalyzerEngine();
            List<AnalysisIssue> issues = await engine.AnalyzeProjectAsync(context);

            issues.Should().NotContain(i => i.Code == "UND001" && i.Description.Contains("ClassA"));
            issues.Should().NotContain(i => i.Code == "UND001" && i.Description.Contains("GetValue"));
        }
    }
}