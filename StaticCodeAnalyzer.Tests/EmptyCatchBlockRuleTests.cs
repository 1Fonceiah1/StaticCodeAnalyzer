using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Tests.Analysis
{
    public class EmptyCatchBlockRuleTests
    {
        [Fact]
        public async Task AnalyzeAsync_ShouldDetectEmptyCatch()
        {
            string code = @"
                class Test 
                {
                    void Method() 
                    {
                        try 
                        {
                            int.Parse(""a"");
                        }
                        catch (Exception)
                        {
                        }
                    }
                }";

            List<AnalysisIssue> issues = await TestHelpers.AnalyzeCodeAsync<EmptyCatchBlockRule>(code);

            issues.Should().ContainSingle(i => i.Code == "ERR001");
            issues.First().Severity.Should().Be("Высокий");
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldNotDetectCatchWithContent()
        {
            string code = @"
                class Test 
                {
                    void Method() 
                    {
                        try 
                        {
                            int.Parse(""a"");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }";

            List<AnalysisIssue> issues = await TestHelpers.AnalyzeCodeAsync<EmptyCatchBlockRule>(code);

            issues.Should().BeEmpty();
        }
    }
}