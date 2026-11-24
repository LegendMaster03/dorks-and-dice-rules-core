namespace RulesCore.Mechanics;

/// <summary>
/// Persistable solar counters, independent of the session-local ledger revision.
/// Every source must be independently authenticated before planning an interval.
/// </summary>
public sealed record NativeConstructSolarState(
    IReadOnlyDictionary<string, long> StoredPowerUnitMinutes,
    IReadOnlyDictionary<string, long> ConsecutiveDeficitMinutes,
    IReadOnlyDictionary<string, int> ExhaustionEventsIssued)
{
    public bool BlocksSolarExhaustionRecovery =>
        ConsecutiveDeficitMinutes.Values.Any(minutes => minutes > 0);
}

public sealed record NativeConstructSolarIntervalPlan(
    bool CanAdvance, NativeConstructSolarState? NextState,
    IReadOnlyList<NativeConstructSolarExhaustionAccrual>? ExhaustionChanges,
    NativeConstructSolarOperationPreview? Operation,
    IReadOnlyList<NativeOriginContractIssue> Issues);

/// <summary>
/// Run the existing ledger on a detached, independently verified snapshot.
/// The local ledger commit is only a calculation; the owning application must
/// atomically commit the resulting counters, time event and exhaustion effects.
/// </summary>
public static class NativeConstructSolarIntervalPlanner
{
    public static NativeConstructSolarIntervalPlan Plan(
        NativeConstructOperatingSession session, NativeConstructOperatingPreset current,
        NativeConstructSolarState state,
        IReadOnlyDictionary<string, NativeConstructSolarExposure> exposures, int elapsedMinutes)
    {
        ArgumentNullException.ThrowIfNull(session);
        var bound = session.EvaluatePreset(current);
        // Generic operation intentionally lacks solar exposure. Validate the
        // installation binding here, and evaluate power in the solar ledger.
        if (!bound.IsStructurallyValid)
            return new(false, null, null, null, bound.PresetIssues);
        if (state?.StoredPowerUnitMinutes is null || state.ConsecutiveDeficitMinutes is null ||
            state.ExhaustionEventsIssued is null || exposures is null || elapsedMinutes <= 0)
            return new(false, null, null, null,
                [new("construct-solar-interval-input-invalid", "solar",
                    NativeOriginIssueSeverity.Invalid, "Complete solar counters and a positive verified interval are required.")]);

        var opened = session.OpenTrustedSolarLedger(state.StoredPowerUnitMinutes,
            state.ConsecutiveDeficitMinutes, state.ExhaustionEventsIssued);
        if (opened.Ledger is not { } ledger)
            return new(false, null, null, null, opened.Issues);

        var enabled = current.Components.ToDictionary(part => part.InstanceId,
            part => part.Enabled, StringComparer.Ordinal);
        var modes = current.Components.Where(part => part.ModeId is not null)
            .ToDictionary(part => part.InstanceId, part => part.ModeId!, StringComparer.Ordinal);
        // Local revision zero belongs solely to this ephemeral ledger. The
        // authenticated durable revision is checked by the transport and host.
        var applied = ledger.TryCommitInterval(0, exposures, elapsedMinutes,
            new ExactIntervalAuthority(exposures, elapsedMinutes), enabled, modes);
        if (!applied.Committed)
            return new(false, null, null, applied.Preview, applied.Issues);

        var next = applied.Snapshot;
        return new(true, new(next.StoredPowerUnitMinutes,
            next.ConsecutiveDeficitMinutes, next.ExhaustionEventsIssued),
            applied.ExhaustionChanges, applied.Preview, applied.Issues);
    }

    // The only accepted interval is the exact one provided by the independent
    // host authority; this callback never accepts a client time/exposure claim.
    private sealed class ExactIntervalAuthority(
        IReadOnlyDictionary<string, NativeConstructSolarExposure> verifiedExposures,
        int verifiedMinutes) : INativeConstructSolarIntervalAuthority
    {
        public bool AuthorizeInterval(
            IReadOnlyDictionary<string, NativeConstructSolarExposure> exposures,
            int elapsedMinutes, long expectedRevision) =>
            ReferenceEquals(exposures, verifiedExposures) &&
            elapsedMinutes == verifiedMinutes && expectedRevision == 0;
    }
}
