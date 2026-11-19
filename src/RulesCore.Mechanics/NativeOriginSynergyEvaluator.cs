using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Evaluates native or verified table-authored combinations of effective Origin characteristics.
/// Candidate offers and unverified proposal-supplied rules never create synergies.
/// Each rule issues at most one stable result; separate acquisition/balance policies
/// remain the responsibility of the origination procedure.
/// </summary>
internal sealed class NativeOriginSynergyEvaluator(NativeCatalogSnapshot catalog)
{
    private sealed record Input(IReadOnlyList<NativeOriginCharacteristicSelector> Alternatives,
        int Count);
    private sealed record Rule(string OwnerId, string Id, string ResultTraitId,
        IReadOnlyList<Input> Inputs, JsonElement Effect, string? TableDecisionId = null);

    public (IReadOnlyList<NativeOriginEffectiveGrant> Grants,
        IReadOnlyList<NativeOriginContractIssue> Issues) Evaluate(
        NativeOriginProposal proposal, IReadOnlyList<NativeOriginEffectiveGrant> effective,
        IReadOnlyList<NativeTableRuleDecision>? verifiedTableSynergies = null)
    {
        var issues = new List<NativeOriginContractIssue>();
        var rules = ReadRules(issues, verifiedTableSynergies);
        if (rules.Count == 0) return ([], issues);

        // An authored interaction graph may be cyclic even if a particular
        // input set would terminate. Do not guess whether its outputs stack.
        var edges = rules.ToDictionary(rule => Key(rule),
            rule => rules.Where(other => rule.Inputs.Any(input =>
                input.Alternatives.Any(selector =>
                    selector.MechanicalDefinitionId == other.ResultTraitId ||
                    selector.SourceId == other.OwnerId))).Select(Key)
                .ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var cyclic = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            var start = Key(rule);
            bool ReachesStart(string node, HashSet<string> visited)
            {
                if (!visited.Add(node)) return false;
                foreach (var target in edges[node])
                    if (target == start || ReachesStart(target, visited))
                        return true;
                return false;
            }
            if (ReachesStart(start, new HashSet<string>(StringComparer.Ordinal)))
                cyclic.Add(start);
        }

        var offers = (proposal.Candidates ?? [])
            .Where(candidate => candidate is not null)
            .GroupBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var available = effective.OrderBy(grant => grant.Id, StringComparer.Ordinal).ToList();
        var derived = new List<NativeOriginEffectiveGrant>();
        var generated = new HashSet<string>(StringComparer.Ordinal);
        var reportedAmbiguities = new HashSet<string>(StringComparer.Ordinal);

        // Compute eligible outputs against the same immutable pass snapshot.
        // Two independently authored rules yielding the same canonical trait
        // do not establish a priority or stacking policy merely because their
        // identifiers sort in a particular order. Block both and report the
        // unresolved policy instead of silently choosing the first.
        // Acyclic transitive rules need at most one successful pass per rule.
        for (var pass = 0; pass < rules.Count; pass++)
        {
            var eligible = new List<(Rule Rule,
                IReadOnlyList<NativeOriginEffectiveGrant> Inputs)>();
            foreach (var rule in rules)
            {
                var key = Key(rule);
                if (generated.Contains(key) || cyclic.Contains(key)) continue;
                if (available.Any(x => x.Identity.MechanicalDefinitionId == rule.ResultTraitId))
                    continue;
                if (TrySelectInputs(rule, available, offers, out var inputs))
                    eligible.Add((rule, inputs));
            }

            var contested = eligible.GroupBy(x => x.Rule.ResultTraitId, StringComparer.Ordinal)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var resultTraitId in contested.OrderBy(x => x, StringComparer.Ordinal))
                if (reportedAmbiguities.Add(resultTraitId))
                    issues.Add(new("synergy-result-ambiguous",
                        "originSynergies." + resultTraitId,
                        NativeOriginIssueSeverity.Unresolved,
                        "Multiple applicable interactions grant the same canonical trait; an explicit authored alternative or precedence rule is required."));

            var changed = false;
            foreach (var (rule, inputs) in eligible)
            {
                if (contested.Contains(rule.ResultTraitId)) continue;
                var key = Key(rule);
                var provenance = inputs.OrderBy(x => x.Id, StringComparer.Ordinal)
                    .Select(x => new NativeOriginSynergyInputEvidence(
                        x.Id, x.ManifestationId, x.Identity,
                        x.SourceCandidateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()))
                    .ToArray();
                var token = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(rule.OwnerId + "\n" + rule.Id)))[..24];
                var manifestationId = "synergy:" + token;
                var identity = new NativeOriginCharacteristicIdentity(
                    NativeOriginCharacteristicKind.Trait,
                    new(new(rule.TableDecisionId is null
                            ? NativeOriginSourceKind.Native : NativeOriginSourceKind.Custom,
                            rule.OwnerId),
                        "/originSynergies/" + Uri.EscapeDataString(rule.Id)),
                    MechanicalDefinitionId: rule.ResultTraitId);
                var grant = new NativeOriginEffectiveGrant("grant:" + manifestationId,
                    manifestationId, identity, provenance
                        .SelectMany(x => x.SourceCandidateIds)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                    EffectiveValue: rule.Effect.Clone(),
                    Authorization: rule.TableDecisionId is null
                        ? new NativeOriginDecisionBasis(NativeOriginAdjudication.Supported,
                            rule.OwnerId)
                        : new NativeOriginDecisionBasis(NativeOriginAdjudication.DmAuthorized,
                            TableDecisionId: rule.TableDecisionId))
                {
                    Synergy = new(rule.OwnerId, rule.Id, provenance)
                };
                derived.Add(grant);
                available.Add(grant);
                available.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
                generated.Add(key);
                changed = true;
            }
            if (!changed) break;
        }

        // Only report a cycle when one of its prerequisites is actually present.
        // Unrelated dormant catalog definitions must not block character creation.
        foreach (var rule in rules.Where(x => cyclic.Contains(Key(x))))
            if (rule.Inputs.Any(input => input.Alternatives.Any(selector =>
                available.Any(grant => Matches(selector, grant, offers)))))
                issues.Add(new("synergy-cycle-unresolved",
                    "originSynergies." + Key(rule), NativeOriginIssueSeverity.Unresolved,
                    "The authored interaction participates in a derived-characteristic cycle."));

        return (derived.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(), issues);
    }

    private IReadOnlyList<Rule> ReadRules(List<NativeOriginContractIssue> issues,
        IReadOnlyList<NativeTableRuleDecision>? verifiedTableSynergies)
    {
        var results = new List<Rule>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        // Table decisions are supplied only by the construction engine after
        // verifying actor authority and the exact trusted scope. Never iterate
        // unverified serialized proposal decisions here.
        var authored = new List<(string OwnerId, string? TargetId,
            JsonElement Definition, string? TableDecisionId)>();
        for (var offset = 0; ; offset += 100)
        {
            var page = catalog.Search(category: "rule", offset: offset, limit: 100);
            foreach (var owner in page.Items)
            {
                if (!owner.Definition.TryGetProperty("originSynergies", out var definitions))
                    continue;
                if (definitions.ValueKind != JsonValueKind.Array)
                {
                    Invalid(owner.Id, "originSynergies must be an array.");
                    continue;
                }
                foreach (var item in definitions.EnumerateArray())
                    authored.Add((owner.Id, null, item, null));
            }
            if (offset + page.Items.Length >= page.Total) break;
        }
        foreach (var decision in verifiedTableSynergies ?? [])
        {
            if (decision.Target?.Kind != "origin-synergy") continue;
            authored.Add(("table-decision:" + decision.Id, decision.Target.Id,
                decision.EffectiveOutcome, decision.Id));
        }

        foreach (var (ownerId, targetedId, item, tableDecisionId) in authored)
        {
            // Reject unimplemented semantic fields. Ignoring a claimed
            // operating condition or negation could accidentally grant power.
            // A newline separates the authoring source and interaction ID in
            // the stable grant hash. Reject delimiter injection rather than
            // letting two distinct rule pairs resolve to one synthetic ID.
            if (ownerId.IndexOfAny(['\r', '\n']) >= 0 ||
                item.ValueKind != JsonValueKind.Object ||
                item.EnumerateObject().Any(p => p.Name is not (
                    "id" or "resultTraitId" or "inputs" or "name" or
                    "description" or "reviewNotes")) ||
                !String(item, "id", out var id) ||
                targetedId is not null && targetedId != id ||
                !String(item, "resultTraitId", out var resultTraitId) ||
                !item.TryGetProperty("inputs", out var inputsJson) ||
                inputsJson.ValueKind != JsonValueKind.Array ||
                inputsJson.GetArrayLength() is < 1 or > 16 ||
                !catalog.TryGetById(resultTraitId, out var result) ||
                result is null || result.Category != "origin-trait" ||
                !result.Definition.TryGetProperty("trait", out var effect) ||
                effect.ValueKind != JsonValueKind.Object ||
                // A fixed canonical result may not depend on an unbound creation
                // choice, level, resource owner or externally supplied parameter.
                // Those must be explicitly implemented before being executable.
                new[] { "originBindings", "requiredCharacterLevel", "choices",
                    "choice", "choiceOptions", "chooseFrom", "choiceCount",
                    "abilityChoice", "featCategory", "spellList", "when",
                    "uses", "sharedUsesFromTrait", "resourcePool" }
                    .Any(key => effect.TryGetProperty(key, out _)) ||
                effect.TryGetProperty("grantType", out var grantType) &&
                grantType.ValueKind == JsonValueKind.String &&
                grantType.GetString() == "feat-choice")
            {
                Invalid(ownerId, "Interaction must identify an authorized, canonical, fixed Origin trait with a matching target.");
                continue;
            }
            var groups = new List<Input>();
            var valid = true;
            var total = 0;
            foreach (var group in inputsJson.EnumerateArray())
            {
                if (group.ValueKind != JsonValueKind.Object ||
                    group.EnumerateObject().Any(p => p.Name is not ("anyOf" or "count")) ||
                    !group.TryGetProperty("anyOf", out var alternativesJson) ||
                    alternativesJson.ValueKind != JsonValueKind.Array ||
                    alternativesJson.GetArrayLength() is < 1 or > 16 ||
                    !group.TryGetProperty("count", out var countJson) ||
                    countJson.ValueKind != JsonValueKind.Number ||
                    !countJson.TryGetInt32(out var count) || count is < 1 or > 16)
                {
                    valid = false;
                    break;
                }
                total += count;
                var alternatives = new List<NativeOriginCharacteristicSelector>();
                foreach (var choice in alternativesJson.EnumerateArray())
                {
                    if (choice.ValueKind != JsonValueKind.Object ||
                        choice.EnumerateObject().Any(p => p.Name is not (
                            "candidateId" or "mechanicalDefinitionId" or "intrinsicSlotId" or
                            "sourceId" or "parameterKey" or "expectedValue")))
                    {
                        valid = false;
                        break;
                    }
                    NativeOriginCharacteristicSelector? selector;
                    try
                    {
                        selector = JsonSerializer.Deserialize<NativeOriginCharacteristicSelector>(
                            choice.GetRawText(),
                            new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    }
                    catch (JsonException)
                    {
                        valid = false;
                        break;
                    }
                    if (selector is null || selector.CandidateId is not null ||
                        selector.MechanicalDefinitionId is null &&
                        selector.IntrinsicSlotId is null && selector.SourceId is null ||
                        selector.ParameterKey is null && selector.ExpectedValue is not null)
                    {
                        valid = false;
                        break;
                    }
                    alternatives.Add(selector);
                }
                if (!valid) break;
                groups.Add(new(alternatives, count));
            }
            if (!valid || total is < 2 or > 16 || !keys.Add(ownerId + "\n" + id))
            {
                Invalid(ownerId, "Interaction has invalid or duplicate selector, count, or ID data.");
                continue;
            }
            results.Add(new(ownerId, id, resultTraitId, groups, effect.Clone(),
                tableDecisionId));
        }
        return results.OrderBy(Key, StringComparer.Ordinal).ToArray();

        void Invalid(string ownerId, string detail) =>
            issues.Add(new("synergy-authoring-invalid", "originSynergies." + ownerId,
                NativeOriginIssueSeverity.Unresolved, detail));
    }

    private static bool TrySelectInputs(Rule rule, IReadOnlyList<NativeOriginEffectiveGrant> available,
        IReadOnlyDictionary<string, NativeOriginCandidate> offers,
        out IReadOnlyList<NativeOriginEffectiveGrant> matched)
    {
        matched = [];
        // Expand each minimum-count requirement into a separate matching slot.
        // Standard augmenting-path matching finds valid distinct inputs even
        // when alternatives overlap; a greedy first-match could reject them.
        var slots = rule.Inputs.SelectMany(input =>
            Enumerable.Repeat(input.Alternatives, input.Count)).ToArray();
        var occupants = new Dictionary<string, int>(StringComparer.Ordinal);
        var ordered = available.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        bool Assign(int slot, HashSet<string> visited)
        {
            foreach (var grant in ordered)
            {
                if (!slots[slot].Any(selector => Matches(selector, grant, offers)) ||
                    !visited.Add(grant.Id)) continue;
                if (!occupants.TryGetValue(grant.Id, out var prior) ||
                    Assign(prior, visited))
                {
                    occupants[grant.Id] = slot;
                    return true;
                }
            }
            return false;
        }
        for (var slot = 0; slot < slots.Length; slot++)
            if (!Assign(slot, new HashSet<string>(StringComparer.Ordinal)))
                return false;

        matched = ordered.Where(grant => occupants.ContainsKey(grant.Id)).ToArray();
        return matched.Count == slots.Length;
    }

    private static bool Matches(NativeOriginCharacteristicSelector selector,
        NativeOriginEffectiveGrant grant,
        IReadOnlyDictionary<string, NativeOriginCandidate> offers)
    {
        if (selector.ParameterKey is not null &&
            (grant.Parameters?.TryGetValue(selector.ParameterKey, out var actual) != true ||
             selector.ExpectedValue is { } expected &&
             !JsonElement.DeepEquals(expected, actual)))
            return false;

        bool IdentityMatches(NativeOriginCharacteristicIdentity identity) =>
            (selector.MechanicalDefinitionId is null ||
             identity.MechanicalDefinitionId == selector.MechanicalDefinitionId) &&
            (selector.IntrinsicSlotId is null ||
             identity.IntrinsicSlotId == selector.IntrinsicSlotId) &&
            (selector.SourceId is null ||
             identity.Occurrence.Source.Id == selector.SourceId);

        // A derived grant has no client-selectable candidate. Its complete
        // source identity was issued by this evaluator from a native rule.
        if (grant.Synergy is not null)
            return IdentityMatches(grant.Identity);

        // Each selector must match one authentic offer, never a synthetic
        // combination of fields from distinct consolidated contributors.
        return grant.SourceCandidateIds.Any(id =>
            offers.TryGetValue(id, out var candidate) &&
            candidate.Identity.Kind == grant.Identity.Kind &&
            candidate.Identity.MechanicalDefinitionId == grant.Identity.MechanicalDefinitionId &&
            candidate.Identity.IntrinsicSlotId == grant.Identity.IntrinsicSlotId &&
            IdentityMatches(candidate.Identity));
    }

    // Length-prefix the owner; concatenation with a colon can collide when
    // independently authored IDs themselves contain colons (including table
    // decision IDs), causing graph construction to throw on duplicate keys.
    private static string Key(Rule rule) =>
        rule.OwnerId.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        ":" + rule.OwnerId + rule.Id;

    private static bool String(JsonElement value, string name, out string result)
    {
        result = "";
        if (!value.TryGetProperty(name, out var field) ||
            field.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(field.GetString()) ||
            field.GetString()!.IndexOfAny(['\r', '\n']) >= 0)
            return false;
        result = field.GetString()!;
        return true;
    }
}
