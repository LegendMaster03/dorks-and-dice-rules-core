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
    private sealed record OperationalInput(
        string InstanceId, string TraitId, string SourceDefinitionId,
        string OccurrencePath, bool IsPowerSource,
        IReadOnlyList<string>? ActiveComponentModes);
    private readonly IReadOnlyDictionary<string, OperationalInput[]> _operationalInputs;
    private readonly IReadOnlyDictionary<string, string[]> _synergyInputs;
    private readonly HashSet<string> _instanceIds;
    private readonly HashSet<string> _protectedIds;

    internal NativeConstructOperatingSession(NativeCatalogSnapshot catalog,
        NativeConstructAssemblyRequest approvedAssembly, NativeOriginProfile approvedProfile,
        IReadOnlyList<NativeConstructOriginOfferCompiler.OperationalOffer> operationalOffers)
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
            approvedAssembly.InitialAllowance, InitialConstruction: approvedAssembly.InitialConstruction,
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
        // Preserve the exact authorized native source occurrence for each
        // candidate. A component, material, design, enhancement and power
        // source may all offer the same canonical trait, but they are not
        // interchangeable operating providers. No grants are synthesized here.
        var operationalCandidates = operationalOffers.ToDictionary(
            offer => offer.CandidateId,
            offer => new OperationalInput(offer.InstanceId, offer.TraitId,
                offer.SourceDefinitionId, offer.OccurrencePath,
                offer.IsPowerSource, offer.ActiveComponentModes),
            StringComparer.Ordinal);

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

    // Return the immutable installation only to the trusted modification
    // planner. It compares snapshots; no public operating request may edit it.
    internal NativeConstructAssemblyRequest Installation => _baseline;

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
        IReadOnlyList<string>? unavailablePowerSourceInstanceIds = null) =>
        EvaluateConfigurationCore(requestedEnabled, requestedModes,
            unavailablePowerSourceInstanceIds, null);

    private NativeConstructOperatingStateResult EvaluateConfigurationCore(
        IReadOnlyDictionary<string, bool>? requestedEnabled,
        IReadOnlyDictionary<string, string>? requestedModes,
        IReadOnlyList<string>? unavailablePowerSourceInstanceIds,
        IReadOnlyDictionary<string, int>? trustedSourceOutputs)
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

        // Rated output alone is insufficient to authorize sources with
        // contextual operating rules. A source with no authenticated
        // environmental, soul-energy, or infernal-fuel context contributes
        // zero pending source-specific adjudication. This is not a
        // determination that a fuel-short generator loses normal output.
        var contextualSources = _baseline.Components
            .Where(x => x.PowerSourceId is
                "construct-power-source.solar-crystal" or
                "construct-power-source.soul-gem" or
                "construct-power-source.infernal-engine")
            .ToArray();
        var missingContext = contextualSources.Where(x =>
            trustedSourceOutputs is null ||
            !trustedSourceOutputs.ContainsKey(x.InstanceId)).ToArray();
        Dictionary<string, int>? evaluatedOutputs = trustedSourceOutputs is null &&
            missingContext.Length == 0 ? null
            : (trustedSourceOutputs ?? new Dictionary<string, int>(
                StringComparer.Ordinal)).ToDictionary(
                    pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (evaluatedOutputs is not null)
            foreach (var source in missingContext)
                evaluatedOutputs[source.InstanceId] = 0;
        var outOfEnergy = evaluatedOutputs is null
            ? Array.Empty<string>()
            : evaluatedOutputs.Where(x => x.Value == 0)
                .Select(x => x.Key).ToArray();
        var configured = _baseline with
        {
            // Never let a transient operating request clear a source outage
            // already captured in the trusted installation snapshot.
            // A conditional generator supplying zero power must also be
            // unavailable for native dependency and shutdown reconciliation.
            UnavailablePowerSourceInstanceIds =
                (_baseline.UnavailablePowerSourceInstanceIds ?? [])
                    .Concat(unavailablePowerSourceInstanceIds ?? [])
                    .Concat(outOfEnergy)
                    .Distinct(StringComparer.Ordinal).ToArray(),
            Components = _baseline.Components.Select(instance => instance with
            {
                Enabled = requestedEnabled.TryGetValue(instance.InstanceId, out var enabled)
                    ? enabled : instance.Enabled,
                ModeId = requestedModes.TryGetValue(instance.InstanceId, out var modeId)
                    ? modeId : instance.ModeId
            }).ToArray()
        };
        var state = _assembly.EvaluateWithSourceOutputOverrides(
            configured, evaluatedOutputs);
        if (missingContext.Length > 0)
            return new(state, [], _originGrantIds,
                state.Issues.Concat(missingContext.Select(x =>
                    new NativeOriginContractIssue(
                        x.PowerSourceId switch
                        {
                            "construct-power-source.solar-crystal" =>
                                "construct-solar-operating-context-required",
                            "construct-power-source.infernal-engine" =>
                                "construct-infernal-operating-context-required",
                            _ => "construct-soul-operating-context-required"
                        },
                        "operatingConfiguration",
                        NativeOriginIssueSeverity.Unresolved,
                        x.PowerSourceId switch
                        {
                            "construct-power-source.solar-crystal" =>
                                "Solar power requires validated light, battery and exhaustion state.",
                            "construct-power-source.infernal-engine" =>
                                "Infernal power requires verified long-rest fuel and instability status.",
                            _ => "Soul-core power requires DM-adjudicated fuel availability."
                        })))
                    .ToArray());
        var componentStates = state.Components.ToDictionary(x => x.InstanceId,
            StringComparer.Ordinal);
        var active = _originGrantIds.ToHashSet(StringComparer.Ordinal);

        // In addition to the evaluator's combined active trait set, validate
        // the selected offer's own operating source. An unselected housing
        // cannot substitute for an offline source, and an installed design
        // cannot substitute for a component trait disabled by its current mode.
        // Invalid or overallocated configurations never activate powered grants.
        foreach (var (grantId, providers) in _operationalInputs)
            if (!state.IsAuthorized || providers.Any(input =>
                !componentStates.TryGetValue(input.InstanceId, out var component) ||
                !component.Operational ||
                component.ActiveOperationalOriginTraitIds is null ||
                !component.ActiveOperationalOriginTraitIds.Contains(input.TraitId,
                    StringComparer.Ordinal) ||
                (input.IsPowerSource && component.SourceSupplyingPower != true) ||
                (input.ActiveComponentModes is not null &&
                    (component.ModeId is null ||
                     !input.ActiveComponentModes.Contains(component.ModeId,
                         StringComparer.Ordinal)))))
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

    private sealed record SolarRuntimeSource(
        NativeConstructComponentInstance Part, long Before, long Capacity,
        int CollectedPower, int EffectiveOutput, bool OverdriveActive);

    /// <summary>
    /// Preview a verified solar condition over elapsed game minutes.
    /// Stored energy uses integral power-unit-minutes (60 per power-unit-hour).
    /// Sunlight supplied here is hypothetical until a trusted game host
    /// authenticates the condition. This preview never creates a lasting
    /// power grant, refills a persistent resource or changes Origin.
    /// </summary>
    public NativeConstructSolarOperationPreview PreviewSolarOperation(
        IReadOnlyDictionary<string, long> storedEnergyPowerUnitMinutes,
        IReadOnlyDictionary<string, NativeConstructSolarExposure> exposureByInstance,
        int elapsedMinutes,
        IReadOnlyDictionary<string, bool>? requestedEnabled = null,
        IReadOnlyDictionary<string, string>? requestedModes = null)
    {
        ArgumentNullException.ThrowIfNull(storedEnergyPowerUnitMinutes);
        ArgumentNullException.ThrowIfNull(exposureByInstance);
        var issues = new List<NativeOriginContractIssue>();
        if (elapsedMinutes <= 0)
            issues.Add(new("construct-solar-elapsed-invalid", "solar.elapsedMinutes",
                NativeOriginIssueSeverity.Invalid,
                "Solar operation requires a positive number of elapsed game minutes."));

        var installed = _baseline.Components.Where(x =>
                x.PowerSourceId == "construct-power-source.solar-crystal")
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal).ToArray();
        if (installed.Length == 0)
            issues.Add(new("construct-solar-source-missing", "solar",
                NativeOriginIssueSeverity.Incomplete,
                "Solar operation requires an installed native solar source."));
        var sourceIds = installed.Select(x => x.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in storedEnergyPowerUnitMinutes.Keys
            .Concat(exposureByInstance.Keys))
            if (!sourceIds.Contains(id))
                issues.Add(new("construct-solar-instance-invalid", "solar",
                    NativeOriginIssueSeverity.Invalid,
                    "Solar input must reference an installed native solar source."));

        var sourceStates = new List<SolarRuntimeSource>();
        var proposedOutputs = new Dictionary<string, int>(StringComparer.Ordinal);
        var unavailable = (_baseline.UnavailablePowerSourceInstanceIds ?? [])
            .ToHashSet(StringComparer.Ordinal);
        foreach (var part in installed)
        {
            var path = "solar." + part.InstanceId;
            if (!_catalog.TryGetById(part.PowerSourceId!, out var native) ||
                native?.Category != "construct-power-source")
            {
                issues.Add(new("construct-solar-definition-missing", path,
                    NativeOriginIssueSeverity.Invalid,
                    "The installed solar source must resolve to native content."));
                continue;
            }
            var raw = native.Definition;
            static int? Natural(JsonElement raw, string name) =>
                raw.TryGetProperty(name, out var field) &&
                field.ValueKind == JsonValueKind.Number &&
                field.TryGetInt32(out var n) && n >= 0 ? n : null;
            var normal = Natural(raw, "powerOutput");
            var overdrive = Natural(raw, "overdrivePowerOutput");
            var hours = Natural(raw, "storageCapacityPowerUnitHours");
            var percentage = Natural(raw, "overdriveIncreasePercent");
            var values = new Dictionary<NativeConstructSolarExposure, int>();
            if (raw.TryGetProperty("solarCollectionPower", out var collection) &&
                collection.ValueKind == JsonValueKind.Object)
            {
                foreach (var (exposureKind, key) in new[]
                {
                    (NativeConstructSolarExposure.None, "none"),
                    (NativeConstructSolarExposure.Indirect, "indirect"),
                    (NativeConstructSolarExposure.Direct, "direct"),
                    (NativeConstructSolarExposure.Concentrated, "concentrated")
                })
                {
                    var rate = Natural(collection, key);
                    if (rate is int n) values[exposureKind] = n;
                }
            }
            if (normal is not int normalOutput ||
                overdrive is not int overdriveOutput ||
                hours is not int batteryHours ||
                percentage is not int increasePercent ||
                normalOutput <= 0 || batteryHours <= 0 ||
                increasePercent != 50 ||
                (long)overdriveOutput != normalOutput +
                    (long)normalOutput * increasePercent / 100 ||
                values.Count != 4)
            {
                issues.Add(new("construct-solar-model-invalid", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "Native solar generation, 50% overdrive and storage must be completely authored."));
                continue;
            }
            var capacity = (long)batteryHours * 60;
            if (!storedEnergyPowerUnitMinutes.TryGetValue(part.InstanceId,
                out var before) || before < 0 || before > capacity)
            {
                issues.Add(new("construct-solar-balance-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "Stored solar energy must be present and within native capacity."));
                continue;
            }
            if (!exposureByInstance.TryGetValue(part.InstanceId, out var exposure) ||
                !values.TryGetValue(exposure, out var collectionPower))
            {
                issues.Add(new("construct-solar-exposure-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "A recognized solar-exposure condition must be supplied for each source."));
                continue;
            }
            var enabled = requestedEnabled is not null &&
                requestedEnabled.TryGetValue(part.InstanceId, out var flag)
                ? flag : part.Enabled;
            var available = enabled && !unavailable.Contains(part.InstanceId);
            var overdriveActive = available &&
                exposure == NativeConstructSolarExposure.Concentrated;
            var generated = available ? collectionPower : 0;
            var limit = overdriveActive ? overdriveOutput : normalOutput;
            // Insufficient sunlight and depleted storage no longer disable
            // ordinary output. Instead, operating beyond the collected and
            // stored supply incurs Construct exhaustion. The host applies
            // the recorded exhaustion consequence to character state.
            var output = available && elapsedMinutes > 0 ? limit : 0;
            proposedOutputs[part.InstanceId] = output;
            sourceStates.Add(new(part, before, capacity, generated,
                output, overdriveActive));
        }
        if (issues.Count > 0)
            return new(null, [], issues.ToArray());

        var operating = EvaluateConfigurationCore(requestedEnabled, requestedModes,
            null, proposedOutputs);
        var componentById = operating.Assembly.Components
            .ToDictionary(x => x.InstanceId, StringComparer.Ordinal);
        var supplied = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var network in (operating.Assembly.PowerNetworks ?? []))
        {
            var inNetwork = sourceStates.Where(x =>
                    network.ComponentInstanceIds.Contains(x.Part.InstanceId,
                        StringComparer.Ordinal))
                .OrderBy(x => x.Part.InstanceId, StringComparer.Ordinal).ToArray();
            var solarCapacity = inNetwork
                .Where(x => componentById.TryGetValue(x.Part.InstanceId,
                    out var state) && state.SourceSupplyingPower == true)
                .Sum(x => (long)x.EffectiveOutput);
            var nonSolarOutput = Math.Max(0, network.AvailableOutput - solarCapacity);
            var activeSolarCount = inNetwork.Count(x =>
                componentById.TryGetValue(x.Part.InstanceId, out var state) &&
                state.SourceSupplyingPower == true);
            if (network.RequestedDemand > 0 && activeSolarCount > 1)
                issues.Add(new("construct-solar-multiple-source-allocation-unresolved",
                    "solar.powerNetworks", NativeOriginIssueSeverity.Unresolved,
                    "More than one active solar source requires an approved dispatch priority."));
            if (network.RequestedDemand > 0 && solarCapacity > 0 &&
                nonSolarOutput > 0)
                issues.Add(new("construct-solar-shared-network-allocation-unresolved",
                    "solar.powerNetworks", NativeOriginIssueSeverity.Unresolved,
                    "A shared solar and non-solar power network needs a separately approved source-dispatch policy."));
            var remaining = Math.Max(0, network.RequestedDemand - nonSolarOutput);
            foreach (var source in inNetwork)
            {
                var online = componentById[source.Part.InstanceId].SourceSupplyingPower == true;
                var delivered = online ? Math.Min(remaining, source.EffectiveOutput) : 0;
                supplied[source.Part.InstanceId] = delivered;
                remaining -= delivered;
            }
        }

        if (issues.Count > 0)
            return new(operating, [], issues.ToArray());

        var states = sourceStates.Select(source =>
        {
            // An invalid allocation cannot spend or produce energy.
            var operational = operating.IsOperatingConfigurationValid &&
                componentById[source.Part.InstanceId].SourceSupplyingPower == true;
            var delivered = operational ? supplied.GetValueOrDefault(
                source.Part.InstanceId) : 0;
            var collection = operational ? source.CollectedPower : 0;
            var netDraw = Math.Max(0L, delivered - collection);
            var unfunded = operational
                ? Math.Max(0L, netDraw * elapsedMinutes - source.Before)
                : 0L;
            var shortageMinutes = unfunded == 0 || netDraw == 0 ? 0 :
                (int)Math.Min(elapsedMinutes,
                    (unfunded + netDraw - 1) / netDraw);
            var after = operational
                ? Math.Clamp(source.Before +
                    ((long)collection - delivered) * elapsedMinutes,
                    0, source.Capacity)
                : source.Before;
            return new NativeConstructSolarSourceState(
                source.Part.InstanceId, source.Part.PowerSourceId!,
                source.Capacity, source.Before, after, collection,
                source.EffectiveOutput, delivered, source.OverdriveActive,
                unfunded, shortageMinutes);
        }).ToArray();
        return new(operating, states, []);
    }

    /// <summary>
    /// Create a revision-checked, in-process solar balance ledger after the
    /// trusted host has authenticated its initial balances. An invalid
    /// operating allocation does not invalidate otherwise genuine reserves.
    /// </summary>
    internal NativeConstructSolarLedgerOpenResult OpenTrustedSolarLedger(
        IReadOnlyDictionary<string, long> authenticatedBalances,
        IReadOnlyDictionary<string, long>? authenticatedDeficitMinutes = null,
        IReadOnlyDictionary<string, int>? authenticatedExhaustionEvents = null)
    {
        ArgumentNullException.ThrowIfNull(authenticatedBalances);
        var exposures = _baseline.Components
            .Where(x => x.PowerSourceId == "construct-power-source.solar-crystal")
            .ToDictionary(x => x.InstanceId, _ => NativeConstructSolarExposure.None,
                StringComparer.Ordinal);
        var inspected = PreviewSolarOperation(authenticatedBalances, exposures, 1);
        var issues = new List<NativeOriginContractIssue>(inspected.Issues);
        if ((authenticatedDeficitMinutes is null) !=
            (authenticatedExhaustionEvents is null))
            issues.Add(new("construct-solar-exhaustion-state-incomplete",
                "solarLedger", NativeOriginIssueSeverity.Invalid,
                "Restored exhaustion state requires both deficit and event counters."));
        if (authenticatedDeficitMinutes is not null &&
            (authenticatedDeficitMinutes.Count != exposures.Count ||
             authenticatedDeficitMinutes.Any(x =>
                 !exposures.ContainsKey(x.Key) || x.Value < 0)))
            issues.Add(new("construct-solar-deficit-state-invalid",
                "solarLedger", NativeOriginIssueSeverity.Invalid,
                "Every installed solar source needs a nonnegative deficit counter."));
        if (authenticatedExhaustionEvents is not null &&
            (authenticatedExhaustionEvents.Count != exposures.Count ||
             authenticatedExhaustionEvents.Any(x =>
                 !exposures.ContainsKey(x.Key) || x.Value < 0)))
            issues.Add(new("construct-solar-exhaustion-state-invalid",
                "solarLedger", NativeOriginIssueSeverity.Invalid,
                "Every installed solar source needs a nonnegative exhaustion-event counter."));
        return issues.Count > 0
            ? new(null, issues)
            : new(new NativeConstructSolarEnergyLedger(this,
                authenticatedBalances, authenticatedDeficitMinutes,
                authenticatedExhaustionEvents), []);
    }

    /// <summary>
    /// Evaluate a hypothetical soul-fueled operating configuration.
    /// Soul-energy availability is a DM decision: no invented fuel quantity,
    /// timed depletion, queue, or implicit replenishment is computed here.
    /// The host must authenticate this state before committing gameplay.
    /// </summary>
    public NativeConstructSoulCoreOperationPreview PreviewSoulCoreOperation(
        IReadOnlyDictionary<string, bool> dmAdjudicatedEnergyAvailable,
        IReadOnlyDictionary<string, bool>? overdriveRequested = null,
        IReadOnlyDictionary<string, bool>? requestedEnabled = null,
        IReadOnlyDictionary<string, string>? requestedModes = null)
    {
        ArgumentNullException.ThrowIfNull(dmAdjudicatedEnergyAvailable);
        overdriveRequested ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        var issues = new List<NativeOriginContractIssue>();
        var installed = _baseline.Components.Where(x =>
                x.PowerSourceId == "construct-power-source.soul-gem")
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal).ToArray();
        var ids = installed.Select(x => x.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0)
            issues.Add(new("construct-soul-core-absent", "soul",
                NativeOriginIssueSeverity.Incomplete,
                "Soul-core operation requires an installed native soul source."));
        if (dmAdjudicatedEnergyAvailable.Keys.Concat(overdriveRequested.Keys)
            .Any(id => !ids.Contains(id)))
            issues.Add(new("construct-soul-core-instance-invalid", "soul",
                NativeOriginIssueSeverity.Invalid,
                "Soul-core decisions must reference installed native source instances."));

        var output = new Dictionary<string, int>(StringComparer.Ordinal);
        var states = new List<NativeConstructSoulCoreSourceState>();
        foreach (var part in installed)
        {
            var path = "soul." + part.InstanceId;
            if (!dmAdjudicatedEnergyAvailable.TryGetValue(
                part.InstanceId, out var energyAvailable))
            {
                issues.Add(new("construct-soul-energy-undetermined", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "The DM must determine whether usable soul energy remains."));
                continue;
            }
            if (!_catalog.TryGetById(part.PowerSourceId!, out var native) ||
                native?.Category != "construct-power-source")
            {
                issues.Add(new("construct-soul-core-definition-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "Soul-core operating values must resolve to native content."));
                continue;
            }
            var raw = native.Definition;
            var normal = 0;
            var maximum = 0;
            var hasNormal = raw.TryGetProperty("powerOutput", out var basePower) &&
                basePower.ValueKind == JsonValueKind.Number &&
                basePower.TryGetInt32(out normal);
            var hasOverdrive = raw.TryGetProperty("overdrivePowerOutput",
                out var enhanced) && enhanced.ValueKind == JsonValueKind.Number &&
                enhanced.TryGetInt32(out maximum);
            if (!hasNormal || !hasOverdrive || normal <= 0 ||
                maximum != normal + (long)normal / 2)
            {
                issues.Add(new("construct-soul-output-unresolved", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "Native normal power and 50% Overdrive output are required."));
                continue;
            }
            var boosted = overdriveRequested.TryGetValue(part.InstanceId,
                out var requested) && requested && energyAvailable;
            var assigned = energyAvailable ? (boosted ? maximum : normal) : 0;
            output[part.InstanceId] = assigned;
            states.Add(new(part.InstanceId, energyAvailable, boosted, assigned));
        }
        if (issues.Count > 0)
            return new(null, [], issues);

        var evaluated = EvaluateConfigurationCore(
            requestedEnabled, requestedModes, null, output);
        return new(evaluated, states, []);
    }

    /// <summary>
    /// Plan one authenticated day or long rest using the native source's
    /// normalized fuel requirement. The host reserves distinct inventory
    /// units for this event and atomically commits any proposed debit.
    /// A missed payment is a valid event, not an invalid operation.
    /// </summary>
    public NativeConstructSustenancePlan PlanSustenance(
        string eventKind, IReadOnlyDictionary<string, int> reservedFuelUnits,
        int? constitutionModifier = null, int? previousDaysWithoutFullRation = null)
    {
        NativeConstructSustenancePlan Reject(string code, string message) =>
            new(false, [], null, [new(code, "sustenance", NativeOriginIssueSeverity.Invalid, message)]);

        var mechanical = eventKind == "mechanical-day";
        if (!mechanical && eventKind != "infernal-long-rest")
            return Reject("construct-sustenance-event-invalid", "An authored daily or long-rest event is required.");
        var sourceId = mechanical ? "construct-power-source.mechanical-engine"
            : "construct-power-source.infernal-engine";
        var installed = _baseline.Components.Where(part => part.PowerSourceId == sourceId)
            .OrderBy(part => part.InstanceId, StringComparer.Ordinal).ToArray();
        if (installed.Length == 0 || (mechanical && installed.Length != 1))
            return Reject("construct-sustenance-source-unresolved",
                "A matching installed source is required; the allocation of one daily ration across multiple mechanical engines is unresolved.");
        if (reservedFuelUnits is null || reservedFuelUnits.Count != installed.Length ||
            installed.Any(part => !reservedFuelUnits.ContainsKey(part.InstanceId)))
            return Reject("construct-sustenance-reservation-invalid",
                "The host must account for fuel reserved for each applicable installed source.");
        if (mechanical && (constitutionModifier is null || previousDaysWithoutFullRation is null ||
            previousDaysWithoutFullRation < 0))
            return Reject("construct-sustenance-deprivation-state-invalid",
                "The host must supply the verified Constitution modifier and existing deprivation counter.");
        if (!mechanical && (constitutionModifier is not null || previousDaysWithoutFullRation is not null))
            return Reject("construct-sustenance-deprivation-state-invalid",
                "Infernal long-rest fuel does not use the ordinary daily deprivation counter.");

        if (!_catalog.TryGetById(sourceId, out var native) || native?.Category != "construct-power-source" ||
            !native.Definition.TryGetProperty(mechanical ? "dailySustenance" : "longRestSustenance", out var rule) ||
            rule.ValueKind != JsonValueKind.Object ||
            !rule.TryGetProperty("resourceKind", out var kind) || kind.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(kind.GetString()) ||
            !rule.TryGetProperty(mechanical ? "unitsPerDay" : "unitsPerLongRest", out var quantity) ||
            quantity.ValueKind != JsonValueKind.Number || !quantity.TryGetInt32(out var required) || required != 1)
            return Reject("construct-sustenance-model-unresolved",
                "The native normalized one-charge sustenance rule is required.");

        var results = new List<NativeConstructSustenanceSourcePlan>();
        NativeDailyRationAssessment? deprivation = null;
        foreach (var part in installed)
        {
            var supplied = reservedFuelUnits[part.InstanceId];
            if (supplied < 0 || supplied > required)
                return Reject("construct-sustenance-reservation-invalid",
                    "Reserved fuel must be zero or the exact authored requirement.");
            var paid = supplied == required;
            if (mechanical)
                deprivation = NativeDailyRationEvaluator.AssessDay(
                    new NativeDailyRationRequirement(kind.GetString()!, required), supplied,
                    constitutionModifier!.Value, previousDaysWithoutFullRation!.Value);
            // Every long rest ends the previous empowerment, regardless of
            // fuel or operating configuration. Sustenance is not activation.
            results.Add(new(part.InstanceId, sourceId, kind.GetString()!,
                required, supplied, paid, !mechanical && !paid,
                mechanical ? null : false));
        }
        return new(true, results, deprivation, []);
    }

    /// <summary>
    /// Preview Infernal Engine output and a host-authenticated empowerment
    /// condition. Fuel for ordinary sustenance is paid at a long rest;
    /// separate fuel activates empowerment until the next long rest.
    /// The empowerment has no approved gameplay effect yet, and never
    /// increases the generator's general 8-unit output.
    /// </summary>
    public NativeConstructInfernalOperationPreview PreviewInfernalOperation(
        IReadOnlyDictionary<string, bool> longRestFuelProvided,
        IReadOnlyDictionary<string, bool>? dmVerifiedEmpowermentActive = null,
        IReadOnlyDictionary<string, bool>? requestedEnabled = null,
        IReadOnlyDictionary<string, string>? requestedModes = null)
    {
        ArgumentNullException.ThrowIfNull(longRestFuelProvided);
        dmVerifiedEmpowermentActive ??=
            new Dictionary<string, bool>(StringComparer.Ordinal);
        var issues = new List<NativeOriginContractIssue>();
        var installed = _baseline.Components.Where(x =>
                x.PowerSourceId == "construct-power-source.infernal-engine")
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal).ToArray();
        var ids = installed.Select(x => x.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0)
            issues.Add(new("construct-infernal-source-absent", "infernal",
                NativeOriginIssueSeverity.Incomplete,
                "Infernal operation requires an installed native Infernal Engine."));
        if (longRestFuelProvided.Keys.Concat(dmVerifiedEmpowermentActive.Keys)
            .Any(id => !ids.Contains(id)))
            issues.Add(new("construct-infernal-instance-invalid", "infernal",
                NativeOriginIssueSeverity.Invalid,
                "Infernal state must reference installed native engines."));

        var outputs = new Dictionary<string, int>(StringComparer.Ordinal);
        var sourceStates = new List<NativeConstructInfernalSourceState>();
        foreach (var part in installed)
        {
            var path = "infernal." + part.InstanceId;
            if (!longRestFuelProvided.TryGetValue(part.InstanceId, out var fed))
            {
                issues.Add(new("construct-infernal-fuel-state-required", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "The host must confirm whether long-rest fuel was provided."));
                continue;
            }
            if (!_catalog.TryGetById(part.PowerSourceId!, out var native) ||
                native?.Category != "construct-power-source")
            {
                issues.Add(new("construct-infernal-source-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "Infernal source must resolve to approved native content."));
                continue;
            }
            var raw = native.Definition;
            var normal = 0;
            if (!raw.TryGetProperty("powerOutput", out var outputValue) ||
                outputValue.ValueKind != JsonValueKind.Number ||
                !outputValue.TryGetInt32(out normal) || normal <= 0 ||
                !raw.TryGetProperty("infernalEmpowerment", out var enhancement) ||
                enhancement.ValueKind != JsonValueKind.Object ||
                !enhancement.TryGetProperty("duration", out var duration) ||
                duration.ValueKind != JsonValueKind.String ||
                duration.GetString() != "until-next-long-rest" ||
                !enhancement.TryGetProperty("powerOutputBonus", out var bonus) ||
                bonus.ValueKind != JsonValueKind.Number ||
                !bonus.TryGetInt32(out var powerBonus) || powerBonus != 0)
            {
                issues.Add(new("construct-infernal-model-unresolved", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "Infernal Empowerment must be authored without a power-output increase."));
                continue;
            }
            var empowered = dmVerifiedEmpowermentActive.TryGetValue(
                part.InstanceId, out var active) && active;
            outputs[part.InstanceId] = normal;
            sourceStates.Add(new(part.InstanceId, fed, empowered,
                !fed, normal));
        }
        if (issues.Count > 0)
            return new(null, [], issues);

        var evaluated = EvaluateConfigurationCore(
            requestedEnabled, requestedModes, null, outputs);
        return new(evaluated, sourceStates, []);
    }

    /// <summary>
    /// Identify every installed soul core within range of a DM-authenticated
    /// eligible death. This is passive and independent of the housing's
    /// enabled flag. It does not select among overlapping cores, mutate
    /// captured-soul history, or decide whether a soul is eligible.
    /// </summary>
    public NativeConstructSoulCapturePreview PreviewAutomaticSoulCapture(
        bool eligibleSoul,
        IReadOnlyDictionary<string, int> distancesFeetBySource)
    {
        ArgumentNullException.ThrowIfNull(distancesFeetBySource);
        var issues = new List<NativeOriginContractIssue>();
        var installed = _baseline.Components.Where(x =>
                x.PowerSourceId == "construct-power-source.soul-gem")
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal).ToArray();
        var ids = installed.Select(x => x.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        if (distancesFeetBySource.Keys.Any(id => !ids.Contains(id)))
            issues.Add(new("construct-soul-capture-instance-invalid", "soulCapture",
                NativeOriginIssueSeverity.Invalid,
                "Death proximity must reference only installed soul cores."));
        var matches = new List<NativeConstructSoulCaptureCandidate>();
        foreach (var part in installed)
        {
            var path = "soulCapture." + part.InstanceId;
            if (!distancesFeetBySource.TryGetValue(part.InstanceId, out var distance) ||
                distance < 0)
            {
                issues.Add(new("construct-soul-capture-distance-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "An authenticated nonnegative distance is required for each installed core."));
                continue;
            }
            if (!_catalog.TryGetById(part.PowerSourceId!, out var native) ||
                native?.Category != "construct-power-source" ||
                !native.Definition.TryGetProperty("captureRangeFeet",
                    out var captureRange) ||
                captureRange.ValueKind != JsonValueKind.Number ||
                !captureRange.TryGetInt32(out var radius) || radius <= 0)
            {
                issues.Add(new("construct-soul-capture-range-unresolved", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "The native soul source must specify its approved candidate capture radius."));
                continue;
            }
            if (eligibleSoul && distance <= radius)
                matches.Add(new(part.InstanceId, distance, radius));
        }
        return new(eligibleSoul,
            issues.Count == 0 ? matches : [],
            issues.ToArray());
    }

    /// <summary>
    /// Open an in-process custody ledger from records already authenticated
    /// by the host. This does not infer consumption or create a starting soul.
    /// The host must still persist committed death and rescue transitions.
    /// </summary>
    internal NativeConstructSoulCustodyOpenResult OpenTrustedSoulCustodyLedger(
        IReadOnlyList<NativeConstructSoulCustodyRecord> authenticatedRecords)
    {
        ArgumentNullException.ThrowIfNull(authenticatedRecords);
        var sources = _baseline.Components.Where(x =>
                x.PowerSourceId == "construct-power-source.soul-gem")
            .Select(x => x.InstanceId).ToHashSet(StringComparer.Ordinal);
        var issues = new List<NativeOriginContractIssue>();
        if (sources.Count == 0)
            issues.Add(new("construct-soul-custody-core-missing",
                "soulCustody", NativeOriginIssueSeverity.Incomplete,
                "An installed soul-powered core is required."));
        var currentlyImprisonedSoulIds =
            new HashSet<string>(StringComparer.Ordinal);
        var deathIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in authenticatedRecords)
        {
            if (record is null || string.IsNullOrWhiteSpace(record.SoulId) ||
                string.IsNullOrWhiteSpace(record.DeathEventId) ||
                record.SoulId.Length > 256 ||
                record.DeathEventId.Length > 256 ||
                !sources.Contains(record.CoreInstanceId) ||
                !Enum.IsDefined(record.Disposition) ||
                (record.Disposition == NativeConstructSoulDisposition.Imprisoned &&
                    !currentlyImprisonedSoulIds.Add(record.SoulId)) ||
                !deathIds.Add(record.DeathEventId))
                issues.Add(new("construct-soul-custody-record-invalid",
                    "soulCustody.records", NativeOriginIssueSeverity.Invalid,
                    "Each source-verified soul and death identity must occur once."));
        }
        return issues.Count > 0
            ? new(null, issues)
            : new(new NativeConstructSoulCustodyLedger(this,
                authenticatedRecords), []);
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



    // The same native source bounds apply to resource consumption and
    // physical transfer. Caller-provided balances are hypothetical and
    // never become authority to mint or replenish resources.
    private sealed record FiniteResource(
        NativeConstructComponentInstance Part, string Kind,
        int Capacity, int Usage, int Remaining,
        bool TransferOut, bool TransferIn);

    private (FiniteResource[] Sources, NativeOriginContractIssue[] Issues)
        ResolveFiniteResources(IReadOnlyDictionary<string, int> remainingResources)
    {
        var issues = new List<NativeOriginContractIssue>();
        var installed = _baseline.Components.Where(x => x.PowerSourceId is not null)
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal).ToArray();
        var known = installed.Select(x => x.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        if (installed.Length == 0)
            issues.Add(new("construct-resource-model-unresolved", "resources",
                NativeOriginIssueSeverity.Unresolved,
                "Finite-resource operations require at least one installed source with authored resource units."));

        var sources = new List<FiniteResource>();
        foreach (var part in installed)
        {
            var path = "resources." + part.InstanceId;
            if (!_catalog.TryGetById(part.PowerSourceId!, out var native) ||
                native?.Category != "construct-power-source")
            {
                issues.Add(new("construct-resource-source-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "The installed source must resolve to a native power-source definition."));
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
                    "The source needs an authored resource kind, finite positive capacity and per-step consumption."));
                continue;
            }
            if (!remainingResources.TryGetValue(part.InstanceId, out var remaining) ||
                remaining < 0 || remaining > capacity)
            {
                issues.Add(new("construct-resource-balance-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "The balance must be present and within the native capacity."));
                continue;
            }

            static bool ReadFlag(JsonElement raw, string field, string path,
                ICollection<NativeOriginContractIssue> issues)
            {
                if (!raw.TryGetProperty(field, out var property)) return false;
                if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    return property.ValueKind == JsonValueKind.True;
                issues.Add(new("construct-resource-transfer-flag-invalid",
                    path + "." + field, NativeOriginIssueSeverity.Invalid,
                    "Native resource-transfer ports must be authored booleans."));
                return false;
            }

            sources.Add(new(part, kind!, capacity, usage, remaining,
                ReadFlag(raw, "resourceTransferOut", path, issues),
                ReadFlag(raw, "resourceTransferIn", path, issues)));
        }
        foreach (var id in remainingResources.Keys)
            if (!known.Contains(id))
                issues.Add(new("construct-resource-instance-invalid", "resources",
                    NativeOriginIssueSeverity.Invalid,
                    "Only installed native sources may have resource balances."));
        return (sources.ToArray(), issues.ToArray());
    }


    /// <summary>
    /// Initialize an in-process resource ledger from balances that a trusted
    /// host has already authenticated. This entrypoint deliberately is not
    /// exposed to client requests: validating native bounds alone cannot
    /// prove resource acquisition or previous consumption.
    /// </summary>
    internal NativeConstructResourceLedgerOpenResult OpenTrustedResourceLedger(
        IReadOnlyDictionary<string, int> trustedBalances)
    {
        ArgumentNullException.ThrowIfNull(trustedBalances);
        var (_, issues) = ResolveFiniteResources(trustedBalances);
        return issues.Length > 0
            ? new(null, issues)
            : new(new NativeConstructResourceLedger(this, trustedBalances), []);
    }

    /// <summary>
    /// Preview a conserved transfer between two *directly physically attached*
    /// finite reservoirs. Both native mechanisms must explicitly author a
    /// compatible import/export port for an exact shared resource kind.
    /// This does not perform a gameplay action or restore a source which
    /// was already marked unavailable by the trusted installation.
    /// </summary>
    public NativeConstructResourceTransferPreview PreviewResourceTransfer(
        IReadOnlyDictionary<string, int> remainingResources,
        string fromInstanceId, string toInstanceId, int amount,
        IReadOnlyDictionary<string, bool>? requestedEnabled = null,
        IReadOnlyDictionary<string, string>? requestedModes = null)
    {
        ArgumentNullException.ThrowIfNull(remainingResources);
        var (sources, issues) = ResolveFiniteResources(remainingResources);
        if (issues.Length > 0)
            return new([], null, issues);

        var errors = new List<NativeOriginContractIssue>();
        var from = sources.SingleOrDefault(x => x.Part.InstanceId == fromInstanceId);
        var to = sources.SingleOrDefault(x => x.Part.InstanceId == toInstanceId);
        if (from is null || to is null || fromInstanceId == toInstanceId)
            errors.Add(new("construct-resource-transfer-endpoints-invalid",
                "transfer", NativeOriginIssueSeverity.Invalid,
                "A resource transfer needs two distinct installed finite-resource sources."));
        if (amount <= 0)
            errors.Add(new("construct-resource-transfer-amount-invalid",
                "transfer.amount", NativeOriginIssueSeverity.Invalid,
                "Transfers require a positive whole number of resource units."));

        if (from is not null && to is not null &&
            fromInstanceId != toInstanceId)
        {
            if (from.Kind != to.Kind)
                errors.Add(new("construct-resource-transfer-kind-invalid",
                    "transfer", NativeOriginIssueSeverity.Invalid,
                    "Reservoirs must use exactly the same native resource kind."));
            if (!from.TransferOut || !to.TransferIn)
                errors.Add(new("construct-resource-transfer-port-unavailable",
                    "transfer", NativeOriginIssueSeverity.Invalid,
                    "Both reservoirs must declare the appropriate native transfer ports."));

            // A mounting joint is an authored physical contact; a direct
            // ConnectedTo reference is also physical, even if written from
            // just one endpoint. Neither condition implies a power wire.
            var attached = from.Part.ParentInstanceId == toInstanceId ||
                to.Part.ParentInstanceId == fromInstanceId ||
                (from.Part.ConnectedTo?.Contains(toInstanceId, StringComparer.Ordinal)
                    ?? false) ||
                (to.Part.ConnectedTo?.Contains(fromInstanceId, StringComparer.Ordinal)
                    ?? false);
            if (!attached)
                errors.Add(new("construct-resource-transfer-disconnected",
                    "transfer", NativeOriginIssueSeverity.Incomplete,
                    "Internal reservoir transfers require a direct physical attachment."));

            if (amount > 0 && (from.Remaining < amount ||
                amount > (long)to.Capacity - to.Remaining))
                errors.Add(new("construct-resource-transfer-capacity-invalid",
                    "transfer.amount", NativeOriginIssueSeverity.Incomplete,
                    "Transfers cannot draw more than the source contains or exceed destination capacity."));
        }
        if (errors.Count > 0)
            return new([], null, errors.ToArray());

        var snapshots = sources.Select(x =>
        {
            var delta = x.Part.InstanceId == fromInstanceId ? -amount :
                x.Part.InstanceId == toInstanceId ? amount : 0;
            return new NativeConstructResourceTransferState(
                x.Part.InstanceId, x.Part.PowerSourceId!, x.Kind,
                x.Capacity, x.Remaining, delta, x.Remaining + delta);
        }).ToArray();
        var balances = snapshots.ToDictionary(x => x.InstanceId, x => x.After,
            StringComparer.Ordinal);
        // This is a separate preview: the transfer's validity is not
        // conditional on whether the new configuration can power all loads.
        var nextStep = PreviewResourceStep(balances, requestedEnabled, requestedModes);
        return new(snapshots, nextStep, []);
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
        var (sources, issues) = ResolveFiniteResources(remainingResources);
        if (issues.Length > 0)
            return new(null, [], [], issues);

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

/// <summary>
/// A proposed resource movement: positive Delta means receipt and negative
/// Delta means export. No units are created, destroyed, or converted.
/// </summary>
public sealed record NativeConstructResourceTransferState(
    string InstanceId, string PowerSourceId, string ResourceKind,
    int Capacity, int Before, int Delta, int After);

/// <summary>
/// Storage transfer and subsequent operation are separately evaluated.
/// CanTransfer never proves an action, a persisted balance, or that the
/// required physical transfer mechanism is available in gameplay.
/// </summary>
public sealed record NativeConstructResourceTransferPreview(
    IReadOnlyList<NativeConstructResourceTransferState> Sources,
    NativeConstructResourceStepPreview? NextStep,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool CanTransfer => Issues.Count == 0 && NextStep is not null;
}

/// <summary>
/// Environmental classification used only to determine solar collection.
/// The DM decides whether concentrated sunlight is available; the
/// gameplay host must authenticate that decision before committing time.
/// </summary>
public enum NativeConstructSolarExposure
{
    None, Indirect, Direct, Concentrated
}

/// <summary>
/// Per-source solar energy, in power-unit-minutes. Dividing by 60 yields
/// power-unit-hours. AvailableOutput is the maximum sustainable for the
/// requested interval; DeliveredPower is its actual allocated contribution.
/// </summary>
public sealed record NativeConstructSolarSourceState(
    string InstanceId, string PowerSourceId,
    long CapacityPowerUnitMinutes, long BeforePowerUnitMinutes,
    long AfterPowerUnitMinutes, int CollectionPower, int AvailableOutput,
    long DeliveredPower, bool OverdriveActive,
    long UnfundedPowerUnitMinutes = 0, int InsufficientEnergyMinutes = 0);

/// <summary>
/// A hypothetical, nonpersistent solar operating interval. A valid
/// evaluation alone is not authority to advance the clock or store energy.
/// </summary>
public sealed record NativeConstructSolarOperationPreview(
    NativeConstructOperatingStateResult? Evaluation,
    IReadOnlyList<NativeConstructSolarSourceState> Sources,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool CanAdvance => Issues.Count == 0 &&
        Evaluation?.IsOperatingConfigurationValid == true;
}
