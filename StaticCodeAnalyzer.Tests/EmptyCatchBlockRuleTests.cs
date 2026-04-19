using FluentAssertions;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Tests.Analysis
{
    public class EmptyCatchBlockRuleTests
    {
        [Fact]
        public void Analyze_ShouldDetectEmptyCatch()
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

            List<AnalysisIssue> issues = TestHelpers.AnalyzeCode<EmptyCatchBlockRule>(code);

            issues.Should().ContainSingle(i => i.Code == "ERR001");
            issues.First().Severity.Should().Be("Высокий");
        }

        [Fact]
        public void Analyze_ShouldNotDetectCatchWithContent()
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

            List<AnalysisIssue> issues = TestHelpers.AnalyzeCode<EmptyCatchBlockRule>(code);

            issues.Should().BeEmpty();
        }
    }
}