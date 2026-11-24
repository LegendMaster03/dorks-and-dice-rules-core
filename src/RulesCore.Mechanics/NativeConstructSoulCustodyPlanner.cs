namespace RulesCore.Mechanics;

/// <summary>
/// Evidence for an independently verified death or extraordinary resolution.
/// For capture, EventId is the actual death identity; for resolution, it is
/// the separate event authorizing extraction or final consumption.
/// </summary>
public sealed record NativeConstructSoulCustodyEvent(
    string EventId, string SoulId, string CoreInstanceId,
    NativeConstructSoulDisposition Outcome,
    IReadOnlyDictionary<string, int>? DistancesFeetBySource = null,
    bool EligibleSoul = false);

public sealed record NativeConstructSoulCustodyPlan(
    bool CanApply, IReadOnlyList<NativeConstructSoulCustodyRecord>? NextRecords,
    IReadOnlyList<NativeOriginContractIssue> Issues);

/// <summary>
/// Evaluate the existing in-process custody ledger on detached records.
/// The local commit is not durable and does not consume the game event.
/// Cross-character capture conflicts and the durable death/resurrection state
/// must be verified and committed by the owning game host.
/// </summary>
public static class NativeConstructSoulCustodyPlanner
{
    public static NativeConstructSoulCustodyPlan Plan(
        NativeConstructOperatingSession session,
        IReadOnlyList<NativeConstructSoulCustodyRecord> records,
        NativeConstructSoulCustodyEvent evidence)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (records is null || evidence is null || !Valid(evidence.EventId) ||
            !Valid(evidence.SoulId) || !Valid(evidence.CoreInstanceId) ||
            !Enum.IsDefined(evidence.Outcome) ||
            (evidence.Outcome == NativeConstructSoulDisposition.Imprisoned
                ? evidence.DistancesFeetBySource is null || !evidence.EligibleSoul
                : evidence.DistancesFeetBySource is not null || evidence.EligibleSoul))
            return new(false, null, [new("construct-soul-custody-event-invalid",
                "soulCustody", NativeOriginIssueSeverity.Invalid,
                "The verified event must identify one capture or extraordinary resolution.")]);

        var opened = session.OpenTrustedSoulCustodyLedger(records);
        if (opened.Ledger is not { } ledger)
            return new(false, null, opened.Issues);

        var authority = new ExactEventAuthority(evidence);
        var result = evidence.Outcome == NativeConstructSoulDisposition.Imprisoned
            ? ledger.TryRecordDeath(0, evidence.EventId, evidence.SoulId,
                evidence.EligibleSoul, evidence.DistancesFeetBySource!, authority,
                evidence.CoreInstanceId)
            : ledger.TryResolveSoul(0, evidence.SoulId, evidence.Outcome, authority);
        return result.Committed
            ? new(true, result.Snapshot.Records, result.Issues)
            : new(false, null, result.Issues);

        static bool Valid(string? value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= 256;
    }

    private sealed class ExactEventAuthority(NativeConstructSoulCustodyEvent evidence)
        : INativeConstructSoulCustodyAuthority
    {
        public bool ConfirmCapture(string deathEventId, string soulId, string coreInstanceId) =>
            evidence.Outcome == NativeConstructSoulDisposition.Imprisoned &&
            evidence.EventId == deathEventId && evidence.SoulId == soulId &&
            evidence.CoreInstanceId == coreInstanceId;

        public bool ConfirmResolution(string soulId, string coreInstanceId,
            NativeConstructSoulDisposition outcome) =>
            evidence.Outcome == outcome && evidence.SoulId == soulId &&
            evidence.CoreInstanceId == coreInstanceId;
    }
}
