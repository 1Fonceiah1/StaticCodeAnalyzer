using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis.Refactoring;

namespace StaticCodeAnalyzer.Tests.Refactoring
{
    // Тесты для правила рефакторинга RefactoringRule_EmptyCatchBlock
    public class RefactoringRule_EmptyCatchBlockTests
    {
        [Fact]
        public void Apply_ShouldAddCommentAndThrowToEmptyCatch()
        {
            // Подготавливает код с пустым блоком catch
            string input = @"
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

            // Применяет рефакторинг
            string result = TestHelpers.ApplyRefactoring<RefactoringRule_EmptyCatchBlock>(input);

            // Проверяет, что добавлен комментарий TODO и оператор throw
            result.Should().Contain("// TODO:");
            result.Should().Contain("throw;");
            result.Should().NotContain("catch (Exception)\n        {\n        }");
        }
    }
}