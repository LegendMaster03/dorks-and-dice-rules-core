using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Adds confirmed 5e.tools companion/fluff semantics around the native adapter. The underlying
/// adapter remains responsible for normal entity/publication parsing; fluff records are removed
/// from the rule-bearing record set and preserved as package-owned companion payloads instead.
/// </summary>
public sealed class FiveEToolsCompanionSourceFormatAdapter : ISourceFormatBatchAdapter
{
    private readonly FiveEToolsSourceFormatAdapter inner = new();

    private static readonly IReadOnlyDictionary<string, string[]> TargetTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["monsterFluff"] = ["monster"],
            ["raceFluff"] = ["race", "species"],
            ["spellFluff"] = ["spell"]
        };

    public string FormatKey => FiveEToolsSourceFormatAdapter.Format;

    public bool IsCandidate(string? fileName, ReadOnlySpan<byte> content) =>
        inner.IsCandidate(fileName, content);

    public NormalizedSourceRepresentation? TryRead(SourceRepresentationArtifact artifact)
    {
        var companions = ReadCompanions(artifact);
        var representation = inner.TryRead(artifact);
        return Combine(artifact, representation, companions);
    }

    public IReadOnlyList<NormalizedSourceRepresentation> TryReadMany(
        IReadOnlyList<SourceRepresentationArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        var parsed = inner.TryReadMany(artifacts)
            .ToDictionary(value => ArtifactKey(value.Artifact), StringComparer.Ordinal);
        var results = new List<NormalizedSourceRepresentation>();
        foreach (var artifact in artifacts)
        {
            var companions = ReadCompanions(artifact);
            parsed.TryGetValue(ArtifactKey(artifact), out var representation);
            var combined = Combine(artifact, representation, companions);
            if (combined is not null)
            {
                results.Add(combined);
            }
        }
        return results;
    }

    internal static bool IsCompanionKind(string? entityType) =>
        !string.IsNullOrWhiteSpace(entityType) && TargetTypes.ContainsKey(entityType.Trim());

    internal static IReadOnlyList<NormalizedSourceCompanionContent> ReadCompanions(
        SourceRepresentationArtifact artifact)
    {
        if (!string.Equals(Path.GetExtension(artifact.FileName), ".json", StringComparison.OrdinalIgnoreCase)
            || artifact.Content.Length == 0)
        {
            return [];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(artifact.Content);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var companions = new List<NormalizedSourceCompanionContent>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!TargetTypes.TryGetValue(property.Name, out var targetTypes)
                    || property.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in property.Value.EnumerateArray())
                {
                    if (TryReadCompanion(property.Name, targetTypes, item, out var companion))
                    {
                        companions.Add(companion);
                    }
                }
            }
            return companions;
        }
    }

    internal static bool TryReadLegacyCompanion(
        string companionKind,
        string rawJson,
        string? fallbackName,
        string? fallbackSourceCode,
        string? fallbackNativeKey,
        out NormalizedSourceCompanionContent companion)
    {
        companion = null!;
        if (!TargetTypes.TryGetValue(companionKind, out var targetTypes))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (TryReadCompanion(companionKind, targetTypes, document.RootElement, out companion))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(fallbackName) || string.IsNullOrWhiteSpace(fallbackSourceCode))
            {
                return false;
            }

            var targets = targetTypes
                .Select(value => new NormalizedSourceCompanionTarget(
                    value,
                    fallbackName.Trim(),
                    fallbackSourceCode.Trim(),
                    "legacy-name-source"))
                .ToArray();
            companion = new NormalizedSourceCompanionContent(
                companionKind,
                fallbackName.Trim(),
                fallbackSourceCode.Trim(),
                fallbackNativeKey ?? $"{companionKind}|{fallbackSourceCode.Trim()}|{fallbackName.Trim()}|",
                rawJson,
                targets);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static NormalizedSourceRepresentation? Combine(
        SourceRepresentationArtifact artifact,
        NormalizedSourceRepresentation? representation,
        IReadOnlyList<NormalizedSourceCompanionContent> companions)
    {
        if (representation is null)
        {
            if (companions.Count == 0)
            {
                return null;
            }

            return new NormalizedSourceRepresentation(
                FiveEToolsSourceFormatAdapter.Format,
                artifact,
                [],
                [],
                JsonSerializer.Serialize(new
                {
                    schemaFamily = "5etools-companion-content",
                    companionCount = companions.Count,
                    semantics = "source-companion-content"
                }))
            {
                CompanionContents = companions
            };
        }

        // Defensive filtering is retained even if the underlying inspector later stops treating
        // fluff arrays as importable entity arrays.
        var ruleRecords = representation.Records
            .Where(value => !IsCompanionKind(value.EntityType))
            .ToArray();
        return representation with
        {
            Records = ruleRecords,
            CompanionContents = companions
        };
    }

    private static bool TryReadCompanion(
        string companionKind,
        IReadOnlyList<string> targetTypes,
        JsonElement item,
        out NormalizedSourceCompanionContent companion)
    {
        companion = null!;
        if (item.ValueKind != JsonValueKind.Object
            || !TryReadString(item, "name", out var name))
        {
            return false;
        }

        var sourceCode = FiveEToolsDocumentInspector.GetSourceCode(
            item,
            FiveEToolsDocumentInspector.AccountSourceFallbackCode);
        var nativeIdentity = FiveEToolsNativeIdentity.Create(
            companionKind,
            item,
            name,
            sourceCode);
        var targets = new List<NormalizedSourceCompanionTarget>();
        AddTargets(targets, targetTypes, name, sourceCode, "name-source");

        if (item.TryGetProperty("_copy", out var copy)
            && copy.ValueKind == JsonValueKind.Object
            && TryReadString(copy, "name", out var copyName))
        {
            var copySource = TryReadString(copy, "source", out var explicitCopySource)
                ? explicitCopySource
                : sourceCode;
            AddTargets(targets, targetTypes, copyName, copySource, "copy-base");
        }

        companion = new NormalizedSourceCompanionContent(
            companionKind,
            name,
            sourceCode,
            nativeIdentity.NativeKey,
            item.GetRawText(),
            targets
                .DistinctBy(value => $"{value.EntityType}\n{value.Name}\n{value.SourceCode}", StringComparer.OrdinalIgnoreCase)
                .ToArray());
        return true;
    }

    private static void AddTargets(
        ICollection<NormalizedSourceCompanionTarget> targets,
        IEnumerable<string> targetTypes,
        string name,
        string sourceCode,
        string evidenceKind)
    {
        foreach (var targetType in targetTypes)
        {
            targets.Add(new NormalizedSourceCompanionTarget(
                targetType,
                name,
                sourceCode,
                evidenceKind));
        }
    }

    private static bool TryReadString(JsonElement item, string propertyName, out string value)
    {
        value = string.Empty;
        if (!item.TryGetProperty(propertyName, out var element)
            || element.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            return false;
        }
        value = element.GetString()!.Trim();
        return true;
    }

    private static string ArtifactKey(SourceRepresentationArtifact artifact) =>
        $"{artifact.OriginIdentity}\n{artifact.FileName}";
}
