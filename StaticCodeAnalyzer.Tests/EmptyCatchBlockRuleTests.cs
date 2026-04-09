using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis;

namespace StaticCodeAnalyzer.Tests.Analysis
{
    public class EmptyCatchBlockRuleTests
    {
        [Fact]
        public async Task AnalyzeAsync_ShouldDetectEmptyCatch()
        {
            // Arrange
            var code = @"
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

            // Act
            var issues = await TestHelpers.AnalyzeCodeAsync<EmptyCatchBlockRule>(code);

            // Assert
            issues.Should().ContainSingle(i => i.Code == "ERR001");
            issues.First().Severity.Should().Be("Высокий");
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldNotDetectCatchWithContent()
        {
            // Arrange
            var code = @"
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

            // Act
            var issues = await TestHelpers.AnalyzeCodeAsync<EmptyCatchBlockRule>(code);

            // Assert
            issues.Should().BeEmpty();
        }
    }
}