using FluentAssertions;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Tests.Integration
{
    public class CrossFileAnalysisTests
    {
        [Fact]
        public void AnalyzeProject_ShouldResolveSymbolsAcrossFiles()
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
            ProjectContext context = TestHelpers.CreateTestProjectContext(files);

            AnalyzerEngine engine = new AnalyzerEngine();
            List<AnalysisIssue> issues = engine.AnalyzeProject(context);

            issues.Should().NotContain(i => i.Code == "UND001" && i.Description.Contains("ClassA"));
            issues.Should().NotContain(i => i.Code == "UND001" && i.Description.Contains("GetValue"));
        }
    }
}