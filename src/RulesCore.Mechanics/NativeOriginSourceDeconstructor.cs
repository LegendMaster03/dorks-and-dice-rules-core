using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Translates selected-native Species/Ancestry occurrences into candidate evidence.
/// No biological compatibility or cross-contributor inheritance is inferred here.
/// </summary>
public sealed class NativeOriginSourceDeconstructor(NativeCatalogSnapshot catalog)
{
    public (NativeOriginProposal Proposal, IReadOnlyList<NativeOriginContractIssue> Issues)
        Deconstruct(NativeOriginProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var issues = new List<NativeOriginContractIssue>();
        var candidates = new SortedDictionary<string, NativeOriginCandidate>(StringComparer.Ordinal);
        var selectedSpecies = proposal.SelectedSpeciesId;
        var selectedAncestry = proposal.SelectedAncestryId;

        // The native ancestry graph may contain intermediate families (for
        // example, Dragonborn -> Chromatic -> Red). Walk its actual parent
        // references; never assume every leaf points directly to Species.
        var ancestors = new List<NativeRuleEntity>();
        NativeRuleEntity? parentSpecies = null;
        if (proposal.Intent == NativeOriginIntent.Direct && selectedAncestry is not null)
        {
            var next = selectedAncestry;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (next is not null && visited.Add(next) && visited.Count <= 64)
            {
                if (!catalog.TryGetById(next, out var entity) || entity is null)
                {
                    Issue("ancestry-parent-unavailable", next, NativeOriginIssueSeverity.Unresolved,
                        "An authored Ancestry parent is unavailable in the native catalog.");
                    break;
                }
                if (entity.Category == "species")
                {
                    parentSpecies = entity;
                    break;
                }
                if (entity.Category != "ancestry")
                {
                    Issue("ancestry-parent-invalid", next, NativeOriginIssueSeverity.Invalid,
                        "Ancestry parentage must resolve through ancestry or species definitions.");
                    break;
                }
                ancestors.Add(entity);
                next = String(entity.Definition, "parent");
                if (next is null)
                    Issue("ancestry-parent-unresolved", entity.Id, NativeOriginIssueSeverity.Unresolved,
                        "This ancestry has no authored root Species relationship.");
            }
            if (parentSpecies is null && next is not null &&
                (visited.Count > 64 || visited.Contains(next) && ancestors.Count > 0))
                Issue("ancestry-parent-cycle", selectedAncestry, NativeOriginIssueSeverity.Invalid,
                    "Ancestry parent chains must terminate in the native catalog.");
            if (parentSpecies is not null)
            {
                if (selectedSpecies is null) selectedSpecies = parentSpecies.Id;
                else if (selectedSpecies != parentSpecies.Id)
                    Issue("ancestry-parent-mismatch", selectedAncestry,
                        NativeOriginIssueSeverity.Conflict,
                        "The selected Species disagrees with the authored Ancestry parent chain.");
            }
        }

        // Binding contexts follow the selected leaf to its root. A shared Species
        // characteristic may depend on an authored descendant field, without
        // changing the source occurrence of the characteristic itself.
        var directLineage = ancestors.ToList();
        if (parentSpecies is not null) directLineage.Add(parentSpecies);

        if (proposal.Intent == NativeOriginIntent.Direct)
        {
            if (selectedSpecies is not null)
                AddSource(new(NativeOriginSourceKind.Native, selectedSpecies), null, true,
                    lineage: directLineage);
            foreach (var ancestor in ancestors.AsEnumerable().Reverse())
                AddSource(new(NativeOriginSourceKind.Native, ancestor.Id), null, true,
                    ancestor.Id != selectedAncestry, directLineage);
        }

        foreach (var contributor in proposal.Contributors ?? [])
        {
            if (contributor is null || contributor.Source is null) continue;
            if (contributor.Source.Kind == NativeOriginSourceKind.Native)
            {
                // An Ancestry contributor supplies its actual Species/family source
                // chain as separate offers. Repeated shared roots remain identical
                // authored occurrences, not a second full grant package.
                var lineage = new List<NativeRuleEntity>();
                var visitedLineage = new HashSet<string>(StringComparer.Ordinal);
                string? next = contributor.Source.Id;
                while (next is not null && visitedLineage.Add(next) &&
                    visitedLineage.Count <= 64)
                {
                    if (!catalog.TryGetById(next, out var entity) || entity is null ||
                        entity.Category is not ("species" or "ancestry"))
                    {
                        Issue("contributor-lineage-unavailable", contributor.Id + ":" + next,
                            NativeOriginIssueSeverity.Unresolved,
                            "Contributor parentage must resolve to native Species/Ancestry.");
                        break;
                    }
                    lineage.Add(entity);
                    if (entity.Category == "species") break;
                    next = String(entity.Definition, "parent");
                    if (next is null)
                        Issue("contributor-lineage-incomplete", contributor.Id + ":" + entity.Id,
                            NativeOriginIssueSeverity.Unresolved,
                            "The contributor Ancestry has no authored parent.");
                }
                if (next is not null && visitedLineage.Count > 64 ||
                    next is not null && visitedLineage.Contains(next) &&
                    lineage.Count > 0 && lineage[^1].Id != next)
                    Issue("contributor-lineage-cycle", contributor.Id,
                        NativeOriginIssueSeverity.Invalid,
                        "A contributor's Ancestry chain contains a cycle.");
                foreach (var entity in lineage.AsEnumerable().Reverse())
                    AddSource(new(NativeOriginSourceKind.Native, entity.Id,
                        contributor.Source.Version), contributor.Id, false,
                        lineage: lineage);
            }
            else if (contributor.Source.Kind == NativeOriginSourceKind.ManifestedProfile)
            {
                if (contributor.ManifestedProfile is null)
                {
                    Issue("contributor-current-missing", contributor.Id,
                        NativeOriginIssueSeverity.Unresolved,
                        "Historical references are not a replacement for a current manifested profile.");
                    continue;
                }
                foreach (var grant in contributor.ManifestedProfile.InherentGrants ?? [])
                {
                    if (grant?.Identity is null) continue;
                    var id = "nested:" + contributor.Id + ":" + grant.Id;
                    candidates.TryAdd(id, new NativeOriginCandidate(id, grant.Identity,
                        new(NativeOriginEligibility.Unreviewed, Explanation:
                            "Previously manifested grants are evidence, not automatic descendant grants."),
                        contributor.Id, grant.EffectiveValue, grant.Parameters));
                }
            }
            // Custom and process-specific sources may participate in the method without
            // granting any characteristics. Explicit custom candidates are handled below.
        }

        foreach (var supplied in proposal.Candidates ?? [])
        {
            if (supplied?.Identity?.Occurrence?.Source is null) continue;
            if (candidates.TryGetValue(supplied.Id, out var generated))
            {
                if (supplied.Identity != generated.Identity ||
                    (supplied.OfferedValue is { } v && generated.OfferedValue?.GetRawText() != v.GetRawText()))
                    Issue("candidate-native-evidence-mismatch", supplied.Id,
                        NativeOriginIssueSeverity.Invalid,
                        "A supplied candidate cannot replace the authoritative source occurrence.");
                continue;
            }
            if (supplied.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Custom)
            {
                Issue("candidate-not-offered", supplied.Id, NativeOriginIssueSeverity.Invalid,
                    "An unknown native or nested offer cannot be injected into the catalog.");
                continue;
            }
            candidates.TryAdd(supplied.Id, supplied with
            {
                SelectionBasis = new(NativeOriginEligibility.Unreviewed,
                    Explanation: "Custom mechanics require a verified table decision.")
            });
        }

        return (proposal with { SelectedSpeciesId = selectedSpecies,
            Candidates = candidates.Values.ToArray() }, issues);

        void Issue(string code, string path, NativeOriginIssueSeverity severity, string detail) =>
            issues.Add(new(code, path, severity, detail));

        void AddSource(NativeOriginSourceReference source, string? contributorId, bool direct,
            bool inheritedFamily = false, IReadOnlyList<NativeRuleEntity>? lineage = null)
        {
            if (!catalog.TryGetById(source.Id, out var entity) || entity is null ||
                entity.Category is not ("species" or "ancestry"))
            {
                Issue("origin-source-unavailable", source.Id, NativeOriginIssueSeverity.Unresolved,
                    "The selected source is not a playable native Species/Ancestry.");
                return;
            }
            // A nonselectable base Species may be the required foundation of a
            // playable Ancestry; this does not make the base selectable alone.
            var paired = direct && entity.Category == "species" &&
                parentSpecies?.Id == entity.Id && ancestors.Count > 0 &&
                (!ancestors[0].Definition.TryGetProperty("selectable", out var leafFlag) ||
                 leafFlag.ValueKind != JsonValueKind.False);
            var restricted = entity.Definition.TryGetProperty("selectable", out var flag) &&
                flag.ValueKind == JsonValueKind.False && !paired && !inheritedFamily;
            if (entity.Definition.TryGetProperty("acquisitionMethods", out var methods) &&
                methods.ValueKind == JsonValueKind.Array &&
                !methods.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String &&
                    x.GetString() == proposal.Method.MethodId))
                restricted = true;
            var selectable = restricted ? NativeOriginEligibility.Restricted :
                direct ? NativeOriginEligibility.Eligible : NativeOriginEligibility.Unreviewed;
            if (direct && selectable == NativeOriginEligibility.Restricted)
                Issue("origin-source-restricted", source.Id, NativeOriginIssueSeverity.Unresolved,
                    "This native source is not selectable by the requested direct process.");
            var basis = new NativeOriginSelectionBasis(selectable,
                selectable == NativeOriginEligibility.Eligible ? entity.Id : null,
                Explanation: direct ? "Direct native selection." :
                    "Contributor participation does not establish trait transmission.");

            foreach (var (property, slot) in new[]
            {
                ("creatureType", "creatureType"), ("size", "size"),
                ("walkingSpeedFeet", "walkingSpeedFeet")
            })
                if (entity.Definition.TryGetProperty(property, out var value) &&
                    value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                    Add(property, NativeOriginCharacteristicKind.Intrinsic, slot, value.Clone());

            if (entity.Definition.TryGetProperty("sizeChoice", out var sizeChoice) &&
                sizeChoice.ValueKind == JsonValueKind.Object &&
                sizeChoice.TryGetProperty("from", out var sizes) && sizes.ValueKind == JsonValueKind.Array)
            {
                var path = "/sizeChoice";
                var req = new NativeOriginChoiceRequirement("size", new(source, path),
                    NativeOriginChoiceTiming.Creation, 1, 1,
                    sizes.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString()!).ToArray(), ParameterKey: "size");
                Add("sizeChoice", NativeOriginCharacteristicKind.Intrinsic, "size", null, [req]);
            }
            if (entity.Definition.TryGetProperty("spellcastingAbilityChoice", out var abilities) &&
                abilities.ValueKind == JsonValueKind.Array)
            {
                var req = new NativeOriginChoiceRequirement("spellcastingAbility",
                    new(source, "/spellcastingAbilityChoice"), NativeOriginChoiceTiming.Creation, 1, 1,
                    abilities.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString()!).ToArray(), ParameterKey: "spellcastingAbility");
                Add("spellcastingAbilityChoice", NativeOriginCharacteristicKind.Intrinsic,
                    "spellcastingAbility", null, [req]);
            }
            if (entity.Definition.TryGetProperty("traits", out var traits) &&
                traits.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var trait in traits.EnumerateArray())
                {
                    var path = "/traits/" + index++;
                    if (trait.ValueKind != JsonValueKind.Object)
                    {
                        Issue("trait-shape-unsupported", source.Id + path,
                            NativeOriginIssueSeverity.Unresolved, "Trait is not a typed native object.");
                        continue;
                    }
                    var offered = trait.Clone();
                    var timing = String(trait, "when") == "on-use" ?
                        NativeOriginChoiceTiming.OnUse : NativeOriginChoiceTiming.Creation;
                    var count = Integer(trait, "choiceCount");
                    var menu = String(trait, "chooseFrom");
                    if (menu is null && String(trait, "grantType") == "feat-choice")
                        menu = "feat:" + (String(trait, "featCategory") ?? "all");
                    if (menu is null && String(trait, "spellList") is string spellList &&
                        String(trait, "spell") is null)
                        menu = "spell:" + spellList;
                    IReadOnlyList<NativeOriginChoiceRequirement>? choices = null;
                    if (menu is not null)
                    {
                        choices = [new("selection", new(source, path), timing,
                            count.GetValueOrDefault(1), count.GetValueOrDefault(1),
                            OptionCatalogId: menu)];
                    }
                    var unresolved = new List<string>();
                    // A description alone does not supply executable semantics.
                    if (!trait.EnumerateObject().Any(p =>
                        p.Name is not ("id" or "name" or "description" or "reviewNotes")))
                        unresolved.Add("Source trait has descriptive text but no normalized mechanics.");
                    var parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    if (trait.TryGetProperty("originBindings", out var bindingDefinitions) &&
                        bindingDefinitions.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var binding in bindingDefinitions.EnumerateObject())
                        {
                            var field = String(binding.Value, "sourceField");
                            var lookup = String(binding.Value, "lookup");
                            var targets = lineage is { Count: > 0 } ? lineage : [entity];
                            var sourceEntity = lookup switch
                            {
                                "leaf" => targets.FirstOrDefault(),
                                "nearest" => targets.FirstOrDefault(x => field is not null &&
                                    x.Definition.TryGetProperty(field, out _)),
                                _ => null
                            };
                            if (field is null || sourceEntity is null ||
                                !sourceEntity.Definition.TryGetProperty(field, out var bound) ||
                                bound.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                                unresolved.Add("Unresolved authored source binding: " + binding.Name);
                            else
                                parameters[binding.Name] = bound.Clone();
                        }
                    }
                    // Symbolic fields are not executable without a complete authored
                    // parameter mapping. The bound values remain separate from the
                    // immutable source trait definition.
                    if (trait.GetRawText().Contains("selected-dragonborn-", StringComparison.Ordinal) &&
                        (bindingDefinitions.ValueKind != JsonValueKind.Object ||
                         parameters.Count != bindingDefinitions.EnumerateObject().Count()))
                        unresolved.Add("Source-dependent trait parameters remain unbound.");
                    var id = "trait:" + (String(trait, "id") ?? (index - 1).ToString());
                    Add(path, NativeOriginCharacteristicKind.Trait, null, offered, choices,
                        source.Id + "#" + id, unresolved, parameters);
                }
            }

            void Add(string property, NativeOriginCharacteristicKind kind, string? slot,
                JsonElement? value, IReadOnlyList<NativeOriginChoiceRequirement>? choices = null,
                string? definitionId = null, IReadOnlyList<string>? unresolved = null,
                IReadOnlyDictionary<string, JsonElement>? parameters = null)
            {
                var path = property.StartsWith('/') ? property : "/" + property;
                var candidateId = (contributorId is null ? "direct" : "contributor:" + contributorId) +
                    ":" + entity.Id + ":" + path;
                var identity = new NativeOriginCharacteristicIdentity(kind,
                    new(source, path), definitionId ?? entity.Id + "#" + path,
                    IntrinsicSlotId: slot);
                candidates.TryAdd(candidateId, new(candidateId, identity, basis, contributorId, value,
                    OfferedParameters: parameters, RequiredChoices: choices,
                    UnresolvedEvidence: unresolved));
            }
        }
    }

    private static string? String(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString() : null;

    private static int? Integer(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.Number &&
        item.TryGetInt32(out var number) ? number : null;
}
