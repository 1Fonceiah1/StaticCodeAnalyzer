using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis.Refactoring;

namespace StaticCodeAnalyzer.Tests.Refactoring
{
    public class RefactoringRule_AsyncAwaitTests
    {
        [Fact]
        public async Task ApplyAsync_ShouldReplaceThreadSleepWithTaskDelay()
        {
            // Arrange
            var input = @"
                using System.Threading;
                class Test 
                {
                    void Method() 
                    {
                        Thread.Sleep(1000);
                    }
                }";

            // Act
            var result = await TestHelpers.ApplyRefactoringAsync<RefactoringRule_AsyncAwait>(input);

            // Assert
            result.Should().Contain("await Task.Delay(1000)");
            result.Should().Contain("async Task");
            result.Should().Contain("using System.Threading.Tasks");
        }

        [Fact]
        public async Task ApplyAsync_ShouldRemoveUselessAsync()
        {
            // Arrange
            var input = @"
                class Test 
                {
                    public async Task Method() 
                    {
                        int x = 5;
                    }
                }";

            // Act
            var result = await TestHelpers.ApplyRefactoringAsync<RefactoringRule_AsyncAwait>(input);

            // Assert
            result.Should().NotContain("async Task");
            result.Should().Contain("public Task Method()");
        }
    }
}