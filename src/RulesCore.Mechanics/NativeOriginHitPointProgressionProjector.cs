using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A read-only projection of an already authorized, selected intrinsic Origin
/// trait with a canonical per-character-level maximum-HP effect. It does not
/// authorize biological inheritance, award Hit Dice, or modify class HP gains.
/// The result is a preview until the host verifies the saved Origin and character.
/// </summary>
public static class NativeOriginHitPointProgressionProjector
{
    public sealed record View(
        string Status, int? CharacterLevel, int? PerLevelBonus,
        int? MaximumHitPointBonus, int? CombinedMaximumHitPoints,
        string? GrantId, IReadOnlyList<string> UnresolvedReasons,
        bool PreviewOnly = true);

    /// <summary>
    /// Report the full committed native trait at a supplied character level,
    /// without needing a particular class, feat, or generated Hit Die result.
    /// One consolidated grant is one effect even with several contributors.
    /// </summary>
    public static View Project(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult origin, int? characterLevel)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(origin);
        if (origin.Status != NativeOriginConstructionStatus.Authorized ||
            origin.Profile is not { } profile ||
            origin.Issues.Count != 0 ||
            profile.ActiveTemporaryTransformation is not null ||
            (profile.OtherActiveTemporaryTransformations?.Count ?? 0) != 0 ||
            !NativeOriginContractValidator.SameConfiguration(
                profile.Underlying, profile.Current))
            return Unresolved("origin-not-authorized-current-baseline");

        var mixed = origin.EvaluatedProposal.Intent == NativeOriginIntent.Mixed;
        if (!mixed && origin.EvaluatedProposal.Intent != NativeOriginIntent.Direct)
            return Unresolved("unsupported-origin-intent");
        if (mixed &&
            NativeOriginApprovedMixedIntrinsicProjector.Project(origin).Status != "resolved")
            return Unresolved("mixed-foundation-unverified");

        // Select by canonical mechanic, but do not treat an unknown serialized
        // property as a grant. Unsupported future HP effect types remain blocked.
        var selected = profile.Underlying.InherentGrants.Where(grant =>
            grant.Identity.Kind == NativeOriginCharacteristicKind.Trait &&
            ((grant.EffectiveValue is { ValueKind: JsonValueKind.Object } value &&
              value.TryGetProperty("hitPointsPerCharacterLevel", out _)) ||
             (grant.Identity.MechanicalDefinitionId is { } id &&
              catalog.TryGetById(id, out var definition) &&
              definition is not null && definition.Category == "origin-trait" &&
              definition.Definition.TryGetProperty("trait", out var trait) &&
              trait.ValueKind == JsonValueKind.Object &&
              trait.TryGetProperty("hitPointsPerCharacterLevel", out _))))
            .ToArray();
        if (selected.Length == 0)
            return characterLevel is > 0 and <= 1000
                ? new("none", characterLevel, 0, 0, null, null, [])
                : Unresolved("character-level-required");
        if (selected.Length != 1)
            return Unresolved("multiple-hp-progressions-require-reconciliation");

        var grant = selected[0];
        if (grant.Identity.MechanicalDefinitionId is not { } traitId ||
            !catalog.TryGetById(traitId, out var canonical) ||
            canonical is null || canonical.Category != "origin-trait" ||
            !canonical.Definition.TryGetProperty("trait", out var authored) ||
            authored.ValueKind != JsonValueKind.Object ||
            !authored.TryGetProperty("hitPointsPerCharacterLevel", out var rate) ||
            !rate.TryGetInt32(out var perLevel) || perLevel is <= 0 or > 1_000_000 ||
            grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } actual ||
            !JsonElement.DeepEquals(actual, authored) ||
            grant.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
            !catalog.TryGetById(grant.Identity.Occurrence.Source.Id, out var source) ||
            source is null || source.Category is not ("species" or "ancestry") ||
            !HasVerifiedGrant(origin, grant, mixed))
            return Unresolved("hp-progressions-not-verified");
        if (characterLevel is null)
            return Unresolved("character-level-required");
        if (characterLevel is <= 0 or > 1000)
            return Unresolved("invalid-character-level");
        var bonus = (long)perLevel * characterLevel.Value;
        if (bonus > int.MaxValue)
            return Unresolved("hp-bonus-overflow");
        return new("resolved", characterLevel, perLevel, (int)bonus,
            null, grant.Id, []);
    }

    /// <summary>
    /// Combine an actual native character HP projection with the projected
    /// Origin bonus, using the *same* resolved total character level. This does
    /// not change the character request's per-level Hit Point Gains or assert
    /// that any client-provided state has been authenticated.
    /// </summary>
    public static View Combine(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult origin,
        NativeCharacterProjection character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (character.RulesetId != catalog.RulesetId ||
            character.Fingerprint != catalog.Fingerprint ||
            !character.Values.TryGetValue("advancement.total-level", out var levels) ||
            levels.Status != "resolved" || levels.Value is not > 0)
            return Unresolved("character-advancement-unresolved");
        var projected = Project(catalog, origin, levels.Value);
        if (projected.Status is not ("resolved" or "none"))
            return projected;
        if (!character.Values.TryGetValue("hit-points.maximum", out var hitPoints) ||
            hitPoints.Status != "resolved" || hitPoints.Value is not >= 0)
            return Unresolved("character-hit-points-unresolved");
        var sum = (long)hitPoints.Value.Value + projected.MaximumHitPointBonus!.Value;
        if (sum > int.MaxValue)
            return Unresolved("combined-hp-overflow");
        return projected with { CombinedMaximumHitPoints = (int)sum };
    }

    private static bool HasVerifiedGrant(NativeOriginConstructionResult result,
        NativeOriginEffectiveGrant grant, bool mixed)
    {
        if (mixed)
            return NativeOriginApprovedMixedIntrinsicProjector
                .HasVerifiedContributingEvidence(result, grant);
        if (grant.Authorization?.Adjudication != NativeOriginAdjudication.Supported ||
            grant.SourceCandidateIds.Count != 1)
            return false;
        var candidateId = grant.SourceCandidateIds[0];
        return result.EvaluatedProposal.Candidates.Any(x =>
            x.Id == candidateId && x.ContributorId is null &&
            x.Identity == grant.Identity &&
            x.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
            result.EvaluatedProposal.Manifestations.Count(m =>
                m.CandidateId == candidateId) == 1);
    }

    private static View Unresolved(string reason) =>
        new("unresolved", null, null, null, null, null, [reason]);
}
