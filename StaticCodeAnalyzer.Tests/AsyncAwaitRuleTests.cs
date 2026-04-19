using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Tests.Analysis
{
    public class AsyncAwaitRuleTests
    {
        [Fact]
        public void Analyze_ShouldDetectAsyncWithoutAwait()
        {
            string code = @"
                class Test 
                {
                    public async Task Method() 
                    {
                        int x = 5;
                    }
                }";

            List<AnalysisIssue> issues = TestHelpers.AnalyzeCode<AsyncAwaitRule>(code);

            issues.Should().ContainSingle(i => i.Code == "ASY001");
            issues.First().Description.Should().Contain("Method");
        }

        [Fact]
        public void Analyze_ShouldNotDetectAsyncWithAwait()
        {
            string code = @"
                class Test 
                {
                    public async Task Method() 
                    {
                        await Task.Delay(100);
                    }
                }";

            List<AnalysisIssue> issues = TestHelpers.AnalyzeCode<AsyncAwaitRule>(code);

            issues.Should().BeEmpty();
        }
    }
}