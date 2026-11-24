using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Read-only interpretation of an explicitly selected, native Origin skill
/// proficiency. This grants neither skill ranks nor a new Skill selection:
/// the Origin constructor must first authorize the complete character.
/// </summary>
public static class NativeOriginSkillProficiencyProjector
{
    public sealed record GrantedProficiency(
        string SkillId, string GrantId, string DefinitionId,
        string SourceId, string OccurrencePath,
        IReadOnlyList<string> SourceCandidateIds);

    public sealed record View(
        string Status, IReadOnlyList<GrantedProficiency> Proficiencies,
        IReadOnlyList<string> UnresolvedReasons, bool PreviewOnly = true);

    public static View Project(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result) =>
        ProjectCore(catalog, result, approvedMixed: false);

    /// <summary>
    /// Report only a complete mixed Origin's individually selected native
    /// grants after the separate mixed foundation and contributor verifier
    /// succeeds. No first-party biological transmission eligibility is added.
    /// </summary>
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
                ? "mixed-native-foundation-unverified"
                : "direct-native-foundation-unverified");

        var projected = new List<GrantedProficiency>();
        foreach (var grant in profile.Underlying.InherentGrants)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait)
                continue;

            var looksLikeSkill = grant.EffectiveValue is
                { ValueKind: JsonValueKind.Object } effect &&
                effect.TryGetProperty("grantType", out var offeredType) &&
                offeredType.ValueKind == JsonValueKind.String &&
                offeredType.GetString() == "skill-proficiency";
            var definitionId = grant.Identity.MechanicalDefinitionId;
            var found = definitionId is not null &&
                catalog.TryGetById(definitionId, out var definition) &&
                definition is not null && definition.Category == "origin-trait" &&
                definition.Definition.TryGetProperty("trait", out var trait) &&
                trait.ValueKind == JsonValueKind.Object;
            if (!found)
            {
                if (looksLikeSkill)
                    return Unresolved("skill-proficiency-definition-unverified");
                continue;
            }

            catalog.TryGetById(definitionId!, out var entity);
            var canonical = entity!.Definition.GetProperty("trait");
            var authoredSkill = String(canonical, "grantType") == "skill-proficiency";
            if (!authoredSkill && !looksLikeSkill)
                continue;
            if (!authoredSkill || !looksLikeSkill ||
                grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } actual ||
                !JsonElement.DeepEquals(canonical, actual) ||
                String(canonical, "chooseFrom") != "any-native-skill" ||
                Number(canonical, "choiceCount") != 1 ||
                String(canonical, "when") != "character-creation" ||
                grant.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
                !catalog.TryGetById(grant.Identity.Occurrence.Source.Id, out var owner) ||
                owner is null || owner.Category is not ("species" or "ancestry") ||
                !VerifiedGrant(result, grant, approvedMixed, out var skillId) ||
                !IsSelectableSkill(catalog, skillId))
                return Unresolved("skill-proficiency-not-verified:" + definitionId);

            projected.Add(new(skillId!, grant.Id, definitionId!,
                grant.Identity.Occurrence.Source.Id,
                grant.Identity.Occurrence.OccurrencePath,
                grant.SourceCandidateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()));
        }

        // Two distinct effective traits selecting the same proficiency do not
        // establish two ranks or an upgrade. Do not silently count duplicates
        // as a stronger benefit or resolve an unapproved overlap policy.
        if (projected.GroupBy(x => x.SkillId, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
            return Unresolved("duplicate-skill-proficiency-requires-reconciliation");

        return new(projected.Count == 0 ? "none" : "resolved",
            projected.OrderBy(x => x.SkillId, StringComparer.Ordinal)
                .ThenBy(x => x.GrantId, StringComparer.Ordinal).ToArray(), []);
    }

    private static bool VerifiedGrant(NativeOriginConstructionResult result,
        NativeOriginEffectiveGrant grant, bool mixed, out string? skillId)
    {
        skillId = null;
        var candidates = result.EvaluatedProposal.Candidates;
        if (mixed)
        {
            if (!NativeOriginApprovedMixedIntrinsicProjector
                .HasVerifiedContributingEvidence(result, grant))
                return false;
        }
        else
        {
            if (grant.Authorization?.Adjudication != NativeOriginAdjudication.Supported ||
                grant.Authorization.NativeRuleReference !=
                    grant.Identity.Occurrence.Source.Id ||
                grant.SourceCandidateIds is not { Count: 1 })
                return false;
        }

        var selectedCandidates = candidates.Where(candidate =>
            grant.SourceCandidateIds.Contains(candidate.Id, StringComparer.Ordinal))
            .ToArray();
        if (selectedCandidates.Length != grant.SourceCandidateIds.Count ||
            selectedCandidates.Any(candidate =>
                candidate.Identity != grant.Identity ||
                (!mixed && (candidate.ContributorId is not null ||
                    candidate.SelectionBasis.Eligibility != NativeOriginEligibility.Eligible)) ||
                candidate.OfferedValue is not { } offered ||
                grant.EffectiveValue is not { } actual ||
                !JsonElement.DeepEquals(offered, actual)))
            return false;

        if (grant.ResolvedChoices is not { Count: 1 } effective ||
            effective[0].RequirementId != "selection" ||
            effective[0].SelectedOptionIds is not { Count: 1 } options ||
            string.IsNullOrWhiteSpace(options[0]) ||
            string.IsNullOrWhiteSpace(effective[0].CatalogEligibilityEvidenceId))
            return false;

        foreach (var candidate in selectedCandidates)
        {
            var manifestations = result.EvaluatedProposal.Manifestations
                .Where(x => x.CandidateId == candidate.Id).ToArray();
            if (manifestations.Length != 1) return false;
            if (mixed && (candidate.RequiredChoices?.Count ?? 0) == 0)
            {
                if ((manifestations[0].Choices?.Count ?? 0) != 0 ||
                    !NativeOriginChoiceCatalog.MatchesInheritedChoice(result, candidate, effective[0]))
                    return false;
                continue;
            }
            if (candidate.RequiredChoices is not { Count: 1 } requirements ||
                requirements[0].Id != "selection" ||
                requirements[0].Timing != NativeOriginChoiceTiming.Creation ||
                requirements[0].OptionCatalogId != "any-native-skill" ||
                requirements[0].Minimum != 1 || requirements[0].Maximum != 1 ||
                manifestations[0].Choices is not { Count: 1 } chosen ||
                chosen[0].RequirementId != effective[0].RequirementId ||
                chosen[0].SelectedOptionIds is not { Count: 1 } selected ||
                selected[0] != options[0] ||
                chosen[0].CatalogEligibilityEvidenceId !=
                    effective[0].CatalogEligibilityEvidenceId)
                return false;
        }
        skillId = options[0];
        return true;
    }

    private static bool IsSelectableSkill(NativeCatalogSnapshot catalog, string? id)
    {
        if (id is null || !catalog.TryGetById(id, out var option) ||
            option is null || option.Category != "skill")
            return false;
        return !option.Definition.TryGetProperty("selectable", out var enabled) ||
            enabled.ValueKind != JsonValueKind.False;
    }

    private static string? String(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.String ? field.GetString() : null;

    private static int? Number(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.Number &&
        field.TryGetInt32(out var number) ? number : null;

    private static View Unresolved(string reason) => new("unresolved", [], [reason]);
}
