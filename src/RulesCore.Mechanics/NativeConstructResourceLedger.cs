using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A session-local, mutable balance ledger that can only be opened by trusted
/// Mechanics code after a host authenticates the starting balances. It is
/// intentionally not a database, a public gameplay API, or an action issuer.
/// Every mutation re-evaluates the current balances through the authorized
/// Construct operating session while holding one revision-checked lock.
/// </summary>
internal sealed class NativeConstructResourceLedger
{
    private readonly NativeConstructOperatingSession _operating;
    private readonly Dictionary<string, int> _balances;
    private readonly object _gate = new();
    private long _revision;

    internal NativeConstructResourceLedger(
        NativeConstructOperatingSession operating,
        IReadOnlyDictionary<string, int> trustedBalances)
    {
        _operating = operating;
        _balances = new(trustedBalances, StringComparer.Ordinal);
    }

    /// <summary>
    /// Return a detached copy of the current balances. Callers cannot mutate
    /// the ledger by retaining or modifying this result.
    /// </summary>
    internal NativeConstructResourceLedgerSnapshot Snapshot()
    {
        lock (_gate) return Current();
    }

    /// <summary>
    /// Consume one fully valid operating step. A resource preview supplied by
    /// a caller is never accepted as a commit instruction: it is recomputed
    /// against the current ledger revision and the trusted installation.
    /// </summary>
    internal NativeConstructResourceLedgerCommit TryCommitStep(
        long expectedRevision,
        IReadOnlyDictionary<string, bool>? requestedEnabled = null,
        IReadOnlyDictionary<string, string>? requestedModes = null)
    {
        lock (_gate)
        {
            var blocked = CheckRevision(expectedRevision);
            if (blocked is not null) return blocked;
            var preview = _operating.PreviewResourceStep(_balances,
                requestedEnabled, requestedModes);
            if (!preview.CanAdvance)
                return Rejected("construct-resource-step-not-authorized",
                    "The current resource balances and operating configuration do not permit this step.",
                    preview: preview);

            // Native consumption is nonincreasing for each installed source.
            var updated = preview.Sources.ToDictionary(
                x => x.InstanceId, x => x.After, StringComparer.Ordinal);
            return Apply(updated, "operating-step", preview, null);
        }
    }

    /// <summary>
    /// Commit a conserved reservoir transfer without consuming an operating
    /// step. The transfer and its source balances are recomputed at commit
    /// time. Gameplay action permission must be checked by the host first.
    /// </summary>
    internal NativeConstructResourceLedgerCommit TryCommitTransfer(
        long expectedRevision, string fromInstanceId,
        string toInstanceId, int amount)
    {
        lock (_gate)
        {
            var blocked = CheckRevision(expectedRevision);
            if (blocked is not null) return blocked;
            var preview = _operating.PreviewResourceTransfer(_balances,
                fromInstanceId, toInstanceId, amount);
            if (!preview.CanTransfer)
                return Rejected("construct-resource-transfer-not-authorized",
                    "The current resource balances and native transfer requirements do not permit this transfer.",
                    transfer: preview);

            // Do not apply NextStep.Sources: it is an independent preview
            // which would additionally consume one step of fuel.
            var updated = preview.Sources.ToDictionary(
                x => x.InstanceId, x => x.After, StringComparer.Ordinal);
            return Apply(updated, "reservoir-transfer", null, preview);
        }
    }

    /// <summary>
    /// Replenish one finite power source after a separate trusted host has
    /// authenticated the source-specific recharge event and any consumed
    /// external resources. Both checks occur under the local revision lock.
    /// This still does not provide a cross-system durable transaction.
    /// </summary>
    internal NativeConstructResourceLedgerCommit TryCommitReplenishment(
        long expectedRevision, string instanceId, string method, int amount,
        INativeConstructReplenishmentAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        lock (_gate)
        {
            var blocked = CheckRevision(expectedRevision);
            if (blocked is not null) return blocked;
            var preview = _operating.PreviewReplenishment(
                _balances, instanceId, method, amount);
            if (!preview.CanReplenish)
                return Rejected("construct-resource-replenishment-not-authorized",
                    "The source, method, amount or current balance does not permit this replenishment.");
            var source = preview.Sources.Single(x => x.InstanceId == instanceId);
            if (!authority.AuthorizeReplenishment(instanceId, source.PowerSourceId,
                    source.ResourceKind, method, amount, _revision))
                return Rejected("construct-resource-replenishment-external-denied",
                    "The trusted host has not authorized the required external replenishment event.");

            var updated = preview.Sources.ToDictionary(
                x => x.InstanceId, x => x.After, StringComparer.Ordinal);
            return Apply(updated, "resource-replenishment", null, null);
        }
    }

