using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// Host verification of the current saved authoritative Origin baseline and
/// a specific source-qualified inherent grant. Passing caller-authored profile
/// data through this interface is not authorization.
/// </summary>
public interface INativeCharacterOriginBaselineAuthority
{
    bool AuthorizesBaseline(string characterId, NativeOriginProfile profile);
    bool AuthorizesGrant(string characterId, NativeOriginProfile profile,
        NativeOriginEffectiveGrant grant);
}

public sealed record NativeCharacterOriginBaselineResult(
    string Status, NativeCharacterState? State, IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// A narrow, opt-in adapter for an already authorized current Origin. It reads
/// only Underlying (never the possibly stale active-form Current or historical
/// PreviousConfiguration) and supplies a trusted base walk-speed input to the
/// existing native character kernel. All other Origin mechanics remain the
/// responsibility of separately verified adapters; this is not an Origin
/// constructor, migration tool, or form activation API.
/// </summary>
public static class NativeCharacterSheetOriginBaselineAdapter
{
    public static NativeCharacterOriginBaselineResult ApplyWalkSpeed(
        NativeCharacterState state, NativeOriginProfile latest,
        INativeCharacterOriginBaselineAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(authority);

        static NativeCharacterOriginBaselineResult Issue(string code, string detail) =>
            new("unresolved", null,
                [new NativeIssue(code, "underlying.inherentGrants.walkingSpeedFeet", detail)]);

        if (string.IsNullOrWhiteSpace(state.CharacterId) ||
            state.PrimaryMechanics is null ||
            latest.Underlying?.InherentGrants is null ||
            latest.Adjudication?.Adjudication is not
                (NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized) ||
            !authority.AuthorizesBaseline(state.CharacterId, latest))
            return Issue("origin-baseline-unverified",
                "The authenticated host must provide the latest verified authoritative Origin.");

        var candidates = latest.Underlying.InherentGrants.Where(grant =>
            grant?.Identity?.Kind == NativeOriginCharacteristicKind.Intrinsic &&
            grant.Identity.IntrinsicSlotId == "walkingSpeedFeet").ToArray();
        if (candidates.Length != 1)
            return Issue("origin-walk-speed-unresolved",
                "Exactly one authoritative walking-speed grant is required.");

        var sourceGrant = candidates[0];
        var occurrence = sourceGrant.Identity.Occurrence;
        if (occurrence is null || occurrence.Source is null ||
            string.IsNullOrWhiteSpace(occurrence.Source.Id) ||
            string.IsNullOrWhiteSpace(occurrence.OccurrencePath) ||
            string.IsNullOrWhiteSpace(sourceGrant.Id) ||
            sourceGrant.Authorization?.Adjudication is not
                (NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized) ||
            !authority.AuthorizesGrant(state.CharacterId, latest, sourceGrant) ||
            sourceGrant.EffectiveValue is not { ValueKind: JsonValueKind.Number } value ||
            !value.TryGetInt32(out var speed) || speed is < 0 or > 1_000_000)
            return Issue("origin-walk-speed-unverified",
                "The authoritative walking-speed grant or its source is not verified.");

        return new("resolved", state with
        {
            PrimaryMechanics = state.PrimaryMechanics with { BaseWalkSpeed = speed }
        }, []);
    }
}
