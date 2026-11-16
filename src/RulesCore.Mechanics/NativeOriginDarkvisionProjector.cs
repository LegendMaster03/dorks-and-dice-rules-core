using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Projects the exact native, intrinsic Darkvision range from a completed,
/// verified initial Origin. No vision-through-magical-darkness, light-level
/// transformation, extended range or free trait inheritance is inferred.
/// </summary>
public static class NativeOriginDarkvisionProjector
{
    public sealed record GrantedDarkvision(
        int RangeFeet, string GrantId, string SourceId, string OccurrencePath);

    public sealed record View(
        string Status, GrantedDarkvision? Darkvision,
        IReadOnlyList<string> UnresolvedReasons, bool PreviewOnly = true);

    public static View Project(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result) => ProjectCore(catalog, result, false);

    public static View ProjectApprovedMixed(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result) => ProjectCore(catalog, result, true);

    private static View ProjectCore(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result, bool mixed)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(result);

        var foundation = mixed ?
            NativeOriginApprovedMixedIntrinsicProjector.Project(result) :
            NativeOriginIntrinsicProjector.Project(result);
        if (foundation.Status != "resolved" ||
            result.Profile is not { } profile)
            return new("unresolved", null,
                [mixed ? "mixed-intrinsics-not-verified" :
                    "direct-intrinsics-not-verified"]);

        var grants = profile.Current.InherentGrants.Where(x =>
            x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
            x.Identity.IntrinsicSlotId == "darkvisionFeet").ToArray();
        if (grants.Length == 0)
            return new("none", null, []);
        if (grants.Length != 1)
            return new("unresolved", null, ["duplicate-darkvision"]);

        var grant = grants[0];
        var source = grant.Identity.Occurrence.Source;
        var evidence = mixed ?
            NativeOriginApprovedMixedIntrinsicProjector.HasVerifiedContributingEvidence(
                result, grant) :
            grant.SourceCandidateIds.Count == 1 &&
            result.EvaluatedProposal.Candidates.Any(candidate =>
                candidate.Id == grant.SourceCandidateIds[0] &&
                candidate.ContributorId is null &&
                candidate.Identity == grant.Identity &&
                candidate.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                candidate.OfferedValue is { } offered &&
                grant.EffectiveValue is { } effective &&
                JsonElement.DeepEquals(offered, effective));

        if (source.Kind != NativeOriginSourceKind.Native ||
            !catalog.TryGetById(source.Id, out var native) ||
            native is null || native.Category is not ("species" or "ancestry") ||
            !native.Definition.TryGetProperty("darkvisionFeet", out var original) ||
            original.ValueKind != JsonValueKind.Number ||
            !original.TryGetInt32(out var authoredRange) ||
            authoredRange is <= 0 or > 1_000_000 ||
            grant.EffectiveValue is not { ValueKind: JsonValueKind.Number } value ||
            !value.TryGetInt32(out var actualRange) ||
            actualRange != authoredRange ||
            grant.Authorization?.Adjudication is not
                (NativeOriginAdjudication.DmAuthorized or NativeOriginAdjudication.Supported) ||
            !evidence)
            return new("unresolved", null, ["darkvision-not-verified"]);

        return new("resolved", new(actualRange, grant.Id,
            source.Id, grant.Identity.Occurrence.OccurrencePath), []);
    }
}
