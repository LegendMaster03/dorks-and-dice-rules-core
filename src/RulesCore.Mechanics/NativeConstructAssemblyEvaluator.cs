using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A construct-specific configuration evaluator. It does not issue Origin grants:
/// an assembled component must first have an authored mapping to a canonical
/// characteristic before the universal Origin engine can manifest it.
/// </summary>
public sealed class NativeConstructAssemblyEvaluator(NativeCatalogSnapshot catalog)
{
    public NativeConstructAssemblyResult Evaluate(NativeConstructAssemblyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<NativeOriginContractIssue>();
        var failures = new List<NativeConstructFailure>();
        void Issue(string code, string path, NativeOriginIssueSeverity severity, string message) =>
            issues.Add(new(code, path, severity, message));

        if (request.Components is null)
            return new(NativeOriginConstructionStatus.Invalid, 0, [], [],
                [new("construct-components-required", "components", NativeOriginIssueSeverity.Invalid,
                    "The assembly component collection must exist.")]);

        if (request.Components.Count > 256)
            Issue("construct-component-count-unsupported", "components",
                NativeOriginIssueSeverity.Invalid,
                "The current bounded evaluator supports at most 256 instances; this is a software limit, not an anatomical limit.");

        var parts = new Dictionary<string, Part>(StringComparer.Ordinal);
        foreach (var instance in request.Components)
        {
            if (instance is null || string.IsNullOrWhiteSpace(instance.InstanceId) ||
                string.IsNullOrWhiteSpace(instance.DefinitionId))
            {
                Issue("construct-instance-invalid", "components", NativeOriginIssueSeverity.Invalid,
                    "Component instances need stable identities and native definitions.");
                continue;
            }
            var path = "components." + instance.InstanceId;
            if (parts.ContainsKey(instance.InstanceId))
            {
                Issue("construct-instance-duplicate", path, NativeOriginIssueSeverity.Invalid,
                    "Multiple installed components must have distinct instance IDs.");
                continue;
            }
            if (!catalog.TryGetById(instance.DefinitionId, out var definition) ||
                definition?.Category != "construct-component")
            {
                Issue("construct-definition-unknown", path, NativeOriginIssueSeverity.Invalid,
                    "Only native construct-component definitions are accepted.");
                continue;
            }
            var raw = definition.Definition;
            var cost = Number(raw, "constructionCost", path, Issue);
            var demand = Number(raw, "powerDemand", path, Issue);
            var output = Number(raw, "powerOutput", path, Issue) ?? 0;
            var provides = Texts(raw, "provides", path, Issue);
            var needs = Requirements(raw, path, Issue);
            var slots = Texts(raw, "materialSlots", path, Issue);
            foreach (var slot in slots)
            {
                if (instance.Materials?.TryGetValue(slot, out var materialId) != true)
                {
                    Issue("construct-material-missing", path + ".materials." + slot,
                        NativeOriginIssueSeverity.Incomplete, "A required material assignment is missing.");
                    continue;
                }
                if (!catalog.TryGetById(materialId, out var material) ||
                    material?.Category != "construct-material")
                    Issue("construct-material-unknown", path + ".materials." + slot,
                        NativeOriginIssueSeverity.Invalid,
                        "Material assignments must reference native construct materials.");
            }
            foreach (var extra in instance.Materials?.Keys ?? [])
                if (!slots.Contains(extra, StringComparer.Ordinal))
                    Issue("construct-material-slot-unknown", path + ".materials." + extra,
                        NativeOriginIssueSeverity.Invalid, "The component does not define this material slot.");

            parts.Add(instance.InstanceId, new(instance, cost, demand, output,
                provides, needs, Text(raw, "shutdownConsequence")));
        }

        var links = parts.Keys.ToDictionary(x => x,
            _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var part in parts.Values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in part.Instance.ConnectedTo ?? [])
            {
                if (target is null || target == part.Instance.InstanceId ||
                    !parts.ContainsKey(target) || !seen.Add(target))
                {
                    Issue("construct-link-invalid", "components." + part.Instance.InstanceId,
                        NativeOriginIssueSeverity.Invalid,
                        "Connections must identify distinct installed instances without duplicates.");
                    continue;
                }
                links[part.Instance.InstanceId].Add(target);
                links[target].Add(part.Instance.InstanceId);
            }
        }

