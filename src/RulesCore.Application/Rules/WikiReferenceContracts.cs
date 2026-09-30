using System.Text.Json;

namespace RulesCore.Application.Rules;

public static class WikiReferenceCategoryModes
{
    public const string AnyVariation = "any";
    public const string Effective = "effective";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return AnyVariation;
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            AnyVariation => AnyVariation,
            Effective => Effective,
            _ => throw new ArgumentException(
                $"Unknown reference category mode '{value}'. Expected '{AnyVariation}' or '{Effective}'.",
                nameof(value))
        };
    }
}

public static class WikiReferenceResolutionStates
{
    public const string Resolved = "resolved";
    public const string CampaignOverride = "campaign-override";
    public const string Inherited = "inherited";
    public const string UnresolvedFallback = "unresolved-fallback";
}

public sealed record WikiReferenceFacetView(
    string Value,
    string DisplayName,
    int Count);

public sealed record WikiReferenceCategoryHistoryView(
    string Category,
    IReadOnlyList<string> Editions);

public sealed record WikiReferenceAdvancementFeatureView(
    string Name,
    int? Level,
    string? FeatureReference);

/// <summary>
/// Projects source-native class-family advancement references for Wiki presentation.
/// The supported reference shapes intentionally mirror the authoritative character
/// projection semantics in ClassCharacterRuleProjectionModule. Integration coverage
/// exercises both projections against the same imported 5e class/subclass fixtures so
/// this focused Wiki projection can not silently drift from character projection.
/// </summary>
public static class WikiReferenceClassFamilyProjection
{
    public static IReadOnlyList<WikiReferenceAdvancementFeatureView> ProjectAdvancementFeatures(
        string? category,
        JsonElement document)
    {
        var normalizedCategory = NormalizeCategory(category);
        var isSubclass = normalizedCategory == "subclass";
        var propertyNames = normalizedCategory switch
        {
            "class" => new[] { "classFeatures" },
            "subclass" => new[] { "subclassFeatures" },
            "prestigeclass" => new[] { "prestigeClassFeatures", "classFeatures" },
            _ => []
        };
        if (propertyNames.Length == 0 || document.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        JsonElement features = default;
        var found = false;
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(document, propertyName, out features)) continue;
            found = true;
            break;
        }
        if (!found) return [];

        var entries = features.ValueKind == JsonValueKind.Array
            ? features.EnumerateArray().ToArray()
            : [features];
        var projected = new List<WikiReferenceAdvancementFeatureView>(entries.Length);
        foreach (var entry in entries)
        {
            if (TryProjectAdvancementFeature(entry, isSubclass, out var feature))
            {
                projected.Add(feature);
            }
        }
        return projected;
    }

    private static bool TryProjectAdvancementFeature(
        JsonElement entry,
        bool isSubclass,
        out WikiReferenceAdvancementFeatureView feature)
    {
        feature = default!;
        string? reference = null;
        if (entry.ValueKind == JsonValueKind.String)
        {
            reference = entry.GetString()?.Trim();
        }
        else if (entry.ValueKind == JsonValueKind.Object)
        {
            var referenceProperty = isSubclass ? "subclassFeature" : "classFeature";
            reference = ReadString(entry, referenceProperty)?.Trim();
            if (string.IsNullOrWhiteSpace(reference))
            {
                var directName = ReadString(entry, "name")?.Trim();
                var directLevel = ReadInteger(entry, "level");
                if (!string.IsNullOrWhiteSpace(directName))
                {
                    feature = new WikiReferenceAdvancementFeatureView(
                        directName,
                        directLevel is > 0 ? directLevel : null,
                        null);
                    return true;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(reference)) return false;
        var parts = reference.Split('|');
        var displayName = parts[0].Trim();
        if (displayName.Length == 0) return false;
        var levelIndex = isSubclass ? 5 : 3;
        int? level = null;
        if (parts.Length > levelIndex
            && int.TryParse(parts[levelIndex], out var parsedLevel)
            && parsedLevel > 0)
        {
            level = parsedLevel;
        }
        feature = new WikiReferenceAdvancementFeatureView(displayName, level, reference);
        return true;
    }

    private static string NormalizeCategory(string? value) =>
        string.Concat((value ?? string.Empty)
            .Where(character => character is not '-' and not '_' && !char.IsWhiteSpace(character)))
            .ToLowerInvariant();

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            value = property.Value;
            return true;
        }
        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInteger(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric)) return numeric;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric)
            ? numeric
            : null;
    }
}

