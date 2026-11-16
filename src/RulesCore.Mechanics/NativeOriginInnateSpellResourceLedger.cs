using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

public enum NativeOriginInnateSpellLedgerActionKind
{
    SpendFreeCast, CheckAtWill, RequestSlotCast, ShortRest, LongRest
}

/// <summary>
/// An immutable, host-owned snapshot of independent Origin free-casting pools.
/// The revision is an optimistic-concurrency input, not authentication: a host
/// must persist this snapshot and check its revision atomically. Constructing a
/// new snapshot is only legal when that host has verified there is no prior one.
/// </summary>
public sealed record NativeOriginInnateSpellLedgerState(
    string RulesetId, string CatalogFingerprint, string OriginProfileId,
    string ProposalId, int CharacterLevel, string ProjectionFingerprint,
    long Revision, IReadOnlyDictionary<string, int> RemainingFreeCasts);

public sealed record NativeOriginInnateSpellLedgerCommand(
    NativeOriginInnateSpellLedgerActionKind Kind, long ExpectedRevision,
    string? GrantId = null);

public sealed record NativeOriginInnateSpellLedgerResult(
    string Status, NativeOriginInnateSpellLedgerState? State,
    string? Reason = null, string? GrantId = null, int? Remaining = null,
    int? Maximum = null, bool PreviewOnly = true);

/// <summary>
/// Validates and computes one state change against the real native, authorized
/// Origin spell projection. It never persists state, grants a spell slot, casts
/// a spell, or changes the underlying Origin. NativeResourceResolution handles
/// the shared count/recovery semantics; grant IDs isolate each authored pool.
/// </summary>
public static class NativeOriginInnateSpellResourceLedger
{
    /// <summary>
    /// A bootstrap candidate for a genuinely new, unspent character ledger.
    /// This method cannot determine that no saved ledger exists. The storage
    /// host must enforce create-once semantics and authoritative identities.
    /// </summary>
    public static NativeOriginInnateSpellLedgerResult Initialize(
        NativeCatalogSnapshot catalog, NativeOriginConstructionResult origin,
        NativeCharacterProjection character)
    {
        var context = Resolve(catalog, origin, character);
        if (context.Reason is not null)
            return Fail(context.Reason);
        var projection = context.Projection!;
        var available = Pools(projection);
        if (available is null)
            return Fail("spell-free-use-pools-invalid");
        var state = new NativeOriginInnateSpellLedgerState(
            catalog.RulesetId, catalog.Fingerprint, origin.Profile!.Id,
            origin.EvaluatedProposal.Id, projection.CharacterLevel!.Value,
            Fingerprint(projection), 0,
            available.ToFrozenDictionary(x => x.Key, x => x.Value,
                StringComparer.Ordinal));
        return new("resolved", state);
    }

