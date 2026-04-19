using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Tests.Analysis
{
    public class MagicNumbersRuleTests
    {
        [Fact]
        public void Analyze_ShouldDetectMagicNumber()
        {
            string code = @"
                class Test 
                {
                    void Method() 
                    {
                        int x = 42;
                    }
                }";

            List<AnalysisIssue> issues = TestHelpers.AnalyzeCode<MagicNumbersRule>(code);

            issues.Should().ContainSingle(i => i.Code == "MAG001");
            issues.First().Description.Should().Contain("42");
        }

        [Fact]
        public void Analyze_ShouldNotDetectAllowedNumbers()
        {
            string code = @"
                class Test 
                {
                    void Method() 
                    {
                        int a = 0;
                        int b = 1;
                        int c = -1;
                        double d = 0.0;
                        double e = 1.0;
                    }
                }";

            List<AnalysisIssue> issues = TestHelpers.AnalyzeCode<MagicNumbersRule>(code);

            issues.Should().BeEmpty();
        }

        [Fact]
        public void Analyze_ShouldIgnoreConstDeclarations()
        {
            string code = @"
                class Test 
                {
                    private const int Max = 100;
                    void Method() 
                    {
                        int x = Max;
                    }
                }";

            List<AnalysisIssue> issues = TestHelpers.AnalyzeCode<MagicNumbersRule>(code);

            issues.Should().BeEmpty();
        }
    }
}