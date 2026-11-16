using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Projects two already-authored, persistent physiological adaptations from a
/// completed initial Origin. This is an authenticated-construction consumer,
/// not source authentication, inheritance eligibility, or runtime adjudication.
/// </summary>
public static class NativeOriginPhysiologyProjector
{
    public sealed record AmphibiousEffect(
        string GrantId, string SourceId, string OccurrencePath,
        IReadOnlyList<string> CanBreathe, bool GrantsSwimSpeed);

    public sealed record AltitudeAcclimationEffect(
        string GrantId, string SourceId, string OccurrencePath,
        int IncludesElevationAboveFeet, int? AltitudeLimitFeet,
        bool MaintainsAcclimationAtLowerElevations,
        IReadOnlyList<string> DoesNotGrant);

    public sealed record View(
        string Status, AmphibiousEffect? Amphibious,
        AltitudeAcclimationEffect? AltitudeAcclimation,
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
                ? "mixed-native-intrinsics-unverified"
                : "direct-native-intrinsics-unverified");

        // Keep this interpreter limited to the two canonical traits whose
        // physiological payloads have been reviewed. Other movement modes,
        // flight, conditions, or adaptation claims need independent handling.
        const string amphibiousId = "trait.shared.amphibious";
        const string altitudeId = "trait.goliath.altitude-acclimation";
        var selected = profile.Current.InherentGrants.Where(grant =>
            grant.Identity.Kind == NativeOriginCharacteristicKind.Trait &&
            grant.Identity.MechanicalDefinitionId is amphibiousId or altitudeId)
            .ToArray();

        if (selected.GroupBy(grant => grant.Identity.MechanicalDefinitionId,
                StringComparer.Ordinal).Any(group => group.Count() != 1))
            return Unresolved("duplicate-physiological-adaptation");

        AmphibiousEffect? amphibious = null;
        AltitudeAcclimationEffect? altitude = null;
        foreach (var grant in selected)
        {
            var id = grant.Identity.MechanicalDefinitionId!;
            if (!VerifyNativeGrant(grant))
                return Unresolved("physiological-adaptation-not-verified:" + id);
            if (!catalog.TryGetById(id, out var native) ||
                native is null || native.Category != "origin-trait" ||
                !native.Definition.TryGetProperty("trait", out var canonical) ||
                canonical.ValueKind != JsonValueKind.Object ||
                grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } actual ||
                !JsonElement.DeepEquals(canonical, actual))
                return Unresolved("physiological-adaptation-payload-invalid:" + id);

            var source = grant.Identity.Occurrence.Source.Id;
            var path = grant.Identity.Occurrence.OccurrencePath;
            if (id == amphibiousId)
            {
                if (!canonical.TryGetProperty("canBreathe", out var environments) ||
                    environments.ValueKind != JsonValueKind.Array ||
                    environments.GetArrayLength() != 2 ||
                    environments.EnumerateArray().Any(x =>
                        x.ValueKind != JsonValueKind.String) ||
                    !environments.EnumerateArray().Select(x => x.GetString()!)
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .SequenceEqual(new[] { "air", "water" }) ||
                    !canonical.TryGetProperty("grantsSwimSpeed", out var swim) ||
                    swim.ValueKind != JsonValueKind.False)
                    return Unresolved("amphibious-grammar-unrecognized");

                amphibious = new(grant.Id, source, path,
                    environments.EnumerateArray().Select(x => x.GetString()!).ToArray(),
                    GrantsSwimSpeed: false);
            }
            else
            {
                if (!canonical.TryGetProperty("naturallyAcclimated", out var natural) ||
                    natural.ValueKind != JsonValueKind.True ||
                    !canonical.TryGetProperty("includesElevationAboveFeet", out var above) ||
                    !above.TryGetInt32(out var elevation) || elevation <= 0 ||
                    !canonical.TryGetProperty("altitudeLimitFeet", out var limit) ||
                    limit.ValueKind != JsonValueKind.Null ||
                    !canonical.TryGetProperty("maintainsAcclimationAtLowerElevations",
                        out var retained) ||
                    retained.ValueKind != JsonValueKind.True ||
                    !canonical.TryGetProperty("doesNotGrant", out var exclusions) ||
                    exclusions.ValueKind != JsonValueKind.Array ||
                    exclusions.EnumerateArray().Any(x =>
                        x.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(x.GetString())))
                    return Unresolved("altitude-acclimation-grammar-unrecognized");

                var denied = exclusions.EnumerateArray()
                    .Select(x => x.GetString()!).ToArray();
                if (!denied.Contains("cold-damage-resistance", StringComparer.Ordinal) ||
                    !denied.Contains("suffocation-immunity", StringComparer.Ordinal))
                    return Unresolved("altitude-acclimation-exclusions-unrecognized");

                altitude = new(grant.Id, source, path, elevation,
                    AltitudeLimitFeet: null,
                    MaintainsAcclimationAtLowerElevations: true, denied);
            }
        }

        return new(selected.Length == 0 ? "none" : "resolved",
            amphibious, altitude, []);

        bool VerifyNativeGrant(NativeOriginEffectiveGrant grant)
        {
            var nativeSource = grant.Identity.Occurrence.Source;
            if (nativeSource.Kind != NativeOriginSourceKind.Native ||
                !catalog.TryGetById(nativeSource.Id, out var source) ||
                source is null || source.Category is not ("species" or "ancestry"))
                return false;

            if (approvedMixed)
                return NativeOriginApprovedMixedIntrinsicProjector
                    .HasVerifiedContributingEvidence(result, grant);

            return grant.Authorization?.Adjudication == NativeOriginAdjudication.Supported &&
                grant.SourceCandidateIds.Count == 1 &&
                result.EvaluatedProposal.Candidates.Any(candidate =>
                    candidate.Id == grant.SourceCandidateIds[0] &&
                    candidate.ContributorId is null &&
                    candidate.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                    candidate.Identity == grant.Identity &&
                    candidate.OfferedValue is { } offered &&
                    grant.EffectiveValue is { } effective &&
                    JsonElement.DeepEquals(offered, effective));
        }
    }

    private static View Unresolved(string reason) =>
        new("unresolved", null, null, [reason]);
}
