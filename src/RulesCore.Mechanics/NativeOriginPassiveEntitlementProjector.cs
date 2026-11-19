using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Projects source-neutral, always-on saving-throw advantages and fixed skill
/// bonuses from selected native Origin traits. Each effect remains attached to
/// its complete canonical trait: this view neither stacks overlapping benefits
/// nor evaluates a saving throw or skill check. Construction authorization is
/// an input, not a conclusion of this projector.
/// </summary>
public static class NativeOriginPassiveEntitlementProjector
{
    public sealed record SavingThrowAdvantage(
        IReadOnlyList<string> Against, IReadOnlyList<string> Abilities, string? Scope);

    public sealed record SkillBonus(string SkillId, int Amount);

    public sealed record GrantedTrait(
        string GrantId, string DefinitionId, string SourceId, string OccurrencePath,
        IReadOnlyList<string> SourceCandidateIds,
        SavingThrowAdvantage? SavingThrows, IReadOnlyList<SkillBonus> SkillBonuses,
        JsonElement CompleteEffect);

    public sealed record View(
        string Status, IReadOnlyList<GrantedTrait> Traits,
        IReadOnlyList<string> UnresolvedReasons, bool PreviewOnly = true);

    public static View Project(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result) =>
        ProjectCore(catalog, result, approvedMixed: false);

    public static View ProjectApprovedMixed(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result) =>
        ProjectCore(catalog, result, approvedMixed: true);

    private static View ProjectCore(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result, bool approvedMixed)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(result);

        var foundation = approvedMixed
            ? NativeOriginApprovedMixedIntrinsicProjector.Project(result)
            : NativeOriginIntrinsicProjector.Project(result);
        if (foundation.Status != "resolved" || result.Profile is not { } profile)
            return Unresolved(approvedMixed
                ? "mixed-origin-foundation-unverified"
                : "direct-origin-foundation-unverified");