    private NativeConstructResourceLedgerCommit? CheckRevision(long expectedRevision)
    {
        if (expectedRevision != _revision)
            return Rejected("construct-resource-ledger-revision-conflict",
                "The supplied resource revision is stale or does not belong to this ledger.");
        if (_revision == long.MaxValue)
            return Rejected("construct-resource-ledger-revision-exhausted",
                "The bounded in-process revision has reached its maximum value.");
        return null;
    }

    private NativeConstructResourceLedgerCommit Apply(
        Dictionary<string, int> updated,
        string operation,
        NativeConstructResourceStepPreview? step,
        NativeConstructResourceTransferPreview? transfer)
    {
        // Fail closed if an implementation change ever omits an installed
        // source or adds an unknown one. This also prevents partial commits.
        if (updated.Count != _balances.Count ||
            updated.Keys.Any(x => !_balances.ContainsKey(x)))
            return Rejected("construct-resource-ledger-shape-invalid",
                "The evaluated operation did not preserve the installed source set.",
                step, transfer);

        var beforeRevision = _revision;
        var changes = _balances.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new NativeConstructResourceLedgerDelta(
                x.Key, x.Value, updated[x.Key])).ToArray();
        foreach (var (id, value) in updated)
            _balances[id] = value;
        _revision++;
        return new(true, Current(),
            new(operation, beforeRevision, _revision, changes),
            [], step, transfer);
    }

    private NativeConstructResourceLedgerCommit Rejected(string code,
        string message,
        NativeConstructResourceStepPreview? preview = null,
        NativeConstructResourceTransferPreview? transfer = null) =>
        new(false, Current(), null,
            [new(code, "resourceLedger", NativeOriginIssueSeverity.Invalid,
                message)], preview, transfer);

    private NativeConstructResourceLedgerSnapshot Current() =>
        new(_revision, new Dictionary<string, int>(_balances,
            StringComparer.Ordinal));
}

/// <summary>
/// The trusted host must source starting balances and authorize the operation
/// separately. A successful in-process result is not a durable database write.
/// </summary>
internal sealed record NativeConstructResourceLedgerOpenResult(
    NativeConstructResourceLedger? Ledger,
    IReadOnlyList<NativeOriginContractIssue> Issues);

internal sealed record NativeConstructResourceLedgerSnapshot(
    long Revision, IReadOnlyDictionary<string, int> Balances);

internal sealed record NativeConstructResourceLedgerDelta(
    string InstanceId, int Before, int After);

/// <summary>
/// A transaction receipt suitable for later host integration; each change
/// references one native-installed source. Revision numbers are local to this
/// ledger instance and are not durable sequence numbers.
/// </summary>
internal sealed record NativeConstructResourceLedgerReceipt(
    string Operation, long PreviousRevision, long NewRevision,
    IReadOnlyList<NativeConstructResourceLedgerDelta> Changes);

internal sealed record NativeConstructResourceLedgerCommit(
    bool Committed, NativeConstructResourceLedgerSnapshot Snapshot,
    NativeConstructResourceLedgerReceipt? Receipt,
    IReadOnlyList<NativeOriginContractIssue> Issues,
    NativeConstructResourceStepPreview? Step = null,
    NativeConstructResourceTransferPreview? Transfer = null);

/// <summary>
/// A trusted host confirms the actual source event and all external resource
/// spending. Calls must be idempotent and tied to an authoritative character,
/// installation and revision; this local ledger does not provide persistence
/// or an atomic transaction with the external resource owner.
/// </summary>
internal interface INativeConstructReplenishmentAuthority
{
    bool AuthorizeReplenishment(string instanceId, string powerSourceId,
        string resourceKind, string method, int units, long expectedRevision);
}

