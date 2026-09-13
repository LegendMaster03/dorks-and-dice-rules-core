using RulesCore.Application.Sources;

namespace RulesCore.Tests;

public sealed class CurrentUserSourceCompatibilityTests
{
    [Fact]
    public void FiveEToolsEntityJsonIsCompatible()
    {
        const string content = """
            {
              "skill": [
                { "name": "Arcana", "source": "PHB", "ability": "int" }
              ]
            }
            """;

        var compatible = CurrentUserSourceCompatibility.TryRead(
            "skills.json",
            content,
            out var document);

        Assert.True(compatible);
        Assert.NotNull(document);
        Assert.Equal(CurrentUserSourceCompatibility.FiveEToolsJsonFormat, document.FormatKey);
        Assert.Contains("PHB", document.SourceCodes);
    }

    [Theory]
    [InlineData("notes.txt", "plain text")]
    [InlineData("notes.json", "plain text")]
    [InlineData("notes.json", "{ \"data\": [{ \"name\": \"Introduction\" }] }")]
    public void UnsupportedOrNonEntityFilesAreNotCompatible(string fileName, string content)
    {
        Assert.False(CurrentUserSourceCompatibility.TryRead(
            fileName,
            content,
            out var document));
        Assert.Null(document);
    }

    [Fact]
    public void SupportedContentWithUnsupportedFileTypeIsNotCompatible()
    {
        const string content = """
            {
              "skill": [
                { "name": "Arcana", "source": "PHB", "ability": "int" }
              ]
            }
            """;

        Assert.False(CurrentUserSourceCompatibility.TryRead(
            "skills.txt",
            content,
            out var document));
        Assert.Null(document);
    }
}
