using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RulesCore.Infrastructure.Sources;

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
                id = row.CorpusId
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
            var sectionId = explicitId ?? $"section-{ordinal:D5}";
            var nativeKey = $"{corpusType}|{corpusId}|{sectionId}";
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
                    sectionId
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
                var nativeIdentity = FiveEToolsNativeIdentity.Create(
                    property.Name,
                    item,
                    name,
                    sourceCode);
                var publicationLocalKey = $"source:{sourceCode}";

                records.Add(new NormalizedSourceRecord(
                    EntityType: property.Name,
                    Name: name,
                    SourceCode: sourceCode,
                    NativeKey: nativeIdentity.NativeKey,
                    RawJson: item.GetRawText(),
                    LocatorKey: ReadPageLocator(item),
                    PublicationLocalKey: publicationLocalKey,
                    NativeIdentityJson: nativeIdentity.IdentityJson));
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
