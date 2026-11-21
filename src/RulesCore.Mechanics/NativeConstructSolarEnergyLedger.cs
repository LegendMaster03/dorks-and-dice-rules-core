using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Trust boundary for actual sunlight exposure and elapsed play time.
/// The caller must use authenticated game state, not client assertions.
/// Approval does not persist time or resources; the game host coordinates
/// those changes with this session-local resource ledger.
/// </summary>
internal interface INativeConstructSolarIntervalAuthority
{
    bool AuthorizeInterval(
        IReadOnlyDictionary<string, NativeConstructSolarExposure> exposures,
        int elapsedMinutes, long expectedRevision);
}

/// <summary>
/// A revision-checked in-process battery ledger, independent of Construct
/// creation and of the ordinary daily fuel ration rules. The host authenticates
/// opening balances and supplies an authority for every committed interval.
/// </summary>
internal sealed class NativeConstructSolarEnergyLedger
{
    private readonly NativeConstructOperatingSession _operating;
    private readonly Dictionary<string, long> _storedEnergy;
    private readonly object _gate = new();
    private long _revision;

    internal NativeConstructSolarEnergyLedger(
        NativeConstructOperatingSession operating,
        IReadOnlyDictionary<string, long> authenticatedBalances)
    {
        _operating = operating;
        _storedEnergy = new(authenticatedBalances, StringComparer.Ordinal);
    }

    internal NativeConstructSolarLedgerSnapshot Snapshot()
    {
        lock (_gate) return Current();
    }

    internal NativeConstructSolarLedgerCommit TryCommitInterval(
        long expectedRevision,
        IReadOnlyDictionary<string, NativeConstructSolarExposure> exposures,
        int elapsedMinutes,
        INativeConstructSolarIntervalAuthority authority,
        IReadOnlyDictionary<string, bool>? requestedEnabled = null,
        IReadOnlyDictionary<string, string>? requestedModes = null)
    {
        ArgumentNullException.ThrowIfNull(authority);
        lock (_gate)
        {
            if (expectedRevision != _revision)
                return Reject("construct-solar-ledger-revision-conflict",
                    "The solar ledger revision has changed.");
            if (_revision == long.MaxValue)
                return Reject("construct-solar-ledger-revision-exhausted",
                    "The solar ledger has exhausted its in-process revision counter.");

            var preview = _operating.PreviewSolarOperation(
                _storedEnergy, exposures, elapsedMinutes,
                requestedEnabled, requestedModes);
            if (!preview.CanAdvance)
                return Reject("construct-solar-interval-not-authorized",
                    "The current installation, source reserves, and power allocation cannot complete the interval.",
                    preview);
            if (!authority.AuthorizeInterval(exposures, elapsedMinutes, _revision))
                return Reject("construct-solar-environment-unverified",
                    "The host has not confirmed the sunlight conditions and elapsed time.",
                    preview);
            var updated = preview.Sources.ToDictionary(x => x.InstanceId,
                x => x.AfterPowerUnitMinutes, StringComparer.Ordinal);
            if (updated.Count != _storedEnergy.Count ||
                updated.Keys.Any(id => !_storedEnergy.ContainsKey(id)))
                return Reject("construct-solar-ledger-shape-invalid",
                    "The preview did not preserve the installed solar source set.",
                    preview);

            var changes = _storedEnergy.OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new NativeConstructSolarLedgerChange(
                    x.Key, x.Value, updated[x.Key])).ToArray();
            var previous = _revision;
            foreach (var (id, after) in updated)
                _storedEnergy[id] = after;
            _revision++;
            return new(true, Current(),
                new(previous, _revision, changes), [], preview);
        }
    }

    private NativeConstructSolarLedgerSnapshot Current() =>
        new(_revision, new Dictionary<string, long>(_storedEnergy,
            StringComparer.Ordinal));

    private NativeConstructSolarLedgerCommit Reject(string code, string message,
        NativeConstructSolarOperationPreview? preview = null) =>
        new(false, Current(), null,
            [new(code, "solarLedger", NativeOriginIssueSeverity.Invalid, message)],
            preview);
}

internal sealed record NativeConstructSolarLedgerOpenResult(
    NativeConstructSolarEnergyLedger? Ledger,
    IReadOnlyList<NativeOriginContractIssue> Issues);

internal sealed record NativeConstructSolarLedgerSnapshot(
    long Revision, IReadOnlyDictionary<string, long> StoredPowerUnitMinutes);

internal sealed record NativeConstructSolarLedgerChange(
    string InstanceId, long Before, long After);

internal sealed record NativeConstructSolarLedgerReceipt(
    long PreviousRevision, long NewRevision,
    IReadOnlyList<NativeConstructSolarLedgerChange> Changes);

internal sealed record NativeConstructSolarLedgerCommit(
    bool Committed, NativeConstructSolarLedgerSnapshot Snapshot,
    NativeConstructSolarLedgerReceipt? Receipt,
    IReadOnlyList<NativeOriginContractIssue> Issues,
    NativeConstructSolarOperationPreview? Preview = null);
