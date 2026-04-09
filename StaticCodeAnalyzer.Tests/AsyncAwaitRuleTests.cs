using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis;

namespace StaticCodeAnalyzer.Tests.Analysis
{
    public class AsyncAwaitRuleTests
    {
        [Fact]
        public async Task AnalyzeAsync_ShouldDetectAsyncWithoutAwait()
        {
            // Arrange
            var code = @"
                class Test 
                {
                    public async Task Method() 
                    {
                        int x = 5;
                    }
                }";

            // Act
            var issues = await TestHelpers.AnalyzeCodeAsync<AsyncAwaitRule>(code);

            // Assert
            issues.Should().ContainSingle(i => i.Code == "ASY001");
            issues.First().Description.Should().Contain("Method");
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldNotDetectAsyncWithAwait()
        {
            // Arrange
            var code = @"
                class Test 
                {
                    public async Task Method() 
                    {
                        await Task.Delay(100);
                    }
                }";

            // Act
            var issues = await TestHelpers.AnalyzeCodeAsync<AsyncAwaitRule>(code);

            // Assert
            issues.Should().BeEmpty();
        }
    }
}