        var projected = new List<GrantedTrait>();
        var seenGrants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grant in profile.Underlying.InherentGrants)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait)
                continue;

            var definitionId = grant.Identity.MechanicalDefinitionId;
            NativeRuleEntity? definition = null;
            if (definitionId is not null)
                catalog.TryGetById(definitionId, out definition);
            var hasCanonical = definition is not null &&
                definition.Category == "origin-trait" &&
                definition.Definition.TryGetProperty("trait", out var authored) &&
                authored.ValueKind == JsonValueKind.Object;
            var canonical = hasCanonical
                ? definition!.Definition.GetProperty("trait") : default;
            var canonicalClaims = hasCanonical && ClaimsSupportedShape(canonical);
            var effectiveClaims = grant.EffectiveValue is
                { ValueKind: JsonValueKind.Object } effect &&
                ClaimsSupportedShape(effect);
            if (!canonicalClaims && !effectiveClaims)
                continue;

            // Inspect both authored and serialized shapes. Otherwise a forged
            // payload could add an entitlement to an unrelated definition, or
            // remove an authored entitlement to evade provenance validation.
            if (!canonicalClaims || !effectiveClaims || definitionId is null ||
                !seenGrants.Add(grant.Id) ||
                grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } actual ||
                !JsonElement.DeepEquals(canonical, actual) ||
                grant.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
                !catalog.TryGetById(grant.Identity.Occurrence.Source.Id, out var source) ||
                source is null || source.Category is not ("species" or "ancestry") ||
                grant.Parameters is { Count: > 0 } ||
                grant.ResourceOwners is { Count: > 0 } ||
                grant.ResolvedChoices is { Count: > 0 } ||
                // This increment supports only unconditional creation-time
                // passives. A future level gate must not activate by default.
                canonical.TryGetProperty("requiredCharacterLevel", out _) ||
                !VerifiedSelectedGrant(result, grant, approvedMixed))
                return Unresolved("passive-entitlement-grant-not-verified:" +
                    (definitionId ?? grant.Id));

            SavingThrowAdvantage? savingThrows = null;
            if (canonical.TryGetProperty("savingThrows", out var saves) &&
                !TrySavingThrows(saves, out savingThrows))
                return Unresolved("saving-throw-grammar-unrecognized:" + definitionId);

            IReadOnlyList<SkillBonus> skillBonuses = [];
            if (canonical.TryGetProperty("skillBonuses", out var skills) &&
                !TrySkillBonuses(catalog, skills, out skillBonuses))
                return Unresolved("skill-bonus-grammar-unrecognized:" + definitionId);

            // No combined bonus, chosen "best" effect, proficiency, or
            // character-state mutation is implied by this collection.
            projected.Add(new(grant.Id, definitionId,
                grant.Identity.Occurrence.Source.Id,
                grant.Identity.Occurrence.OccurrencePath,
                grant.SourceCandidateIds.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                savingThrows, skillBonuses, canonical.Clone()));
        }

        return new(projected.Count == 0 ? "none" : "resolved",
            projected.OrderBy(x => x.DefinitionId, StringComparer.Ordinal)
                .ThenBy(x => x.SourceId, StringComparer.Ordinal)
                .ThenBy(x => x.GrantId, StringComparer.Ordinal).ToArray(), []);
    }

    private static bool ClaimsSupportedShape(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object &&
        (value.TryGetProperty("savingThrows", out _) ||
         value.TryGetProperty("skillBonuses", out _));

    private static bool TrySavingThrows(JsonElement value,
        out SavingThrowAdvantage? advantage)
    {
        advantage = null;
        if (value.ValueKind != JsonValueKind.Object) return false;
        var properties = value.EnumerateObject().Select(x => x.Name).ToArray();
        if (properties.Length == 1 && properties[0] == "advantageAgainst" &&
            value.TryGetProperty("advantageAgainst", out var against) &&
            TryStrings(against, out var effects))
        {
            advantage = new(effects, [], null);
            return true;
        }

        if (properties.Length == 2 &&
            properties.Contains("advantageAbilities", StringComparer.Ordinal) &&
            properties.Contains("scope", StringComparer.Ordinal) &&
            value.TryGetProperty("scope", out var scope) &&
            scope.ValueKind == JsonValueKind.String &&
            scope.GetString() == "magic-only" &&
            value.TryGetProperty("advantageAbilities", out var abilities) &&
            TryStrings(abilities, out var chosen) &&
            chosen.All(x => x is "strength" or "dexterity" or "constitution"
                or "intelligence" or "wisdom" or "charisma"))
        {
            advantage = new([], chosen, "magic-only");
            return true;
        }
        return false;
    }

    private static bool TrySkillBonuses(NativeCatalogSnapshot catalog,
        JsonElement value, out IReadOnlyList<SkillBonus> bonuses)
    {
        bonuses = [];
        if (value.ValueKind != JsonValueKind.Object) return false;
        var fields = value.EnumerateObject().ToArray();
        if (fields.Length == 0) return false;
        var result = new List<SkillBonus>();
        foreach (var field in fields)
        {
            if (!field.Name.StartsWith("skill.", StringComparison.Ordinal) ||
                !catalog.TryGetById(field.Name, out var skill) ||
                skill is null || skill.Category != "skill" ||
                field.Value.ValueKind != JsonValueKind.Number ||
                !field.Value.TryGetInt32(out var amount) ||
                amount is <= 0 or > 1_000_000)
                return false;
            result.Add(new(field.Name, amount));
        }
        bonuses = result.OrderBy(x => x.SkillId, StringComparer.Ordinal).ToArray();
        return true;
    }

    private static bool TryStrings(JsonElement value, out IReadOnlyList<string> result)
    {
        result = [];
        if (value.ValueKind != JsonValueKind.Array ||
            value.GetArrayLength() == 0) return false;
        var values = new List<string>();
        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(element.GetString()))
                return false;
            values.Add(element.GetString()!);
        }
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            return false;
        result = values.ToArray();
        return true;
    }

    private static bool VerifiedSelectedGrant(NativeOriginConstructionResult result,
        NativeOriginEffectiveGrant grant, bool mixed)
    {
        if (mixed)
        {
            if (!NativeOriginApprovedMixedIntrinsicProjector
                .HasVerifiedContributingEvidence(result, grant))
                return false;
        }
        else if (grant.Authorization?.Adjudication != NativeOriginAdjudication.Supported ||
            grant.Authorization.NativeRuleReference !=
                grant.Identity.Occurrence.Source.Id ||
            grant.SourceCandidateIds.Count != 1)
            return false;

        if (grant.SourceCandidateIds.Count == 0 ||
            grant.SourceCandidateIds.Distinct(StringComparer.Ordinal).Count() !=
                grant.SourceCandidateIds.Count)
            return false;

        var selected = result.EvaluatedProposal.Candidates
            .Where(x => grant.SourceCandidateIds.Contains(x.Id, StringComparer.Ordinal))
            .ToArray();
        return selected.Length == grant.SourceCandidateIds.Count &&
            selected.All(candidate =>
                candidate.Identity == grant.Identity &&
                (!mixed && candidate.ContributorId is null ||
                    mixed && candidate.ContributorId is not null) &&
                (!mixed ? candidate.SelectionBasis.Eligibility ==
                    NativeOriginEligibility.Eligible
                    : candidate.SelectionBasis.Eligibility is
                        (NativeOriginEligibility.Eligible or NativeOriginEligibility.DmAuthorized)) &&
                candidate.OfferedValue is { } offered &&
                grant.EffectiveValue is { } actual &&
                JsonElement.DeepEquals(offered, actual) &&
                candidate.OfferedParameters is not { Count: > 0 } &&
                candidate.RequiredChoices is not { Count: > 0 } &&
                candidate.UnresolvedEvidence is not { Count: > 0 } &&
                result.EvaluatedProposal.Manifestations.Count(x =>
                    x.CandidateId == candidate.Id) == 1);
    }

    private static View Unresolved(string reason) =>
        new("unresolved", [], [reason]);
}
