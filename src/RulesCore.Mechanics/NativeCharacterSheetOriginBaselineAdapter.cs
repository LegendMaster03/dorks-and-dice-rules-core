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

        // The caller supplies the character's verified state; reject malformed
        // transport shapes before copying nested collections into the derived
        // snapshot. The primary/sheet authority still needs to verify ownership
        // and revision before this derived input can be used for mechanics.
        if (!CanSnapshot(state))
            return Issue("invalid-character-state",
                "The primary character or alternate-sheet input is malformed.");

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

        // Do not return aliases to mutable host-owned maps, progression lists
        // or choices. A later mutation of the saved character must not rewrite
        // an already generated read-only projection.
        var snapshot = NativeCharacterSheetResolver.SnapshotState(state);
        return new("resolved", snapshot with
        {
            PrimaryMechanics = snapshot.PrimaryMechanics with { BaseWalkSpeed = speed }
        }, []);
    }

    private static bool CanSnapshot(NativeCharacterState state)
    {
        if (state.PrimaryMechanics?.AbilityScores is null ||
            state.PrimaryMechanics.Advancements is null ||
            state.AlternateSheets is null ||
            !NativeCharacterSheetResolver.WithinLimits(state))
            return false;

        static bool MapValid<T>(NativeSheetMapPatch<T>? map) =>
            map is null || map.Entries is not null;
        static bool CollectionValid(NativeSheetCollectionPatch<string>? patch) =>
            patch is null || patch.Added is not null;

        foreach (var sheet in state.AlternateSheets)
        {
            if (sheet is null || sheet.Progression?.Any(progress =>
                progress is null || progress.Grants is null) == true)
                return false;
            var patch = sheet.Overrides;
            if (patch is not null &&
                (!MapValid(patch.AbilityScores) || !MapValid(patch.Skills) ||
                 !MapValid(patch.Competencies) || !MapValid(patch.DefenseContributions) ||
                 !MapValid(patch.CurrentResources) || !MapValid(patch.HitPointGains) ||
                 !CollectionValid(patch.SelectedOptions) || !CollectionValid(patch.ConditionIds)))
                return false;
        }
        return true;
    }
}