        // An unconnected component is permitted. Connectivity is an authored
        // functional requirement, not a universal humanoid-body topology.
        var networks = new List<HashSet<string>>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in parts.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!visited.Add(root)) continue;
            var members = new HashSet<string>(StringComparer.Ordinal) { root };
            var queue = new Queue<string>();
            queue.Enqueue(root);
            while (queue.Count > 0)
                foreach (var neighbor in links[queue.Dequeue()])
                    if (visited.Add(neighbor))
                    {
                        members.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
            networks.Add(members);
        }

        var online = new HashSet<string>(StringComparer.Ordinal);
        // Fixed-point activation permits provider chains but cannot bootstrap
        // mutually dependent components without an independent provider.
        for (var pass = 0; pass < parts.Count; pass++)
        {
            var changed = false;
            foreach (var part in parts.Values.OrderBy(x => x.Instance.InstanceId,
                StringComparer.Ordinal))
            {
                var id = part.Instance.InstanceId;
                if (online.Contains(id) || !part.Instance.Enabled ||
                    part.PowerDemand is null) continue;
                var network = networks.First(x => x.Contains(id));
                if (part.Requirements.All(group => group.Any(capability =>
                    online.Any(providerId => providerId != id &&
                        network.Contains(providerId) &&
                        parts[providerId].Provides.Contains(capability, StringComparer.Ordinal)))))
                {
                    online.Add(id);
                    changed = true;
                }
            }
            if (!changed) break;
        }

        foreach (var part in parts.Values)
            if (part.Instance.Enabled && !online.Contains(part.Instance.InstanceId) &&
                part.PowerDemand is not null)
                Issue("construct-dependency-unavailable",
                    "components." + part.Instance.InstanceId,
                    NativeOriginIssueSeverity.Incomplete,
                    "The requested operating configuration lacks a connected, operational provider.");

        long totalDemand = 0, totalOutput = 0;
        var overloaded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var network in networks)
        {
            var demand = network.Where(online.Contains)
                .Sum(id => (long)(parts[id].PowerDemand ?? 0));
            var output = network.Where(online.Contains)
                .Sum(id => (long)parts[id].PowerOutput);
            totalDemand += demand;
            totalOutput += output;
            if (demand <= output) continue;
            foreach (var id in network) overloaded.Add(id);
            Issue("construct-power-overallocated",
                "components." + network.OrderBy(x => x, StringComparer.Ordinal).First(),
                NativeOriginIssueSeverity.Incomplete,
                "Requested simultaneous demand exceeds output in this connected power network.");
        }

        long knownCost = 0;
        foreach (var part in parts.Values)
        {
            if (part.ConstructionCost is int cost) knownCost += cost;
            else if (request.InitialConstruction)
                Issue("construct-cost-unresolved",
                    "components." + part.Instance.InstanceId,
                    NativeOriginIssueSeverity.Unresolved,
                    "An initial construction cost has not been authored for this component.");
            if (part.PowerDemand is null)
                Issue("construct-power-demand-unresolved",
                    "components." + part.Instance.InstanceId,
                    NativeOriginIssueSeverity.Unresolved,
                    "The component's power demand has not been authored.");
        }
        if (request.InitialConstruction)
        {
            if (request.InitialAllowance is null || request.InitialAllowance < 0)
                Issue("construct-allowance-required", "initialAllowance",
                    NativeOriginIssueSeverity.Incomplete,
                    "Initial construction requires an applicable nonnegative allowance.");
            else if (knownCost > request.InitialAllowance.Value)
                Issue("construct-allowance-exceeded", "initialAllowance",
                    NativeOriginIssueSeverity.Incomplete,
                    "The configured component costs exceed the starting allowance.");
        }

        var states = parts.Values.OrderBy(x => x.Instance.InstanceId, StringComparer.Ordinal)
            .Select(part =>
            {
                var id = part.Instance.InstanceId;
                var active = online.Contains(id) && !overloaded.Contains(id);
                if (!active && part.ShutdownConsequence is string consequence)
                    failures.Add(new(id, consequence));
                return new NativeConstructComponentState(id, part.Instance.DefinitionId,
                    true, active, active ? part.Provides : [], part.Instance.Enabled);
            }).ToArray();

        var status = issues.Any(x => x.Severity == NativeOriginIssueSeverity.Invalid)
            ? NativeOriginConstructionStatus.Invalid
            : issues.Any(x => x.Severity == NativeOriginIssueSeverity.Conflict)
                ? NativeOriginConstructionStatus.Conflicting
                : issues.Any(x => x.Severity == NativeOriginIssueSeverity.Unresolved)
                    ? NativeOriginConstructionStatus.Unresolved
                    : issues.Any(x => x.Severity == NativeOriginIssueSeverity.Incomplete)
                        ? NativeOriginConstructionStatus.Incomplete
                        : NativeOriginConstructionStatus.Authorized;
        return new(status, knownCost, states, failures, issues);
    }

    private sealed record Part(NativeConstructComponentInstance Instance, int? ConstructionCost,
        int? PowerDemand, int PowerOutput, string[] Provides, string[][] Requirements,
        string? ShutdownConsequence);

    private static string? Text(JsonElement definition, string key) =>
        definition.TryGetProperty(key, out var field) &&
        field.ValueKind == JsonValueKind.String ? field.GetString() : null;

    private static int? Number(JsonElement definition, string key, string path,
        Action<string, string, NativeOriginIssueSeverity, string> issue)
    {
        if (!definition.TryGetProperty(key, out var field) ||
            field.ValueKind == JsonValueKind.Null) return null;
        if (field.ValueKind == JsonValueKind.Number &&
            field.TryGetInt32(out var value) && value >= 0) return value;
        issue("construct-number-invalid", path + "." + key,
            NativeOriginIssueSeverity.Invalid, "Authored numerical values must be nonnegative integers.");
        return null;
    }

    private static string[] Texts(JsonElement definition, string key, string path,
        Action<string, string, NativeOriginIssueSeverity, string> issue)
    {
        if (!definition.TryGetProperty(key, out var field)) return [];
        if (field.ValueKind == JsonValueKind.Array &&
            field.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(x.GetString())))
            return field.EnumerateArray().Select(x => x.GetString()!).ToArray();
        issue("construct-list-invalid", path + "." + key,
            NativeOriginIssueSeverity.Invalid, "Expected a list of nonempty identifiers.");
        return [];
    }

    private static string[][] Requirements(JsonElement definition, string path,
        Action<string, string, NativeOriginIssueSeverity, string> issue)
    {
        if (!definition.TryGetProperty("requiresAnyOf", out var field)) return [];
        if (field.ValueKind != JsonValueKind.Array)
        {
            issue("construct-requirements-invalid", path, NativeOriginIssueSeverity.Invalid,
                "Requirements must be a list of alternative-capability groups.");
            return [];
        }
        var groups = new List<string[]>();
        foreach (var item in field.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Array ||
                !item.EnumerateArray().Any() ||
                item.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(x.GetString())))
            {
                issue("construct-requirements-invalid", path,
                    NativeOriginIssueSeverity.Invalid, "Every requirement needs nonempty alternatives.");
                continue;
            }
            groups.Add(item.EnumerateArray().Select(x => x.GetString()!).ToArray());
        }
        return groups.ToArray();
    }
}

public sealed record NativeConstructComponentInstance(
    string InstanceId, string DefinitionId,
    IReadOnlyDictionary<string, string>? Materials = null,
    IReadOnlyList<string>? ConnectedTo = null,
    bool Enabled = true);

public sealed record NativeConstructAssemblyRequest(
    IReadOnlyList<NativeConstructComponentInstance> Components,
    int? InitialAllowance = null, bool InitialConstruction = true);

public sealed record NativeConstructComponentState(
    string InstanceId, string DefinitionId, bool Installed, bool Operational,
    IReadOnlyList<string> ActiveCapabilities, bool RequestedEnabled);

public sealed record NativeConstructFailure(string InstanceId, string ConsequenceId);

public sealed record NativeConstructAssemblyResult(
    NativeOriginConstructionStatus Status, long KnownConstructionCost,
    IReadOnlyList<NativeConstructComponentState> Components,
    IReadOnlyList<NativeConstructFailure> ShutdownConsequences,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool IsAuthorized => Status == NativeOriginConstructionStatus.Authorized;
}
