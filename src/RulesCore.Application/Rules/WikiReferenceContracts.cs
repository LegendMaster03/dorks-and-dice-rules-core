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
    JsonElement Document);

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
    JsonElement EffectiveDocument);

public sealed record WikiReferenceComparisonRequest(
    string ReferenceIdentity,
    Guid LeftSourceEntityRevisionId,
    Guid RightSourceEntityRevisionId);
