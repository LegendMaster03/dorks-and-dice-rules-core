using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Presents complete, source-authored movement-mode traits from an already
/// authorized initial Origin. This is a read-only mechanical projection, not
/// first-party inheritance permission, combat movement execution, or trust
/// in an arbitrary serialized Origin profile.
/// </summary>
public static class NativeOriginMovementModeProjector
{
    public sealed record GrantedMode(
        string Mode, int SpeedFeet, string GrantId, string DefinitionId,
        string SourceId, string OccurrencePath, JsonElement CompleteEffect);

    public sealed record View(
        string Status, IReadOnlyList<GrantedMode> Modes,
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

        var modes = new List<GrantedMode>();
        foreach (var grant in profile.Current.InherentGrants)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait)
                continue;

            var effective = grant.EffectiveValue;
            var submittedMovement = effective is { ValueKind: JsonValueKind.Object } payload &&
                payload.TryGetProperty("movementMode", out _);
            var definitionId = grant.Identity.MechanicalDefinitionId;
            var hasDefinition = definitionId is not null &&
                catalog.TryGetById(definitionId, out var found) &&
                found is not null && found.Category == "origin-trait" &&
                found.Definition.TryGetProperty("trait", out var authored) &&
                authored.ValueKind == JsonValueKind.Object;
            if (!hasDefinition)
            {
                if (submittedMovement)
                    return Unresolved("movement-mode-definition-unverified");
                continue;
            }

            // A forged payload must not introduce a new movement mode on a
            // nonmovement trait or strip the mode from a canonical grant.
            catalog.TryGetById(definitionId!, out var canonicalDefinition);
            var canonical = canonicalDefinition!.Definition.GetProperty("trait");
            var authoredMovement = canonical.TryGetProperty("movementMode", out _);
            if (!authoredMovement && !submittedMovement)
                continue;
            if (!authoredMovement || !submittedMovement ||
                effective is not { ValueKind: JsonValueKind.Object } value ||
                !JsonElement.DeepEquals(canonical, value) ||
                !canonical.TryGetProperty("movementMode", out var mode) ||
                mode.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(mode.GetString()) ||
                !canonical.TryGetProperty("speedFeet", out var speed) ||
                speed.ValueKind != JsonValueKind.Number ||
                !speed.TryGetInt32(out var feet) || feet is <= 0 or > 1_000_000 ||
                !VerifiedNativeGrant(grant))
                return Unresolved("movement-mode-not-verified:" + definitionId);

            modes.Add(new(mode.GetString()!, feet, grant.Id, definitionId!,
                grant.Identity.Occurrence.Source.Id,
                grant.Identity.Occurrence.OccurrencePath, canonical.Clone()));
        }

        // A table may authorize retaining two different native modes with the
        // same name. Expose each complete source effect without replacing or
        // adding speed/terrain values. A consumer must not treat this as a
        // selected effective speed or a resolved action-economy decision.
        return new(modes.Count == 0 ? "none" : "resolved",
            modes.OrderBy(x => x.Mode, StringComparer.Ordinal)
                .ThenBy(x => x.DefinitionId, StringComparer.Ordinal)
                .ThenBy(x => x.GrantId, StringComparer.Ordinal).ToArray(), []);

        bool VerifiedNativeGrant(NativeOriginEffectiveGrant grant)
        {
            var source = grant.Identity.Occurrence.Source;
            if (source.Kind != NativeOriginSourceKind.Native ||
                !catalog.TryGetById(source.Id, out var owner) ||
                owner is null || owner.Category is not ("species" or "ancestry"))
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
                    grant.EffectiveValue is { } actual &&
                    JsonElement.DeepEquals(offered, actual));
        }
    }

    private static View Unresolved(string reason) =>
        new("unresolved", [], [reason]);
}
