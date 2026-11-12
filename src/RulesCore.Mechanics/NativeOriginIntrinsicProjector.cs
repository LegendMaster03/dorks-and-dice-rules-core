using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// Interprets a small, expressly supported set of native intrinsic grants from
/// an already adjudicated Origin result. This is read-only interpretation, not
/// a new inheritance rule or proof that a caller owns the Origin.
/// </summary>
public static class NativeOriginIntrinsicProjector
{
    public sealed record IntrinsicValue<T>(
        T Value, string GrantId, string SourceId, string OccurrencePath);

    public sealed record View(
        string Status,
        IntrinsicValue<string>? CreatureType,
        IntrinsicValue<string>? Size,
        IntrinsicValue<int>? WalkingSpeedFeet,
        IReadOnlyList<string> UnresolvedSlots,
        bool PreviewOnly = true);

    public static View Project(NativeOriginConstructionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        // A mixed, post-origination, table-adjudicated, or unresolved source
        // must never be rendered as authoritative Species/Ancestry mechanics.
        if (result.Status != NativeOriginConstructionStatus.Authorized ||
            result.Profile is not { } profile ||
            result.EvaluatedProposal.Intent != NativeOriginIntent.Direct ||
            profile.Adjudication.Adjudication != NativeOriginAdjudication.Supported ||
            profile.ActiveTemporaryTransformation is not null ||
            (profile.OtherActiveTemporaryTransformations?.Count ?? 0) > 0 ||
            !ReferenceEquals(profile.Current, profile.Underlying) ||
            profile.IndependentGrants.Count != 0 ||
            (profile.PersistentInfluences?.Count ?? 0) != 0 ||
            result.Issues.Count != 0)
            return new("unresolved", null, null, null,
                ["origin-not-a-supported-initial-direct-profile"]);

        var issues = new List<string>();
        var supported = new Dictionary<string, NativeOriginEffectiveGrant>(
            StringComparer.Ordinal);
        foreach (var grant in profile.Current.InherentGrants)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Intrinsic ||
                grant.Identity.IntrinsicSlotId is not ("creatureType" or "size" or "walkingSpeedFeet"))
                continue;

            var slot = grant.Identity.IntrinsicSlotId;
            var source = grant.Identity.Occurrence.Source;
            if (source.Kind != NativeOriginSourceKind.Native ||
                !IsOriginSource(profile, source.Id) ||
                grant.Authorization?.Adjudication != NativeOriginAdjudication.Supported ||
                grant.SourceCandidateIds.Count == 0 ||
                !supported.TryAdd(slot!, grant))
            {
                issues.Add("unsupported-or-conflicting-intrinsic:" + slot);
            }
        }

        IntrinsicValue<string>? creatureType = Text("creatureType");
        IntrinsicValue<string>? size = Text("size");
        IntrinsicValue<int>? walkingSpeedFeet = Number("walkingSpeedFeet");
        if (issues.Count != 0)
            return new("unresolved", null, null, null, issues.Distinct().ToArray());
        if (creatureType is null) issues.Add("creature-type-unavailable");
        if (size is null) issues.Add("size-unavailable");
        if (walkingSpeedFeet is null) issues.Add("walking-speed-unavailable");
        return new(issues.Count == 0 ? "resolved" : "partial",
            creatureType, size, walkingSpeedFeet, issues);

        IntrinsicValue<string>? Text(string slot)
        {
            if (!supported.TryGetValue(slot, out var grant)) return null;
            if (grant.EffectiveValue is not { ValueKind: JsonValueKind.String } value ||
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                issues.Add("invalid-intrinsic-value:" + slot);
                return null;
            }
            return new(value.GetString()!, grant.Id,
                grant.Identity.Occurrence.Source.Id,
                grant.Identity.Occurrence.OccurrencePath);
        }

        IntrinsicValue<int>? Number(string slot)
        {
            if (!supported.TryGetValue(slot, out var grant)) return null;
            if (grant.EffectiveValue is not { ValueKind: JsonValueKind.Number } value ||
                !value.TryGetInt32(out var speed) || speed is < 0 or > 1_000_000)
            {
                issues.Add("invalid-intrinsic-value:" + slot);
                return null;
            }
            return new(speed, grant.Id,
                grant.Identity.Occurrence.Source.Id,
                grant.Identity.Occurrence.OccurrencePath);
        }
    }

    private static bool IsOriginSource(NativeOriginProfile profile, string sourceId) =>
        profile.Current.SpeciesId == sourceId ||
        profile.Current.AncestryId == sourceId ||
        // An Ancestry can inherit its base Species through an authored
        // lineage; verification of that chain is done by the native
        // deconstructor, not inferred from these ID strings.
        profile.Current.InherentGrants.Any(x =>
            x.Identity.Occurrence.Source.Id == sourceId &&
            x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic);
}
