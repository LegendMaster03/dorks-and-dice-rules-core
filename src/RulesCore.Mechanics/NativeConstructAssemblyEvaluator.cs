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
        var unavailableSources = (request.UnavailablePowerSourceInstanceIds ?? [])
            .ToHashSet(StringComparer.Ordinal);

        // Native opt-in: reuse crafting recipes and resource-unit prices instead
        // of charging arbitrary finished-part prices. Older fixture content
        // retains the original direct-cost contract unchanged.
        JsonElement? recipeCostMethod = null;
        if (catalog.TryGetById("rule.creature-origination", out var originationRule) &&
            originationRule?.Definition.TryGetProperty("methodDefinitions",
                out var methods) == true && methods.ValueKind == JsonValueKind.Array)
        {
            foreach (var method in methods.EnumerateArray())
            {
                if (method.ValueKind != JsonValueKind.Object ||
                    !method.TryGetProperty("id", out var methodId) ||
                    methodId.ValueKind != JsonValueKind.String ||
                    methodId.GetString() != "construct-assembly" ||
                    !method.TryGetProperty("initialConstructionCostModel", out var mode))
                    continue;
                if (mode.ValueKind == JsonValueKind.String &&
                    mode.GetString() == "recipe-inputs")
                    recipeCostMethod = method;
                else if (mode.ValueKind != JsonValueKind.String ||
                    mode.GetString() != "direct")
                    Issue("construct-cost-model-invalid", "method.initialConstructionCostModel",
                        NativeOriginIssueSeverity.Invalid,
                        "Unsupported native starting-construction cost model.");
                break;
            }
        }
        var recipeSelections = new List<NativeCraftingQuotedSelection>();
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
            if (instance.ConstructionVariantId is not null && !recipeCostMethod.HasValue)
                Issue("construct-variant-needs-recipe-cost-model", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "A manufacturing variant needs an authored recipe-input construction method.");
            if (recipeCostMethod.HasValue)
                recipeSelections.Add(new(definition.Id, instance.Materials,
                    instance.ConstructionVariantId));
            var cost = recipeCostMethod.HasValue ? 0 :
                Number(raw, "constructionCost", path, Issue);
            var baseDemand = Number(raw, "powerDemand", path, Issue);
            var baseOutput = Number(raw, "powerOutput", path, Issue) ?? 0;
            var outputKnown = true;
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
            // Continuity is native-authored, not a caller-controlled priority.
            var continuous = false;
            if (raw.TryGetProperty("requiresContinuousPower", out var continuity))
            {
                if (continuity.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    Issue("construct-continuity-invalid", path + ".requiresContinuousPower",
                        NativeOriginIssueSeverity.Invalid,
                        "The authored continuous-power flag must be boolean.");
                else
                    continuous = continuity.ValueKind == JsonValueKind.True;
            }
            // Structural interfaces are installation properties, independent of
            // powered capability tags and per-instance operating modes.
            var mountInterfaces = Texts(raw, "mountInterfaces", path, Issue);
            var acceptsMountInterfaces = Texts(raw, "acceptsMountInterfaces", path, Issue);

            // A design and its optional enhancements are catalog definitions,
            // not duplicated material-specific components or client-supplied effects.
            // They contribute to the installed cost even when switched off.
            var addedProvides = new List<string>();
            var addedOperationalTraits = new List<string>();
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
                if (recipeCostMethod.HasValue)
                    recipeSelections.Add(new(option.Id, instance.Materials));
                else
                {
                    var optionCost = Number(option.Definition, "constructionCost",
                        optionPath, Issue);
                    if (optionCost is int priced) knownModificationCost += priced;
                    else if (request.InitialConstruction)
                        Issue("construct-option-cost-unresolved", optionPath,
                            NativeOriginIssueSeverity.Unresolved,
                            "The option has no authored construction cost.");
                }
                // Option powerDemand is incremental, unlike a selected operating
                // mode which replaces the component's general demand.
                extraDemand += Number(option.Definition, "powerDemand",
                    optionPath, Issue) ?? 0;
                addedProvides.AddRange(Texts(option.Definition, "provides",
                    optionPath, Issue));
                addedOperationalTraits.AddRange(Texts(option.Definition,
                    "operationalOriginTraitIds", optionPath, Issue));
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
            var sourceAvailable = !unavailableSources.Contains(instance.InstanceId);
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
                    if (recipeCostMethod.HasValue)
                        recipeSelections.Add(new(source.Id, instance.Materials));
                    else
                    {
                        var sourceCost = Number(source.Definition,
                            "constructionCost", sourcePath, Issue);
                        if (sourceCost is int priced) knownModificationCost += priced;
                        else if (request.InitialConstruction)
                            Issue("construct-power-source-cost-unresolved", sourcePath,
                                NativeOriginIssueSeverity.Unresolved,
                                "The selected source's construction cost has not been authored.");
                    }
                    var sourceOutput = Number(source.Definition,
                        "powerOutput", sourcePath, Issue);
                    if (sourceOutput is int authoredOutput)
                        baseOutput = authoredOutput;
                    else
                    {
                        outputKnown = false;
                        Issue("construct-power-source-output-unresolved", sourcePath,
                            NativeOriginIssueSeverity.Unresolved,
                            "This power source has no authored simultaneous output.");
                    }
                    var sourceDemand = Number(source.Definition,
                        "powerDemand", sourcePath, Issue) ?? 0;
                    var sourceProvides = Texts(source.Definition,
                        "provides", sourcePath, Issue);
                    var sourceNeeds = Requirements(source.Definition, sourcePath, Issue);
                    if (sourceAvailable)
                    {
                        extraDemand += sourceDemand;
                        addedProvides.AddRange(sourceProvides);
                        addedNeeds.AddRange(sourceNeeds);
                        addedOperationalTraits.AddRange(Texts(source.Definition,
                            "operationalOriginTraitIds", sourcePath, Issue));
                    }
                }
            }
            // A source sets the housing's baseline output. An explicitly
            // authored operating mode may override that output.
            output = selectedMode?.PowerOutput ?? baseOutput;
            // An exhausted mechanism cannot create output through a mode.
            // The housing remains physically present and separately costed.
            if (instance.PowerSourceId is not null && !sourceAvailable)
                output = 0;
            // Add-ons supply additional capabilities and requirements; mode
            // overrides still apply only to the base component properties.
            provides = provides.Concat(addedProvides).Distinct(StringComparer.Ordinal).ToArray();
            needs = needs.Concat(addedNeeds).ToArray();
            if (demand is int authoredDemand)
            {
                if (extraDemand > int.MaxValue - authoredDemand)
                {
                    Issue("construct-power-demand-overflow", path + ".powerDemand",
                        NativeOriginIssueSeverity.Invalid,
                        "Combined power demand exceeds the supported numerical limit.");
                    demand = null;
                }
                else
                    demand = (int)(authoredDemand + extraDemand);
            }
            // If the component's demand remains unauthored, additional demand
            // must not turn it into a zero-cost operating component. The existing
            // unresolved-demand validation below handles that case.

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
                addedOperationalTraits.AddRange(Texts(material.Definition,
                    "operationalOriginTraitIds", path + ".materials." + slot, Issue));
                // Recipe material slots bind an ingredient identity and amount.
                // Charging the material's old per-slot price here would double-count it.
                if (!recipeCostMethod.HasValue)
                {
                    var materialCost = Number(material.Definition, "constructionCost",
                        path + ".materials." + slot, Issue);
                    if (materialCost is int authoredCost)
                        knownMaterialCost += authoredCost;
                    else if (request.InitialConstruction)
                        Issue("construct-material-cost-unresolved",
                            path + ".materials." + slot, NativeOriginIssueSeverity.Unresolved,
                            "The selected material has no authorized construction cost.");
                }
            }
            foreach (var extra in instance.Materials?.Keys ?? [])
                if (!slots.Contains(extra, StringComparer.Ordinal))
                    Issue("construct-material-slot-unknown", path + ".materials." + extra,
                        NativeOriginIssueSeverity.Invalid, "The component does not define this material slot.");

            // Installed add-on entitlements may be power-gated by the same
            // operational component, but are never created by a mode change.
            modeTraits = modeTraits.Concat(addedOperationalTraits)
                .Distinct(StringComparer.Ordinal).ToArray();
            parts.Add(instance.InstanceId, new(instance, cost, demand, output,
                provides, needs, Text(raw, "shutdownConsequence"), modeTraits,
                mountInterfaces, acceptsMountInterfaces, outputKnown, continuous,
                instance.PowerSourceId is not null && sourceAvailable));
        }

        // Fail closed on nonexistent, duplicated, or non-source outages.
        if ((request.UnavailablePowerSourceInstanceIds?.Count ?? 0) > 256)
            Issue("construct-unavailable-source-count-invalid",
                "unavailablePowerSourceInstanceIds", NativeOriginIssueSeverity.Invalid,
                "At most 256 installed sources can be marked unavailable.");
        var outageSeen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in (request.UnavailablePowerSourceInstanceIds ?? []).Take(257))
        {
            if (string.IsNullOrWhiteSpace(id) || !outageSeen.Add(id) ||
                !parts.TryGetValue(id, out var sourcePart) ||
                string.IsNullOrWhiteSpace(sourcePart.Instance.PowerSourceId))
                Issue("construct-unavailable-source-invalid",
                    "unavailablePowerSourceInstanceIds", NativeOriginIssueSeverity.Invalid,
                    "Outages must reference distinct installed power-source housings.");
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

        // Optional mounting assembles independently configurable native parts
        // into composite components. An attachment creates physical adjacency,
        // not a mandatory root, powered capability, or power source. An interface
        // must be explicitly authored on both parent and child definitions.
        foreach (var part in parts.Values.OrderBy(x => x.Instance.InstanceId,
            StringComparer.Ordinal))
        {
            var child = part.Instance;
            var path = "components." + child.InstanceId;
            if (child.ParentInstanceId is null)
            {
                if (child.MountInterfaceId is not null)
                    Issue("construct-mount-parent-required", path + ".mountInterfaceId",
                        NativeOriginIssueSeverity.Invalid,
                        "A mounting interface selection requires a parent component.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(child.ParentInstanceId) ||
                child.ParentInstanceId == child.InstanceId ||
                !parts.TryGetValue(child.ParentInstanceId, out var parent))
            {
                Issue("construct-mount-parent-invalid", path + ".parentInstanceId",
                    NativeOriginIssueSeverity.Invalid,
                    "A mounted subcomponent needs another installed parent instance.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(child.MountInterfaceId))
            {
                Issue("construct-mount-interface-required", path + ".mountInterfaceId",
                    NativeOriginIssueSeverity.Incomplete,
                    "A mounted component needs a selected, authored mounting interface.");
                continue;
            }
            if (!part.MountInterfaces.Contains(child.MountInterfaceId, StringComparer.Ordinal) ||
                !parent.AcceptsMountInterfaces.Contains(child.MountInterfaceId,
                    StringComparer.Ordinal))
            {
                Issue("construct-mount-interface-incompatible", path + ".mountInterfaceId",
                    NativeOriginIssueSeverity.Invalid,
                    "The chosen mounting interface must be supported by both component definitions.");
                continue;
            }

            // Following parent references also detects cycles longer than two.
            // No component is required to have a parent; any number of
            // independent or composite top-level assemblies are permitted.
            var ancestors = new HashSet<string>(StringComparer.Ordinal)
                { child.InstanceId };
            var cursor = child.ParentInstanceId;
            var cycle = false;
            while (cursor is not null && parts.TryGetValue(cursor, out var ancestor))
            {
                if (!ancestors.Add(cursor))
                {
                    cycle = true;
                    break;
                }
                cursor = ancestor.Instance.ParentInstanceId;
            }
            if (cycle)
            {
                Issue("construct-mount-cycle", path + ".parentInstanceId",
                    NativeOriginIssueSeverity.Invalid,
                    "Installed component containment cannot form a cycle.");
                continue;
            }

            // The mounting joint is a real physical contact. Only an explicit
            // PowerConnections edge makes it conduct power in explicit mode.
            links[child.InstanceId].Add(parent.Instance.InstanceId);
            links[parent.Instance.InstanceId].Add(child.InstanceId);
        }

        // An unconnected component is permitted. Physical connectivity
        // permits functional dependencies; it does not prescribe anatomy.
        static List<HashSet<string>> ConnectedNetworks(
            IReadOnlyDictionary<string, HashSet<string>> adjacency)
        {
            var result = new List<HashSet<string>>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in adjacency.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!visited.Add(root)) continue;
                var members = new HashSet<string>(StringComparer.Ordinal) { root };
                var queue = new Queue<string>();
                queue.Enqueue(root);
                while (queue.Count > 0)
                    foreach (var neighbor in adjacency[queue.Dequeue()])
                        if (visited.Add(neighbor))
                        {
                            members.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                result.Add(members);
            }
            return result;
        }

        var networks = ConnectedNetworks(links);

        // Null retains legacy assemblies: all physical links are also
        // assumed to conduct power. An explicit list, including an empty
        // list, instead defines independently wired power networks.
        // Power links must follow authored physical component connections.
        var powerNetworks = networks;
        if (request.PowerConnections is not null)
        {
            var powerLinks = parts.Keys.ToDictionary(x => x,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);
            if (request.PowerConnections.Count > 4096)
                Issue("construct-power-link-count-invalid", "powerConnections",
                    NativeOriginIssueSeverity.Invalid,
                    "The evaluator supports at most 4096 explicit power links.");
            var seenPowerLinks = new HashSet<string>(StringComparer.Ordinal);
            foreach (var connection in request.PowerConnections.Take(4096))
            {
                if (connection is null ||
                    string.IsNullOrWhiteSpace(connection.FromInstanceId) ||
                    string.IsNullOrWhiteSpace(connection.ToInstanceId) ||
                    !parts.ContainsKey(connection.FromInstanceId) ||
                    !parts.ContainsKey(connection.ToInstanceId) ||
                    connection.FromInstanceId == connection.ToInstanceId ||
                    !links[connection.FromInstanceId].Contains(connection.ToInstanceId))
                {
                    Issue("construct-power-link-invalid", "powerConnections",
                        NativeOriginIssueSeverity.Invalid,
                        "Power links must join distinct, directly connected installed components.");
                    continue;
                }
                var a = connection.FromInstanceId;
                var b = connection.ToInstanceId;
                var pair = string.CompareOrdinal(a, b) < 0
                    ? a + "\u0000" + b : b + "\u0000" + a;
                if (!seenPowerLinks.Add(pair))
                {
                    Issue("construct-power-link-duplicate", "powerConnections",
                        NativeOriginIssueSeverity.Invalid,
                        "Each undirected power connection may be specified only once.");
                    continue;
                }
                powerLinks[a].Add(b);
                powerLinks[b].Add(a);
            }
            powerNetworks = ConnectedNetworks(powerLinks);
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
        foreach (var network in powerNetworks)
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

        var derivedSize = NativeConstructBodySizeResolver.Resolve(
            catalog, request.Components, Issue);
        long knownCost = knownMaterialCost + knownModificationCost;
        IReadOnlyDictionary<string, long>? requiredIngredients = null;
        long? retainedMaterialMass = null;
        IReadOnlyList<string>? missingMassMaterials = null;
        if (recipeCostMethod is JsonElement costMethod)
        {
            var quote = NativeCraftingResourceQuoter.Evaluate(
                catalog, costMethod, recipeSelections);
            knownCost = quote.KnownCost;
            requiredIngredients = quote.RequiredResources;
            retainedMaterialMass = quote.KnownRetainedMaterialMassPounds;
            missingMassMaterials = quote.UnresolvedRetainedMassMaterials;
            issues.AddRange(quote.Issues);
        }
        foreach (var part in parts.Values)
        {
            if (!recipeCostMethod.HasValue)
            {
                if (part.ConstructionCost is int cost) knownCost += cost;
                else if (request.InitialConstruction)
                    Issue("construct-cost-unresolved",
                        "components." + part.Instance.InstanceId,
                        NativeOriginIssueSeverity.Unresolved,
                        "An initial construction cost has not been authored for this component.");
            }
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

        // Refuse automatic load shedding. An overallocated power network
        // cannot operate its powered loads until the requested allocation
        // changes. Passive zero-demand, dependency-free components remain on.
        var activeIds = parts.Values.Where(part =>
                online.Contains(part.Instance.InstanceId) &&
                (!overloaded.Contains(part.Instance.InstanceId) ||
                    part.PowerDemand == 0 && part.Requirements.Length == 0))
            .Select(part => part.Instance.InstanceId)
            .ToHashSet(StringComparer.Ordinal);

        // Functional dependents must not appear operational merely because
        // their provider was provisionally online before the power check.
        // Propagate power failures through cross-network dependency chains.
        var dependenciesChanged = true;
        while (dependenciesChanged)
        {
            dependenciesChanged = false;
            foreach (var part in parts.Values.OrderBy(x => x.Instance.InstanceId,
                StringComparer.Ordinal))
            {
                var id = part.Instance.InstanceId;
                if (!activeIds.Contains(id)) continue;
                var network = networks.First(x => x.Contains(id));
                if (part.Requirements.All(group => group.Any(capability =>
                    activeIds.Any(providerId => providerId != id &&
                        network.Contains(providerId) &&
                        parts[providerId].Provides.Contains(capability, StringComparer.Ordinal)))))
                    continue;
                activeIds.Remove(id);
                dependenciesChanged = true;
                Issue("construct-dependency-unavailable", "components." + id,
                    NativeOriginIssueSeverity.Incomplete,
                    "A connected functional provider is no longer operational after power allocation.");
            }
        }

        var states = parts.Values.OrderBy(x => x.Instance.InstanceId, StringComparer.Ordinal)
            .Select(part =>
            {
                var id = part.Instance.InstanceId;
                var active = activeIds.Contains(id);
                if (!active && part.ShutdownConsequence is string consequence)
                    failures.Add(new(id, consequence));
                return new NativeConstructComponentState(id, part.Instance.DefinitionId,
                    true, active, active ? part.Provides : [], part.Instance.Enabled,
                    part.Instance.ModeId,
                    active ? part.OperationalOriginTraitIds : [], part.RequiresContinuousPower,
                    part.Instance.PowerSourceId is null ? null :
                        part.SourceAvailable && active);
            }).ToArray();

        // Keep requested power separate from the subset that actually
        // operates after power and dependency reconciliation. Unknown values
        // produce unknown headroom, rather than misleading free capacity.
        var powerStates = powerNetworks.Select(network =>
        {
            var members = network.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var requestedDemand = members.Where(online.Contains)
                .Sum(id => (long)(parts[id].PowerDemand ?? 0));
            var availableOutput = members.Where(online.Contains)
                .Sum(id => (long)parts[id].PowerOutput);
            var operationalDemand = members.Where(activeIds.Contains)
                .Sum(id => (long)(parts[id].PowerDemand ?? 0));
            var operationalOutput = members.Where(activeIds.Contains)
                .Sum(id => (long)parts[id].PowerOutput);
            var unknownDemand = members.Any(id =>
                parts[id].Instance.Enabled && parts[id].PowerDemand is null);
            var unknownOutput = members.Any(id =>
                parts[id].Instance.Enabled && !parts[id].OutputKnown);
            var known = !unknownDemand && !unknownOutput;
            return new NativeConstructPowerNetworkState(members[0], members,
                requestedDemand, availableOutput, operationalDemand,
                operationalOutput,
                known ? availableOutput - requestedDemand : null,
                known ? requestedDemand > availableOutput : null,
                unknownDemand, unknownOutput);
        }).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();

        var status = issues.Any(x => x.Severity == NativeOriginIssueSeverity.Invalid)
            ? NativeOriginConstructionStatus.Invalid
            : issues.Any(x => x.Severity == NativeOriginIssueSeverity.Conflict)
                ? NativeOriginConstructionStatus.Conflicting
                : issues.Any(x => x.Severity == NativeOriginIssueSeverity.Unresolved)
                    ? NativeOriginConstructionStatus.Unresolved
                    : issues.Any(x => x.Severity == NativeOriginIssueSeverity.Incomplete)
                        ? NativeOriginConstructionStatus.Incomplete
                        : NativeOriginConstructionStatus.Authorized;
        return new(status, knownCost, states, failures, issues, powerStates)
        {
            RequiredConstructionResources = requiredIngredients,
            DerivedCreatureSize = derivedSize?.Size,
            KnownRetainedMaterialMassPounds = retainedMaterialMass,
            UnresolvedRetainedMassMaterials = missingMassMaterials
        };
    }

    private sealed record Part(NativeConstructComponentInstance Instance, int? ConstructionCost,
        int? PowerDemand, int PowerOutput, string[] Provides, string[][] Requirements,
        string? ShutdownConsequence, string[] OperationalOriginTraitIds,
        string[] MountInterfaces, string[] AcceptsMountInterfaces,
        bool OutputKnown, bool RequiresContinuousPower, bool SourceAvailable);

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
    string? PowerSourceId = null,
    string? ParentInstanceId = null, string? MountInterfaceId = null,
    string? ConstructionVariantId = null);

/// <summary>
/// A passive, undirected power path between directly connected components.
/// This does not grant capabilities, provide energy, or prescribe a body layout.
/// </summary>
public sealed record NativeConstructPowerConnection(
    string FromInstanceId, string ToInstanceId);

public sealed record NativeConstructAssemblyRequest(
    IReadOnlyList<NativeConstructComponentInstance> Components,
    int? InitialAllowance = null, bool InitialConstruction = true,
    IReadOnlyList<NativeConstructPowerConnection>? PowerConnections = null,
    IReadOnlyList<string>? UnavailablePowerSourceInstanceIds = null);

public sealed record NativeConstructComponentState(
    string InstanceId, string DefinitionId, bool Installed, bool Operational,
    IReadOnlyList<string> ActiveCapabilities, bool RequestedEnabled,
    string? ModeId = null, IReadOnlyList<string>? ActiveOperationalOriginTraitIds = null,
    bool RequiresContinuousPower = false, bool? SourceSupplyingPower = null);

public sealed record NativeConstructFailure(string InstanceId, string ConsequenceId);

/// <summary>
/// One connected power circuit. Requested totals describe the attempted
/// allocation; operational totals reflect dependent shutdowns. These are
/// simultaneous power figures, not reserves or stored energy.
/// </summary>
public sealed record NativeConstructPowerNetworkState(
    string Id, IReadOnlyList<string> ComponentInstanceIds,
    long RequestedDemand, long AvailableOutput,
    long OperationalDemand, long OperationalOutput,
    long? RequestedHeadroom, bool? IsOverallocated,
    bool HasUnresolvedDemand, bool HasUnresolvedOutput);

public sealed record NativeConstructAssemblyResult(
    NativeOriginConstructionStatus Status, long KnownConstructionCost,
    IReadOnlyList<NativeConstructComponentState> Components,
    IReadOnlyList<NativeConstructFailure> ShutdownConsequences,
    IReadOnlyList<NativeOriginContractIssue> Issues,
    IReadOnlyList<NativeConstructPowerNetworkState>? PowerNetworks = null)
{
    // Native recipe-input quotation exposes the sourced ingredient bill.
    // It is advisory for initial creation, not an inventory withdrawal.
    public IReadOnlyDictionary<string, long>? RequiredConstructionResources { get; init; }
    // A verified body-defining construction variant, not independent player size.
    public string? DerivedCreatureSize { get; init; }
    // Partial material evidence only. Retained unpriced/unquantified material
    // and component masses must be reconciled before computing encumbrance.
    public long? KnownRetainedMaterialMassPounds { get; init; }
    public IReadOnlyList<string>? UnresolvedRetainedMassMaterials { get; init; }
    public bool IsAuthorized => Status == NativeOriginConstructionStatus.Authorized;
}
