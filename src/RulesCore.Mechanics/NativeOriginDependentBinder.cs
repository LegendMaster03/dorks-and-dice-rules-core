using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Applies canonical authored source bindings to selected inherent characteristics.
/// A related ability or spell never receives another contributor's choice merely
/// because the choices share a display name. Only selected grants provide values.
/// </summary>
internal static class NativeOriginDependentBinder
{
    public static (IReadOnlyList<NativeOriginEffectiveGrant> Grants,
        IReadOnlyList<NativeOriginContractIssue> Issues) Bind(
        NativeCatalogSnapshot catalog, NativeOriginProposal proposal,
        IReadOnlyList<NativeOriginEffectiveGrant> reconciled)
    {
        var grants = new List<NativeOriginEffectiveGrant>();
        var issues = new List<NativeOriginContractIssue>();
        var offered = (proposal.Candidates ?? [])
            .ToDictionary(x => x.Id, StringComparer.Ordinal);

        foreach (var grant in reconciled)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
                grant.EffectiveValue is not { } value ||
                value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty("originBindings", out var definitions) ||
                definitions.ValueKind != JsonValueKind.Object)
            {
                grants.Add(grant);
                continue;
            }

            var parameters = (grant.Parameters ?? new Dictionary<string, JsonElement>())
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            var sources = (grant.ParameterSources ?? new Dictionary<string, string>())
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            var incomplete = false;

            foreach (var binding in definitions.EnumerateObject())
            {
                var lookup = Text(binding.Value, "lookup");
                if (lookup is "leaf" or "nearest")
                    continue; // Canonical source binding was verified in deconstruction.
                if (lookup == "manifested-intrinsic")
                {
                    var slot = Text(binding.Value, "intrinsicSlot");
                    var scope = Text(binding.Value, "scope");
                    if (slot is null || scope != "source")
                    {
                        Issue("origin-binding-unsupported", grant.Id,
                            NativeOriginIssueSeverity.Unresolved,
                            "The authored intrinsic binding has unsupported semantics.");
                        continue;
                    }

                    var matching = reconciled.Where(other =>
                        other.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                        other.Identity.IntrinsicSlotId == slot &&
                        other.Identity.Occurrence.Source == grant.Identity.Occurrence.Source &&
                        // Two distinct parents may reference the same canonical
                        // ancestry. Native source identity is not contributor
                        // ownership; require matching selected candidate evidence.
                        SharesContributor(grant, other, offered))
                        .ToArray();
                    // Previously manifested contributors already carry a bound
                    // spell parameter. It must not silently change in descendants.
                    if (parameters.TryGetValue(binding.Name, out var inherited))
                    {
                        if (matching.Length > 0 &&
                            (matching.Length != 1 || matching[0].EffectiveValue is null ||
                             !JsonElement.DeepEquals(inherited,
                                 matching[0].EffectiveValue!.Value)))
                            Issue("origin-binding-parameter-conflict",
                                grant.Id + ":" + binding.Name,
                                NativeOriginIssueSeverity.Conflict,
                                "An inherited parameter conflicts with the source choice.");
                        continue;
                    }
                    if (matching.Length != 1 || matching[0].EffectiveValue is null)
                    {
                        Issue(matching.Length == 0 ? "origin-binding-choice-missing" :
                            "origin-binding-ambiguous", grant.Id + ":" + binding.Name,
                            matching.Length == 0 ? NativeOriginIssueSeverity.Incomplete :
                                NativeOriginIssueSeverity.Unresolved,
                            "Exactly one intrinsic choice from the authored source is required.");
                        continue;
                    }
                    parameters[binding.Name] = matching[0].EffectiveValue!.Value.Clone();
                    continue;
                }

                if (lookup != "manifested-trait-choice" ||
                    Text(binding.Value, "scope") != "contributor" ||
                    Text(binding.Value, "mechanicalDefinitionId") is not { } id ||
                    Text(binding.Value, "choiceRequirementId") is not { } choiceId ||
                    !CanonicalBindingMatches(catalog, grant, binding.Name, binding.Value))
                {
                    Issue("origin-binding-unsupported", grant.Id + ":" + binding.Name,
                        NativeOriginIssueSeverity.Unresolved,
                        "This source-bound trait choice lacks an executable canonical binding.");
                    continue;
                }
                var matchingOwners = reconciled.Where(other =>
                    other.Identity.Kind == NativeOriginCharacteristicKind.Trait &&
                    other.Identity.MechanicalDefinitionId == id &&
                    SharesContributor(grant, other, offered)).ToArray();
                if (matchingOwners.Length != 1)
                {
                    Issue(matchingOwners.Length == 0 ?
                            "origin-binding-choice-missing" : "origin-binding-ambiguous",
                        grant.Id + ":" + binding.Name,
                        matchingOwners.Length == 0 ? NativeOriginIssueSeverity.Incomplete :
                            NativeOriginIssueSeverity.Unresolved,
                        "A dependent trait needs one selected source-bound aptitude grant.");
                    continue;
                }
                var owner = matchingOwners[0];
                var selection = owner.ResolvedChoices?.Where(x =>
                    x.RequirementId == choiceId).ToArray() ?? [];
                if (selection.Length != 1 ||
                    selection[0].SelectedOptionIds is not { Count: 1 } ||
                    !NativeChoicePermits(catalog, id, selection[0].SelectedOptionIds[0]))
                {
                    Issue("origin-binding-choice-invalid",
                        grant.Id + ":" + binding.Name,
                        NativeOriginIssueSeverity.Invalid,
                        "The source trait's persistent choice is missing or not native-authored.");
                    continue;
                }
                var chosen = JsonSerializer.SerializeToElement(selection[0].SelectedOptionIds[0]);
                if (parameters.TryGetValue(binding.Name, out var inheritedValue) &&
                    !JsonElement.DeepEquals(inheritedValue, chosen))
                {
                    Issue("origin-binding-parameter-conflict",
                        grant.Id + ":" + binding.Name,
                        NativeOriginIssueSeverity.Conflict,
                        "The dependent trait conflicts with its selected source aptitude.");
                    continue;
                }

                parameters[binding.Name] = chosen;
                // The choice originates in the selected owner's candidate, not
                // an arbitrary global stat. Grant ownership stays separate.
                var evidence = MatchingContributorEvidence(grant, owner, offered);
                if (evidence is null)
                {
                    Issue("origin-binding-source-missing", grant.Id + ":" + binding.Name,
                        NativeOriginIssueSeverity.Unresolved,
                        "The selected aptitude has no verified contributor evidence.");
                    continue;
                }
                sources[binding.Name] = evidence;
            }

