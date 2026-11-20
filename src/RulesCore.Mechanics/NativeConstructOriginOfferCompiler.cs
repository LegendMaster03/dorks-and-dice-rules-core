using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Produces provenance-preserving, catalog-authored offers for Construct Assembly.
/// All selections, choices, reconciliation and grants remain owned by the shared
/// Origin construction engine. No component capability is implicitly a trait.
/// </summary>
internal static class NativeConstructOriginOfferCompiler
{
    internal sealed record OperationalOffer(
        string CandidateId, string InstanceId, string TraitId,
        string SourceDefinitionId, string OccurrencePath, bool IsPowerSource,
        IReadOnlyList<string>? ActiveComponentModes);
    internal sealed record Compilation(
        IReadOnlyList<NativeOriginCandidate> Candidates,
        IReadOnlyList<NativeOriginContractIssue> Issues,
        IReadOnlyList<OperationalOffer> OperationalOffers);

    internal static Compilation Compile(NativeCatalogSnapshot catalog,
        NativeOriginProposal proposal, NativeConstructAssemblyRequest assembly)
    {
        var offers = new List<NativeOriginCandidate>();
        var issues = new List<NativeOriginContractIssue>();
        var operational = new List<OperationalOffer>();

        void Issue(string code, string path, NativeOriginIssueSeverity severity, string detail) =>
            issues.Add(new(code, path, severity, detail));

        void AddIntrinsic(string slot, string path, JsonElement? value,
            IReadOnlyList<NativeOriginChoiceRequirement>? choices = null)
        {
            var source = new NativeOriginSourceReference(NativeOriginSourceKind.Native,
                proposal.Method.RuleId);
            var occurrence = new NativeOriginSourceOccurrence(source, path);
            offers.Add(new("construct:intrinsic:" + slot,
                new(NativeOriginCharacteristicKind.Intrinsic, occurrence,
                    proposal.Method.RuleId + "#" + path, IntrinsicSlotId: slot),
                new(NativeOriginEligibility.Eligible,
                    RuleReference: proposal.Method.RuleId,
                    Explanation: "Authored intrinsic selection of the Construct Assembly method."),
                OfferedValue: value, RequiredChoices: choices));
        }

        // A process classification is not a creature-type grant. Optional,
        // method-authored intrinsics enter the same Origin reconciliation as
        // Species intrinsics and never imply a fixed shape or size.
        if (proposal.Intent == NativeOriginIntent.Custom)
        {
            var method = new NativeOriginContractValidator(catalog).FindMethod(proposal.Method);
            if (method?.AuthoredDefinition.TryGetProperty("constructIntrinsics",
                    out var intrinsics) == true)
            {
                if (intrinsics.ValueKind != JsonValueKind.Object)
                    Issue("construct-intrinsics-invalid", "method.constructIntrinsics",
                        NativeOriginIssueSeverity.Invalid,
                        "Construct intrinsic authoring must be an object.");
                else
                {
                    if (intrinsics.TryGetProperty("creatureType", out var creatureType))
                    {
                        var typeName = creatureType.ValueKind == JsonValueKind.String
                            ? creatureType.GetString() : null;
                        var typeId = typeName is null ? null :
                            typeName.StartsWith("creature-type.", StringComparison.Ordinal)
                                ? typeName : "creature-type." + typeName;
                        if (string.IsNullOrWhiteSpace(typeName) ||
                            !catalog.TryGetById(typeId!, out var nativeType) ||
                            nativeType?.Category != "creatureType" ||
                            !nativeType.Definition.TryGetProperty("originationMethods",
                                out var allowed) ||
                            allowed.ValueKind != JsonValueKind.Array ||
                            !allowed.EnumerateArray().Any(x =>
                                x.ValueKind == JsonValueKind.String &&
                                x.GetString() == proposal.Method.MethodId))
                            Issue("construct-creature-type-invalid",
                                "method.constructIntrinsics.creatureType",
                                NativeOriginIssueSeverity.Invalid,
                                "The authored creature type must permit this origination method.");
                        else
                            AddIntrinsic("creatureType",
                                "/methodDefinitions/" + proposal.Method.MethodId +
                                "/constructIntrinsics/creatureType",
                                JsonSerializer.SerializeToElement(
                                    typeId!["creature-type.".Length..]));
                    }
                    var fixedSize = intrinsics.TryGetProperty("size", out var size);
                    var variableSize = intrinsics.TryGetProperty("sizeChoice", out var sizeChoice);
                    if (fixedSize && variableSize)
                        Issue("construct-size-ambiguous", "method.constructIntrinsics",
                            NativeOriginIssueSeverity.Invalid,
                            "A Construct method must not define both fixed and selectable sizes.");
                    else if (fixedSize)
                    {
                        if (size.ValueKind != JsonValueKind.String ||
                            string.IsNullOrWhiteSpace(size.GetString()))
                            Issue("construct-size-invalid", "method.constructIntrinsics.size",
                                NativeOriginIssueSeverity.Invalid,
                                "An authored fixed size must be a nonempty string.");
                        else
                            AddIntrinsic("size", "/methodDefinitions/" + proposal.Method.MethodId +
                                "/constructIntrinsics/size", size.Clone());
                    }
                    else if (variableSize)
                    {
                        if (sizeChoice.ValueKind != JsonValueKind.Object ||
                            !sizeChoice.TryGetProperty("from", out var sizes) ||
                            sizes.ValueKind != JsonValueKind.Array)
                            Issue("construct-size-choice-invalid",
                                "method.constructIntrinsics.sizeChoice",
                                NativeOriginIssueSeverity.Invalid,
                                "A size choice needs a finite authored list.");
                        else
                        {
                            var options = sizes.EnumerateArray()
                                .Where(x => x.ValueKind == JsonValueKind.String)
                                .Select(x => x.GetString()!).ToArray();
                            if (options.Length == 0 || options.Any(string.IsNullOrWhiteSpace) ||
                                options.Length != sizes.GetArrayLength() ||
                                options.Distinct(StringComparer.Ordinal).Count() != options.Length)
                                Issue("construct-size-choice-invalid",
                                    "method.constructIntrinsics.sizeChoice",
                                    NativeOriginIssueSeverity.Invalid,
                                    "Size options must be distinct nonempty strings.");
                            else
                            {
                                var path = "/methodDefinitions/" + proposal.Method.MethodId +
                                    "/constructIntrinsics/sizeChoice";
                                var occurrence = new NativeOriginSourceOccurrence(
                                    new(NativeOriginSourceKind.Native, proposal.Method.RuleId), path);
                                AddIntrinsic("size", path, null,
                                    [new("size", occurrence, NativeOriginChoiceTiming.Creation,
                                        1, 1, FiniteOptions: options, ParameterKey: "size")]);
                            }
                        }
                    }
                }
            }
        }

        void AddTraitOffers(string definitionId, string expectedCategory,
            string instanceId, string context)
        {
            if (!catalog.TryGetById(definitionId, out var sourceDefinition) ||
                sourceDefinition?.Category != expectedCategory)
            {
                Issue("construct-origin-source-invalid", instanceId,
                    NativeOriginIssueSeverity.Invalid,
                    "An installed characteristic source must resolve to its native catalog kind.");
                return;
            }
            var sourceDefinitionJson = sourceDefinition.Definition;
            foreach (var field in new[] { "installedOriginTraitIds", "operationalOriginTraitIds" })
            {
                if (!sourceDefinitionJson.TryGetProperty(field, out var traits))
                    continue;
                if (traits.ValueKind != JsonValueKind.Array)
                {
                    Issue("construct-origin-traits-invalid", sourceDefinition.Id + "." + field,
                        NativeOriginIssueSeverity.Invalid,
                        "Origin trait references must be an array of canonical IDs.");
                    continue;
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var index = 0;
                foreach (var item in traits.EnumerateArray())
                {
                    var ordinal = index++;
                    var id = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                    var referencePath = "components." + instanceId + "." + context +
                        "." + field + "." + ordinal;
                    if (string.IsNullOrWhiteSpace(id) || !seen.Add(id) ||
                        !catalog.TryGetById(id, out var nativeTrait) ||
                        nativeTrait?.Category != "origin-trait" ||
                        !nativeTrait.Definition.TryGetProperty("trait", out var canonical) ||
                        canonical.ValueKind != JsonValueKind.Object)
                    {
                        Issue("construct-origin-trait-reference-invalid", referencePath,
                            NativeOriginIssueSeverity.Invalid,
                            "An installed characteristic must reference a distinct canonical origin-trait.");
                        continue;
                    }

                    var path = "/" + field + "/" + ordinal +
                        (context.Length == 0 ? "" : "/" + context) +
                        "/instance/" + instanceId;
                    var candidateId = "construct:" + instanceId +
                        (context.Length == 0 ? "" : ":" + context.Replace('/', ':')) +
                        (field == "installedOriginTraitIds" ? ":trait:" : ":operational-trait:") +
                        ordinal;
                    var source = new NativeOriginSourceReference(NativeOriginSourceKind.Native,
                        sourceDefinition.Id);
                    var occurrence = new NativeOriginSourceOccurrence(source, path);
                    var choices = new List<NativeOriginChoiceRequirement>();
                    var unresolved = new List<string>();
                    var parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

                    string? Text(JsonElement value, string key) =>
                        value.ValueKind == JsonValueKind.Object &&
                        value.TryGetProperty(key, out var child) &&
                        child.ValueKind == JsonValueKind.String ? child.GetString() : null;

                    var timing = Text(canonical, "when") == "on-use" ?
                        NativeOriginChoiceTiming.OnUse : NativeOriginChoiceTiming.Creation;
                    var count = 1;
                    if (canonical.TryGetProperty("choiceCount", out var countValue))
                    {
                        if (countValue.ValueKind != JsonValueKind.Number ||
                            !countValue.TryGetInt32(out count) || count < 0)
                            unresolved.Add("Authored choiceCount is not a nonnegative integer.");
                    }
                    var menu = Text(canonical, "chooseFrom");
                    if (menu is null && Text(canonical, "grantType") == "feat-choice")
                        menu = "feat:" + (Text(canonical, "featCategory") ?? "all");
                    if (menu is null && Text(canonical, "spellList") is string spellList &&
                        Text(canonical, "spell") is null)
                        menu = "spell:" + spellList;
                    if (menu is not null)
                        choices.Add(new("selection", occurrence, timing, count, count,
                            OptionCatalogId: menu));
                    if (canonical.TryGetProperty("abilityChoice", out var abilityChoice))
                    {
                        if (abilityChoice.ValueKind != JsonValueKind.Object ||
                            !abilityChoice.TryGetProperty("from", out var rawOptions) ||
                            rawOptions.ValueKind != JsonValueKind.Array)
                            unresolved.Add("Ability choice has no authored finite options.");
                        else
                        {
                            var from = rawOptions.EnumerateArray()
                                .Where(x => x.ValueKind == JsonValueKind.String)
                                .Select(x => x.GetString()!).ToArray();
                            if (from.Length == 0 || from.Length != rawOptions.GetArrayLength() ||
                                from.Any(string.IsNullOrWhiteSpace) ||
                                from.Distinct(StringComparer.Ordinal).Count() != from.Length)
                                unresolved.Add("Ability choice has invalid finite options.");
                            var choiceTiming = abilityChoice.TryGetProperty("perCharacter",
                                out var persistent) && persistent.ValueKind == JsonValueKind.True
                                ? NativeOriginChoiceTiming.Creation
                                : NativeOriginChoiceTiming.Unknown;
                            choices.Add(new("abilityChoice", occurrence, choiceTiming, 1, 1,
                                FiniteOptions: from, ParameterKey: "abilityChoice"));
                        }
                    }
                    if (canonical.TryGetProperty("choices", out _))
                        unresolved.Add("Unrecognized trait choice authoring requires canonical support.");
                    if (canonical.TryGetProperty("originBindings", out var bindings))
                    {
                        if (bindings.ValueKind != JsonValueKind.Object)
                            unresolved.Add("Origin bindings must be an object.");
                        else
                            foreach (var binding in bindings.EnumerateObject())
                            {
                                var lookup = Text(binding.Value, "lookup");
                                if (lookup is "manifested-intrinsic" or
                                    "manifested-trait-choice")
                                    continue; // Shared dependent binder verifies effective grants.
                                var fieldName = Text(binding.Value, "sourceField");
                                if (lookup is not ("leaf" or "nearest") ||
                                    fieldName is null ||
                                    !sourceDefinitionJson.TryGetProperty(fieldName,
                                        out var bound) ||
                                    bound.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                                    unresolved.Add("Unresolved authored source binding: " +
                                        binding.Name);
                                else
                                    parameters[binding.Name] = bound.Clone();
                            }
                    }
                    if (!canonical.EnumerateObject().Any(property =>
                        property.Name is not ("id" or "name" or "description" or "reviewNotes")))
                        unresolved.Add("Source trait has no normalized mechanical properties.");
                    if (canonical.GetRawText().Contains("selected-dragonborn-",
                            StringComparison.Ordinal) &&
                        (!canonical.TryGetProperty("originBindings", out var sourceBindings) ||
                         sourceBindings.ValueKind != JsonValueKind.Object ||
                         parameters.Count != sourceBindings.EnumerateObject().Count()))
                        unresolved.Add("Source-dependent trait parameters remain unbound.");

                    offers.Add(new(candidateId,
                        new(NativeOriginCharacteristicKind.Trait, occurrence, nativeTrait.Id),
                        new(NativeOriginEligibility.Eligible,
                            RuleReference: proposal.Method.RuleId,
                            Explanation: "Catalog-authored installed trait of a Construct assembly."),
                        OfferedValue: canonical.Clone(),
                        OfferedParameters: parameters,
                        RequiredChoices: choices,
                        UnresolvedEvidence: unresolved));
                    if (field == "operationalOriginTraitIds")
                    {
                        // A candidate's native occurrence determines its runtime
                        // provider. Another source offering the same canonical ID
                        // must never substitute for the authorized occurrence.
                        // Only the base component is gated by its authored modes;
                        // separately installed options keep their own provenance.
                        string[]? activeComponentModes = null;
                        if (context.Length == 0 &&
                            sourceDefinitionJson.TryGetProperty("operatingModes",
                                out var modes) &&
                            modes.ValueKind == JsonValueKind.Array)
                            activeComponentModes = modes.EnumerateArray()
                                .Where(mode => mode.ValueKind == JsonValueKind.Object &&
                                    mode.TryGetProperty("id", out var modeId) &&
                                    modeId.ValueKind == JsonValueKind.String &&
                                    mode.TryGetProperty("operationalOriginTraitIds",
                                        out var modeTraits) &&
                                    modeTraits.ValueKind == JsonValueKind.Array &&
                                    modeTraits.EnumerateArray().Any(trait =>
                                        trait.ValueKind == JsonValueKind.String &&
                                        trait.GetString() == id))
                                .Select(mode => mode.GetProperty("id").GetString()!)
                                .ToArray();
                        operational.Add(new(candidateId, instanceId, id!,
                            sourceDefinition.Id, path, context == "power-source",
                            activeComponentModes));
                    }
                }
            }
        }

        foreach (var instance in assembly.Components.OrderBy(x => x.InstanceId,
            StringComparer.Ordinal))
        {
            AddTraitOffers(instance.DefinitionId, "construct-component",
                instance.InstanceId, "");
            foreach (var (slot, material) in (instance.Materials ??
                new Dictionary<string, string>()).OrderBy(x => x.Key, StringComparer.Ordinal))
                AddTraitOffers(material, "construct-material", instance.InstanceId,
                    "material/" + Uri.EscapeDataString(slot));
            if (instance.DesignId is not null)
                AddTraitOffers(instance.DesignId, "construct-design", instance.InstanceId,
                    "design");
            foreach (var enhancement in instance.EnhancementIds ?? [])
                AddTraitOffers(enhancement, "construct-enhancement", instance.InstanceId,
                    "enhancement/" + Uri.EscapeDataString(enhancement));
            if (instance.PowerSourceId is not null)
                AddTraitOffers(instance.PowerSourceId, "construct-power-source",
                    instance.InstanceId, "power-source");
        }

        return new(offers, issues, operational);
    }
}
