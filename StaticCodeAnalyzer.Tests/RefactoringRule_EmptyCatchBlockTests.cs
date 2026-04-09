using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis.Refactoring;

namespace StaticCodeAnalyzer.Tests.Refactoring
{
    public class RefactoringRule_EmptyCatchBlockTests
    {
        [Fact]
        public async Task ApplyAsync_ShouldAddCommentAndThrowToEmptyCatch()
        {
            // Arrange
            var input = @"
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
            var result = await TestHelpers.ApplyRefactoringAsync<RefactoringRule_EmptyCatchBlock>(input);

            // Assert
            result.Should().Contain("// TODO:");
            result.Should().Contain("throw;");
            result.Should().NotContain("catch (Exception)\n        {\n        }");
        }
    }
}