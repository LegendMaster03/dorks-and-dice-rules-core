using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A live, nonpersistent operating view of one already-authorized Construct
/// build. Caller-controlled input may change only per-instance enabled flags;
/// installed parts, material assignments, links and Origin grants are captured
/// from the trusted construction result. This session is not a grant issuer.
/// </summary>
public sealed class NativeConstructOperatingSession
{
    private readonly NativeConstructAssemblyRequest _baseline;
    private readonly NativeConstructAssemblyEvaluator _assembly;
    private readonly string[] _originGrantIds;
    private readonly IReadOnlyDictionary<string, string[]> _operationalInputs;
    private readonly IReadOnlyDictionary<string, string[]> _synergyInputs;
    private readonly HashSet<string> _instanceIds;

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
                ConnectedTo = instance.ConnectedTo?.ToArray()
            }).ToArray(),
            approvedAssembly.InitialAllowance, InitialConstruction: true);

        _instanceIds = _baseline.Components.Select(x => x.InstanceId)
            .ToHashSet(StringComparer.Ordinal);
        var grants = approvedProfile.Current.InherentGrants.ToArray();
        _originGrantIds = grants.Select(g => g.Id)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();

        // Catalog-authored operational trait references identify *which*
        // component instances an effective grant requires at runtime.
        // The common Origin engine already verified the complete grant and
        // its source candidates. No new result is synthesized from this map.
        var operationalCandidates = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (var instance in _baseline.Components)
        {
            if (!catalog.TryGetById(instance.DefinitionId, out var definition) ||
                definition?.Category != "construct-component" ||
                !definition.Definition.TryGetProperty("operationalOriginTraitIds",
                    out var traits) || traits.ValueKind != JsonValueKind.Array)
                continue;

            var index = 0;
            foreach (var _ in traits.EnumerateArray())
            {
                operationalCandidates.Add(
                    "construct:" + instance.InstanceId + ":operational-trait:" +
                    index++, instance.InstanceId);
            }
        }

        var needs = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var grant in grants)
        {
            var inputInstances = grant.SourceCandidateIds
                .Where(operationalCandidates.ContainsKey)
                .Select(candidate => operationalCandidates[candidate])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (inputInstances.Length > 0)
                needs.Add(grant.Id, inputInstances);
        }
        _operationalInputs = needs;
        _synergyInputs = grants.Where(x => x.Synergy is not null)
            .ToDictionary(g => g.Id,
                g => g.Synergy!.Inputs.Select(x => x.GrantId)
                    .Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Apply a proposed power allocation without reconstructing the permanent
    /// Origin. Unknown IDs fail closed; an incomplete operating configuration
    /// suppresses all powered traits while preserving passive entitlements.
    /// </summary>
    public NativeConstructOperatingStateResult EvaluateAllocation(
        IReadOnlyDictionary<string, bool> requestedEnabled)
    {
        ArgumentNullException.ThrowIfNull(requestedEnabled);
        var unexpected = requestedEnabled.Keys
            .Where(id => string.IsNullOrWhiteSpace(id) ||
                !_instanceIds.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (unexpected.Length > 0)
        {
            var baseline = _assembly.Evaluate(_baseline);
            return new(baseline, [], _originGrantIds,
                [new("construct-operating-instance-unknown",
                    "operatingAllocation", NativeOriginIssueSeverity.Invalid,
                    "Power allocation may only refer to instances in the authorized assembly.")]);
        }

        var configured = _baseline with
        {
            Components = _baseline.Components.Select(instance =>
                requestedEnabled.TryGetValue(instance.InstanceId, out var enabled)
                    ? instance with { Enabled = enabled }
                    : instance).ToArray()
        };
        var state = _assembly.Evaluate(configured);
        var onlineIds = state.Components.Where(x => x.Operational)
            .Select(x => x.InstanceId).ToHashSet(StringComparer.Ordinal);
        var active = _originGrantIds.ToHashSet(StringComparer.Ordinal);

        // Invalid and incomplete allocations can not activate new effects.
        // Passive entitlements remain present; consumers must also check the
        // overall status before accepting the operating configuration.
        foreach (var (grantId, providers) in _operationalInputs)
            if (!state.IsAuthorized || providers.Any(id => !onlineIds.Contains(id)))
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
