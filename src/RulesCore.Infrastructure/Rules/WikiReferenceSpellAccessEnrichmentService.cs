using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Adds class-list metadata from the 5e.tools generated spell-source lookup to the private Wiki
/// catalog. The lookup remains package-owned companion evidence so source grants are preserved and
/// generated indexing data does not become part of canonical spell identity.
/// </summary>
public sealed class WikiReferenceSpellAccessEnrichmentService(RulesCoreDbContext dbContext)
{
    public async Task<WikiReferenceCatalogView> EnrichAsync(
        WikiReferenceCatalogView catalog,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var spellReferences = catalog.References
            .Where(value => string.Equals(
                RuleConceptEntityTypes.Normalize(value.BrowseVariation.Category),
                "spell",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (spellReferences.Length == 0) return catalog;

        var sourceEntityIds = spellReferences
            .Select(value => value.BrowseVariation.SourceEntityId)
            .Distinct()
            .ToArray();
        var rows = await new SourceCompanionContentStore(dbContext)
            .ReadAccessibleForSourceEntitiesAsync(sourceEntityIds, userId, cancellationToken);
        var lookupRows = rows
            .Where(value => string.Equals(
                value.CompanionKind,
                FiveEToolsCompanionSourceFormatAdapter.SpellSourceLookupCompanionKind,
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(value => value.SourceEntityId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        if (lookupRows.Count == 0) return catalog;

        var references = catalog.References.Select(reference =>
        {
            if (!lookupRows.TryGetValue(reference.BrowseVariation.SourceEntityId, out var evidence))
            {
                return reference;
            }

            var classNames = evidence
                .SelectMany(value => ReadClassNames(value.RawJson))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (classNames.Length == 0) return reference;

            var fields = reference.BrowserFields.ToList();
            var existingIndex = fields.FindIndex(value =>
                string.Equals(value.Key, "spellList", StringComparison.OrdinalIgnoreCase));
            var existingValues = existingIndex >= 0
                ? SplitDisplayValues(fields[existingIndex].Value)
                : [];
            var display = string.Join(", ", existingValues
                .Concat(classNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            var field = new ResolvedRuleBrowserFieldView(
                "spellList",
                "Lists / Classes",
                display);
            if (existingIndex >= 0) fields[existingIndex] = field;
            else fields.Add(field);
            return reference with { BrowserFields = fields.ToArray() };
        }).ToArray();

        return catalog with { References = references };
    }

    internal static IReadOnlyList<string> ReadClassNames(string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("access", out var access)
                || access.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var names = new List<string>();
            AddClassNames(access, "class", names);
            AddClassNames(access, "classVariant", names);
            return names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void AddClassNames(
        JsonElement access,
        string propertyName,
        ICollection<string> names)
    {
        if (!access.TryGetProperty(propertyName, out var byClassSource)
            || byClassSource.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var source in byClassSource.EnumerateObject())
        {
            if (source.Value.ValueKind != JsonValueKind.Object) continue;
            foreach (var classEntry in source.Value.EnumerateObject())
            {
                if (!string.IsNullOrWhiteSpace(classEntry.Name))
                {
                    names.Add(classEntry.Name.Trim());
                }
            }
        }
    }

    private static IReadOnlyList<string> SplitDisplayValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
