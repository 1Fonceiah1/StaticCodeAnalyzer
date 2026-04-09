using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis.Refactoring;

namespace StaticCodeAnalyzer.Tests.Refactoring
{
    public class RefactoringRule_MagicNumbersTests
    {
        [Fact]
        public async Task ApplyAsync_ShouldReplaceMagicNumberWithConst()
        {
            // Arrange
            var input = @"
                class Test 
                {
                    void Method() 
                    {
                        int x = 42;
                        int y = 42;
                    }
                }";

            // Act
            var result = await TestHelpers.ApplyRefactoringAsync<RefactoringRule_MagicNumbers>(input);

            // Assert
            result.Should().Contain("private const int");
            result.Should().Contain("Const_42");
            // Проверяем, что использования заменены на имя константы
            result.Should().Contain("int x = Const_42;");
            result.Should().Contain("int y = Const_42;");
            // Проверяем, что константа инициализируется исходным значением
            result.Should().Contain("Const_42 = 42;");
        }

        [Fact]
        public async Task ApplyAsync_ShouldDetectCorrectTypeForDouble()
        {
            // Arrange
            var input = @"
                class Test 
                {
                    void Method() 
                    {
                        double x = 3.14;
                    }
                }";

            // Act
            var result = await TestHelpers.ApplyRefactoringAsync<RefactoringRule_MagicNumbers>(input);

            // Assert
            result.Should().Contain("private const double");
            result.Should().Contain("Const_3_14");
            result.Should().Contain("= 3.14");
        }

        [Fact]
        public async Task ApplyAsync_ShouldNotReplaceAllowedNumbers()
        {
            // Arrange
            var input = @"
                class Test 
                {
                    void Method() 
                    {
                        int a = 0;
                        int b = 1;
                    }
                }";

            // Act
            var result = await TestHelpers.ApplyRefactoringAsync<RefactoringRule_MagicNumbers>(input);

            // Assert
            result.Should().NotContain("private const");
            result.Should().Contain("= 0;");
            result.Should().Contain("= 1;");
        }
    }
}