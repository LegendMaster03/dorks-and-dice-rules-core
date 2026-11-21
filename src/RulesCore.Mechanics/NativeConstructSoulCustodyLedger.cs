using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// The game host must authenticate creature death, soul eligibility,
/// physical range, conflicts between cores and extraordinary extraction.
/// A client cannot use ledger inputs to create souls or authorize rescue.
/// </summary>
internal interface INativeConstructSoulCustodyAuthority
{
    bool ConfirmCapture(string deathEventId, string soulId,
        string receivingCoreInstanceId);

    bool ConfirmResolution(string soulId, string coreInstanceId,
        NativeConstructSoulDisposition outcome);
}

/// <summary>
/// "Consumed" is the DM's decision that the captive soul is no longer
/// available for ordinary restoration, not a numerical fuel computation.
/// The record does not determine what echoes or remnants exist inside.
/// </summary>
public enum NativeConstructSoulDisposition
{
    Imprisoned, Extracted, Consumed
}

/// <summary>
/// Durable-data shape for an individually identified soul. Multiple
/// records may belong to the same core; there is no fixed storage cap.
/// </summary>
public sealed record NativeConstructSoulCustodyRecord(
    string SoulId, string CoreInstanceId, string DeathEventId,
    NativeConstructSoulDisposition Disposition);

public sealed record NativeConstructSoulCustodySnapshot(
    long Revision, IReadOnlyList<NativeConstructSoulCustodyRecord> Records)
{
    public bool IsImprisoned(string soulId) =>
        Records.Any(x => x.SoulId == soulId &&
            x.Disposition == NativeConstructSoulDisposition.Imprisoned);
}

public sealed record NativeConstructSoulCustodyTransition(
    bool Committed, NativeConstructSoulCustodySnapshot Snapshot,
    IReadOnlyList<NativeOriginContractIssue> Issues);

internal sealed record NativeConstructSoulCustodyOpenResult(
    NativeConstructSoulCustodyLedger? Ledger,
    IReadOnlyList<NativeOriginContractIssue> Issues);

/// <summary>
/// In-process, revision-checked custody. Neither capture nor release is a
/// player-controlled operating mode. Host integration must persist successful
/// transitions with the corresponding death/resurrection game state.
/// </summary>
internal sealed class NativeConstructSoulCustodyLedger
{
    private readonly NativeConstructOperatingSession _session;
    private readonly Dictionary<string, NativeConstructSoulCustodyRecord> _records;
    private readonly HashSet<string> _deathEvents;
    private readonly object _gate = new();
    private long _revision;

    internal NativeConstructSoulCustodyLedger(
        NativeConstructOperatingSession session,
        IReadOnlyList<NativeConstructSoulCustodyRecord> authenticatedRecords)
    {
        _session = session;
        _records = authenticatedRecords.ToDictionary(x => x.DeathEventId,
            StringComparer.Ordinal);
        _deathEvents = authenticatedRecords.Select(x => x.DeathEventId)
            .ToHashSet(StringComparer.Ordinal);
    }

    public NativeConstructSoulCustodySnapshot Snapshot()
    {
        lock (_gate) return Current();
    }

