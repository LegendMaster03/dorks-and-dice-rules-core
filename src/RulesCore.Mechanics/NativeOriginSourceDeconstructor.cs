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

        if (proposal.Intent == NativeOriginIntent.Direct && selectedAncestry is not null &&
            catalog.TryGetById(selectedAncestry, out var ancestry) && ancestry?.Category == "ancestry")
        {
            var parent = String(ancestry.Definition, "parent");
            if (selectedSpecies is null && parent is not null) selectedSpecies = parent;
            else if (parent is not null && selectedSpecies != parent)
                Issue("ancestry-parent-mismatch", selectedAncestry, NativeOriginIssueSeverity.Conflict,
                    "The selected Ancestry declares a different Species parent.");
        }

        if (proposal.Intent == NativeOriginIntent.Direct)
        {
            if (selectedSpecies is not null) AddSource(new(NativeOriginSourceKind.Native, selectedSpecies),
                null, true);
            if (selectedAncestry is not null) AddSource(new(NativeOriginSourceKind.Native, selectedAncestry),
                null, true);
        }

        foreach (var contributor in proposal.Contributors ?? [])
        {
            if (contributor is null || contributor.Source is null) continue;
            if (contributor.Source.Kind == NativeOriginSourceKind.Native)
                AddSource(contributor.Source, contributor.Id, false);
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

        void AddSource(NativeOriginSourceReference source, string? contributorId, bool direct)
        {
            if (!catalog.TryGetById(source.Id, out var entity) || entity is null ||
                entity.Category is not ("species" or "ancestry"))
            {
                Issue("origin-source-unavailable", source.Id, NativeOriginIssueSeverity.Unresolved,
                    "The selected source is not a playable native Species/Ancestry.");
                return;
            }
            var selectable = entity.Definition.TryGetProperty("selectable", out var flag) &&
                flag.ValueKind == JsonValueKind.False ? NativeOriginEligibility.Restricted :
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
                    // Source-dependent references must be bound by authored policy in later phases.
                    if (trait.GetRawText().Contains("selected-dragonborn-", StringComparison.Ordinal))
                        unresolved.Add("Leaf-dependent source binding requires an approved relationship.");
                    var id = "trait:" + (String(trait, "id") ?? (index - 1).ToString());
                    Add(path, NativeOriginCharacteristicKind.Trait, null, offered, choices,
                        source.Id + "#" + id, unresolved);
                }
            }

            void Add(string property, NativeOriginCharacteristicKind kind, string? slot,
                JsonElement? value, IReadOnlyList<NativeOriginChoiceRequirement>? choices = null,
                string? definitionId = null, IReadOnlyList<string>? unresolved = null)
            {
                var path = property.StartsWith('/') ? property : "/" + property;
                var candidateId = (contributorId is null ? "direct" : "contributor:" + contributorId) +
                    ":" + entity.Id + ":" + path;
                var identity = new NativeOriginCharacteristicIdentity(kind,
                    new(source, path), definitionId ?? entity.Id + "#" + path,
                    IntrinsicSlotId: slot);
                candidates.TryAdd(candidateId, new(candidateId, identity, basis, contributorId, value,
                    RequiredChoices: choices, UnresolvedEvidence: unresolved));
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
