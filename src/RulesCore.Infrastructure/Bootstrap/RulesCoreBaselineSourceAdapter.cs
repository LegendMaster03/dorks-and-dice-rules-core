using System.Text;
using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Bootstrap;

internal static class RulesCoreBaselineSourceAdapter
{
    public const string FormatKey = "rules-core-json";
    private const string PublicationKey = "dorks-and-dice-house-rules";

    public static NormalizedSourceRepresentation Read(Import5eToolsDocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var document = JsonDocument.Parse(request.Json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The Rules Core baseline source must have a JSON object root.");
        }

        var records = new List<NormalizedSourceRecord>();
        var duplicateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Name.StartsWith('_') || property.Value.ValueKind != JsonValueKind.Array)
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
                var sourceCode = ReadString(item, "source");
                var explicitId = ReadIdentity(item);
                var baseKey = $"{property.Name}|{sourceCode ?? string.Empty}|{name}|{explicitId ?? string.Empty}";
                duplicateCounts.TryGetValue(baseKey, out var duplicateOrdinal);
                duplicateCounts[baseKey] = duplicateOrdinal + 1;
                var nativeKey = duplicateOrdinal == 0
                    ? baseKey
                    : $"{baseKey}|duplicate-{duplicateOrdinal}";

                records.Add(new NormalizedSourceRecord(
                    property.Name,
                    name,
                    sourceCode,
                    nativeKey,
                    item.GetRawText(),
                    LocatorKey: ReadPageLocator(item),
                    PublicationLocalKey: PublicationKey,
                    NativeIdentityJson: JsonSerializer.Serialize(new
                    {
                        source = sourceCode,
                        id = ReadRawIdentity(item, "id"),
                        uniqueId = ReadRawIdentity(item, "uniqueId")
                    })));
            }
        }

        if (records.Count == 0)
        {
            throw new InvalidDataException("The Rules Core baseline source did not contain any named source records.");
        }

        var artifact = new SourceRepresentationArtifact(
            "dorks-and-dice-baseline.json",
            Encoding.UTF8.GetBytes(request.Json),
            "builtin:dorks-and-dice-baseline:v1",
            MediaType: "application/json");

        return new NormalizedSourceRepresentation(
            FormatKey,
            artifact,
            records,
            [new NormalizedSourcePublication(
                PublicationKey,
                request.WorkDisplayName,
                Publisher: request.Publisher ?? request.Provider,
                GameEdition: request.GameEdition,
                PublicationDate: request.PublicationDate,
                ExternalIdentifiers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["rules-core-source-code"] = RulesCoreBaselineCatalog.HouseRulesSourceCode
                })],
            JsonSerializer.Serialize(new
            {
                schemaFamily = "rules-core-baseline",
                nativeRecordCount = records.Count,
                semantics = "dorks-and-dice-authored-source-records"
            }));
    }

    private static string? ReadString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static string? ReadIdentity(JsonElement item)
    {
        foreach (var propertyName in new[] { "uniqueId", "id" })
        {
            var value = ReadRawIdentity(item, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }

    private static string? ReadRawIdentity(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.String when !string.IsNullOrWhiteSpace(value.GetString()) => value.GetString()!.Trim(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static string? ReadPageLocator(JsonElement item)
    {
        if (!item.TryGetProperty("page", out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.Number => $"page:{value.GetRawText()}",
            JsonValueKind.String when !string.IsNullOrWhiteSpace(value.GetString()) => $"page:{value.GetString()!.Trim()}",
            _ => null
        };
    }
}
