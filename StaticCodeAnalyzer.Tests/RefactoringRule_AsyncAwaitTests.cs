using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis.Refactoring;

namespace StaticCodeAnalyzer.Tests.Refactoring
{
    // Тесты для правила рефакторинга RefactoringRule_AsyncAwait
    public class RefactoringRule_AsyncAwaitTests
    {
        [Fact]
        public async Task ApplyAsync_ShouldReplaceThreadSleepWithTaskDelay()
        {
            // Подготавливает код с Thread.Sleep
            string input = @"
                using System.Threading;
                class Test 
                {
                    void Method() 
                    {
                        Thread.Sleep(1000);
                    }
                }";

            // Применяет рефакторинг
            string result = await TestHelpers.ApplyRefactoringAsync<RefactoringRule_AsyncAwait>(input);

            // Проверяет, что Thread.Sleep заменён на await Task.Delay, добавлены async Task и using
            result.Should().Contain("await Task.Delay(1000)");
            result.Should().Contain("async Task");
            result.Should().Contain("using System.Threading.Tasks");
        }

        [Fact]
        public async Task ApplyAsync_ShouldRemoveUselessAsync()
        {
            // Подготавливает код с async-методом без await
            string input = @"
                class Test 
                {
                    public async Task Method() 
                    {
                        int x = 5;
                    }
                }";

            // Применяет рефакторинг
            string result = await TestHelpers.ApplyRefactoringAsync<RefactoringRule_AsyncAwait>(input);

            // Проверяет, что модификатор async удалён, а Task остался
            result.Should().NotContain("async Task");
            result.Should().Contain("public Task Method()");
        }
    }
}