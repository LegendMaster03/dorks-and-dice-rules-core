using System.Text;
using RulesCore.Application.Sources;

namespace RulesCore.Tests;

public sealed class CurrentUserSourceCompatibilityTests
{
    private const string CompatibleContent = """
        {
          "skill": [
            { "name": "Arcana", "source": "PHB", "ability": "int" }
          ]
        }
        """;

    [Fact]
    public void FiveEToolsEntityJsonIsCompatible()
    {
        var compatible = CurrentUserSourceCompatibility.TryRead(
            "skills.json",
            CompatibleContent,
            out var document);

        Assert.True(compatible);
        Assert.NotNull(document);
        Assert.Equal(CurrentUserSourceCompatibility.FiveEToolsJsonFormat, document.FormatKey);
        Assert.Contains("PHB", document.SourceCodes);
    }

    [Fact]
    public void FiveEToolsEntityJsonBytesAreCompatible()
    {
        var compatible = CurrentUserSourceCompatibility.TryRead(
            "skills.json",
            Encoding.UTF8.GetBytes(CompatibleContent),
            out var document);

        Assert.True(compatible);
        Assert.NotNull(document);
        Assert.Equal(CompatibleContent, document.ImportDocument);
        Assert.Contains("PHB", document.SourceCodes);
    }

    [Fact]
    public void InvalidUtf8BytesAreNotCompatible()
    {
        byte[] content = [0xFF, 0xFE, 0xFA, 0xFB];

        Assert.False(CurrentUserSourceCompatibility.TryRead(
            "skills.json",
            content,
            out var document));
        Assert.Null(document);
    }

    [Fact]
    public void Base64UploadRequestUsesCompatibilityBoundary()
    {
        var request = new AddCurrentUserSourceRequest
        {
            Kind = CurrentUserSourceKinds.Upload,
            FileName = "skills.json",
            ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(CompatibleContent))
        };

        Assert.Equal(CompatibleContent, request.Json);
    }

    [Fact]
    public void InvalidBase64UploadIsRejected()
    {
        var request = new AddCurrentUserSourceRequest
        {
            Kind = CurrentUserSourceKinds.Upload,
            FileName = "skills.json",
            ContentBase64 = "not-base64!"
        };

        Assert.Throws<InvalidDataException>(() => _ = request.Json);
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
        Assert.False(CurrentUserSourceCompatibility.TryRead(
            "skills.txt",
            CompatibleContent,
            out var document));
        Assert.Null(document);
    }
}
