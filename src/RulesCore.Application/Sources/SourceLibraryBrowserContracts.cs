using System.Text.Json;

namespace RulesCore.Application.Sources;

public sealed record SourceLibraryLinkView(
    string ToolSlug,
    string ToolRelativePath,
    string RouteIdentity);

public sealed record SourceLibraryCategoryView(
    string EntityType,
    string DisplayName,
    int Count,
    SourceLibraryLinkView BrowserLink);

public sealed record SourceLibraryPublicationView(
    Guid PublicationId,
    string DisplayName,
    string WorkDisplayName,
    string EditionDisplayName,
    string Provider,
    string? License,
    bool IsPublic,
    int EntityCount,
    DateTimeOffset? LatestImportedAt,
    IReadOnlyList<SourceLibraryCategoryView> Categories,
    SourceLibraryLinkView BrowserLink);

public sealed record SourceLibraryEntitySummaryView(
    Guid EntityId,
    Guid PublicationId,
    string EntityType,
    string Name,
    string SourceCode,
    int LatestRevisionNumber,
    DateTimeOffset LatestImportedAt,
    string PublicationDisplayName,
    string EditionDisplayName,
    SourceLibraryLinkView BrowserLink);

public sealed record SourceLibraryEntityView(
    Guid EntityId,
    Guid PublicationId,
    string EntityType,
    string Name,
    string SourceCode,
    int RevisionNumber,
    string Fingerprint,
    DateTimeOffset ImportedAt,
    string PublicationDisplayName,
    string EditionDisplayName,
    string Provider,
    string? License,
    JsonElement Document,
    SourceLibraryLinkView BrowserLink);

public static class SourceLibraryRoutes
{
    public const string ToolSlug = "rules-core";

    private static readonly IReadOnlyDictionary<string, string> EntityTypeToSegment =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["monster"] = "monsters",
            ["spell"] = "spells",
            ["class"] = "classes",
            ["subclass"] = "subclasses",
            ["classFeature"] = "class-features",
            ["subclassFeature"] = "subclass-features",
            ["prestigeClass"] = "prestige-classes",
            ["npcClass"] = "npc-classes",
            ["race"] = "races",
            ["species"] = "species",
            ["feat"] = "feats",
            ["skill"] = "skills",
            ["item"] = "items",
            ["power"] = "powers",
            ["domain"] = "domains",
            ["divineAbility"] = "divine-abilities",
            ["condition"] = "conditions",
            ["houseRule"] = "house-rules",
            ["rule"] = "rules",
            ["source-fragment"] = "documents",
            ["background"] = "backgrounds",
            ["optionalfeature"] = "optional-features",
            ["reward"] = "rewards",
            ["object"] = "objects",
            ["trap"] = "traps",
            ["vehicle"] = "vehicles",
            ["deity"] = "deities",
            ["language"] = "languages",
            ["cult"] = "cults",
            ["boon"] = "boons",
            ["recipe"] = "recipes"
        };

    public static SourceLibraryLinkView ForPublication(Guid publicationId) =>
        new(
            ToolSlug,
            $"/library/{publicationId:D}",
            $"source-publication:{publicationId:D}");

    public static SourceLibraryLinkView ForCollection(Guid? publicationId, string entityType)
    {
        var normalizedType = RequireText(entityType, nameof(entityType));
        var collectionPath = EntityTypeToSegment.TryGetValue(normalizedType, out var segment)
            ? segment
            : $"types/{Uri.EscapeDataString(normalizedType)}";
        var prefix = publicationId.HasValue
            ? $"/library/{publicationId.Value:D}"
            : "/library";
        return new SourceLibraryLinkView(
            ToolSlug,
            $"{prefix}/{collectionPath}",
            publicationId.HasValue
                ? $"source-collection:{publicationId.Value:D}:{normalizedType}"
                : $"source-collection:all:{normalizedType}");
    }

    public static SourceLibraryLinkView ForEntity(
        Guid publicationId,
        string entityType,
        Guid entityId)
    {
        var normalizedType = RequireText(entityType, nameof(entityType));
        var collectionPath = EntityTypeToSegment.TryGetValue(normalizedType, out var segment)
            ? segment
            : $"types/{Uri.EscapeDataString(normalizedType)}";
        return new SourceLibraryLinkView(
            ToolSlug,
            $"/library/{publicationId:D}/{collectionPath}/{entityId:D}",
            $"source-entity:{entityId:D}");
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
