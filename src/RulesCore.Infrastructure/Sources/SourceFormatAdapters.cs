using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RulesCore.Infrastructure.Sources;

public sealed class SourceFormatAdapterRegistry(IEnumerable<ISourceFormatAdapter> adapters)
    : ISourceFormatAdapterRegistry
{
    private readonly ISourceFormatAdapter[] adapters = adapters.ToArray();

    public bool IsCandidateFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && adapters.Any(adapter => adapter.IsCandidate(fileName, ReadOnlySpan<byte>.Empty));

    public NormalizedSourceRepresentation? TryRead(SourceRepresentationArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        foreach (var adapter in adapters)
        {
            if (!adapter.IsCandidate(artifact.FileName, artifact.Content))
            {
                continue;
            }

            var result = adapter.TryRead(artifact);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }
}

public sealed class FiveEToolsSourceFormatAdapter : ISourceFormatAdapter
{
    public const string Format = "5etools-json";
    public string FormatKey => Format;

    public bool IsCandidate(string? fileName, ReadOnlySpan<byte> content) =>
        string.Equals(Path.GetExtension(fileName ?? string.Empty), ".json", StringComparison.OrdinalIgnoreCase);

    public NormalizedSourceRepresentation? TryRead(SourceRepresentationArtifact artifact)
    {
        if (!IsCandidate(artifact.FileName, artifact.Content) || artifact.Content.Length == 0)
        {
            return null;
        }

        string json;
        try
        {
            json = new UTF8Encoding(false, true).GetString(artifact.Content);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        IReadOnlyList<string> sourceCodes;
        try
        {
            sourceCodes = FiveEToolsDocumentInspector.DiscoverSourceCodes(
                json,
                FiveEToolsDocumentInspector.AccountSourceFallbackCode);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentException)
        {
            return null;
        }
        if (sourceCodes.Count == 0)
        {
            return null;
        }

        var publications = new List<NormalizedSourcePublication>();
        foreach (var sourceCode in sourceCodes)
        {
            var filtered = FiveEToolsDocumentInspector.FilterBySourceCodes(
                json,
                FiveEToolsDocumentInspector.AccountSourceFallbackCode,
                [sourceCode],
                out var selectedEntityCount);
            if (selectedEntityCount == 0)
            {
                continue;
            }

            using var document = JsonDocument.Parse(filtered);
            var records = ReadRecords(document.RootElement, sourceCode);
            if (records.Count == 0)
            {
                continue;
            }

            var publication = ReadPublicationEvidence(document.RootElement, sourceCode);
            publications.Add(new NormalizedSourcePublication(
                LocalKey: sourceCode,
                DisplayName: publication.Title ?? sourceCode,
                Records: records,
                Publisher: publication.Publisher,
                GameEdition: null,
                PublicationDate: publication.PublicationDate,
                ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["5etools-source-code"] = sourceCode
                }));
        }

        return publications.Count == 0
            ? null
            : new NormalizedSourceRepresentation(Format, artifact, publications);
    }

    private static IReadOnlyList<NormalizedSourceRecord> ReadRecords(JsonElement root, string sourceCode)
    {
        var records = new List<NormalizedSourceRecord>();
        var naturalKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!FiveEToolsDocumentInspector.IsImportableArray(property))
            {
                continue;
            }

            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("name", out var nameValue)
                    || nameValue.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(nameValue.GetString()))
                {
                    continue;
                }

                var name = nameValue.GetString()!.Trim();
                var identitySuffix = ReadIdentitySuffix(item);
                var naturalKey = NormalizeKey($"{property.Name}|{sourceCode}|{name}|{identitySuffix}");
                if (!naturalKeys.Add(naturalKey))
                {
                    continue;
                }

                records.Add(new NormalizedSourceRecord(
                    property.Name,
                    name,
                    sourceCode,
                    naturalKey,
                    item.GetRawText(),
                    ReadPageLocator(item)));
            }
        }
        return records;
    }

    private static (string? Title, string? Publisher, DateOnly? PublicationDate) ReadPublicationEvidence(
        JsonElement root,
        string sourceCode)
    {
        foreach (var type in new[] { "book", "adventure" })
        {
            if (!root.TryGetProperty(type, out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in values.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                var itemSource = FiveEToolsDocumentInspector.GetSourceCode(item, sourceCode);
                if (!string.Equals(itemSource, sourceCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 5e.tools can model a child adventure as its own navigation entry while the
                // actual printed source remains the parent book. FRAiF-TLLoL, for example, has
                // source FRAiF and parentSource FRAiF. Its title is not bibliographic evidence
                // that the FRAiF publication itself is named "The Lost Library of Lethchauntos".
                var itemId = ReadString(item, "id");
                var parentSource = ReadString(item, "parentSource");
                if (!string.IsNullOrWhiteSpace(parentSource)
                    || (!string.IsNullOrWhiteSpace(itemId)
                        && !string.Equals(itemId, sourceCode, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var title = ReadString(item, "name");
                var publisher = ReadString(item, "publisher");
                DateOnly? date = null;
                var published = ReadString(item, "published");
                if (published is not null
                    && DateOnly.TryParseExact(
                        published,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsed))
                {
                    date = parsed;
                }
                return (title, publisher, date);
            }
        }
        return (null, null, null);
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static string ReadIdentitySuffix(JsonElement item)
    {
        foreach (var propertyName in new[] { "uniqueId", "id" })
        {
            if (!item.TryGetProperty(propertyName, out var value))
            {
                continue;
            }
            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString()!.Trim();
            }
            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetRawText();
            }
        }
        return string.Empty;
    }

    private static string? ReadPageLocator(JsonElement item)
    {
        if (!item.TryGetProperty("page", out var page))
        {
            return null;
        }
        return page.ValueKind switch
        {
            JsonValueKind.Number => $"page:{page.GetRawText()}",
            JsonValueKind.String when !string.IsNullOrWhiteSpace(page.GetString()) => $"page:{page.GetString()!.Trim()}",
            _ => null
        };
    }

    private static string NormalizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        var separator = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (separator && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(character);
                separator = false;
            }
            else
            {
                separator = true;
            }
        }
        return builder.ToString();
    }
}

public sealed partial class PdfSourceFormatAdapter : ISourceFormatAdapter
{
    public const string Format = "pdf";
    public string FormatKey => Format;

    public bool IsCandidate(string? fileName, ReadOnlySpan<byte> content)
    {
        if (string.Equals(Path.GetExtension(fileName ?? string.Empty), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return content.Length >= 5
            && content[0] == (byte)'%'
            && content[1] == (byte)'P'
            && content[2] == (byte)'D'
            && content[3] == (byte)'F'
            && content[4] == (byte)'-';
    }

    public NormalizedSourceRepresentation? TryRead(SourceRepresentationArtifact artifact)
    {
        if (!IsCandidate(artifact.FileName, artifact.Content) || artifact.Content.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = PdfDocument.Open(artifact.Content);
            var pages = new List<(int Number, string Text)>();
            foreach (var page in document.GetPages())
            {
                var text = ContentOrderTextExtractor.GetText(page)?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    pages.Add((page.Number, text));
                }
            }
            if (pages.Count == 0)
            {
                return null;
            }

            var frontMatter = string.Join('\n', pages.Take(8).Select(value => value.Text));
            var title = Normalize(document.Information.Title)
                ?? FindLabeledValue(frontMatter, "Title")
                ?? Path.GetFileNameWithoutExtension(artifact.FileName).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "Uploaded PDF";
            }

            var publisher = FindLabeledValue(frontMatter, "Publisher");
            var gameEdition = FindLabeledValue(frontMatter, "Game Edition")
                ?? FindLabeledValue(frontMatter, "System");
            var publicationDate = ParsePublicationDate(FindLabeledValue(frontMatter, "Publication Date"));
            var identifiers = ReadIdentifiers(frontMatter);
            var localKey = BuildLocalKey(title, identifiers, artifact.Content);

            var records = pages.Select(page => new NormalizedSourceRecord(
                EntityType: "source-fragment",
                Name: $"Page {page.Number}",
                SourceCode: localKey,
                NaturalKey: $"source-fragment|page-{page.Number}",
                RawJson: JsonSerializer.Serialize(new
                {
                    kind = "source-fragment",
                    name = $"Page {page.Number}",
                    page = page.Number,
                    text = page.Text,
                    extraction = "pdf-text-layer"
                }),
                LocatorKey: $"page:{page.Number}"))
                .ToArray();

            var metadataJson = JsonSerializer.Serialize(new
            {
                pdfTitle = Normalize(document.Information.Title),
                pdfAuthor = Normalize(document.Information.Author),
                extractedPages = pages.Count,
                totalPages = document.NumberOfPages,
                extraction = "pdf-text-layer"
            });

            return new NormalizedSourceRepresentation(
                Format,
                artifact,
                [new NormalizedSourcePublication(
                    localKey,
                    title,
                    records,
                    publisher,
                    gameEdition,
                    publicationDate,
                    identifiers)],
                metadataJson);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException
            and not OperationCanceledException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string> ReadIdentifiers(string text)
    {
        var identifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var match = IsbnRegex().Match(text);
        if (match.Success)
        {
            var normalized = new string(match.Groups[1].Value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (normalized.Length is 10 or 13)
            {
                identifiers["isbn"] = normalized;
            }
        }
        return identifiers;
    }

    private static string BuildLocalKey(
        string title,
        IReadOnlyDictionary<string, string> identifiers,
        byte[] content)
    {
        if (identifiers.TryGetValue("isbn", out var isbn))
        {
            return $"pdf-isbn-{isbn.ToLowerInvariant()}";
        }
        var titleKey = CanonicalSourceIdentity.NormalizeIdentityPart(title);
        var hash = CanonicalSourceIdentity.Fingerprint(Convert.ToHexString(content));
        return $"pdf-{(string.IsNullOrEmpty(titleKey) ? "publication" : titleKey)}-{hash[..12]}";
    }

    private static string? FindLabeledValue(string text, string label)
    {
        var match = Regex.Match(
            text,
            $@"(?im)^\s*{Regex.Escape(label)}\s*:\s*(?<value>[^\r\n]+?)\s*$",
            RegexOptions.CultureInvariant);
        return match.Success ? Normalize(match.Groups["value"].Value) : null;
    }

    private static DateOnly? ParsePublicationDate(string? value)
    {
        if (value is null)
        {
            return null;
        }
        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date)
            ? date
            : null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"(?im)\bISBN(?:-1[03])?\s*:?\s*([0-9Xx][0-9Xx\-\s]{8,20}[0-9Xx])\b", RegexOptions.CultureInvariant)]
    private static partial Regex IsbnRegex();
}