    /// <summary>
    /// Resolve a verified death into its compulsory soul capture. The host
    /// may choose a receiving core only when overlapping capture zones need
    /// adjudication, not to opt out of otherwise valid capture.
    /// </summary>
    internal NativeConstructSoulCustodyTransition TryRecordDeath(
        long expectedRevision, string deathEventId, string soulId,
        bool dmConfirmedEligibleSoul,
        IReadOnlyDictionary<string, int> distancesFeetBySource,
        INativeConstructSoulCustodyAuthority authority,
        string? receivingCoreInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(distancesFeetBySource);
        lock (_gate)
        {
            if (expectedRevision != _revision || _revision == long.MaxValue)
                return Reject("construct-soul-custody-revision-invalid",
                    "The soul-custody ledger revision is stale or exhausted.");
            if (string.IsNullOrWhiteSpace(deathEventId) ||
                string.IsNullOrWhiteSpace(soulId) ||
                deathEventId.Length > 256 || soulId.Length > 256 ||
                _deathEvents.Contains(deathEventId) || _records.Values.Any(x =>
                    x.SoulId == soulId &&
                    x.Disposition == NativeConstructSoulDisposition.Imprisoned))
                return Reject("construct-soul-custody-identity-invalid",
                    "Capture requires unique nonempty death and soul identities.");
            var candidate = _session.PreviewAutomaticSoulCapture(
                dmConfirmedEligibleSoul, distancesFeetBySource);
            if (candidate.Issues.Count != 0 || !candidate.HasAutomaticCapture)
                return Reject("construct-soul-custody-no-capture",
                    "No eligible, valid automatic capture is available.");
            string? selected;
            if (candidate.CandidateCores.Count == 1)
            {
                selected = candidate.CandidateCores[0].CoreInstanceId;
                if (receivingCoreInstanceId is not null &&
                    receivingCoreInstanceId != selected)
                    return Reject("construct-soul-custody-destination-invalid",
                        "The selected core is not the eligible capture destination.");
            }
            else
            {
                selected = receivingCoreInstanceId;
                if (selected is null || !candidate.CandidateCores.Any(x =>
                    x.CoreInstanceId == selected))
                    return Reject("construct-soul-custody-destination-unresolved",
                        "The DM must resolve overlapping eligible capture areas.");
            }

            if (!authority.ConfirmCapture(deathEventId, soulId, selected))
                return Reject("construct-soul-custody-event-unverified",
                    "The host has not authenticated the death and capture.");
            _records.Add(deathEventId, new(soulId, selected, deathEventId,
                NativeConstructSoulDisposition.Imprisoned));
            _deathEvents.Add(deathEventId);
            _revision++;
            return new(true, Current(), []);
        }
    }

    /// <summary>
    /// Extraordinary extraction or final consumption is a DM-authenticated
    /// narrative event. It is not triggered by elapsed time or a power step.
    /// </summary>
    internal NativeConstructSoulCustodyTransition TryResolveSoul(
        long expectedRevision, string soulId,
        NativeConstructSoulDisposition outcome,
        INativeConstructSoulCustodyAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        lock (_gate)
        {
            if (expectedRevision != _revision || _revision == long.MaxValue)
                return Reject("construct-soul-custody-revision-invalid",
                    "The soul-custody ledger revision is stale or exhausted.");
            var record = _records.Values.FirstOrDefault(x => x.SoulId == soulId &&
                x.Disposition == NativeConstructSoulDisposition.Imprisoned);
            if (string.IsNullOrWhiteSpace(soulId) || record is null ||
                outcome == NativeConstructSoulDisposition.Imprisoned ||
                !Enum.IsDefined(outcome))
                return Reject("construct-soul-custody-resolution-invalid",
                    "Only an imprisoned soul may be marked extracted or consumed.");
            if (!authority.ConfirmResolution(soulId,
                record.CoreInstanceId, outcome))
                return Reject("construct-soul-custody-resolution-unverified",
                    "The host has not confirmed a valid rescue or consumption event.");

            _records[record.DeathEventId] = record with { Disposition = outcome };
            _revision++;
            return new(true, Current(), []);
        }
    }

    private NativeConstructSoulCustodySnapshot Current() =>
        new(_revision, _records.Values
            .OrderBy(x => x.DeathEventId, StringComparer.Ordinal)
            .ThenBy(x => x.SoulId, StringComparer.Ordinal).ToArray());

    private NativeConstructSoulCustodyTransition Reject(
        string code, string message) =>
        new(false, Current(), [
            new NativeOriginContractIssue(code, "soulCustody",
                NativeOriginIssueSeverity.Invalid, message)
        ]);
}
