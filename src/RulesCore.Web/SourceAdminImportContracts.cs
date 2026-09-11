using System.Text;
using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Web;

public sealed record SourceAdminImportRequest(
    string PackageKey,
    string PackageDisplayName,
    string Provider,
    string? License,
    bool IsPublic,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    string Json,
    string? GameEdition = null,
    string? ReleaseKind = null,
    DateOnly? PublicationDate = null,
    IReadOnlyList<string>? IncludedSourceCodes = null);

public sealed record SourceAdminImportPreparation(
    Import5eToolsDocumentRequest LogicalRequest,
    IReadOnlyList<string> AvailableSourceCodes,
    IReadOnlyList<string> IncludedSourceCodes,
    IReadOnlyList<string> Warnings);

public static class SourceAdminImportPartitioner
{
    public static SourceAdminImportPreparation Prepare(SourceAdminImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Json))
        {
            throw new ArgumentException("Source JSON can not be blank.", nameof(request));
        }

        using var document = JsonDocument.Parse(request.Json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A 5e.tools source document must have a JSON object root.");
        }

        var available = DiscoverSourceCodes(document.RootElement, request.EditionKey);
        var included = NormalizeSourceCodes(request.IncludedSourceCodes);
        var warnings = new List<string>();
        string logicalJson;

        if (included.Count == 0)
        {
            logicalJson = request.Json;
            if (available.Count > 1)
            {
                warnings.Add(
                    $"The submitted aggregate contains multiple source codes ({string.Join(", ", available)}). All imported entities will receive the requested work/release metadata. If those source codes represent different logical publications or releases, preview and import each partition separately with IncludedSourceCodes.");
            }
        }
        else
        {
            var missing = included
                .Where(code => !available.Contains(code, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            if (missing.Length > 0)
            {
                warnings.Add(
                    $"The requested source-code filter contains codes not present in this document: {string.Join(", ", missing)}.");
            }

            logicalJson = FilterDocument(
                document.RootElement,
                request.EditionKey,
                included,
                out var selectedEntityCount);
            if (selectedEntityCount == 0)
            {
                throw new InvalidDataException(
                    $"The source-code filter did not select any importable entities. Available source codes: {string.Join(", ", available)}.");
            }

            warnings.Add(
                $"Source-code partition active: only entities from {string.Join(", ", included)} are included in this preview/import. The submitted aggregate remains unchanged outside Rules Core.");
        }

        return new SourceAdminImportPreparation(
            new Import5eToolsDocumentRequest(
                request.PackageKey,
                request.PackageDisplayName,
                request.Provider,
                request.License,
                request.IsPublic,
                request.WorkKey,
                request.WorkDisplayName,
                request.EditionKey,
                request.EditionDisplayName,
                logicalJson,
                request.GameEdition,
                request.ReleaseKind,
                request.PublicationDate),
            available,
            included,
            warnings);
    }

    private static IReadOnlyList<string> DiscoverSourceCodes(JsonElement root, string editionKey)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.StartsWith('_') || property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                codes.Add(GetSourceCode(item, editionKey));
            }
        }

        return codes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> NormalizeSourceCodes(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string FilterDocument(
        JsonElement root,
        string editionKey,
        IReadOnlyList<string> included,
        out int selectedEntityCount)
    {
        var includedSet = included.ToHashSet(StringComparer.OrdinalIgnoreCase);
        selectedEntityCount = 0;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (property.Name.StartsWith('_') || property.Value.ValueKind != JsonValueKind.Array)
                {
                    property.Value.WriteTo(writer);
                    continue;
                }

                writer.WriteStartArray();
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object
                        && includedSet.Contains(GetSourceCode(item, editionKey)))
                    {
                        item.WriteTo(writer);
                        selectedEntityCount++;
                    }
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string GetSourceCode(JsonElement item, string editionKey)
    {
        if (item.TryGetProperty("source", out var sourceElement)
            && sourceElement.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(sourceElement.GetString()))
        {
            return sourceElement.GetString()!.Trim();
        }

        if (string.IsNullOrWhiteSpace(editionKey))
        {
            throw new InvalidDataException(
                "An entity without an explicit source code requires a non-blank release key for source partitioning.");
        }
        return editionKey.Trim();
    }
}