public sealed record WikiReferenceVariationSummaryView(
    Guid? CanonicalEntityId,
    Guid SourceEntityId,
    Guid SourceEntityRevisionId,
    int SourceRevisionNumber,
    string Name,
    string Category,
    string SourceCode,
    string PackageKey,
    string PackageDisplayName,
    Guid PublicationId,
    string PublicationKey,
    string PublicationDisplayName,
    string EditionKey,
    string EditionDisplayName,
    DateOnly? PublicationDate,
    bool IsEffective);

public sealed record WikiReferenceVariationView(
    Guid? CanonicalEntityId,
    Guid SourceEntityId,
    Guid SourceEntityRevisionId,
    int SourceRevisionNumber,
    string Name,
    string Category,
    string SourceCode,
    string PackageKey,
    string PackageDisplayName,
    Guid PublicationId,
    string PublicationKey,
    string PublicationDisplayName,
    string EditionKey,
    string EditionDisplayName,
    DateOnly? PublicationDate,
    bool IsEffective,
    JsonElement Document)
{
    public IReadOnlyList<WikiReferenceAdvancementFeatureView> AdvancementFeatures =>
        WikiReferenceClassFamilyProjection.ProjectAdvancementFeatures(Category, Document);
}

public sealed record WikiReferenceItemView(
    string ReferenceIdentity,
    Guid? RuleConceptId,
    string? ConceptKey,
    string DisplayName,
    string EntityType,
    string EffectiveCategory,
    string EffectiveEditionKey,
    string EffectiveEditionDisplayName,
    string ResolutionState,
    bool HasCampaignOverride,
    WikiReferenceVariationSummaryView EffectiveVariation,
    WikiReferenceVariationSummaryView BrowseVariation,
    IReadOnlyList<WikiReferenceCategoryHistoryView> CategoryHistory,
    IReadOnlyList<ResolvedRuleBrowserFieldView> BrowserFields,
    IReadOnlyList<ResolvedRuleRelationshipView> Relationships)
{
    public string SourceCode => BrowseVariation.SourceCode;
    public string PackageKey => BrowseVariation.PackageKey;
    public string PackageDisplayName => BrowseVariation.PackageDisplayName;
    public string EditionKey => BrowseVariation.EditionKey;
    public string EditionDisplayName => BrowseVariation.EditionDisplayName;
    public Guid SourceEntityId => BrowseVariation.SourceEntityId;
    public Guid SourceEntityRevisionId => BrowseVariation.SourceEntityRevisionId;
    public int SourceRevisionNumber => BrowseVariation.SourceRevisionNumber;
    public string SourceEntityName => BrowseVariation.Name;

    public RuleLinkTargetView BrowserLink => ConceptKey is not null
        ? RuleBrowserRoutes.ForConcept(EntityType, ConceptKey)
        : new RuleLinkTargetView(
            RuleBrowserRoutes.ToolSlug,
            $"/references/{Uri.EscapeDataString(ReferenceIdentity)}",
            ReferenceIdentity);
}

public sealed record WikiReferenceCatalogView(
    string Scope,
    Guid? CampaignId,
    int? RevisionNumber,
    DateTimeOffset? PublishedAt,
    int TotalCount,
    string CategoryMode,
    IReadOnlyList<WikiReferenceFacetView> EntityTypeFacets,
    IReadOnlyList<WikiReferenceFacetView> SourceFacets,
    IReadOnlyList<WikiReferenceFacetView> PackageFacets,
    IReadOnlyList<WikiReferenceFacetView> EditionFacets,
    IReadOnlyList<WikiReferenceItemView> References)
{
    // Keep the familiar collection name available while Rules Wiki migrates its Phase 2 browser.
    public IReadOnlyList<WikiReferenceItemView> Rules => References;
}

public sealed record WikiReferenceDetailView(
    string Scope,
    Guid? CampaignId,
    WikiReferenceItemView Reference,
    IReadOnlyList<WikiReferenceVariationView> Variations,
    JsonElement EffectiveDocument)
{
    public IReadOnlyList<WikiReferenceAdvancementFeatureView> EffectiveAdvancementFeatures =>
        WikiReferenceClassFamilyProjection.ProjectAdvancementFeatures(
            Reference.EffectiveCategory,
            EffectiveDocument);
}

public sealed record WikiReferenceComparisonRequest(
    string ReferenceIdentity,
    Guid LeftSourceEntityRevisionId,
    Guid RightSourceEntityRevisionId);
