using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A live, nonpersistent operating view of one already-authorized Construct
/// build. Caller-controlled input may change only per-instance enabled flags
/// and authored operating modes. Installed parts, materials, links and grants are captured
/// from the trusted construction result. This session is not a grant issuer.
/// </summary>
public sealed class NativeConstructOperatingSession
{
    private readonly NativeConstructAssemblyRequest _baseline;
    private readonly NativeConstructAssemblyEvaluator _assembly;
    private readonly string[] _originGrantIds;
    private sealed record OperationalInput(string InstanceId, string TraitId);
    private readonly IReadOnlyDictionary<string, OperationalInput[]> _operationalInputs;
    private readonly IReadOnlyDictionary<string, string[]> _synergyInputs;
    private readonly HashSet<string> _instanceIds;
    private readonly HashSet<string> _protectedIds;

    internal NativeConstructOperatingSession(NativeCatalogSnapshot catalog,
        NativeConstructAssemblyRequest approvedAssembly, NativeOriginProfile approvedProfile)
    {
        _assembly = new NativeConstructAssemblyEvaluator(catalog);
        _baseline = new NativeConstructAssemblyRequest(
            approvedAssembly.Components.Select(instance => instance with
            {
                Materials = instance.Materials is null ? null
                    : new Dictionary<string, string>(instance.Materials,
                        StringComparer.Ordinal),
                ConnectedTo = instance.ConnectedTo?.ToArray(),
                EnhancementIds = instance.EnhancementIds?.ToArray()
            }).ToArray(),
            approvedAssembly.InitialAllowance, InitialConstruction: true,
            PowerConnections: approvedAssembly.PowerConnections?.ToArray());

        _instanceIds = _baseline.Components.Select(x => x.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        // Only the trusted native evaluator can identify protected loads.
        _protectedIds = _assembly.Evaluate(_baseline).Components
            .Where(x => x.RequiresContinuousPower)
            .Select(x => x.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        var grants = approvedProfile.Current.InherentGrants.ToArray();
        _originGrantIds = grants.Select(g => g.Id)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();

        // A record copy also retains the authorized optional parent and mount
        // interface selections. Runtime callers cannot reparent installed parts.
        // Catalog-authored operational trait references identify *which*
        // component instances an effective grant requires at runtime.
        // The common Origin engine already verified the complete grant and
        // its source candidates. No new result is synthesized from this map.
        var operationalCandidates = new Dictionary<string, OperationalInput>(
            StringComparer.Ordinal);
        foreach (var instance in _baseline.Components)
        {
            if (!catalog.TryGetById(instance.DefinitionId, out var definition) ||
                definition?.Category != "construct-component" ||
                !definition.Definition.TryGetProperty("operationalOriginTraitIds",
                    out var traits) || traits.ValueKind != JsonValueKind.Array)
                continue;

            var index = 0;
            foreach (var trait in traits.EnumerateArray())
            {
                operationalCandidates.Add(
                    "construct:" + instance.InstanceId + ":operational-trait:" +
                    index++, new(instance.InstanceId, trait.GetString()!));
            }
        }

        var needs = new Dictionary<string, OperationalInput[]>(StringComparer.Ordinal);
        foreach (var grant in grants)
        {
            var inputs = grant.SourceCandidateIds
                .Where(operationalCandidates.ContainsKey)
                .Select(candidate => operationalCandidates[candidate])
                .Distinct()
                .OrderBy(x => x.InstanceId, StringComparer.Ordinal)
                .ThenBy(x => x.TraitId, StringComparer.Ordinal)
                .ToArray();
            if (inputs.Length > 0)
                needs.Add(grant.Id, inputs);
        }
        _operationalInputs = needs;
        _synergyInputs = grants.Where(x => x.Synergy is not null)
            .ToDictionary(g => g.Id,
                g => g.Synergy!.Inputs.Select(x => x.GrantId)
                    .Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Apply an enabled/disabled power allocation. Existing callers can use
    /// this shorthand when no authored mode change is needed.
    /// </summary>
    public NativeConstructOperatingStateResult EvaluateAllocation(
        IReadOnlyDictionary<string, bool> requestedEnabled) =>
        EvaluateConfiguration(requestedEnabled, null);

    /// <summary>
    /// Evaluate a proposed mode selection and power allocation against the
    /// original validated installation. The caller can neither add components
    /// nor make an undeclared mode or trait available.
    /// </summary>
    public NativeConstructOperatingStateResult EvaluateConfiguration(
        IReadOnlyDictionary<string, bool>? requestedEnabled = null,
        IReadOnlyDictionary<string, string>? requestedModes = null)
    {
        requestedEnabled ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        requestedModes ??= new Dictionary<string, string>(StringComparer.Ordinal);
        var unexpectedEnabled = requestedEnabled.Keys
            .Where(id => string.IsNullOrWhiteSpace(id) || !_instanceIds.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var unexpectedModes = requestedModes.Keys
            .Where(id => string.IsNullOrWhiteSpace(id) || !_instanceIds.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (unexpectedEnabled.Length > 0 || unexpectedModes.Length > 0)
        {
            var baseline = _assembly.Evaluate(_baseline);
            return new(baseline, [], _originGrantIds,
                [new("construct-operating-instance-unknown",
                    "operatingConfiguration", NativeOriginIssueSeverity.Invalid,
                    "Operating changes may only address instances in the authorized assembly.")]);
        }

        var configured = _baseline with
        {
            Components = _baseline.Components.Select(instance => instance with
            {
                Enabled = requestedEnabled.TryGetValue(instance.InstanceId, out var enabled)
                    ? enabled : instance.Enabled,
                ModeId = requestedModes.TryGetValue(instance.InstanceId, out var modeId)
                    ? modeId : instance.ModeId
            }).ToArray()
        };
        var state = _assembly.Evaluate(configured);
        var componentStates = state.Components.ToDictionary(x => x.InstanceId,
            StringComparer.Ordinal);
        var active = _originGrantIds.ToHashSet(StringComparer.Ordinal);

        // Mode-dependent applicability needs both an operational component and
        // a selected mode which explicitly authorizes this exact trait.
        // Invalid or overallocated configurations never activate powered grants.
        foreach (var (grantId, providers) in _operationalInputs)
            if (!state.IsAuthorized || providers.Any(input =>
                !componentStates.TryGetValue(input.InstanceId, out var component) ||
                !component.Operational ||
                component.ActiveOperationalOriginTraitIds is null ||
                !component.ActiveOperationalOriginTraitIds.Contains(input.TraitId,
                    StringComparer.Ordinal)))
                active.Remove(grantId);

        // Native synergies retain the same authorized effective grant record.
        // Only their runtime applicability is suppressed when an input is off.
        // Iterate to a fixed point for transitive synergy dependencies.
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (grantId, inputs) in _synergyInputs)
                if (active.Contains(grantId) &&
                    inputs.Any(id => !active.Contains(id)))
                {
                    active.Remove(grantId);
                    changed = true;
                }
        }
        return new(state,
            active.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            _originGrantIds.Where(id => !active.Contains(id)).ToArray(),
            state.Issues);
    }
    /// <summary>
    /// Preview ranked optional shedding without changing the approved Origin.
    /// Shutdown candidates are explicitly ordered, least desired first.
    /// A preview never applies real power changes or grants new capabilities.
    /// </summary>
    public NativeConstructPriorityPreview PreviewPriorityShedding(
        IReadOnlyList<string> shutdownOrder,
        IReadOnlyDictionary<string, bool>? requestedEnabled = null,
        IReadOnlyDictionary<string, string>? requestedModes = null)
    {
        ArgumentNullException.ThrowIfNull(shutdownOrder);
        var issues = new List<NativeOriginContractIssue>();
        var enabled = requestedEnabled is null
            ? new Dictionary<string, bool>(StringComparer.Ordinal)
            : new Dictionary<string, bool>(requestedEnabled, StringComparer.Ordinal);
        var initial = EvaluateConfiguration(enabled, requestedModes);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (shutdownOrder.Count > _instanceIds.Count)
            issues.Add(new("construct-priority-selection-invalid", "shutdownOrder",
                NativeOriginIssueSeverity.Invalid, "The shutdown list exceeds the installed instance count."));
        foreach (var id in shutdownOrder.Take(_instanceIds.Count + 1))
        {
            if (string.IsNullOrWhiteSpace(id) || !_instanceIds.Contains(id) || !seen.Add(id))
                issues.Add(new("construct-priority-selection-invalid", "shutdownOrder",
                    NativeOriginIssueSeverity.Invalid, "The shutdown order contains an unknown or duplicate instance."));
            else if (_protectedIds.Contains(id))
                issues.Add(new("construct-priority-protected", "shutdownOrder",
                    NativeOriginIssueSeverity.Invalid, "Continuous-power components cannot be automatically shed."));
        }

        NativeConstructPriorityPreview Finish(
            NativeConstructOperatingStateResult evaluation, IReadOnlyList<string> shed)
        {
            var unavailable = evaluation.Assembly.Components
                .Where(x => x.RequiresContinuousPower && !x.Operational)
                .Select(x => x.InstanceId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (unavailable.Length > 0)
                issues.Add(new("construct-continuous-power-unavailable", "operatingConfiguration",
                    NativeOriginIssueSeverity.Incomplete, "A continuous-power component is offline; inspect its authored consequences."));
            return new(evaluation, shed, unavailable, issues.ToArray());
        }

        if (issues.Count > 0 || initial.Issues.Any(x => x.Severity is
            NativeOriginIssueSeverity.Invalid or NativeOriginIssueSeverity.Unresolved
            or NativeOriginIssueSeverity.Conflict))
            return Finish(initial, []);

        var current = initial;
        var shedIds = new List<string>();
        foreach (var id in shutdownOrder)
        {
            var circuits = current.Assembly.PowerNetworks ?? [];
            if (!circuits.Any(x => x.IsOverallocated == true) ||
                circuits.Any(x => x.IsOverallocated is null)) break;
            if (!circuits.Any(x => x.IsOverallocated == true &&
                    x.ComponentInstanceIds.Contains(id, StringComparer.Ordinal))) continue;
            var oldOverride = enabled.TryGetValue(id, out var wasEnabled);
            if (!(oldOverride ? wasEnabled :
                    _baseline.Components.Single(x => x.InstanceId == id).Enabled)) continue;
            var before = circuits.Where(x => x.RequestedHeadroom < 0)
                .Sum(x => -x.RequestedHeadroom!.Value);
            enabled[id] = false;
            var trial = EvaluateConfiguration(enabled, requestedModes);
            var afterCircuits = trial.Assembly.PowerNetworks ?? [];
            var after = afterCircuits.Where(x => x.RequestedHeadroom < 0)
                .Sum(x => -x.RequestedHeadroom!.Value);
            var criticalLost = current.Assembly.Components.Any(part =>
                part.RequiresContinuousPower && part.Operational &&
                !trial.Assembly.Components.Any(next =>
                    next.InstanceId == part.InstanceId && next.Operational));
            if (criticalLost || after >= before ||
                afterCircuits.Any(x => x.IsOverallocated is null) ||
                trial.Issues.Any(x => x.Severity is
                    NativeOriginIssueSeverity.Invalid or NativeOriginIssueSeverity.Unresolved
                    or NativeOriginIssueSeverity.Conflict))
            {
                if (oldOverride) enabled[id] = wasEnabled;
                else enabled.Remove(id);
                issues.Add(new("construct-priority-shutdown-rejected", "shutdownOrder",
                    NativeOriginIssueSeverity.Incomplete,
                    "The shutdown fails to reduce known overload or interrupts an operating continuous-power component."));
                continue;
            }
            current = trial;
            shedIds.Add(id);
        }
        if ((current.Assembly.PowerNetworks ?? []).Any(x => x.IsOverallocated == true))
            issues.Add(new("construct-priority-capacity-insufficient", "shutdownOrder",
                NativeOriginIssueSeverity.Incomplete,
                "The supplied optional shutdown order cannot resolve all power overloads."));
        return Finish(current, shedIds.ToArray());
    }

}

/// <summary>
/// This is a transient applicability view, not a replacement Origin profile or
/// an independently authorized mechanical source. No profile mutation occurs.
/// </summary>
public sealed record NativeConstructOperatingStateResult(
    NativeConstructAssemblyResult Assembly,
    IReadOnlyList<string> ApplicableOriginGrantIds,
    IReadOnlyList<string> InactiveOriginGrantIds,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool IsOperatingConfigurationValid =>
        Assembly.IsAuthorized && Issues.Count == 0;
}

/// <summary>
/// A simulation result, not a persisted control system or Origin grant.
/// </summary>
public sealed record NativeConstructPriorityPreview(
    NativeConstructOperatingStateResult Evaluation,
    IReadOnlyList<string> ProposedShedInstanceIds,
    IReadOnlyList<string> InoperativeContinuousPowerInstanceIds,
    IReadOnlyList<NativeOriginContractIssue> PlanningIssues)
{
    public bool IsSatisfied => Evaluation.IsOperatingConfigurationValid &&
        InoperativeContinuousPowerInstanceIds.Count == 0 &&
        PlanningIssues.Count == 0;
}
