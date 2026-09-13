using System.Text;
using System.Text.Json;

namespace RulesCore.Application.Sources;

public sealed record CompatibleCurrentUserSourceDocument(
    string FormatKey,
    string ImportDocument,
    IReadOnlyList<string> SourceCodes);

public static class CurrentUserSourceCompatibility
{
    public const string FiveEToolsJsonFormat = "5etools-json";

    private static readonly string[] SupportedFileExtensions = [".json"];

    public static bool IsCandidateFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName.Trim());
        return SupportedFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryRead(
        string? fileName,
        ReadOnlySpan<byte> content,
        out CompatibleCurrentUserSourceDocument? document)
    {
        document = null;
        if (!IsCandidateFileName(fileName) || content.IsEmpty)
        {
            return false;
        }

        try
        {
            var text = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(content);
            return TryRead(fileName, text, out document);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    public static bool TryRead(
        string? fileName,
        string? content,
        out CompatibleCurrentUserSourceDocument? document)
    {
        document = null;
        if (!IsCandidateFileName(fileName) || string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            var sourceCodes = FiveEToolsDocumentInspector.DiscoverSourceCodes(
                content,
                FiveEToolsDocumentInspector.AccountSourceFallbackCode);
            if (sourceCodes.Count == 0)
            {
                return false;
            }

            document = new CompatibleCurrentUserSourceDocument(
                FiveEToolsJsonFormat,
                content,
                sourceCodes);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