    /// <summary>
    /// Calculate an atomic candidate update. The host must reject stale
    /// ExpectedRevision values in its own transaction, not only here.
    /// A slot-cast request is a checked handoff to the separate character
    /// spell-slot system. It never decrements an Origin free-casting pool.
    /// </summary>
    public static NativeOriginInnateSpellLedgerResult Apply(
        NativeCatalogSnapshot catalog, NativeOriginConstructionResult origin,
        NativeCharacterProjection character, NativeOriginInnateSpellLedgerState state,
        NativeOriginInnateSpellLedgerCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        var context = Resolve(catalog, origin, character);
        if (context.Reason is not null)
            return Fail(context.Reason);
        var projection = context.Projection!;
        var allowed = Pools(projection);
        if (allowed is null)
            return Fail("spell-free-use-pools-invalid");

        if (state.RulesetId != catalog.RulesetId ||
            state.CatalogFingerprint != catalog.Fingerprint ||
            state.OriginProfileId != origin.Profile!.Id ||
            state.ProposalId != origin.EvaluatedProposal.Id ||
            state.CharacterLevel != projection.CharacterLevel ||
            state.ProjectionFingerprint != Fingerprint(projection))
            return Fail("spell-ledger-snapshot-mismatch");

        if (state.Revision < 0 || state.Revision == long.MaxValue ||
            command.ExpectedRevision != state.Revision)
            return Fail("spell-ledger-revision-mismatch");

        // A caller-provided state is never evidence of extra entitlement.
        // Unknown, missing, negative or over-capacity entries fail as a whole.
        if (state.RemainingFreeCasts is null ||
            state.RemainingFreeCasts.Count != allowed.Count ||
            state.RemainingFreeCasts.Any(x =>
                !allowed.TryGetValue(x.Key, out var max) ||
                x.Value < 0 || x.Value > max) ||
            allowed.Keys.Any(x => !state.RemainingFreeCasts.ContainsKey(x)))
            return Fail("spell-ledger-state-invalid");

        if (command.Kind is NativeOriginInnateSpellLedgerActionKind.ShortRest or
            NativeOriginInnateSpellLedgerActionKind.LongRest)
        {
            if (command.GrantId is not null)
                return Fail("spell-ledger-unexpected-grant-id");
            var rest = command.Kind == NativeOriginInnateSpellLedgerActionKind.LongRest
                ? NativeRecoveryEvent.LongRest
                : NativeRecoveryEvent.ShortRest;
            var next = state.RemainingFreeCasts.ToDictionary(x => x.Key,
                x => x.Value, StringComparer.Ordinal);
            foreach (var (grantId, max) in allowed)
            {
                var resource = new NativeResource("origin-spell-free:" + grantId,
                    max, next[grantId], "long-rest");
                var change = NativeResourceResolution.Recover(resource,
                    next[grantId], rest);
                if (change.Status != "resolved" || change.Current is null)
                    return Fail("spell-ledger-recovery-unresolved");
                next[grantId] = change.Current.Value;
            }
            return new("resolved", state with
            {
                Revision = state.Revision + 1,
                RemainingFreeCasts = next.ToFrozenDictionary(StringComparer.Ordinal)
            });
        }

        if (string.IsNullOrWhiteSpace(command.GrantId))
            return Fail("spell-ledger-grant-required");
        var spell = projection.Spells.SingleOrDefault(x =>
            x.GrantId == command.GrantId);
        if (spell is null)
            return Fail("spell-ledger-grant-not-selected");
        if (spell.Availability != "available")
            return Fail("spell-ledger-spell-locked");

        switch (command.Kind)
        {
            case NativeOriginInnateSpellLedgerActionKind.CheckAtWill:
                return spell.AtWill
                    ? new("permitted", state, GrantId: spell.GrantId)
                    : Fail("spell-ledger-not-at-will");
            case NativeOriginInnateSpellLedgerActionKind.RequestSlotCast:
                return spell.SlotCasting
                    ? new("external-slot-required", state,
                        "Select a verified applicable character spell slot; no Origin free use was spent.",
                        spell.GrantId)
                    : Fail("spell-ledger-slot-casting-not-granted");
            case NativeOriginInnateSpellLedgerActionKind.SpendFreeCast:
            {
                if (!allowed.TryGetValue(spell.GrantId, out var max))
                    return Fail("spell-ledger-free-cast-not-granted");
                var current = state.RemainingFreeCasts[spell.GrantId];
                var resource = new NativeResource("origin-spell-free:" + spell.GrantId,
                    max, current, spell.Recharge!);
                var change = NativeResourceResolution.Spend(resource, current, 1);
                if (change.Status == "insufficient-resource")
                    return new("insufficient-free-casts", state,
                        GrantId: spell.GrantId, Remaining: current, Maximum: max);
                if (change.Status != "resolved" || change.Current is null)
                    return Fail("spell-ledger-spend-unresolved");
                var next = state.RemainingFreeCasts.ToDictionary(x => x.Key,
                    x => x.Value, StringComparer.Ordinal);
                next[spell.GrantId] = change.Current.Value;
                return new("resolved", state with
                {
                    Revision = state.Revision + 1,
                    RemainingFreeCasts = next.ToFrozenDictionary(StringComparer.Ordinal)
                }, GrantId: spell.GrantId, Remaining: change.Current, Maximum: max);
            }
            default:
                return Fail("spell-ledger-action-unsupported");
        }
    }

    private static (NativeOriginInnateSpellProgressionProjector.View? Projection,
        string? Reason) Resolve(
        NativeCatalogSnapshot catalog, NativeOriginConstructionResult origin,
        NativeCharacterProjection character)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(character);
        var view = NativeOriginInnateSpellProgressionProjector.ForCharacter(
            catalog, origin, character);
        if (view.Status is not ("resolved" or "none") ||
            view.CharacterLevel is null)
            return (null, view.UnresolvedReasons.FirstOrDefault() ??
                "spell-progression-unresolved");
        // No mutable free-use pool may be created from an unresolved spell
        // amount or a future-locked spell.
        if (view.Spells.Any(x => x.FreeUsesFormula is not null &&
                x.Availability == "available" && x.FreeUsesPerRecharge is null))
            return (null, "spell-free-use-formula-unresolved");
        return (view, null);
    }

    private static SortedDictionary<string, int>? Pools(
        NativeOriginInnateSpellProgressionProjector.View view)
    {
        var pools = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var spell in view.Spells)
        {
            if (spell.Availability != "available" ||
                spell.FreeUsesPerRecharge is null)
                continue;
            if (spell.FreeUsesPerRecharge is <= 0 or > 1_000_000 ||
                spell.Recharge != "long-rest" ||
                !pools.TryAdd(spell.GrantId, spell.FreeUsesPerRecharge.Value))
                return null;
        }
        return pools;
    }

    private static string Fingerprint(
        NativeOriginInnateSpellProgressionProjector.View view)
    {
        // This detects a changed source/progression before a state transition,
        // but is NOT a signature or proof of state authenticity.
        var canonical = view.Spells.OrderBy(x => x.GrantId, StringComparer.Ordinal)
            .ToArray();
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new { view.CharacterLevel, Spells = canonical }));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static NativeOriginInnateSpellLedgerResult Fail(string reason) =>
        new("unresolved", null, reason);
}
