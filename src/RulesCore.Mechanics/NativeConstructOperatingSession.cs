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
    private readonly NativeCatalogSnapshot _catalog;
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
        _catalog = catalog;
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
            PowerConnections: approvedAssembly.PowerConnections?.ToArray(),
            UnavailablePowerSourceInstanceIds:
                approvedAssembly.UnavailablePowerSourceInstanceIds?.ToArray());

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
        IReadOnlyDictionary<string, string>? requestedModes = null,
        IReadOnlyList<string>? unavailablePowerSourceInstanceIds = null)
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
            // Never let a transient operating request clear a source outage
            // already captured in the trusted installation snapshot.
            UnavailablePowerSourceInstanceIds =
                unavailablePowerSourceInstanceIds is null
                    ? _baseline.UnavailablePowerSourceInstanceIds
                    : (_baseline.UnavailablePowerSourceInstanceIds ?? [])
                        .Concat(unavailablePowerSourceInstanceIds)
                        .Distinct(StringComparer.Ordinal).ToArray(),
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
    /// Capture an operating preset as ordinary data, not as authorization.
    /// Only configurations fully accepted by the current trusted evaluator
    /// may be captured. A saved preset must be revalidated before reuse.
    /// </summary>
    public NativeConstructPresetCapture CapturePreset(
        string name,
        IReadOnlyDictionary<string, bool>? requestedEnabled = null,
        IReadOnlyDictionary<string, string>? requestedModes = null)
    {
        var evaluated = EvaluateConfiguration(requestedEnabled, requestedModes);
        var issues = new List<NativeOriginContractIssue>();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 96 ||
            name.Any(char.IsControl))
            issues.Add(new("construct-preset-name-invalid", "name",
                NativeOriginIssueSeverity.Invalid,
                "Operating presets need nonempty names of at most 96 printable characters."));
        if (issues.Count > 0 || !evaluated.IsOperatingConfigurationValid)
            return new(null, new(evaluated, issues.ToArray()));

        var entries = _baseline.Components
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal)
            .Select(x => new NativeConstructPresetComponent(
                x.InstanceId, x.DefinitionId,
                requestedEnabled is not null &&
                    requestedEnabled.TryGetValue(x.InstanceId, out var flag)
                    ? flag : x.Enabled,
                requestedModes is not null &&
                    requestedModes.TryGetValue(x.InstanceId, out var mode)
                    ? mode : x.ModeId))
            .ToArray();
        return new(new(name, entries), new(evaluated, []));
    }

    /// <summary>
    /// Rebind externally stored operating preferences to this session's
    /// approved installation. Every instance and its native definition must
    /// match exactly; the serialized preset never supplies installation data.
    /// </summary>
    public NativeConstructPresetEvaluation EvaluatePreset(
        NativeConstructOperatingPreset? preset)
    {
        var issues = new List<NativeOriginContractIssue>();
        if (preset is null || preset.Components is null ||
            string.IsNullOrWhiteSpace(preset.Name) ||
            preset.Name.Length > 96 || preset.Name.Any(char.IsControl) ||
            preset.Components.Count != _baseline.Components.Count)
        {
            issues.Add(new("construct-preset-structure-invalid", "preset",
                NativeOriginIssueSeverity.Invalid,
                "A preset needs a valid name and exactly one entry for each installed component."));
        }

        var known = _baseline.Components.ToDictionary(x => x.InstanceId,
            StringComparer.Ordinal);
        var enabled = new Dictionary<string, bool>(StringComparer.Ordinal);
        var modes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (preset?.Components is not null)
        {
            // Bound the amount of caller-controlled data inspected even when
            // the reported component count has already failed validation.
            foreach (var component in preset.Components.Take(257))
            {
                if (component is null || string.IsNullOrWhiteSpace(component.InstanceId) ||
                    !known.TryGetValue(component.InstanceId, out var installed) ||
                    component.DefinitionId != installed.DefinitionId ||
                    !enabled.TryAdd(component.InstanceId, component.Enabled) ||
                    (component.ModeId is null && installed.ModeId is not null))
                {
                    issues.Add(new("construct-preset-instance-invalid", "preset.components",
                        NativeOriginIssueSeverity.Invalid,
                        "Preset entries must match unique installed instance and native definition IDs."));
                    continue;
                }
                if (component.ModeId is not null)
                    modes.Add(component.InstanceId, component.ModeId);
            }
        }

        if (issues.Count > 0 || enabled.Count != _instanceIds.Count)
        {
            if (issues.Count == 0)
                issues.Add(new("construct-preset-structure-invalid", "preset.components",
                    NativeOriginIssueSeverity.Invalid, "The preset omits installed components."));
            // Do not expose applicable grants from the fallback baseline as
            // though an untrusted preset successfully established their state.
            var baseline = EvaluateConfiguration();
            var denied = new NativeConstructOperatingStateResult(
                baseline.Assembly, [], _originGrantIds, issues.ToArray());
            return new(denied, issues.ToArray());
        }
        return new(EvaluateConfiguration(enabled, modes), []);
    }

    /// <summary>
    /// Compare two fully installation-bound presets without executing any
    /// action or treating recovery as reversal of past failure consequences.
    /// A power-loss source state may be Incomplete; target validity is
    /// evaluated independently through the native operating session.
    /// </summary>
    public NativeConstructOperatingTransitionPreview PreviewPresetTransition(
        NativeConstructOperatingPreset? before,
        NativeConstructOperatingPreset? after)
    {
        var source = EvaluatePreset(before);
        var target = EvaluatePreset(after);
        if (!source.IsStructurallyValid || !target.IsStructurallyValid ||
            source.Operating.Assembly.Status is
                NativeOriginConstructionStatus.Invalid or
                NativeOriginConstructionStatus.Unresolved or
                NativeOriginConstructionStatus.Conflicting ||
            target.Operating.Assembly.Status is
                NativeOriginConstructionStatus.Invalid or
                NativeOriginConstructionStatus.Unresolved or
                NativeOriginConstructionStatus.Conflicting)
            return new(source, target, [], [], [], [], [], [], []);

        var sourceParts = source.Operating.Assembly.Components.ToDictionary(
            x => x.InstanceId, StringComparer.Ordinal);
        var targetParts = target.Operating.Assembly.Components.ToDictionary(
            x => x.InstanceId, StringComparer.Ordinal);
        var restored = targetParts.Values.Where(x => x.Operational &&
                !sourceParts[x.InstanceId].Operational)
            .Select(x => x.InstanceId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var interrupted = sourceParts.Values.Where(x => x.Operational &&
                !targetParts[x.InstanceId].Operational)
            .Select(x => x.InstanceId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var sourceGrants = source.Operating.ApplicableOriginGrantIds.ToHashSet(
            StringComparer.Ordinal);
        var targetGrants = target.Operating.ApplicableOriginGrantIds.ToHashSet(
            StringComparer.Ordinal);
        // A consequence is newly exposed only when a component was
        // previously operating. Recovery does not erase historical effects.
        var newFailures = target.Operating.Assembly.ShutdownConsequences
            .Where(x => interrupted.Contains(x.InstanceId, StringComparer.Ordinal))
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal)
            .ThenBy(x => x.ConsequenceId, StringComparer.Ordinal).ToArray();
        return new(source, target, restored, interrupted,
            targetGrants.Except(sourceGrants, StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            sourceGrants.Except(targetGrants, StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            newFailures,
            restored.Where(x => targetParts[x].RequiresContinuousPower).ToArray(),
            interrupted.Where(x => sourceParts[x].RequiresContinuousPower).ToArray());
    }


    /// <summary>
    /// Preview one finite resource-consumption step from a caller-owned
    /// balance snapshot. This is not an authorization to refill resources
    /// or proof that an external ledger contains the supplied balances.
    /// </summary>
    public NativeConstructResourceStepPreview PreviewResourceStep(
        IReadOnlyDictionary<string, int> remainingResources,
        IReadOnlyDictionary<string, bool>? requestedEnabled = null,
        IReadOnlyDictionary<string, string>? requestedModes = null)
    {
        ArgumentNullException.ThrowIfNull(remainingResources);
        var issues = new List<NativeOriginContractIssue>();
        var installed = _baseline.Components.Where(x => x.PowerSourceId is not null)
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal).ToArray();
        var known = installed.Select(x => x.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        if (installed.Length == 0)
            issues.Add(new("construct-resource-model-unresolved", "resources",
                NativeOriginIssueSeverity.Unresolved,
                "A finite-duration preview requires at least one installed source with an authored resource model."));
        var sources = new List<(NativeConstructComponentInstance Part,
            string Kind, int Capacity, int Usage, int Remaining)>();
        foreach (var part in installed)
        {
            var path = "resources." + part.InstanceId;
            if (!_catalog.TryGetById(part.PowerSourceId!, out var native) ||
                native?.Category != "construct-power-source")
            {
                issues.Add(new("construct-resource-source-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "A resource source must resolve to a native installed power source."));
                continue;
            }
            var raw = native.Definition;
            var kind = raw.TryGetProperty("resourceKind", out var kindValue) &&
                kindValue.ValueKind == JsonValueKind.String ? kindValue.GetString() : null;
            var capacity = raw.TryGetProperty("resourceCapacity", out var capacityValue) &&
                capacityValue.ValueKind == JsonValueKind.Number &&
                capacityValue.TryGetInt32(out var c) && c > 0 ? c : 0;
            var usage = raw.TryGetProperty("resourceUsePerStep", out var usageValue) &&
                usageValue.ValueKind == JsonValueKind.Number &&
                usageValue.TryGetInt32(out var u) && u > 0 ? u : 0;
            if (string.IsNullOrWhiteSpace(kind) || capacity == 0 ||
                usage == 0 || usage > capacity)
            {
                issues.Add(new("construct-resource-model-unresolved", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "Finite resource kind, positive capacity and viable per-step cost must be authored."));
                continue;
            }
            if (!remainingResources.TryGetValue(part.InstanceId, out var remaining) ||
                remaining < 0 || remaining > capacity)
            {
                issues.Add(new("construct-resource-balance-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "The source balance must exist within its native capacity."));
                continue;
            }
            sources.Add((part, kind!, capacity, usage, remaining));
        }
        foreach (var id in remainingResources.Keys)
            if (!known.Contains(id))
                issues.Add(new("construct-resource-instance-invalid", "resources",
                    NativeOriginIssueSeverity.Invalid,
                    "Only installed native sources can have resource balances."));
        if (issues.Count > 0)
            return new(null, [], [], issues.ToArray());

        // The unavailable-source overlay removes source output, not the
        // source housing or any of its installed Origin entitlements.
        var unavailable = (_baseline.UnavailablePowerSourceInstanceIds ?? [])
            .Concat(sources.Where(x => x.Remaining < x.Usage)
                .Select(x => x.Part.InstanceId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var operating = EvaluateConfiguration(requestedEnabled, requestedModes,
            unavailable);
        var states = operating.Assembly.Components.ToDictionary(x =>
            x.InstanceId, StringComparer.Ordinal);
        var canSpend = operating.IsOperatingConfigurationValid;
        var result = sources.Select(x =>
        {
            var consumed = canSpend && states.TryGetValue(x.Part.InstanceId,
                out var component) && component.SourceSupplyingPower == true
                ? x.Usage : 0;
            return new NativeConstructResourceSourceState(
                x.Part.InstanceId, x.Part.PowerSourceId!, x.Kind,
                x.Capacity, x.Remaining, consumed, x.Remaining - consumed,
                x.Remaining >= x.Usage);
        }).ToArray();
        return new(operating, result, unavailable, []);
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

/// <summary>
/// Stored operating preferences, not an Origin snapshot or an authority token.
/// Every entry names an existing instance and the definition it was installed
/// from; saved data is rechecked on each use in the trusted operating session.
/// </summary>
public sealed record NativeConstructOperatingPreset(
    string Name, IReadOnlyList<NativeConstructPresetComponent> Components);

public sealed record NativeConstructPresetComponent(
    string InstanceId, string DefinitionId, bool Enabled, string? ModeId);

public sealed record NativeConstructPresetEvaluation(
    NativeConstructOperatingStateResult Operating,
    IReadOnlyList<NativeOriginContractIssue> PresetIssues)
{
    public bool IsStructurallyValid => PresetIssues.Count == 0;
    public bool IsOperatingConfigurationValid =>
        IsStructurallyValid && Operating.IsOperatingConfigurationValid;
}

public sealed record NativeConstructPresetCapture(
    NativeConstructOperatingPreset? Preset,
    NativeConstructPresetEvaluation Evaluation);

/// <summary>
/// A comparison of authorized installation-bound operating views. "Restored"
/// means power/function becomes available, not that past injuries, fuel use,
/// failures, or delayed consequences have been reversed.
/// </summary>
public sealed record NativeConstructOperatingTransitionPreview(
    NativeConstructPresetEvaluation Before,
    NativeConstructPresetEvaluation After,
    IReadOnlyList<string> RestoredComponentInstanceIds,
    IReadOnlyList<string> InterruptedComponentInstanceIds,
    IReadOnlyList<string> NewlyApplicableOriginGrantIds,
    IReadOnlyList<string> NewlyInactiveOriginGrantIds,
    IReadOnlyList<NativeConstructFailure> NewlyTriggeredShutdownConsequences,
    IReadOnlyList<string> RestoredContinuousPowerInstanceIds,
    IReadOnlyList<string> InterruptedContinuousPowerInstanceIds)
{
    public bool IsTargetValid =>
        Before.IsStructurallyValid && After.IsOperatingConfigurationValid &&
        // A technically evaluable manual shutdown is not a successful
        // recovery target if it leaves a continuous-power load offline.
        After.Operating.Assembly.Components.All(x =>
            !x.RequiresContinuousPower || x.Operational);
}

/// <summary>
/// One resource counter belongs to one installed source and an authored
/// resource kind. This is not an energy-output or recharge specification.
/// </summary>
public sealed record NativeConstructResourceSourceState(
    string InstanceId, string PowerSourceId, string ResourceKind,
    int Capacity, int Before, int Consumed, int After, bool SufficientForStep);

/// <summary>
/// Advisory finite-resource result. A gameplay ledger must separately
/// authenticate current balances and authorize any durable consumption.
/// </summary>
public sealed record NativeConstructResourceStepPreview(
    NativeConstructOperatingStateResult? Evaluation,
    IReadOnlyList<NativeConstructResourceSourceState> Sources,
    IReadOnlyList<string> UnavailablePowerSourceInstanceIds,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool CanAdvance => Evaluation?.IsOperatingConfigurationValid == true &&
        Issues.Count == 0;
}
