using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

public sealed record NativeOriginChoiceOption(string Id, string Name);
public sealed record NativeOriginChoiceCatalogView(string CatalogId, string Status,
    bool RequiresCharacterAuthority, IReadOnlyList<NativeOriginChoiceOption> Options);

/// <summary>
/// Closed native membership predicates for authored open Origin choices.
/// Catalog membership is not character ownership, feat prerequisite approval,
/// or permission to cast a spell. Working candidates are never consulted.
/// </summary>
public static class NativeOriginChoiceCatalog
{
    public static bool IsMember(NativeCatalogSnapshot snapshot, string catalogId, string optionId)
    {
        if (string.IsNullOrWhiteSpace(optionId) || optionId.Length > 160 ||
            !snapshot.TryGetById(optionId, out var entity) || entity is null ||
            !Selectable(entity.Definition)) return false;
        return catalogId switch
        {
            "any-native-skill" => entity.Category == "skill",
            "spell:wizard" => IsCantrip(entity) &&
                HasExplicitList(entity.Definition, "wizard"),
            "feat:origin" => entity.Category == "feat" &&
                entity.Id.StartsWith("feat.", StringComparison.Ordinal) &&
                Text(entity.Definition, "featCategory") == "origin",
            _ => false
        };
    }

    // Inherited configurations retain their original selection evidence, but
    // membership must still hold in the active catalog. Never reopen a saved
    // parent's menu or substitute an option from the working catalog.
    internal static bool ValidInheritedOpenChoice(NativeCatalogSnapshot snapshot,
        NativeOriginCandidate candidate, IReadOnlyList<NativeOriginChoiceSelection>? choices)
    {
        if (candidate.Identity.MechanicalDefinitionId is not { } id ||
            !snapshot.TryGetById(id, out var entity) || entity?.Category != "origin-trait" ||
            !entity.Definition.TryGetProperty("trait", out var trait) ||
            trait.ValueKind != JsonValueKind.Object)
            return true; // Other inherited characteristics use their own validators.

        var menu = Text(trait, "chooseFrom");
        if (menu is null && Text(trait, "grantType") == "feat-choice")
            menu = "feat:" + (Text(trait, "featCategory") ?? "all");
        if (menu is null && Text(trait, "spellList") is { } list && Text(trait, "spell") is null)
            menu = "spell:" + list;
        if (menu is null || Text(trait, "when") == "on-use") return true;
        var count = trait.TryGetProperty("choiceCount", out var number) &&
            number.TryGetInt32(out var n) ? n : 1;
        return count > 0 && choices is { Count: 1 } &&
            choices[0] is { RequirementId: "selection" } choice &&
            !string.IsNullOrWhiteSpace(choice.CatalogEligibilityEvidenceId) &&
            choice.SelectedOptionIds is { } selected && selected.Count == count &&
            selected.Distinct(StringComparer.Ordinal).Count() == count &&
            selected.All(option => IsMember(snapshot, menu, option));
    }

    // Match a projected selection against the exact authenticated donor
    // occurrence carried in the evaluated proposal, not an ancestral menu.
    internal static bool MatchesInheritedChoice(NativeOriginConstructionResult result,
        NativeOriginCandidate candidate, NativeOriginChoiceSelection selected)
    {
        var donors = result.EvaluatedProposal.Contributors.Where(c =>
            c.Id == candidate.ContributorId &&
            c.Source.Kind == NativeOriginSourceKind.ManifestedProfile &&
            !string.IsNullOrWhiteSpace(c.Source.Version) &&
            c.ManifestedProfile is not null).ToArray();
        if (donors.Length != 1) return false;
        var donor = donors[0];
        var snapshot = donor.ManifestedProfile!;
        bool Matches(IReadOnlyList<NativeOriginChoiceSelection>? choices) =>
            choices is { Count: 1 } && choices[0] is { } choice &&
            choice.RequirementId == selected.RequirementId &&
            choice.CatalogEligibilityEvidenceId == selected.CatalogEligibilityEvidenceId &&
            choice.SelectedOptionIds is not null && selected.SelectedOptionIds is not null &&
            choice.SelectedOptionIds.SequenceEqual(selected.SelectedOptionIds, StringComparer.Ordinal);
        return (snapshot.BiologicalGenes ?? []).Count(g =>
            candidate.Id == "nested:" + donor.Id + ":gene:" + g.Id &&
            candidate.Identity == g.Identity && Matches(g.ResolvedChoices)) +
            snapshot.InherentGrants.Count(g =>
                candidate.Id == "nested:" + donor.Id + ":" + g.Id &&
                candidate.Identity == g.Identity && Matches(g.ResolvedChoices)) == 1;
    }

    public static bool IsNativeCantrip(NativeCatalogSnapshot snapshot, string optionId) =>
        !string.IsNullOrWhiteSpace(optionId) && optionId.Length <= 160 &&
        snapshot.TryGetById(optionId, out var entity) && entity is not null &&
        Selectable(entity.Definition) && IsCantrip(entity);

    public static NativeOriginChoiceCatalogView Discover(NativeCatalogSnapshot snapshot, string catalogId)
    {
        var category = catalogId switch
        {
            "any-native-skill" => "skill",
            "spell:wizard" => "spell",
            "feat:origin" => "feat",
            _ => null
        };
        if (category is null) return new(catalogId, "unsupported", false, []);
        var options = new List<NativeOriginChoiceOption>();
        for (var offset = 0; offset < snapshot.CountByCategory(category); offset += 100)
            foreach (var entity in snapshot.Search(category: category, offset: offset, limit: 100).Items)
                if (IsMember(snapshot, catalogId, entity.Id))
                    options.Add(new(entity.Id, entity.Name));
        return new(catalogId, options.Count == 0 ? "catalog-unavailable" : "available",
            category == "feat", options.ToArray());
    }

    private static bool IsCantrip(NativeRuleEntity entity) =>
        entity.Category == "spell" && entity.Id.StartsWith("spell.", StringComparison.Ordinal) &&
        entity.Definition.TryGetProperty("level", out var level) &&
        level.ValueKind == JsonValueKind.Number && level.TryGetInt32(out var value) && value == 0;

    // List identity is explicitly authored in native spell records. Do not
    // infer membership from a title, a class feature, or historical SRD lists.
    private static bool HasExplicitList(JsonElement definition, string list)
    {
        if (!definition.TryGetProperty("spellListIds", out var field) ||
            field.ValueKind != JsonValueKind.Array) return false;
        var values = field.EnumerateArray().ToArray();
        if (values.Length == 0 || values.Any(item => item.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(item.GetString()))) return false;
        var ids = values.Select(item => item.GetString()!).ToArray();
        return ids.Distinct(StringComparer.Ordinal).Count() == ids.Length &&
            ids.Contains(list, StringComparer.Ordinal);
    }

    private static bool Selectable(JsonElement definition) =>
        !definition.TryGetProperty("selectable", out var flag) || flag.ValueKind == JsonValueKind.True;

    private static string? Text(JsonElement definition, string key) =>
        definition.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String
            ? field.GetString() : null;
}

/// <summary>
/// Separate character-specific authority for an already native Origin feat.
/// Verify prerequisites, repetition, and other owned/proposed feat grants;
/// never approve a feat merely because it is listed in the catalog.
/// </summary>
public interface INativeOriginFeatChoiceAuthority
{
    bool Authorizes(NativeOriginCandidate candidate, string featId);
}
