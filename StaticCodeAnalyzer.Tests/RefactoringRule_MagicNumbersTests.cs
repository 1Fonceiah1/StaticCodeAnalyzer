using FluentAssertions;
using StaticCodeAnalyzer.Analysis.Refactoring;

namespace StaticCodeAnalyzer.Tests.Refactoring
{
    // Тесты для правила рефакторинга RefactoringRule_MagicNumbers
    public class RefactoringRule_MagicNumbersTests
    {
        [Fact]
        public void Apply_ShouldReplaceMagicNumberWithConst()
        {
            // Подготавливает код с повторяющимся магическим числом 42
            string input = @"
                class Test 
                {
                    void Method() 
                    {
                        int x = 42;
                        int y = 42;
                    }
                }";

            // Применяет рефакторинг
            string result = TestHelpers.ApplyRefactoring<RefactoringRule_MagicNumbers>(input);

            // Проверяет, что создана приватная константа и оба использования заменены
            result.Should().Contain("private const int");
            result.Should().Contain("Const_42");
            result.Should().Contain("int x = Const_42;");
            result.Should().Contain("int y = Const_42;");
            result.Should().Contain("Const_42 = 42;");
        }

        [Fact]
        public void Apply_ShouldDetectCorrectTypeForDouble()
        {
            // Подготавливает код с числом с плавающей точкой
            string input = @"
                class Test 
                {
                    void Method() 
                    {
                        double x = 3.14;
                    }
                }";

            // Применяет рефакторинг
            string result = TestHelpers.ApplyRefactoring<RefactoringRule_MagicNumbers>(input);

            // Проверяет, что создана константа типа double
            result.Should().Contain("private const double");
            result.Should().Contain("Const_3_14");
            result.Should().Contain("= 3.14");
        }

        [Fact]
        public void Apply_ShouldNotReplaceAllowedNumbers()
        {
            // Подготавливает код с разрешёнными числами 0 и 1
            string input = @"
                class Test 
                {
                    void Method() 
                    {
                        int a = 0;
                        int b = 1;
                    }
                }";

            // Применяет рефакторинг
            string result = TestHelpers.ApplyRefactoring<RefactoringRule_MagicNumbers>(input);

            // Проверяет, что константы не создавались
            result.Should().NotContain("private const");
            result.Should().Contain("= 0;");
            result.Should().Contain("= 1;");
        }
    }
}