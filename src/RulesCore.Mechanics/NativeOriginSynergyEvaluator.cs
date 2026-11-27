using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Evaluates native or verified table-authored interactions of effective grants or
/// explicitly selected, authenticated inherited instances. Mere contributor offers,
/// unexpressed genes, and unverified proposal-supplied rules never grant synergies.
/// Each rule issues at most one stable result; separate acquisition/balance policies
/// remain the responsibility of the origination procedure.
/// </summary>
internal sealed class NativeOriginSynergyEvaluator(NativeCatalogSnapshot catalog)
{
    private sealed record Input(IReadOnlyList<NativeOriginCharacteristicSelector> Alternatives,
        int Count);
    private sealed record Rule(string OwnerId, string Id, string ResultTraitId,
        IReadOnlyList<Input> Inputs, JsonElement Effect, string? TableDecisionId = null,
        bool Inherited = false);

    public (IReadOnlyList<NativeOriginEffectiveGrant> Grants,
        IReadOnlyList<NativeOriginContractIssue> Issues) Evaluate(
        NativeOriginProposal proposal, IReadOnlyList<NativeOriginEffectiveGrant> effective,
        IReadOnlyList<NativeTableRuleDecision>? verifiedTableSynergies = null)
    {
        var issues = new List<NativeOriginContractIssue>();
        var rules = ReadRules(issues, verifiedTableSynergies)
            .Where(rule => !rule.Inherited).ToList();
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
        var starting = effective.OrderBy(grant => grant.Id, StringComparer.Ordinal).ToArray();
        var available = starting.ToList();
        var derived = new List<NativeOriginEffectiveGrant>();
        var generated = new HashSet<string>(StringComparer.Ordinal);
        var rejectedResults = new HashSet<string>(StringComparer.Ordinal);

        // Conflicts can emerge in later passes: a newly derived prerequisite
        // may activate a second rule for a trait already issued by the first.
        // When that happens, discard the entire temporary closure and rebuild
        // without the disputed result. This also retracts any transitive
        // benefits already issued from the ambiguous trait. Each restart
        // excludes at least one additional result, so the bound is finite.
        for (var restart = 0; restart <= rules.Count; restart++)
        {
            available = starting.ToList();
            derived.Clear();
            generated.Clear();
            var rebuild = false;

            // Compute all eligibility from a common snapshot per pass; do not
            // let rule ID sort order decide between simultaneous outcomes.
            for (var pass = 0; pass < rules.Count; pass++)
            {
                var eligible = new List<(Rule Rule,
                    IReadOnlyList<NativeOriginEffectiveGrant> Inputs)>();
                foreach (var rule in rules)
                {
                    var key = Key(rule);
                    if (generated.Contains(key) || cyclic.Contains(key) ||
                        rejectedResults.Contains(rule.ResultTraitId)) continue;
                    // A directly acquired trait is already effective; only
                    // previously generated synergy grants can create a new
                    // competition for the same canonical result.
                    if (starting.Any(x =>
                        x.Identity.MechanicalDefinitionId == rule.ResultTraitId))
                        continue;
                    if (TrySelectInputs(rule, available, offers, out var inputs))
                        eligible.Add((rule, inputs));
                }

                var contested = eligible.GroupBy(x => x.Rule.ResultTraitId,
                        StringComparer.Ordinal)
                    .Where(x => x.Count() > 1)
                    .Select(x => x.Key)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var candidate in eligible)
                    if (derived.Any(grant =>
                        grant.Identity.MechanicalDefinitionId ==
                            candidate.Rule.ResultTraitId))
                        contested.Add(candidate.Rule.ResultTraitId);

                if (contested.Count > 0)
                {
                    foreach (var resultTraitId in contested.OrderBy(x => x,
                        StringComparer.Ordinal))
                    {
                        rejectedResults.Add(resultTraitId);
                        issues.Add(new("synergy-result-ambiguous",
                            "originSynergies." + resultTraitId,
                            NativeOriginIssueSeverity.Unresolved,
                            "Multiple applicable interactions grant the same canonical trait; an explicit authored alternative or precedence rule is required."));
                    }
                    rebuild = true;
                    break;
                }

                var changed = false;
                foreach (var (rule, inputs) in eligible)
                {
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
            if (!rebuild) break;
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

    /// <summary>
    /// Evaluates explicitly selected genotype expressions. This shares the
    /// ordinary synergy rule parser and canonical fixed-trait safeguards but
    /// reads authenticated inherited instances, not available contributor offers.
    /// The caller retracts consumed ordinary grants before dependency closure.
    /// </summary>
    public (IReadOnlyList<NativeOriginEffectiveGrant> Grants,
        IReadOnlySet<string> ConsumedCandidateIds,
        IReadOnlyList<NativeOriginContractIssue> Issues) EvaluateInherited(
        NativeOriginProposal proposal,
        IReadOnlyList<NativeOriginBiologicalGeneInstance> genes,
        IReadOnlyList<NativeOriginEffectiveGrant> directGrants,
        int maxExpressedInstancesPerKind,
        IReadOnlyList<NativeTableRuleDecision>? verifiedTableSynergies = null)
    {
        var issues = new List<NativeOriginContractIssue>();
        var choices = proposal.BiologicalTransmission?.Combinations ?? [];
        if (choices.Count == 0)
            return ([], new HashSet<string>(StringComparer.Ordinal), issues);
        var rules = ReadRules(issues, verifiedTableSynergies)
            .Where(rule => rule.Inherited).ToArray();
        var offers = (proposal.Candidates ?? []).Where(x =>
                x is not null && !string.IsNullOrWhiteSpace(x.Id) &&
                x.Identity?.Occurrence?.Source is not null)
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var verified = genes.Where(g =>
                g is not null && !string.IsNullOrWhiteSpace(g.Id) &&
                !string.IsNullOrWhiteSpace(g.SourceCandidateId) &&
                g.Identity?.Occurrence?.Source is not null)
            .GroupBy(g => g.SourceCandidateId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var transmitted = (proposal.BiologicalTransmission?.CandidateIds ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var selectedRules = new HashSet<string>(StringComparer.Ordinal);
        var matched = new List<(Rule Rule, NativeOriginBiologicalGeneInstance[] Genes)>();

        foreach (var choice in choices)
        {
            var path = "biologicalTransmission.combinations";
            if (choice is null || string.IsNullOrWhiteSpace(choice.RuleOwnerId) ||
                string.IsNullOrWhiteSpace(choice.InteractionId) ||
                choice.CandidateIds is null || choice.CandidateIds.Count is < 2 or > 16 ||
                choice.CandidateIds.Any(string.IsNullOrWhiteSpace) ||
                choice.CandidateIds.Distinct(StringComparer.Ordinal).Count() !=
                    choice.CandidateIds.Count ||
                !selectedRules.Add(choice.RuleOwnerId + "\n" + choice.InteractionId))
            {
                issues.Add(new("inherited-combination-selection-invalid", path,
                    NativeOriginIssueSeverity.Invalid,
                    "A selected expression needs one distinct authored rule and two or more distinct transmitted instances."));
                continue;
            }
            path += "." + choice.InteractionId;
            var rule = rules.FirstOrDefault(x => x.OwnerId == choice.RuleOwnerId &&
                x.Id == choice.InteractionId);
            if (rule is null)
            {
                issues.Add(new("inherited-combination-rule-unavailable", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "The selected inherited expression has no authorized authored rule."));
                continue;
            }
            if (choice.CandidateIds.Any(id => !transmitted.Contains(id) ||
                !verified.ContainsKey(id) || !offers.TryGetValue(id, out var offer) ||
                offer.ContributorId is null || offer.Identity != verified[id].Identity ||
                offer.SelectionBasis?.Eligibility is not
                    (NativeOriginEligibility.Eligible or NativeOriginEligibility.DmAuthorized)))
            {
                issues.Add(new("inherited-combination-instance-unverified", path,
                    NativeOriginIssueSeverity.Invalid,
                    "Every input must be an actually transmitted, authenticated inherited occurrence."));
                continue;
            }
            var selected = choice.CandidateIds.Select(id => verified[id]).ToArray();
            if (!TrySelectInheritedInputs(rule, selected, out var ordered))
            {
                issues.Add(new("inherited-combination-input-mismatch", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "The exact inherited instances do not meet the authored selectors and counts."));
                continue;
            }
            matched.Add((rule, ordered));
        }

        // Evaluate all selections together. Sort order must never choose a
        // winner for competing canonical results or shared genetic evidence.
        var rejected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in matched)
            if (directGrants.Any(grant =>
                    grant.Identity.MechanicalDefinitionId == entry.Rule.ResultTraitId &&
                    !entry.Genes.Any(g => grant.SourceCandidateIds.Contains(
                        g.SourceCandidateId, StringComparer.Ordinal))) ||
                matched.Any(other => !ReferenceEquals(other.Rule, entry.Rule) &&
                    (other.Rule.ResultTraitId == entry.Rule.ResultTraitId ||
                     other.Genes.Any(g => entry.Genes.Any(x =>
                         x.SourceCandidateId == g.SourceCandidateId)))))
                rejected.Add(Key(entry.Rule));

        var involvedKinds = matched.SelectMany(x => x.Genes)
            .Select(g => GeneKind(g.Identity)).ToHashSet();
        // A rejected expression restores its direct inputs. Recompute capacity
        // until stable so later rejections can never leave an over-limit grant
        // authorized based on inputs that were only provisionally consumed.
        bool newlyRejected;
        do
        {
            var active = new Dictionary<
                (NativeOriginCharacteristicKind Kind, string Id), int>();
            foreach (var grant in directGrants.Where(x => x.Synergy is null))
            {
                var kind = GeneKind(grant.Identity);
                if (!involvedKinds.Contains(kind)) continue;
                if (matched.Any(entry => !rejected.Contains(Key(entry.Rule)) &&
                    entry.Genes.Any(g =>
                        grant.SourceCandidateIds.Contains(g.SourceCandidateId,
                            StringComparer.Ordinal))))
                    continue;
                active[kind] = active.GetValueOrDefault(kind) + 1;
            }
            // Count one combined grant per kind, not once per contributing gene.
            foreach (var entry in matched.Where(x => !rejected.Contains(Key(x.Rule))))
                foreach (var kind in entry.Genes.Select(g =>
                    GeneKind(g.Identity)).Distinct())
                    active[kind] = active.GetValueOrDefault(kind) + 1;
            newlyRejected = false;
            foreach (var entry in matched.Where(x => !rejected.Contains(Key(x.Rule))))
                if (entry.Genes.Any(g =>
                    active.GetValueOrDefault(GeneKind(g.Identity)) >
                        maxExpressedInstancesPerKind))
                    newlyRejected |= rejected.Add(Key(entry.Rule));
        } while (newlyRejected);

        foreach (var entry in matched.Where(x => rejected.Contains(Key(x.Rule))))
            issues.Add(new("inherited-combination-conflict",
                "biologicalTransmission.combinations." + entry.Rule.Id,
                NativeOriginIssueSeverity.Conflict,
                "The expression conflicts with another result, shared input, or the authored expression capacity."));

        var consumed = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<NativeOriginEffectiveGrant>();
        foreach (var (rule, sourceGenes) in matched.Where(x => !rejected.Contains(Key(x.Rule))))
        {
            var proof = sourceGenes.OrderBy(x => x.Id, StringComparer.Ordinal)
                .Select(g => new NativeOriginInheritedSynergyInputEvidence(g)).ToArray();
            var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                rule.OwnerId + "\n" + rule.Id)))[..24];
            var manifestationId = "synergy:" + token;
            var identity = new NativeOriginCharacteristicIdentity(
                NativeOriginCharacteristicKind.Trait,
                new(new(rule.TableDecisionId is null
                        ? NativeOriginSourceKind.Native : NativeOriginSourceKind.Custom,
                        rule.OwnerId),
                    "/originSynergies/" + Uri.EscapeDataString(rule.Id)),
                MechanicalDefinitionId: rule.ResultTraitId);
            results.Add(new NativeOriginEffectiveGrant("grant:" + manifestationId,
                manifestationId, identity,
                sourceGenes.Select(x => x.SourceCandidateId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                EffectiveValue: rule.Effect.Clone(),
                Authorization: rule.TableDecisionId is null
                    ? new NativeOriginDecisionBasis(NativeOriginAdjudication.Supported,
                        rule.OwnerId)
                    : new NativeOriginDecisionBasis(NativeOriginAdjudication.DmAuthorized,
                        TableDecisionId: rule.TableDecisionId))
            {
                Synergy = new(rule.OwnerId, rule.Id, [], proof)
            });
            foreach (var gene in sourceGenes) consumed.Add(gene.SourceCandidateId);
        }
        return (results.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            consumed, issues);
    }

    private static (NativeOriginCharacteristicKind Kind, string Id) GeneKind(
        NativeOriginCharacteristicIdentity identity) =>
        (identity.Kind, identity.Kind == NativeOriginCharacteristicKind.Trait
            ? identity.MechanicalDefinitionId ?? "" : identity.IntrinsicSlotId ?? "");

    /// <summary>
    /// Return only authored native rules for which the authenticated genotype
    /// contains a distinct matching input set. Never enumerate combinatorial
    /// subsets, choose the player's inputs, or expose table-only rulings.
    /// Null denotes an unavailable/oversized discovery, not an empty catalog.
    /// </summary>
    public IReadOnlyList<NativeOriginBiologicalInteractionOption>? DiscoverInherited(
        IReadOnlyList<NativeOriginBiologicalGeneInstance> genes)
    {
        if (genes.Count < 2) return [];
        if (genes.Count > 512 || genes.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() != genes.Count ||
            genes.Select(g => g.SourceCandidateId).Distinct(StringComparer.Ordinal).Count() != genes.Count)
            return null;
        var issues = new List<NativeOriginContractIssue>();
        var rules = ReadRules(issues, null).Where(rule => rule.Inherited).ToArray();
        var options = new List<NativeOriginBiologicalInteractionOption>();
        foreach (var rule in rules)
        {
            if (!TrySelectInheritedInputs(rule, genes, out _, exactSelection: false)) continue;
            if (!catalog.TryGetById(rule.ResultTraitId, out var result) || result is null ||
                string.IsNullOrWhiteSpace(result.Name) || result.Name.Length > 160 ||
                rule.OwnerId.Length > 160 || rule.Id.Length > 160 || options.Count >= 100)
                return null;
            var inputs = rule.Inputs.Select(input => new NativeOriginBiologicalInteractionInput(
                input.Count, genes.Where(g => input.Alternatives.Any(selector => MatchesInherited(selector, g)))
                    .Select(g => g.SourceCandidateId).OrderBy(id => id, StringComparer.Ordinal).ToArray())).ToArray();
            options.Add(new(rule.OwnerId, rule.Id, rule.ResultTraitId, result.Name, inputs));
        }
        return options.OrderBy(o => o.RuleOwnerId, StringComparer.Ordinal)
            .ThenBy(o => o.InteractionId, StringComparer.Ordinal).ToArray();
    }

    private static bool TrySelectInheritedInputs(Rule rule,
        IReadOnlyList<NativeOriginBiologicalGeneInstance> selected,
        out NativeOriginBiologicalGeneInstance[] ordered, bool exactSelection = true)
    {
        ordered = [];
        var slots = rule.Inputs.SelectMany(input =>
            Enumerable.Repeat(input.Alternatives, input.Count)).ToArray();
        if (exactSelection ? slots.Length != selected.Count : slots.Length > selected.Count) return false;
        var occupants = new Dictionary<string, int>(StringComparer.Ordinal);
        var genes = selected.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        bool Assign(int slot, HashSet<string> visited)
        {
            foreach (var gene in genes)
            {
                if (!slots[slot].Any(selector => MatchesInherited(selector, gene)) ||
                    !visited.Add(gene.Id)) continue;
                if (!occupants.TryGetValue(gene.Id, out var prior) ||
                    Assign(prior, visited))
                {
                    occupants[gene.Id] = slot;
                    return true;
                }
            }
            return false;
        }
        for (var slot = 0; slot < slots.Length; slot++)
            if (!Assign(slot, new HashSet<string>(StringComparer.Ordinal)))
                return false;
        ordered = genes.Where(x => occupants.ContainsKey(x.Id)).ToArray();
        return ordered.Length == slots.Length;
    }

    private static bool MatchesInherited(NativeOriginCharacteristicSelector selector,
        NativeOriginBiologicalGeneInstance gene)
    {
        if (selector.MechanicalDefinitionId is not null &&
                selector.MechanicalDefinitionId != gene.Identity.MechanicalDefinitionId ||
            selector.IntrinsicSlotId is not null &&
                selector.IntrinsicSlotId != gene.Identity.IntrinsicSlotId ||
            selector.SourceId is not null &&
                selector.SourceId != gene.Identity.Occurrence.Source.Id)
            return false;
        if (selector.ParameterKey is null) return true;
        if (gene.Parameters is null ||
            !gene.Parameters.TryGetValue(selector.ParameterKey, out var actual))
            return false;
        return selector.ExpectedValue is not { } expected ||
            JsonElement.DeepEquals(expected, actual);
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
                    "id" or "resultTraitId" or "inputs" or "inputEvidence" or
                    "name" or "description" or "reviewNotes")) ||
                item.TryGetProperty("inputEvidence", out var inputEvidence) &&
                (inputEvidence.ValueKind != JsonValueKind.String ||
                 inputEvidence.GetString() is not ("effective-grant" or "inherited-gene")) ||
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
                // A display-only entry is not a mechanical entitlement. Apply
                // the same minimum normalized-mechanic check used for source
                // traits during native candidate deconstruction.
                !effect.EnumerateObject().Any(p => p.Name is not (
                    "id" or "name" or "description" or "reviewNotes")) ||
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
            var inherited = item.TryGetProperty("inputEvidence", out var evidenceType) &&
                evidenceType.GetString() == "inherited-gene";
            if (inherited && !NativeOriginBiologicalTraitShape.IsRecognized(effect))
            {
                Invalid(ownerId, "An inherited expression result has an unsupported biological mechanic.");
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
                tableDecisionId, inherited));
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
