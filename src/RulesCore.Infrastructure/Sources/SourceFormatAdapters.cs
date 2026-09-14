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

    public IReadOnlyList<NormalizedSourceRepresentation> TryReadMany(
        IReadOnlyList<SourceRepresentationArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        if (artifacts.Count == 0)
        {
            return [];
        }

        var resolved = new Dictionary<string, NormalizedSourceRepresentation>(StringComparer.Ordinal);
        foreach (var adapter in adapters)
        {
            var candidates = artifacts
                .Where(artifact => !resolved.ContainsKey(ArtifactKey(artifact)))
                .Where(artifact => adapter.IsCandidate(artifact.FileName, artifact.Content))
                .ToArray();
            if (candidates.Length == 0)
            {
                continue;
            }

            if (adapter is ISourceFormatBatchAdapter batchAdapter)
            {
                foreach (var representation in batchAdapter.TryReadMany(candidates))
                {
                    resolved[ArtifactKey(representation.Artifact)] = representation;
                }
                continue;
            }

            foreach (var artifact in candidates)
            {
                var representation = adapter.TryRead(artifact);
                if (representation is not null)
                {
                    resolved[ArtifactKey(artifact)] = representation;
                }
            }
        }

        return artifacts
            .Select(artifact => resolved.GetValueOrDefault(ArtifactKey(artifact)))
            .Where(representation => representation is not null)
            .Cast<NormalizedSourceRepresentation>()
            .ToArray();
    }

    private static string ArtifactKey(SourceRepresentationArtifact artifact) =>
        $"{artifact.OriginIdentity}\n{artifact.FileName}";
}

public sealed class FiveEToolsSourceFormatAdapter : ISourceFormatBatchAdapter
{
    public const string Format = "5etools-json";
    private static readonly DateOnly ModernRulesThreshold = new(2024, 9, 17);

    public string FormatKey => Format;

    public bool IsCandidate(string? fileName, ReadOnlySpan<byte> content) =>
        string.Equals(Path.GetExtension(fileName ?? string.Empty), ".json", StringComparison.OrdinalIgnoreCase);

    public NormalizedSourceRepresentation? TryRead(SourceRepresentationArtifact artifact) =>
        TryReadCore(artifact, catalog: null);

    public IReadOnlyList<NormalizedSourceRepresentation> TryReadMany(
        IReadOnlyList<SourceRepresentationArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        var catalog = CorpusCatalog.Build(artifacts);
        return artifacts
            .Select(artifact => TryReadCore(artifact, catalog))
            .Where(representation => representation is not null)
            .Cast<NormalizedSourceRepresentation>()
            .ToArray();
    }

