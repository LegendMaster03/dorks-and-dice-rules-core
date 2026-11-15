using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// A separate, deliberately restricted interpreter for a completed, DM-authorized
/// mixed initial construction. The ordinary first-party intrinsic projector must
/// continue to reject mixed proposals; neither interpreter grants authority or
/// decides which contributing characteristics a new character inherits.
/// </summary>
public static class NativeOriginApprovedMixedIntrinsicProjector
{
    public static NativeOriginIntrinsicProjector.View Project(
        NativeOriginConstructionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var profile = result.Profile;
        if (result.Status != NativeOriginConstructionStatus.Authorized ||
            profile is null ||
            result.EvaluatedProposal.Intent != NativeOriginIntent.Mixed ||
            profile.Adjudication.Adjudication != NativeOriginAdjudication.DmAuthorized ||
            !ReferenceEquals(profile.Current, profile.Underlying) ||
            profile.IndependentGrants.Count != 0 ||
            (profile.PersistentInfluences?.Count ?? 0) != 0 ||
            profile.ActiveTemporaryTransformation is not null ||
            (profile.OtherActiveTemporaryTransformations?.Count ?? 0) != 0 ||
            result.Issues.Count != 0)
            return Unresolved("mixed-origin-not-completely-authorized");

        var issues = new List<string>();
        var permitted = new Dictionary<string, NativeOriginEffectiveGrant>(StringComparer.Ordinal);
        foreach (var grant in profile.Current.InherentGrants)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Intrinsic ||
                grant.Identity.IntrinsicSlotId is not (
                    "creatureType" or "size" or "walkingSpeedFeet"))
                continue;

            var slot = grant.Identity.IntrinsicSlotId!;
            var source = grant.Identity.Occurrence.Source;
            if (source.Kind != NativeOriginSourceKind.Native ||
                !HasVerifiedContributingEvidence(result, grant) ||
                !permitted.TryAdd(slot, grant))
                issues.Add("mixed-intrinsic-unverified:" + slot);
        }

        var type = Text("creatureType");
        var size = Text("size");
        var walk = Number("walkingSpeedFeet");
        if (type is null) issues.Add("creature-type-unavailable");
        if (size is null) issues.Add("size-unavailable");
        if (walk is null) issues.Add("walking-speed-unavailable");

        return issues.Count == 0
            ? new("resolved", type, size, walk, [], true)
            : new("unresolved", null, null, null, issues.Distinct().ToArray(), true);

        NativeOriginIntrinsicProjector.IntrinsicValue<string>? Text(string slot)
        {
            if (!permitted.TryGetValue(slot, out var grant)) return null;
            if (grant.EffectiveValue is not { ValueKind: JsonValueKind.String } value ||
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                issues.Add("mixed-intrinsic-invalid:" + slot);
                return null;
            }
            return new(value.GetString()!, grant.Id,
                grant.Identity.Occurrence.Source.Id,
                grant.Identity.Occurrence.OccurrencePath);
        }

        NativeOriginIntrinsicProjector.IntrinsicValue<int>? Number(string slot)
        {
            if (!permitted.TryGetValue(slot, out var grant)) return null;
            if (grant.EffectiveValue is not { ValueKind: JsonValueKind.Number } value ||
                !value.TryGetInt32(out var speed) || speed is < 0 or > 1_000_000)
            {
                issues.Add("mixed-intrinsic-invalid:" + slot);
                return null;
            }
            return new(speed, grant.Id,
                grant.Identity.Occurrence.Source.Id,
                grant.Identity.Occurrence.OccurrencePath);
        }
    }

    /// <summary>
    /// The constructor may consolidate the same native occurrence offered by several
    /// parents. Every evidence path must correspond to an actually manifested and
    /// individually authorized candidate; consolidation is not permission to invent
    /// an extra grant, use pool or otherwise unselected contributor.
    /// </summary>
    internal static bool HasVerifiedContributingEvidence(
        NativeOriginConstructionResult result, NativeOriginEffectiveGrant grant)
    {
        var authorization = grant.Authorization;
        var ids = grant.SourceCandidateIds;
        if (grant.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
            authorization?.Adjudication != NativeOriginAdjudication.DmAuthorized ||
            string.IsNullOrWhiteSpace(authorization.TableDecisionId) ||
            ids.Count == 0 ||
            ids.Distinct(StringComparer.Ordinal).Count() != ids.Count ||
            ids.Any(id => result.EvaluatedProposal.Manifestations.Count(manifest =>
                manifest.CandidateId == id) != 1))
            return false;

        var candidates = result.EvaluatedProposal.Candidates
            .Where(candidate => ids.Contains(candidate.Id, StringComparer.Ordinal))
            .ToArray();
        return candidates.Length == ids.Count &&
            candidates.Any(candidate =>
                candidate.SelectionBasis.TableDecisionId == authorization.TableDecisionId) &&
            candidates.All(candidate =>
                candidate.ContributorId is not null &&
                candidate.Identity == grant.Identity &&
                candidate.SelectionBasis.Eligibility == NativeOriginEligibility.DmAuthorized &&
                !string.IsNullOrWhiteSpace(candidate.SelectionBasis.TableDecisionId) &&
                candidate.OfferedValue is { } offered &&
                grant.EffectiveValue is { } effective &&
                JsonElement.DeepEquals(offered, effective));
    }

    private static NativeOriginIntrinsicProjector.View Unresolved(string why) =>
        new("unresolved", null, null, null, [why], true);
}
