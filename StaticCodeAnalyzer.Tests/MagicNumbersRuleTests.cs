using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis;

namespace StaticCodeAnalyzer.Tests.Analysis
{
    public class MagicNumbersRuleTests
    {
        [Fact]
        public async Task AnalyzeAsync_ShouldDetectMagicNumber()
        {
            // Arrange
            var code = @"
                class Test 
                {
                    void Method() 
                    {
                        int x = 42;
                    }
                }";

            // Act
            var issues = await TestHelpers.AnalyzeCodeAsync<MagicNumbersRule>(code);

            // Assert
            issues.Should().ContainSingle(i => i.Code == "MAG001");
            issues.First().Description.Should().Contain("42");
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldNotDetectAllowedNumbers()
        {
            // Arrange
            var code = @"
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

            // Act
            var issues = await TestHelpers.AnalyzeCodeAsync<MagicNumbersRule>(code);

            // Assert
            issues.Should().BeEmpty();
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldIgnoreConstDeclarations()
        {
            // Arrange
            var code = @"
                class Test 
                {
                    private const int Max = 100;
                    void Method() 
                    {
                        int x = Max;
                    }
                }";

            // Act
            var issues = await TestHelpers.AnalyzeCodeAsync<MagicNumbersRule>(code);

            // Assert
            issues.Should().BeEmpty();
        }
    }
}