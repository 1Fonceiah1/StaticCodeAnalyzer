using FluentAssertions;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Models;

namespace StaticCodeAnalyzer.Tests.Integration
{
    public class AnalyzerEngineIntegrationTests
    {
        [Fact]
        public void AnalyzeFile_ShouldDetectMultipleIssues()
        {
            string code = @"
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

            string tempFile = Path.GetTempFileName() + ".cs";
            File.WriteAllText(tempFile, code);

            AnalyzerEngine engine = new AnalyzerEngine();
            List<AnalysisIssue> issues = engine.AnalyzeFile(tempFile);

            issues.Should().Contain(i => i.Code == "ENC001");
            issues.Should().Contain(i => i.Code == "ASY001");
            issues.Should().Contain(i => i.Code == "MAG001");
            issues.Should().Contain(i => i.Code == "ERR001");
            issues.Should().NotBeEmpty();

            File.Delete(tempFile);
        }
    }
}