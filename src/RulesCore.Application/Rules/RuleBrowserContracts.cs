using System.Text.Json;

namespace RulesCore.Application.Rules;

public sealed record RuleLinkTargetView(
    string ToolSlug,
    string ToolRelativePath,
    string RouteIdentity);

public static class RuleBrowserRoutes
{
    public const string ToolSlug = "rules-core";

    private static readonly IReadOnlyDictionary<string, string> EntityTypeToSegment =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["monster"] = "monsters",
            ["spell"] = "spells",
            ["class"] = "classes",
            ["subclass"] = "subclasses",
            ["prestigeClass"] = "prestige-classes",
            ["feat"] = "feats",
            ["background"] = "backgrounds",
            ["optionalfeature"] = "optional-features",
            ["species"] = "species",
            ["subspecies"] = "subspecies",
            ["item"] = "items",
            ["condition"] = "conditions",
            ["skill"] = "skills"
        };

    private static readonly IReadOnlyDictionary<string, string> SegmentToEntityType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["monsters"] = "monster",
            ["spells"] = "spell",
            ["classes"] = "class",
            ["subclasses"] = "subclass",
            ["prestige-classes"] = "prestigeclass",
            ["feats"] = "feat",
            ["backgrounds"] = "background",
            ["optional-features"] = "optionalfeature",
            ["species"] = "species",
            ["subspecies"] = "subspecies",
            ["items"] = "item",
            ["conditions"] = "condition",
            ["skills"] = "skill",
            // Legacy deep links remain resolvable, but normal navigation no longer emits them.
            ["races"] = "species",
            ["subraces"] = "subspecies"
        };

    public static RuleLinkTargetView ForConcept(string entityType, string conceptKey)
    {
        var normalizedType = NormalizeEntityType(RequireText(entityType, nameof(entityType)));
        var normalizedKey = RequireText(conceptKey, nameof(conceptKey)).ToLowerInvariant();

        if (EntityTypeToSegment.TryGetValue(normalizedType, out var segment))
        {
            var prefix = normalizedType + ".";
            if (!normalizedKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // Persisted legacy keys such as race.elf remain stable identities. Use the generic
                // route instead of fabricating a species.race.elf identity from the canonical type.
                return new RuleLinkTargetView(
                    ToolSlug,
                    $"/rules/{Uri.EscapeDataString(normalizedKey)}",
                    normalizedKey);
            }

            var routeKey = normalizedKey[prefix.Length..];
            return new RuleLinkTargetView(
                ToolSlug,
                $"/{segment}/{Uri.EscapeDataString(routeKey)}",
                normalizedKey);
        }

        return new RuleLinkTargetView(
            ToolSlug,
            $"/rules/{Uri.EscapeDataString(normalizedKey)}",
            normalizedKey);
    }

    public static string CatalogPath(string entityType)
    {
        var normalizedType = NormalizeEntityType(RequireText(entityType, nameof(entityType)));
        return EntityTypeToSegment.TryGetValue(normalizedType, out var segment)
            ? $"/{segment}"
            : $"/types/{Uri.EscapeDataString(normalizedType)}";
    }

    public static bool TryResolveConceptKey(
        string? toolRelativePath,
        out string conceptKey,
        out string? entityType)
    {
        conceptKey = string.Empty;
        entityType = null;
        if (string.IsNullOrWhiteSpace(toolRelativePath))
        {
            return false;
        }

        var path = StripQueryAndFragment(toolRelativePath.Trim());
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        if (string.Equals(segments[0], "rules", StringComparison.OrdinalIgnoreCase))
        {
            conceptKey = Uri.UnescapeDataString(segments[1]);
            return !string.IsNullOrWhiteSpace(conceptKey);
        }

        if (!SegmentToEntityType.TryGetValue(segments[0], out var resolvedType))
        {
            return false;
        }

        var routeKey = Uri.UnescapeDataString(segments[1]);
        if (string.IsNullOrWhiteSpace(routeKey))
        {
            return false;
        }

        entityType = resolvedType;
        conceptKey = $"{resolvedType}.{routeKey}";
        return true;
    }

    public static string? EntityTypeForCatalogPath(string? toolRelativePath)
    {
        if (string.IsNullOrWhiteSpace(toolRelativePath))
        {
            return null;
        }

        var path = StripQueryAndFragment(toolRelativePath.Trim());
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 1
            && SegmentToEntityType.TryGetValue(segments[0], out var entityType))
        {
            return entityType;
        }

        if (segments.Length == 2
            && string.Equals(segments[0], "types", StringComparison.OrdinalIgnoreCase))
        {
            var dynamicType = NormalizeEntityType(Uri.UnescapeDataString(segments[1]).Trim());
            return string.IsNullOrWhiteSpace(dynamicType) ? null : dynamicType;
        }

        return null;
    }

    private static string NormalizeEntityType(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "race" => "species",
            "subrace" => "subspecies",
            "prestigeclass" => "prestigeclass",
            _ => normalized
        };
    }

    private static string StripQueryAndFragment(string path)
    {
        var queryIndex = path.IndexOf('?');
        var fragmentIndex = path.IndexOf('#');
        var end = path.Length;
        if (queryIndex >= 0) end = Math.Min(end, queryIndex);
        if (fragmentIndex >= 0) end = Math.Min(end, fragmentIndex);
        return path[..end];
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", parameterName);
        }
        return value.Trim();
    }
}

public static class RuleAdjudicationScopeKinds
{
    public const string Global = "global";
    public const string Campaign = "campaign";
}

public sealed record RuleAdjudicationScopeRequest(
    string Kind,
    Guid? CampaignId = null);

public sealed record RuleAdjudicationScopeView(
    string Kind,
    Guid? CampaignId,
    string DisplayName,
    bool CanBrowse,
    bool CanAdjudicate)
{
    public string ScopeKey => CampaignId is null ? Kind : $"{Kind}:{CampaignId:D}";
}

public sealed record RuleWorkspaceScopesView(
    IReadOnlyList<RuleAdjudicationScopeView> Scopes);

public sealed record CampaignRuleBaselineView(
    Guid CampaignId,
    Guid RuleConceptId,
    string ConceptKey,
    string EntityType,
    string DisplayName,
    int BaselineRulesetRevisionNumber,
    string BaselineRulesetFingerprint,
    Guid GlobalRuleDecisionId,
    int GlobalDecisionNumber,
    string GlobalDecisionKind,
    string? GlobalDecisionNote,
    Guid SourceEntityId,
    Guid SourceEntityRevisionId,
    int SourceRevisionNumber,
    string SourceEntityName,
    string SourceCode,
    string PackageDisplayName,
    string EditionDisplayName,
    IReadOnlyList<ResolvedRuleContributionView> Contributions,
    JsonElement Document)
{
    public RuleLinkTargetView BrowserLink => RuleBrowserRoutes.ForConcept(EntityType, ConceptKey);
}