            if (!incomplete)
                grants.Add(grant with
                {
                    Parameters = parameters,
                    ParameterSources = sources.Count == 0 ? grant.ParameterSources : sources
                });

            void Issue(string code, string path, NativeOriginIssueSeverity severity,
                string detail)
            {
                issues.Add(new(code, path, severity, detail));
                incomplete = true;
            }
        }

        return (grants, issues);
    }

    private static bool CanonicalBindingMatches(NativeCatalogSnapshot catalog,
        NativeOriginEffectiveGrant grant, string key, JsonElement actual)
    {
        var id = grant.Identity.MechanicalDefinitionId;
        return id is not null &&
            catalog.TryGetById(id, out var entity) && entity is not null &&
            entity.Category == "origin-trait" &&
            entity.Definition.TryGetProperty("trait", out var trait) &&
            trait.TryGetProperty("originBindings", out var bindings) &&
            bindings.TryGetProperty(key, out var canonical) &&
            JsonElement.DeepEquals(canonical, actual);
    }

    private static bool NativeChoicePermits(NativeCatalogSnapshot catalog,
        string traitId, string value)
    {
        return catalog.TryGetById(traitId, out var entity) &&
            entity is not null && entity.Category == "origin-trait" &&
            entity.Definition.TryGetProperty("trait", out var trait) &&
            trait.TryGetProperty("abilityChoice", out var choice) &&
            choice.TryGetProperty("from", out var from) &&
            from.ValueKind == JsonValueKind.Array &&
            from.EnumerateArray().Any(x =>
                x.ValueKind == JsonValueKind.String && x.GetString() == value);
    }

    private static bool SharesContributor(NativeOriginEffectiveGrant dependent,
        NativeOriginEffectiveGrant owner,
        IReadOnlyDictionary<string, NativeOriginCandidate> offered) =>
        MatchingContributorEvidence(dependent, owner, offered) is not null;

    private static string? MatchingContributorEvidence(
        NativeOriginEffectiveGrant dependent, NativeOriginEffectiveGrant owner,
        IReadOnlyDictionary<string, NativeOriginCandidate> offered)
    {
        var from = dependent.SourceCandidateIds.Where(offered.ContainsKey)
            .Select(x => offered[x]).ToArray();
        var owners = owner.SourceCandidateIds.Where(offered.ContainsKey)
            .Select(x => offered[x]).ToArray();
        if (from.Length != dependent.SourceCandidateIds.Count ||
            owners.Length != owner.SourceCandidateIds.Count)
            return null;

        // A consolidated aptitude grant can have evidence from multiple
        // contributors. Attribute the dependent's actual choice to the matching
        // contributor, not the lexicographically first unrelated ancestor.
        return owner.SourceCandidateIds.OrderBy(x => x, StringComparer.Ordinal)
            .FirstOrDefault(id => offered.TryGetValue(id, out var ownerCandidate) &&
                from.Any(d => d.ContributorId is not null
                    ? d.ContributorId == ownerCandidate.ContributorId
                    : ownerCandidate.ContributorId is null &&
                      d.Identity.Occurrence.Source.Kind == NativeOriginSourceKind.Native &&
                      ownerCandidate.Identity.Occurrence.Source.Kind ==
                          NativeOriginSourceKind.Native));
    }

    private static string? Text(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(key, out var item) &&
        item.ValueKind == JsonValueKind.String ? item.GetString() : null;
}
