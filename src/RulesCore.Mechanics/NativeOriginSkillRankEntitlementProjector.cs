using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Read-only native Origin skill-rank entitlement schedule. It describes the
/// additional ranks made available for player allocation; it cannot determine
/// where they were spent, override rank limits, or amend a character's skills.
/// </summary>
public static class NativeOriginSkillRankEntitlementProjector
{
    public sealed record View(
        string Status, int? CharacterLevel, int? AdditionalRanksAtLevelOne,
        int? AdditionalRanksPerLaterLevel, int? TotalEntitledExtraRanks,
        int? NewExtraRanksAtThisLevel, string? GrantId,
        IReadOnlyList<string> UnresolvedReasons, bool PreviewOnly = true);

    public static View Project(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult origin, int? characterLevel)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(origin);

        if (origin.Status != NativeOriginConstructionStatus.Authorized ||
            origin.Profile is not { } profile || origin.Issues.Count != 0 ||
            profile.ActiveTemporaryTransformation is not null ||
            (profile.OtherActiveTemporaryTransformations?.Count ?? 0) != 0 ||
            profile.IndependentGrants.Count != 0 ||
            (profile.PersistentInfluences?.Count ?? 0) != 0 ||
            !NativeOriginContractValidator.SameConfiguration(
                profile.Current, profile.Underlying))
            return Unresolved("origin-not-authorized-current-baseline");

        var mixed = origin.EvaluatedProposal.Intent == NativeOriginIntent.Mixed;
        if (mixed)
        {
            if (NativeOriginApprovedMixedIntrinsicProjector.Project(origin).Status !=
                "resolved")
                return Unresolved("mixed-foundation-unverified");
        }
        else if (origin.EvaluatedProposal.Intent != NativeOriginIntent.Direct)
            return Unresolved("unsupported-origin-intent");

        // Only actual selected inherent grants count. Unmanifested parental
        // offers and historical inherited evidence are never extra allowances.
        var grants = profile.Underlying.InherentGrants.Where(grant =>
            grant.Identity.Kind == NativeOriginCharacteristicKind.Trait &&
            grant.EffectiveValue is { ValueKind: JsonValueKind.Object } value &&
            (value.TryGetProperty("additionalRanksAtCharacterLevel1", out _) ||
             value.TryGetProperty("additionalRanksPerSubsequentCharacterLevel", out _)))
            .ToArray();
        if (grants.Length == 0)
            return characterLevel is > 0 and <= 1000
                ? new("none", characterLevel, 0, 0, 0, 0, null, [])
                : Unresolved("character-level-required");
        if (grants.Length != 1)
            return Unresolved("multiple-skill-rank-schedules-require-reconciliation");

        var grant = grants[0];
        if (grant.Identity.MechanicalDefinitionId is not { } id ||
            grant.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
            !catalog.TryGetById(id, out var definition) || definition is null ||
            definition.Category != "origin-trait" ||
            !catalog.TryGetById(grant.Identity.Occurrence.Source.Id, out var source) ||
            source is null || source.Category is not ("species" or "ancestry") ||
            !definition.Definition.TryGetProperty("trait", out var canonical) ||
            canonical.ValueKind != JsonValueKind.Object ||
            grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } current ||
            !JsonElement.DeepEquals(current, canonical) ||
            !canonical.TryGetProperty("additionalRanksAtCharacterLevel1", out var first) ||
            !first.TryGetInt32(out var initial) || initial is <= 0 or > 1_000_000 ||
            !canonical.TryGetProperty("additionalRanksPerSubsequentCharacterLevel",
                out var subsequent) ||
            !subsequent.TryGetInt32(out var later) || later is < 0 or > 1_000_000 ||
            !canonical.TryGetProperty("allocation", out var allocation) ||
            allocation.ValueKind != JsonValueKind.String ||
            allocation.GetString() != "player-choice" ||
            !canonical.TryGetProperty("obeysNormalSkillRankLimits", out var limits) ||
            limits.ValueKind != JsonValueKind.True ||
            !VerifiedSelectedGrant(origin, grant, mixed))
            return Unresolved("skill-rank-schedule-not-verified");

        if (characterLevel is null)
            return Unresolved("character-level-required");
        if (characterLevel is <= 0 or > 1000)
            return Unresolved("invalid-character-level");
        var total = (long)initial + (long)(characterLevel.Value - 1) * later;
        if (total > int.MaxValue)
            return Unresolved("skill-rank-schedule-overflow");

        // These are entitlements, not a verified allocation or a remaining
        // balance; no unapproved skill-cap formula is silently introduced.
        return new("resolved", characterLevel, initial, later, (int)total,
            characterLevel == 1 ? initial : later, grant.Id, []);
    }

    /// <summary>
    /// Use the existing native character kernel solely to obtain a verified
    /// aggregate level. Its Skills input does not yet encode a full per-level
    /// spend ledger, so no rank mutations or legality claims are made here.
    /// </summary>
    public static View ForCharacter(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult origin, NativeCharacterProjection character)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(character);
        if (character.RulesetId != catalog.RulesetId ||
            character.Fingerprint != catalog.Fingerprint ||
            !character.Values.TryGetValue("advancement.total-level", out var total) ||
            total.Status != "resolved" || total.Value is not > 0)
            return Unresolved("character-advancement-unresolved");
        return Project(catalog, origin, total.Value);
    }

    private static bool VerifiedSelectedGrant(NativeOriginConstructionResult origin,
        NativeOriginEffectiveGrant grant, bool mixed)
    {
        if (mixed)
            return NativeOriginApprovedMixedIntrinsicProjector
                .HasVerifiedContributingEvidence(origin, grant);
        if (grant.Authorization?.Adjudication != NativeOriginAdjudication.Supported ||
            grant.Authorization.NativeRuleReference != "rule.creature-origination" ||
            grant.SourceCandidateIds is not { Count: 1 })
            return false;
        var candidateId = grant.SourceCandidateIds[0];
        return origin.EvaluatedProposal.Candidates.Any(candidate =>
            candidate.Id == candidateId && candidate.ContributorId is null &&
            candidate.Identity == grant.Identity &&
            candidate.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
            candidate.OfferedValue is { } offered &&
            JsonElement.DeepEquals(offered, grant.EffectiveValue!.Value) &&
            origin.EvaluatedProposal.Manifestations.Count(manifest =>
                manifest.CandidateId == candidateId) == 1);
    }

    private static View Unresolved(string code) =>
        new("unresolved", null, null, null, null, null, null, [code]);
}