    private static NormalizedSourceRepresentation? TryReadCore(
        SourceRepresentationArtifact artifact,
        CorpusCatalog? catalog)
    {
        if (!IsJson(artifact) || artifact.Content.Length == 0)
        {
            return null;
        }

        var schemaId = FiveEToolsSchemaContract.GetSiteSchemaId(artifact.FileName);
        if (FiveEToolsSchemaContract.IsUnderDataTree(artifact.FileName) && schemaId is null)
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(artifact.Content);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var registry = TryReadCorpusRegistry(artifact, document.RootElement, schemaId);
            if (registry is not null)
            {
                return registry;
            }

            var corpus = TryReadCorpusContent(artifact, document.RootElement, schemaId, catalog);
            if (corpus is not null)
            {
                return corpus;
            }

            var records = ReadNativeRecords(document.RootElement);
            if (records.Count == 0)
            {
                return null;
            }

            var publications = ReadPublicationEvidence(
                document.RootElement,
                records,
                catalog,
                AllowsBareSourcePublicationFallback(artifact));
            return new NormalizedSourceRepresentation(
                Format,
                artifact,
                records,
                publications,
                JsonSerializer.Serialize(new
                {
                    schemaFamily = schemaId is null
                        ? document.RootElement.TryGetProperty("_meta", out _) ? "5etools-homebrew" : "5etools-json"
                        : "5etools-site",
                    schemaId,
                    nativeRecordCount = records.Count,
                    semantics = "native-records-with-independent-publication-evidence"
                }));
        }
    }

    private static NormalizedSourceRepresentation? TryReadCorpusRegistry(
        SourceRepresentationArtifact artifact,
        JsonElement root,
        string? schemaId)
    {
        var fileName = Path.GetFileName(artifact.FileName);
        var corpusType = string.Equals(schemaId, "books.json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "books.json", StringComparison.OrdinalIgnoreCase)
                ? "book"
                : string.Equals(schemaId, "adventures.json", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(fileName, "adventures.json", StringComparison.OrdinalIgnoreCase)
                    ? "adventure"
                    : null;
        if (corpusType is null
            || !root.TryGetProperty(corpusType, out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var rows = ReadCorpusRows(corpusType, items);
        if (rows.Count == 0)
        {
            return null;
        }

        var records = rows.Select(row => new NormalizedSourceRecord(
            EntityType: row.CorpusType,
            Name: row.Title,
            SourceCode: row.SourceCode,
            NativeKey: $"{row.CorpusType}|corpus|{row.CorpusId}",
            RawJson: row.RawJson,
            PublicationLocalKey: row.PublicationLocalKey,
            NativeIdentityJson: JsonSerializer.Serialize(new
            {
                corpusType = row.CorpusType,
                id = row.CorpusId,
                source = row.SourceCode,
                parentSource = row.ParentSource,
                edition = row.NativeEdition
            }))).ToArray();

        var publications = rows.Select(row => PublicationFromCorpus(row)).ToArray();

        return new NormalizedSourceRepresentation(
            Format,
            artifact,
            records,
            publications,
            JsonSerializer.Serialize(new
            {
                schemaFamily = "5etools-site-corpus-registry",
                schemaId,
                corpusType,
                corpusCount = rows.Count,
                semantics = "corpus-id-source-parentSource-preserved-independently"
            }));
    }

    private static NormalizedSourceRepresentation? TryReadCorpusContent(
        SourceRepresentationArtifact artifact,
        JsonElement root,
        string? schemaId,
        CorpusCatalog? catalog)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        if (!TryGetCorpusBodyIdentity(artifact.FileName, catalog, out var corpusType, out var corpusId)
            || catalog is null
            || !catalog.TryGetCorpus(corpusType, corpusId, out var corpus))
        {
            return null;
        }

        var records = new List<NormalizedSourceRecord>();
        var ordinal = 0;
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = ReadString(item, "name") ?? $"Section {ordinal + 1}";
            var explicitId = ReadIdentityValue(item);
            var nativeKey = $"{corpusType}|{corpusId}|{explicitId ?? $"section-{ordinal:D5}"}";
            records.Add(new NormalizedSourceRecord(
                EntityType: $"{corpusType}-section",
                Name: name,
                SourceCode: corpus.SourceCode,
                NativeKey: nativeKey,
                RawJson: item.GetRawText(),
                LocatorKey: ReadPageLocator(item),
                PublicationLocalKey: corpus.PublicationLocalKey,
                NativeIdentityJson: JsonSerializer.Serialize(new
                {
                    corpusType,
                    corpusId,
                    source = corpus.SourceCode,
                    parentSource = corpus.ParentSource,
                    id = explicitId,
                    ordinal
                })));
            ordinal++;
        }

        if (records.Count == 0)
        {
            return null;
        }

        return new NormalizedSourceRepresentation(
            Format,
            artifact,
            records,
            [PublicationFromCorpus(corpus)],
            JsonSerializer.Serialize(new
            {
                schemaFamily = "5etools-site-corpus-content",
                schemaId,
                corpusType,
                corpusId,
                source = corpus.SourceCode,
                parentSource = corpus.ParentSource
            }));
    }

    private static IReadOnlyList<NormalizedSourceRecord> ReadNativeRecords(JsonElement root)
    {
        var records = new List<NormalizedSourceRecord>();
        var duplicateCounts = new Dictionary<string, int>(StringComparer.Ordinal);

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
                var sourceCode = FiveEToolsDocumentInspector.GetSourceCode(
                    item,
                    FiveEToolsDocumentInspector.AccountSourceFallbackCode);
                var explicitIdentity = ReadIdentityValue(item);
                var baseKey = $"{property.Name}|{sourceCode}|{name}|{explicitIdentity ?? string.Empty}";
                duplicateCounts.TryGetValue(baseKey, out var duplicateOrdinal);
                duplicateCounts[baseKey] = duplicateOrdinal + 1;
                var nativeKey = duplicateOrdinal == 0 ? baseKey : $"{baseKey}|duplicate-{duplicateOrdinal}";
                var publicationLocalKey = $"source:{sourceCode}";

                records.Add(new NormalizedSourceRecord(
                    EntityType: property.Name,
                    Name: name,
                    SourceCode: sourceCode,
                    NativeKey: nativeKey,
                    RawJson: item.GetRawText(),
                    LocatorKey: ReadPageLocator(item),
                    PublicationLocalKey: publicationLocalKey,
                    NativeIdentityJson: JsonSerializer.Serialize(new
                    {
                        source = sourceCode,
                        id = ReadRawIdentity(item, "id"),
                        uniqueId = ReadRawIdentity(item, "uniqueId"),
                        parentSource = ReadString(item, "parentSource"),
                        edition = ReadString(item, "edition")
                    })));
            }
        }

        return records;
    }

    private static IReadOnlyList<NormalizedSourcePublication> ReadPublicationEvidence(
        JsonElement root,
        IReadOnlyList<NormalizedSourceRecord> records,
        CorpusCatalog? catalog,
        bool allowBareSourceFallback)
    {
        var evidence = new Dictionary<string, NormalizedSourcePublication>(StringComparer.OrdinalIgnoreCase);
        var metaEdition = root.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object
            ? MapEdition(ReadString(meta, "edition"))
            : null;

        if (root.TryGetProperty("_meta", out meta)
            && meta.ValueKind == JsonValueKind.Object
            && meta.TryGetProperty("sources", out var sources)
            && sources.ValueKind == JsonValueKind.Array)
        {
            foreach (var source in sources.EnumerateArray())
            {
                if (source.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var sourceCode = ReadString(source, "json");
                if (sourceCode is null)
                {
                    continue;
                }

                evidence[$"source:{sourceCode}"] = new NormalizedSourcePublication(
                    $"source:{sourceCode}",
                    ReadString(source, "full") ?? sourceCode,
                    GameEdition: metaEdition,
                    PublicationDate: ParseDate(ReadString(source, "dateReleased")),
                    ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["5etools-source-code"] = sourceCode
                    });
            }
        }

        foreach (var sourceCode in records
                     .Select(value => value.SourceCode)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Cast<string>()
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var localKey = $"source:{sourceCode}";
            if (evidence.ContainsKey(localKey))
            {
                continue;
            }

            if (catalog is not null && catalog.TryGetUniqueBySourceCode(sourceCode, out var corpus))
            {
                evidence[localKey] = PublicationFromCorpus(corpus, localKey);
                continue;
            }

            if (!allowBareSourceFallback)
            {
                continue;
            }

            evidence[localKey] = new NormalizedSourcePublication(
                localKey,
                sourceCode,
                GameEdition: metaEdition,
                ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["5etools-source-code"] = sourceCode
                });
        }

        return evidence.Values.ToArray();
    }

    private static NormalizedSourcePublication PublicationFromCorpus(
        CorpusMetadata row,
        string? localKey = null) =>
        new(
            LocalKey: localKey ?? row.PublicationLocalKey,
            DisplayName: row.Title,
            Publisher: row.Publisher,
            GameEdition: row.GameEdition,
            PublicationDate: row.Published,
            ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["5etools-corpus-id"] = row.CorpusId,
                ["5etools-source-code"] = row.SourceCode
            });

    private static bool AllowsBareSourcePublicationFallback(SourceRepresentationArtifact artifact) =>
        artifact.OriginIdentity.StartsWith("admin:", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<CorpusMetadata> ReadCorpusRows(string corpusType, JsonElement items)
    {
        var rows = new List<CorpusMetadata>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = ReadString(item, "id");
            var sourceCode = ReadString(item, "source");
            var title = ReadString(item, "name");
            if (id is null || sourceCode is null || title is null)
            {
                continue;
            }

            var published = ParseDate(ReadString(item, "published"));
            var nativeEdition = ReadString(item, "edition");
            rows.Add(new CorpusMetadata(
                corpusType,
                id,
                sourceCode,
                title,
                ReadString(item, "parentSource"),
                ReadString(item, "publisher"),
                nativeEdition,
                MapEdition(nativeEdition) ?? InferEdition(published),
                published,
                item.GetRawText()));
        }
        return rows;
    }

    private static bool TryGetCorpusBodyIdentity(
        string fileName,
        CorpusCatalog? catalog,
        out string corpusType,
        out string corpusId)
    {
        if (FiveEToolsSchemaContract.TryGetCorpusBodyIdentity(fileName, out corpusType, out corpusId))
        {
            return true;
        }

        corpusType = string.Empty;
        corpusId = string.Empty;
        if (catalog is null)
        {
            return false;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        foreach (var candidate in new[] { (Type: "book", Prefix: "book-"), (Type: "adventure", Prefix: "adventure-") })
        {
            if (!baseName.StartsWith(candidate.Prefix, StringComparison.OrdinalIgnoreCase)
                || baseName.Length <= candidate.Prefix.Length)
            {
                continue;
            }

            var id = baseName[candidate.Prefix.Length..];
            if (!catalog.TryGetCorpus(candidate.Type, id, out _))
            {
                continue;
            }

            corpusType = candidate.Type;
            corpusId = id;
            return true;
        }

        return false;
    }

    private static bool IsJson(SourceRepresentationArtifact artifact) =>
        string.Equals(Path.GetExtension(artifact.FileName), ".json", StringComparison.OrdinalIgnoreCase);

    private static string? MapEdition(string? edition) =>
        string.Equals(edition, "one", StringComparison.OrdinalIgnoreCase)
            ? "5.5e"
            : string.Equals(edition, "classic", StringComparison.OrdinalIgnoreCase)
                ? "5e"
                : null;

    private static string? InferEdition(DateOnly? publicationDate) =>
        publicationDate.HasValue
            ? publicationDate.Value < ModernRulesThreshold ? "5e" : "5.5e"
            : null;

    private static DateOnly? ParseDate(string? value) =>
        value is not null
        && DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static object? ReadRawIdentity(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => value.GetRawText()
        };
    }

    private static string? ReadIdentityValue(JsonElement item)
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
        return null;
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

    private sealed record CorpusMetadata(
        string CorpusType,
        string CorpusId,
        string SourceCode,
        string Title,
        string? ParentSource,
        string? Publisher,
        string? NativeEdition,
        string? GameEdition,
        DateOnly? Published,
        string RawJson)
    {
        public string PublicationLocalKey => $"corpus:{CorpusType}:{CorpusId}";
    }

    private sealed class CorpusCatalog
    {
        private readonly Dictionary<string, CorpusMetadata> byCorpus;

        private CorpusCatalog(Dictionary<string, CorpusMetadata> byCorpus)
        {
            this.byCorpus = byCorpus;
        }

        public static CorpusCatalog Build(IReadOnlyList<SourceRepresentationArtifact> artifacts)
        {
            var byCorpus = new Dictionary<string, CorpusMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var artifact in artifacts)
            {
                var fileName = Path.GetFileName(artifact.FileName);
                var corpusType = string.Equals(fileName, "books.json", StringComparison.OrdinalIgnoreCase)
                    ? "book"
                    : string.Equals(fileName, "adventures.json", StringComparison.OrdinalIgnoreCase)
                        ? "adventure"
                        : null;
                if (corpusType is null || artifact.Content.Length == 0)
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(artifact.Content);
                    if (document.RootElement.ValueKind != JsonValueKind.Object
                        || !document.RootElement.TryGetProperty(corpusType, out var items)
                        || items.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var row in ReadCorpusRows(corpusType, items))
                    {
                        byCorpus.TryAdd(Key(row.CorpusType, row.CorpusId), row);
                    }
                }
                catch (JsonException)
                {
                }
            }
            return new CorpusCatalog(byCorpus);
        }

        public bool TryGetCorpus(string corpusType, string corpusId, out CorpusMetadata metadata) =>
            byCorpus.TryGetValue(Key(corpusType, corpusId), out metadata!);

        public bool TryGetUniqueBySourceCode(string sourceCode, out CorpusMetadata metadata)
        {
            var matches = byCorpus.Values
                .Where(value => string.Equals(value.SourceCode, sourceCode, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            if (matches.Length == 1)
            {
                metadata = matches[0];
                return true;
            }

            metadata = null!;
            return false;
        }

        private static string Key(string corpusType, string corpusId) => $"{corpusType}:{corpusId}";
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
                NativeKey: $"source-fragment|page-{page.Number}",
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
