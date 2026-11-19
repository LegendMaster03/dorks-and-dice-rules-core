using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Source-backed, read-only interpretation of the two reviewed native resting
/// and survival adaptations from an already authorized initial Origin. This
/// is not a rest executor, hit point mutation, resource ledger or independent
/// authority to inherit a trait.
/// </summary>
public static class NativeOriginRestSurvivalProjector
{
    public sealed record TranceEffect(
        string GrantId, string SourceId, string OccurrencePath,
        int LongRestHours, bool SleepRequired);

    public sealed record RelentlessEnduranceEffect(
        string GrantId, string SourceId, string OccurrencePath,
        string Trigger, int HitPointsInstead, int Uses,
        string Recharge);

    public sealed record View(
        string Status, TranceEffect? Trance,
        RelentlessEnduranceEffect? RelentlessEndurance,
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

        const string tranceId = "trait.elf.trance";
        const string enduranceId = "trait.orc.relentless-endurance";

        // A changed canonical trait must not become an absent benefit merely
        // because its stored definition ID or executable payload is removed.
        var chosen = profile.Underlying.InherentGrants.Where(grant =>
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait)
                return false;
            if (grant.Identity.MechanicalDefinitionId is tranceId or enduranceId)
                return true;
            return grant.EffectiveValue is { ValueKind: JsonValueKind.Object } value &&
                (value.TryGetProperty("longRestHours", out _) ||
                 value.TryGetProperty("hitPointsInstead", out _));
        }).ToArray();

        if (chosen.GroupBy(x => x.Identity.MechanicalDefinitionId,
                StringComparer.Ordinal).Any(group => group.Count() > 1))
            return Unresolved("duplicate-native-rest-survival-grant");

        TranceEffect? trance = null;
        RelentlessEnduranceEffect? endurance = null;
        foreach (var grant in chosen)
        {
            var id = grant.Identity.MechanicalDefinitionId;
            if (id is not (tranceId or enduranceId) ||
                grant.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
                !catalog.TryGetById(grant.Identity.Occurrence.Source.Id, out var source) ||
                source is null || source.Category is not ("species" or "ancestry") ||
                !catalog.TryGetById(id, out var definition) ||
                definition is null || definition.Category != "origin-trait" ||
                !definition.Definition.TryGetProperty("trait", out var canonical) ||
                canonical.ValueKind != JsonValueKind.Object ||
                grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } value ||
                !JsonElement.DeepEquals(canonical, value) ||
                !VerifiedSelectedGrant(grant))
                return Unresolved("rest-survival-grant-not-verified:" + id);

            var occurrence = grant.Identity.Occurrence;
            if (id == tranceId)
            {
                if (!canonical.TryGetProperty("longRestHours", out var duration) ||
                    duration.ValueKind != JsonValueKind.Number ||
                    !duration.TryGetInt32(out var hours) || hours != 4 ||
                    !canonical.TryGetProperty("sleepRequired", out var sleep) ||
                    sleep.ValueKind != JsonValueKind.False)
                    return Unresolved("trance-native-grammar-unrecognized");

                trance = new(grant.Id, occurrence.Source.Id,
                    occurrence.OccurrencePath, hours, SleepRequired: false);
            }
            else
            {
                if (!canonical.TryGetProperty("trigger", out var trigger) ||
                    trigger.ValueKind != JsonValueKind.String ||
                    trigger.GetString() !=
                        "reduced-to-zero-hit-points-but-not-killed-outright" ||
                    !canonical.TryGetProperty("hitPointsInstead", out var hitPoints) ||
                    hitPoints.ValueKind != JsonValueKind.Number ||
                    !hitPoints.TryGetInt32(out var remaining) || remaining != 1 ||
                    !canonical.TryGetProperty("uses", out var uses) ||
                    uses.ValueKind != JsonValueKind.Object ||
                    !uses.TryGetProperty("value", out var count) ||
                    count.ValueKind != JsonValueKind.Number ||
                    !count.TryGetInt32(out var available) || available != 1 ||
                    !uses.TryGetProperty("recharge", out var rest) ||
                    rest.ValueKind != JsonValueKind.String ||
                    rest.GetString() != "long-rest")
                    return Unresolved("relentless-endurance-native-grammar-unrecognized");

                endurance = new(grant.Id, occurrence.Source.Id,
                    occurrence.OccurrencePath, trigger.GetString()!,
                    remaining, available, rest.GetString()!);
            }
        }

        return new(chosen.Length == 0 ? "none" : "resolved",
            trance, endurance, []);

        bool VerifiedSelectedGrant(NativeOriginEffectiveGrant grant)
        {
            if (approvedMixed)
                return NativeOriginApprovedMixedIntrinsicProjector
                    .HasVerifiedContributingEvidence(result, grant);

            return grant.Authorization?.Adjudication == NativeOriginAdjudication.Supported &&
                grant.Authorization.NativeRuleReference ==
                    grant.Identity.Occurrence.Source.Id &&
                grant.SourceCandidateIds.Count == 1 &&
                result.EvaluatedProposal.Candidates.Any(candidate =>
                    candidate.Id == grant.SourceCandidateIds[0] &&
                    candidate.ContributorId is null &&
                    candidate.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                    candidate.Identity == grant.Identity &&
                    candidate.OfferedValue is { } offered &&
                    grant.EffectiveValue is { } effective &&
                    JsonElement.DeepEquals(offered, effective) &&
                    result.EvaluatedProposal.Manifestations.Count(manifest =>
                        manifest.CandidateId == candidate.Id) == 1);
        }
    }

    private static View Unresolved(string reason) =>
        new("unresolved", null, null, [reason]);
}
