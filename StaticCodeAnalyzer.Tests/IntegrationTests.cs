// IntegrationTests.cs
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using StaticCodeAnalyzer.Analysis;

namespace StaticCodeAnalyzer.Tests.Integration
{
    public class AnalyzerEngineIntegrationTests
    {
        [Fact]
        public async Task AnalyzeFileAsync_ShouldDetectMultipleIssues()
        {
            // Arrange
            var code = @"
                class BadClass 
                {
                    public int PublicField;
                    
                    public async Task UselessAsync() 
                    {
                        int x = 5;
                    }
                    
                    public void Method() 
                    {
                        int magic = 100;
                        try 
                        {
                            int.Parse(""a"");
                        }
                        catch (Exception) 
                        {
                        }
                    }
                }";

            var tempFile = Path.GetTempFileName() + ".cs";
            await File.WriteAllTextAsync(tempFile, code);

            // Act
            var engine = new AnalyzerEngine();
            var issues = await engine.AnalyzeFileAsync(tempFile);

            // Assert - проверяет наличие ключевых проблем, а не точное количество
            issues.Should().Contain(i => i.Code == "ENC001");  // PublicField
            issues.Should().Contain(i => i.Code == "ASY001");  // UselessAsync без await
            issues.Should().Contain(i => i.Code == "MAG001");  // magic = 100
            issues.Should().Contain(i => i.Code == "ERR001");  // пустой catch
            
            // Дополнительно: проверяет, что анализ вообще что-то нашёл
            issues.Should().NotBeEmpty();

            File.Delete(tempFile);
        }
    }
}