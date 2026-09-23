using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RulesCore.Infrastructure.Sources;

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

            // Page numbers are only unique within one document. A package can contain
            // multiple PDF representations (for example a GitHub tree), so scope native
            // page identity to the stable artifact identity rather than content bytes.
            // This keeps refreshes of the same artifact stable while preventing page 1
            // from one PDF from aliasing page 1 of another PDF in the same package.
            var documentIdentity = CanonicalSourceIdentity.Fingerprint(
                $"{artifact.OriginIdentity}\n{artifact.FileName}")[..24];
            var records = pages.Select(page => new NormalizedSourceRecord(
                EntityType: "source-fragment",
                Name: $"Page {page.Number}",
                SourceCode: localKey,
                NativeKey: $"source-fragment|{documentIdentity}|page-{page.Number}",
                RawJson: JsonSerializer.Serialize(new
                {
                    kind = "source-fragment",
                    name = $"Page {page.Number}",
                    page = page.Number,
                    text = page.Text,
                    extraction = "pdf-text-layer"
                }),
                LocatorKey: $"page:{page.Number}",
                PublicationLocalKey: localKey,
                NativeIdentityJson: JsonSerializer.Serialize(new
                {
                    documentIdentity,
                    page = page.Number,
                    extraction = "pdf-text-layer"
                }))).ToArray();

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
                records,
                [new NormalizedSourcePublication(
                    localKey,
                    title,
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
