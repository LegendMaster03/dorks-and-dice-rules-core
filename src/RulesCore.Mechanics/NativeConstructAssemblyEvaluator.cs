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
        long knownMaterialCost = 0;
        long knownModificationCost = 0;
        foreach (var instance in request.Components)
        {
            if (instance is null || string.IsNullOrWhiteSpace(instance.InstanceId) ||
                string.IsNullOrWhiteSpace(instance.DefinitionId) ||
                instance.InstanceId.Length > 96 ||
                instance.InstanceId.Any(c => !(char.IsAsciiLetterOrDigit(c) ||
                    c is '-' or '_' or '.')))
            {
                Issue("construct-instance-invalid", "components", NativeOriginIssueSeverity.Invalid,
                    "Component instances need path-safe stable identities and native definitions.");
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
            var baseDemand = Number(raw, "powerDemand", path, Issue);
            var baseOutput = Number(raw, "powerOutput", path, Issue) ?? 0;
            var baseProvides = Texts(raw, "provides", path, Issue);
            var baseNeeds = Requirements(raw, path, Issue);
            var allowedTraits = Texts(raw, "operationalOriginTraitIds", path, Issue);
            var selectedMode = SelectOperatingMode(raw, instance.ModeId,
                allowedTraits, path, Issue);
            var demand = selectedMode?.PowerDemand ?? baseDemand;
            var output = selectedMode?.PowerOutput ?? baseOutput;
            var provides = selectedMode?.Provides ?? baseProvides;
            var needs = selectedMode?.Requirements ?? baseNeeds;
            var modeTraits = selectedMode?.OperationalOriginTraitIds ?? allowedTraits;
            var slots = Texts(raw, "materialSlots", path, Issue);

            // A design and its optional enhancements are catalog definitions,
            // not duplicated material-specific components or client-supplied effects.
            // They contribute to the installed cost even when switched off.
            var addedProvides = new List<string>();
            var addedNeeds = new List<string[]>();
            long extraDemand = 0;

            void ApplyOption(string? selectedId, string category, string field)
            {
                if (string.IsNullOrWhiteSpace(selectedId) ||
                    !catalog.TryGetById(selectedId, out var option) ||
                    option?.Category != category)
                {
                    Issue("construct-option-reference-invalid", path + "." + field,
                        NativeOriginIssueSeverity.Invalid,
                        "A selected option must resolve to the appropriate native Construct catalog.");
                    return;
                }
                var optionPath = path + "." + field;
                var compatible = Texts(option.Definition,
                    "compatibleComponentCapabilities", optionPath, Issue);
                if (compatible.Length == 0 ||
                    !compatible.Any(cap => baseProvides.Contains(cap, StringComparer.Ordinal)))
                {
                    Issue("construct-option-incompatible", optionPath,
                        NativeOriginIssueSeverity.Invalid,
                        "The authored option does not support this component's base capabilities.");
                    return;
                }
                var optionCost = Number(option.Definition, "constructionCost",
                    optionPath, Issue);
                if (optionCost is int priced) knownModificationCost += priced;
                else if (request.InitialConstruction)
                    Issue("construct-option-cost-unresolved", optionPath,
                        NativeOriginIssueSeverity.Unresolved,
                        "The option has no authored construction cost.");
                // Option powerDemand is incremental, unlike a selected operating
                // mode which replaces the component's general demand.
                extraDemand += Number(option.Definition, "powerDemand",
                    optionPath, Issue) ?? 0;
                addedProvides.AddRange(Texts(option.Definition, "provides",
                    optionPath, Issue));
                addedNeeds.AddRange(Requirements(option.Definition, optionPath, Issue));
            }

            if (instance.DesignId is string design)
                ApplyOption(design, "construct-design", "designId");
            if ((instance.EnhancementIds?.Count ?? 0) > 32)
                Issue("construct-enhancement-count-invalid", path + ".enhancementIds",
                    NativeOriginIssueSeverity.Invalid,
                    "At most 32 enhancement selections can be evaluated per component.");
            var seenEnhancements = new HashSet<string>(StringComparer.Ordinal);
            foreach (var enhancementId in instance.EnhancementIds ?? [])
            {
                if (string.IsNullOrWhiteSpace(enhancementId) ||
                    !seenEnhancements.Add(enhancementId))
                {
                    Issue("construct-enhancement-selection-invalid",
                        path + ".enhancementIds", NativeOriginIssueSeverity.Invalid,
                        "Enhancements must have distinct nonempty catalog identities.");
                    continue;
                }
                ApplyOption(enhancementId, "construct-enhancement", "enhancementIds");
            }

            // The power source is independent of its containing hardware.
            // Power output is supplied by a selected native source, never by
            // a client-provided numerical claim. Storage/endurance is distinct.
            var hasSourceSlot = false;
            if (raw.TryGetProperty("powerSourceSlot", out var sourceSlot))
            {
                if (sourceSlot.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    Issue("construct-power-source-slot-invalid", path + ".powerSourceSlot",
                        NativeOriginIssueSeverity.Invalid,
                        "The authored power-source slot must be a boolean.");
                hasSourceSlot = sourceSlot.ValueKind == JsonValueKind.True;
            }
            if (!hasSourceSlot && instance.PowerSourceId is not null)
                Issue("construct-power-source-unexpected", path + ".powerSourceId",
                    NativeOriginIssueSeverity.Invalid,
                    "This component has no authored power-source slot.");
            if (hasSourceSlot && string.IsNullOrWhiteSpace(instance.PowerSourceId))
                Issue("construct-power-source-required", path + ".powerSourceId",
                    NativeOriginIssueSeverity.Incomplete,
                    "An installed power-source housing needs a selected mechanism.");
            if (hasSourceSlot && !string.IsNullOrWhiteSpace(instance.PowerSourceId))
            {
                if (!catalog.TryGetById(instance.PowerSourceId, out var source) ||
                    source?.Category != "construct-power-source")
                {
                    Issue("construct-power-source-unknown", path + ".powerSourceId",
                        NativeOriginIssueSeverity.Invalid,
                        "The selected source must be a native Construct power source.");
                }
                else
                {
                    var sourcePath = path + ".powerSourceId";
                    var compatible = Texts(source.Definition,
                        "compatibleComponentCapabilities", sourcePath, Issue);
                    if (compatible.Length == 0 ||
                        !compatible.Any(cap => baseProvides.Contains(cap, StringComparer.Ordinal)))
                        Issue("construct-power-source-incompatible", sourcePath,
                            NativeOriginIssueSeverity.Invalid,
                            "The selected source is not compatible with this housing.");
                    var sourceCost = Number(source.Definition,
                        "constructionCost", sourcePath, Issue);
                    if (sourceCost is int priced) knownModificationCost += priced;
                    else if (request.InitialConstruction)
                        Issue("construct-power-source-cost-unresolved", sourcePath,
                            NativeOriginIssueSeverity.Unresolved,
                            "The selected source's construction cost has not been authored.");
                    var sourceOutput = Number(source.Definition,
                        "powerOutput", sourcePath, Issue);
                    if (sourceOutput is int authoredOutput)
                        baseOutput = authoredOutput;
                    else
                        Issue("construct-power-source-output-unresolved", sourcePath,
                            NativeOriginIssueSeverity.Unresolved,
                            "This power source has no authored simultaneous output.");
                    extraDemand += Number(source.Definition,
                        "powerDemand", sourcePath, Issue) ?? 0;
                    addedProvides.AddRange(Texts(source.Definition,
                        "provides", sourcePath, Issue));
                    addedNeeds.AddRange(Requirements(source.Definition, sourcePath, Issue));
                }
            }
            // Add-ons supply additional capabilities and requirements; mode
            // overrides still apply only to the base component properties.
            provides = provides.Concat(addedProvides).Distinct(StringComparer.Ordinal).ToArray();
            needs = needs.Concat(addedNeeds).ToArray();
            if (demand is int authoredDemand)
                demand = extraDemand > int.MaxValue - authoredDemand
                    ? null : (int)(authoredDemand + extraDemand);
            if (extraDemand > int.MaxValue || demand is null && extraDemand > 0)
                Issue("construct-power-demand-overflow", path + ".powerDemand",
                    NativeOriginIssueSeverity.Invalid,
                    "Combined power demand exceeds the supported numerical limit.");

            foreach (var slot in slots)
            {
                if (instance.Materials is null || !instance.Materials.TryGetValue(slot, out var materialId))
                {
                    Issue("construct-material-missing", path + ".materials." + slot,
                        NativeOriginIssueSeverity.Incomplete, "A required material assignment is missing.");
                    continue;
                }
                if (!catalog.TryGetById(materialId, out var material) ||
                    material?.Category != "construct-material")
                {
                    Issue("construct-material-unknown", path + ".materials." + slot,
                        NativeOriginIssueSeverity.Invalid,
                        "Material assignments must reference native construct materials.");
                    continue;
                }
                var materialCost = Number(material.Definition, "constructionCost",
                    path + ".materials." + slot, Issue);
                if (materialCost is int authoredCost)
                    knownMaterialCost += authoredCost;
                else if (request.InitialConstruction)
                    Issue("construct-material-cost-unresolved",
                        path + ".materials." + slot, NativeOriginIssueSeverity.Unresolved,
                        "The selected material has no authorized construction cost.");
            }
            foreach (var extra in instance.Materials?.Keys ?? [])
                if (!slots.Contains(extra, StringComparer.Ordinal))
                    Issue("construct-material-slot-unknown", path + ".materials." + extra,
                        NativeOriginIssueSeverity.Invalid, "The component does not define this material slot.");

            parts.Add(instance.InstanceId, new(instance, cost, demand, output,
                provides, needs, Text(raw, "shutdownConsequence"), modeTraits));
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


        var overloaded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var network in networks)
        {
            var demand = network.Where(online.Contains)
                .Sum(id => (long)(parts[id].PowerDemand ?? 0));
            var output = network.Where(online.Contains)
                .Sum(id => (long)parts[id].PowerOutput);
            if (demand <= output) continue;
            foreach (var id in network) overloaded.Add(id);
            Issue("construct-power-overallocated",
                "components." + network.OrderBy(x => x, StringComparer.Ordinal).First(),
                NativeOriginIssueSeverity.Incomplete,
                "Requested simultaneous demand exceeds output in this connected power network.");
        }

        long knownCost = knownMaterialCost + knownModificationCost;
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
                var active = online.Contains(id) &&
                    (!overloaded.Contains(id) || part.PowerDemand == 0 &&
                        part.Requirements.Length == 0);
                if (!active && part.ShutdownConsequence is string consequence)
                    failures.Add(new(id, consequence));
                return new NativeConstructComponentState(id, part.Instance.DefinitionId,
                    true, active, active ? part.Provides : [], part.Instance.Enabled,
                    part.Instance.ModeId,
                    active ? part.OperationalOriginTraitIds : []);
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
        string? ShutdownConsequence, string[] OperationalOriginTraitIds);

    private sealed record OperatingMode(string Id, int? PowerDemand, int? PowerOutput,
        string[]? Provides, string[][]? Requirements, string[] OperationalOriginTraitIds);

    /// <summary>
    /// One selected mode replaces the authored operating parameters for this
    /// instance. It does not change installation cost or grant new Origin traits.
    /// Every mode's trait IDs must be a subset of the component's installed,
    /// catalog-authored operational entitlements.
    /// </summary>
    private static OperatingMode? SelectOperatingMode(JsonElement component,
        string? selectedModeId, IReadOnlyList<string> allowedTraits, string path,
        Action<string, string, NativeOriginIssueSeverity, string> issue)
    {
        if (!component.TryGetProperty("operatingModes", out var modes))
        {
            if (selectedModeId is not null)
                issue("construct-mode-unexpected", path + ".modeId",
                    NativeOriginIssueSeverity.Invalid,
                    "The component has no authored operating modes.");
            return null;
        }
        if (modes.ValueKind != JsonValueKind.Array ||
            modes.GetArrayLength() is < 1 or > 32)
        {
            issue("construct-modes-invalid", path + ".operatingModes",
                NativeOriginIssueSeverity.Invalid,
                "Operating modes must contain between one and 32 authored states.");
            return null;
        }

        var definitions = new Dictionary<string, OperatingMode>(StringComparer.Ordinal);
        var index = 0;
        foreach (var mode in modes.EnumerateArray())
        {
            var modePath = path + ".operatingModes." + index++;
            var id = mode.ValueKind == JsonValueKind.Object ? Text(mode, "id") : null;
            if (string.IsNullOrWhiteSpace(id) || id.Length > 96 ||
                id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) ||
                definitions.ContainsKey(id))
            {
                issue("construct-mode-definition-invalid", modePath,
                    NativeOriginIssueSeverity.Invalid,
                    "Operating mode IDs must be unique and path-safe.");
                continue;
            }
            var modeDemand = Number(mode, "powerDemand", modePath, issue);
            var modeOutput = Number(mode, "powerOutput", modePath, issue);
            var provides = mode.TryGetProperty("provides", out _)
                ? Texts(mode, "provides", modePath, issue) : null;
            var needs = mode.TryGetProperty("requiresAnyOf", out _)
                ? Requirements(mode, modePath, issue) : null;
            var activeTraits = Texts(mode, "operationalOriginTraitIds", modePath, issue);
            if (activeTraits.Distinct(StringComparer.Ordinal).Count() != activeTraits.Length ||
                activeTraits.Any(x => !allowedTraits.Contains(x, StringComparer.Ordinal)))
                issue("construct-mode-traits-invalid", modePath + ".operationalOriginTraitIds",
                    NativeOriginIssueSeverity.Invalid,
                    "Mode capabilities may activate only distinct operational traits installed by the component.");
            definitions.Add(id, new(id, modeDemand, modeOutput, provides, needs,
                activeTraits));
        }

        if (selectedModeId is null)
        {
            issue("construct-mode-selection-required", path + ".modeId",
                NativeOriginIssueSeverity.Incomplete,
                "A component with authored operating modes needs a selected mode.");
            return null;
        }
        if (!definitions.TryGetValue(selectedModeId, out var selected))
        {
            issue("construct-mode-unknown", path + ".modeId",
                NativeOriginIssueSeverity.Invalid,
                "The requested operating mode is not authored for this component.");
            return null;
        }
        return selected;
    }

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
    bool Enabled = true, string? ModeId = null,
    string? DesignId = null, IReadOnlyList<string>? EnhancementIds = null,
    string? PowerSourceId = null);

public sealed record NativeConstructAssemblyRequest(
    IReadOnlyList<NativeConstructComponentInstance> Components,
    int? InitialAllowance = null, bool InitialConstruction = true);

public sealed record NativeConstructComponentState(
    string InstanceId, string DefinitionId, bool Installed, bool Operational,
    IReadOnlyList<string> ActiveCapabilities, bool RequestedEnabled,
    string? ModeId = null, IReadOnlyList<string>? ActiveOperationalOriginTraitIds = null);

public sealed record NativeConstructFailure(string InstanceId, string ConsequenceId);

public sealed record NativeConstructAssemblyResult(
    NativeOriginConstructionStatus Status, long KnownConstructionCost,
    IReadOnlyList<NativeConstructComponentState> Components,
    IReadOnlyList<NativeConstructFailure> ShutdownConsequences,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool IsAuthorized => Status == NativeOriginConstructionStatus.Authorized;
}
