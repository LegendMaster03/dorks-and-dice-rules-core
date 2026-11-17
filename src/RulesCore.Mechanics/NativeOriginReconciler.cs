using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

internal sealed record NativeOriginSelectedGrant(
    NativeOriginCandidate Candidate, NativeOriginManifestation Manifestation,
    NativeOriginEffectiveGrant Grant);

/// <summary>
/// Reconciles actual authorized manifestations, not unselected candidate offers.
/// Repeated occurrences are not multiplied; other apparent equivalences need authored
/// evidence or a verified table disposition. Ordering never selects a conflict winner.
/// </summary>
internal sealed class NativeOriginReconciler(NativeCatalogSnapshot catalog)
{
    public (IReadOnlyList<NativeOriginEffectiveGrant> Grants,
        IReadOnlyList<NativeOriginContractIssue> Issues, bool UsedDm) Reconcile(
        NativeOriginProposal proposal, IReadOnlyList<NativeOriginSelectedGrant> selected,
        Func<string, string, NativeTableRuleDecision?> verified)
    {
        var issues = new List<NativeOriginContractIssue>();
        var active = selected.ToDictionary(x => x.Candidate.Id, StringComparer.Ordinal);
        var ignored = new HashSet<string>(StringComparer.Ordinal);
        var merged = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var replacements = new Dictionary<string, NativeOriginEffectiveGrant>(StringComparer.Ordinal);
        // Explicit, authorized coexistence is tracked per selected pair. It is
        // never inferred from a shared sense name or from donor ordering.
        var authorizedCoexistence = new HashSet<(string, string)>();
        var usedDm = false;

        foreach (var relation in (proposal.Relationships ?? [])
            .Where(relation => relation is not null)
            .OrderBy(relation => relation.Id, StringComparer.Ordinal))
        {
            if (relation?.CandidateIds is null) continue;
            var involved = relation.CandidateIds.Where(active.ContainsKey)
                .Distinct(StringComparer.Ordinal).ToArray();
            if (involved.Length < 2) continue;
            var resolution = proposal.Resolutions?.FirstOrDefault(x => x.RelationshipId == relation.Id);
            if (resolution is null)
            {
                Issue("relationship-unresolved", relation.Id, NativeOriginIssueSeverity.Unresolved,
                    "Selected candidates have an unresolved authored relationship.");
                foreach (var id in involved) ignored.Add(id);
                continue;
            }
            var authenticated = resolution.Basis?.Adjudication == NativeOriginAdjudication.DmAuthorized ?
                verified("relationship", relation.Id) : null;
            var authorized = authenticated is not null &&
                JsonString(authenticated.EffectiveOutcome, "action") == ActionName(resolution.Action);
            var authored = resolution.Basis?.Adjudication == NativeOriginAdjudication.Supported &&
                relation.AuthoredRuleReference == resolution.Basis.NativeRuleReference &&
                (resolution.Action == NativeOriginResolutionAction.Replace ?
                    NativeTraitCompositionAuthorizes(relation.AuthoredRuleReference,
                        involved, active, resolution) :
                    NativeRelationshipAuthorizes(relation.Id, resolution.Action,
                        relation.AuthoredRuleReference));
            if (!authorized && !authored)
            {
                Issue("relationship-disposition-unauthorized", relation.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    "The proposed relationship disposition lacks an executable authored rule or verified table decision.");
                foreach (var id in involved) ignored.Add(id);
                continue;
            }
            usedDm |= authorized;
            switch (resolution.Action)
            {
                case NativeOriginResolutionAction.RetainFirst:
                case NativeOriginResolutionAction.RetainSecond:
                    var index = resolution.Action == NativeOriginResolutionAction.RetainFirst ? 0 : 1;
                    if (involved.Length <= index) goto default;
                    foreach (var id in involved)
                        if (id != involved[index]) ignored.Add(id);
                    break;
                case NativeOriginResolutionAction.ConsolidateEvidence:
                    if (!involved.Skip(1).All(id => SameMechanic(active[involved[0]],
                            active[id]))) goto default;
                    foreach (var id in involved.Skip(1))
                    {
                        ignored.Add(id);
                        if (!merged.TryGetValue(involved[0], out var evidence))
                            merged[involved[0]] = evidence = new(StringComparer.Ordinal);
                        evidence.Add(id);
                    }
                    break;
                case NativeOriginResolutionAction.RetainBoth:
                    for (var i = 0; i < involved.Length; i++)
                    for (var j = i + 1; j < involved.Length; j++)
                        authorizedCoexistence.Add(OrderedPair(involved[i], involved[j]));
                    break;
                case NativeOriginResolutionAction.Replace:
                    // Until the native catalog defines an executable replacement
                    // grammar, require an exact-scoped table decision for both the
                    // selected source and every final parameter/source binding.
                    // Do not infer recombination from similar trait names.
                    if (!TryComposeReplacement(involved, active, resolution,
                            authorized ? authenticated : null,
                            authored ? relation.AuthoredRuleReference : null,
                            out var replacement))
                        goto default;
                    var winner = resolution.ReplacementCandidateId!;
                    replacements[winner] = replacement;
                    if (!merged.TryGetValue(winner, out var sources))
                        merged[winner] = sources = new(StringComparer.Ordinal);
                    foreach (var id in involved.Where(id => id != winner))
                    {
                        ignored.Add(id);
                        sources.Add(id);
                    }
                    break;
                default:
                    Issue("relationship-action-unresolved", relation.Id, NativeOriginIssueSeverity.Unresolved,
                        "The disposition lacks a verified and executable result.");
                    foreach (var id in involved) ignored.Add(id);
                    break;
            }
        }

        // One identical authored occurrence can appear through multiple contributors.
        // Its already selected mechanic is one grant with many source-offer references.
        foreach (var group in active.Values.Where(x => !ignored.Contains(x.Candidate.Id))
            .GroupBy(x => x.Candidate.Identity))
        {
            var entries = group.OrderBy(x => x.Candidate.Id, StringComparer.Ordinal).ToArray();
            if (entries.Length < 2) continue;
            if (!entries.Skip(1).All(x => SameMechanic(entries[0], x)))
            {
                foreach (var x in entries) ignored.Add(x.Candidate.Id);
                Issue("same-definition-different-manifestations", entries[0].Candidate.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    "The same authored occurrence has incompatible values or selected choices.");
                continue;
            }
            foreach (var entry in entries.Skip(1))
            {
                ignored.Add(entry.Candidate.Id);
                if (!merged.TryGetValue(entries[0].Candidate.Id, out var evidence))
                    merged[entries[0].Candidate.Id] = evidence = new(StringComparer.Ordinal);
                evidence.Add(entry.Candidate.Id);
            }
        }

        // Even an explicitly shared canonical mechanic does not license duplicate
        // effective benefits from different authored occurrences. Require a real
        // relationship policy instead of treating shared identity as stacking.
        foreach (var group in active.Values.Where(x => !ignored.Contains(x.Candidate.Id) &&
            x.Candidate.Identity.MechanicalDefinitionId is not null)
            .GroupBy(x => x.Candidate.Identity.MechanicalDefinitionId!, StringComparer.Ordinal))
        {
            var entries = group.ToArray();
            if (entries.Select(x => x.Candidate.Identity.Occurrence)
                .Distinct().Count() < 2) continue;
            foreach (var x in entries) ignored.Add(x.Candidate.Id);
            Issue("shared-definition-reconciliation-required", group.Key,
                NativeOriginIssueSeverity.Unresolved,
                "Shared canonical mechanics from distinct occurrences require an authored disposition.");
        }

        // Two distinct complete traits can offer the same typed sense with
        // different activation, range, terrain or resource rules. Do not grant
        // both simply because their canonical definition IDs differ, and do
        // not construct an unapproved hybrid from their shared display name.
        // A scoped resolution may retain either one or explicitly retain both.
        foreach (var group in active.Values
            .Where(x => !ignored.Contains(x.Candidate.Id))
            .Select(x => (Entry: x, Sense: NativeSense(x.Candidate)))
            .Where(x => x.Sense is not null)
            .GroupBy(x => x.Sense!, StringComparer.Ordinal))
        {
            var entries = group.Select(x => x.Entry)
                .OrderBy(x => x.Candidate.Id, StringComparer.Ordinal).ToArray();
            if (entries.Length < 2) continue;
            var conflicting = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            for (var j = i + 1; j < entries.Length; j++)
            {
                var a = entries[i].Candidate;
                var b = entries[j].Candidate;
                if (a.Identity.MechanicalDefinitionId ==
                    b.Identity.MechanicalDefinitionId) continue;
                if (authorizedCoexistence.Contains(OrderedPair(a.Id, b.Id)))
                    continue;
                conflicting.Add(a.Id);
                conflicting.Add(b.Id);
            }
            if (conflicting.Count == 0) continue;
            foreach (var id in conflicting) ignored.Add(id);
            Issue("same-sense-reconciliation-required", group.Key,
                NativeOriginIssueSeverity.Unresolved,
                "Selected distinct canonical senses overlap. Explicitly retain one " +
                "complete mechanic or authorize the coexistence of their different modes.");
        }

        // Permanent resistance expressed by distinct complete traits is
        // similarly source-specific. Compare the *typed damage key*, not the
        // word "resistance"; do not infer percentage stacking or silently
        // separate a Dwarf's linked poison-saving-throw benefit.
        // Event-scoped/activated defense is deliberately not a permanent
        // resistance contender.
        foreach (var group in active.Values
            .Where(x => !ignored.Contains(x.Candidate.Id))
            .SelectMany(x => NativeResistanceKeys(x).Select(damageType =>
                (Entry: x, DamageType: damageType)))
            .GroupBy(x => x.DamageType, StringComparer.Ordinal))
        {
            var entries = group.Select(x => x.Entry)
                .OrderBy(x => x.Candidate.Id, StringComparer.Ordinal).ToArray();
            if (entries.Length < 2) continue;
            var conflicting = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            for (var j = i + 1; j < entries.Length; j++)
            {
                var a = entries[i].Candidate;
                var b = entries[j].Candidate;
                if (a.Identity.MechanicalDefinitionId ==
                    b.Identity.MechanicalDefinitionId) continue;
                if (authorizedCoexistence.Contains(OrderedPair(a.Id, b.Id)))
                    continue;
                conflicting.Add(a.Id);
                conflicting.Add(b.Id);
            }
            if (conflicting.Count == 0) continue;
            foreach (var id in conflicting) ignored.Add(id);
            Issue("same-damage-resistance-reconciliation-required", group.Key,
                NativeOriginIssueSeverity.Unresolved,
                "Selected distinct inherent resistances overlap for this damage type. " +
                "Retain a complete source mechanic or explicitly authorize coexistence.");
        }

        // Native movement modes can appear in separate canonical traits with
        // different terrain limits, speeds, or other conditions. Identical
        // mode names do not authorize stacking or selecting the fastest speed.
        // Preserve complete authored traits and require a verified disposition
        // for any selected, distinct definitions of the same movement mode.
        foreach (var group in active.Values
            .Where(x => !ignored.Contains(x.Candidate.Id))
            .Select(x => (Entry: x, Mode: NativeMovementMode(x.Candidate)))
            .Where(x => x.Mode is not null)
            .GroupBy(x => x.Mode!, StringComparer.Ordinal))
        {
            var entries = group.Select(x => x.Entry)
                .OrderBy(x => x.Candidate.Id, StringComparer.Ordinal).ToArray();
            if (entries.Length < 2) continue;
            var conflicting = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            for (var j = i + 1; j < entries.Length; j++)
            {
                var a = entries[i].Candidate;
                var b = entries[j].Candidate;
                if (a.Identity.MechanicalDefinitionId ==
                    b.Identity.MechanicalDefinitionId ||
                    authorizedCoexistence.Contains(OrderedPair(a.Id, b.Id)))
                    continue;
                conflicting.Add(a.Id);
                conflicting.Add(b.Id);
            }
            if (conflicting.Count == 0) continue;
            foreach (var id in conflicting) ignored.Add(id);
            Issue("same-movement-mode-reconciliation-required", group.Key,
                NativeOriginIssueSeverity.Unresolved,
                "Selected distinct native movement traits provide the same mode. " +
                "Retain a complete authored trait or explicitly authorize coexistence; " +
                "do not infer speed or terrain combinations.");
        }

        // Two distinct canonical, passive traits can reference the SAME
        // executable native rule (Elf and Tiefling Low-Light Vision currently do).
        // Their different definitions do not represent two independently
        // stackable benefits. They may be retained by a scoped explicit
        // disposition, but no dominant source or automatic union is inferred.
        foreach (var group in active.Values
            .Where(x => !ignored.Contains(x.Candidate.Id))
            .Select(x => (Entry: x, Rule: NativePassiveRule(x.Candidate)))
            .Where(x => x.Rule is not null)
            .GroupBy(x => x.Rule!, StringComparer.Ordinal))
        {
            var entries = group.Select(x => x.Entry)
                .OrderBy(x => x.Candidate.Id, StringComparer.Ordinal).ToArray();
            var conflicting = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            for (var j = i + 1; j < entries.Length; j++)
            {
                var a = entries[i].Candidate;
                var b = entries[j].Candidate;
                if (a.Identity.MechanicalDefinitionId == b.Identity.MechanicalDefinitionId ||
                    authorizedCoexistence.Contains(OrderedPair(a.Id, b.Id)))
                    continue;
                conflicting.Add(a.Id);
                conflicting.Add(b.Id);
            }
            if (conflicting.Count == 0) continue;
            foreach (var id in conflicting) ignored.Add(id);
            Issue("same-passive-native-rule-reconciliation-required", group.Key,
                NativeOriginIssueSeverity.Unresolved,
                "Selected distinct traits reference the same passive native effect. " +
                "Explicitly retain a complete authored trait or authorize coexistence.");
        }

        // Different occurrences offering the same intrinsic slot have no general
        // first-party winner, even where values happen to coincide.
        foreach (var group in active.Values.Where(x => !ignored.Contains(x.Candidate.Id) &&
            x.Candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
            x.Candidate.Identity.IntrinsicSlotId is not null)
            .GroupBy(x => x.Candidate.Identity.IntrinsicSlotId!, StringComparer.Ordinal))
        {
            var entries = group.ToArray();
            if (entries.Length < 2) continue;
            foreach (var x in entries) ignored.Add(x.Candidate.Id);
            Issue("intrinsic-slot-interaction-unresolved", group.Key,
                NativeOriginIssueSeverity.Unresolved,
                "Different source occurrences cannot choose or combine an intrinsic value without a rule.");
        }

        var grants = active.Values.Where(x => !ignored.Contains(x.Candidate.Id))
            .OrderBy(x => x.Candidate.Id, StringComparer.Ordinal)
            .Select(x =>
            {
                var evidence = new SortedSet<string>(StringComparer.Ordinal) { x.Candidate.Id };
                if (merged.TryGetValue(x.Candidate.Id, out var additional))
                    evidence.UnionWith(additional);
                var effective = replacements.TryGetValue(x.Candidate.Id, out var replaced) ?
                    replaced : x.Grant;
                // A consolidated grant can have multiple verified mixed parents.
                // The losing grants do not create mechanical duplicates, but
                // every winning contributor's immediate lineage is retained.
                var lineage = evidence.SelectMany(id =>
                        active[id].Grant.InheritedFrom ?? [])
                    .OrderBy(x => x.ContributorId, StringComparer.Ordinal)
                    .ThenBy(x => x.ContributorProfileId, StringComparer.Ordinal)
                    .ThenBy(x => x.SourceGrantId, StringComparer.Ordinal).ToArray();
                return effective with
                {
                    SourceCandidateIds = evidence.ToArray(),
                    InheritedFrom = lineage.Length == 0 ? null : lineage
                };
            }).ToArray();
        return (grants, issues, usedDm);

        void Issue(string code, string path, NativeOriginIssueSeverity level, string detail) =>
            issues.Add(new(code, path, level, detail));
    }

    /// <summary>
    /// Recombine one complete native trait with parameter values actually offered by
    /// selected contributors. The player nominates the source of each field; the
    /// authenticated table outcome must match it exactly. This is not automatic
    /// inheritance eligibility, an allocation decision or arbitrary trait merging.
    /// </summary>
    private static bool TryComposeReplacement(
        IReadOnlyList<string> involved,
        IReadOnlyDictionary<string, NativeOriginSelectedGrant> active,
        NativeOriginResolution resolution, NativeTableRuleDecision? decision,
        string? authoredRule, out NativeOriginEffectiveGrant result)
    {
        result = null!;
        var winner = resolution.ReplacementCandidateId;
        var values = resolution.ReplacementParameters;
        var sources = resolution.ReplacementParameterSources;
        if (winner is null || !involved.Contains(winner, StringComparer.Ordinal) ||
            values is null || sources is null || values.Count == 0 ||
            (decision is null && authoredRule is null))
            return false;
        var authorizedValues = default(JsonElement);
        var authorizedSources = default(JsonElement);
        if (decision is not null)
        {
            var outcome = decision.EffectiveOutcome;
            if (outcome.ValueKind != JsonValueKind.Object ||
                JsonString(outcome, "resultCandidateId") != winner ||
                !outcome.TryGetProperty("parameters", out authorizedValues) ||
                authorizedValues.ValueKind != JsonValueKind.Object ||
                !outcome.TryGetProperty("parameterSources", out authorizedSources) ||
                authorizedSources.ValueKind != JsonValueKind.Object)
                return false;
        }

        var root = active[winner];
        if (root.Candidate.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
            root.Candidate.Identity.MechanicalDefinitionId is null ||
            root.Grant.EffectiveValue is null)
            return false;

        // A replacement is one configured instance of the *same complete mechanic*.
        // No cross-definition, cross-choice or intrinsic blending is authorized.
        // Every simultaneously selected occurrence of that exact identity must
        // participate, so no extra copy remains to double a resource pool.
        if (active.Values.Any(x => x.Candidate.Identity == root.Candidate.Identity &&
            !involved.Contains(x.Candidate.Id, StringComparer.Ordinal)))
            return false;
        var entries = involved.Select(id => active[id]).ToArray();
        if (entries.Any(x =>
            x.Candidate.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
            x.Candidate.Identity.MechanicalDefinitionId != root.Candidate.Identity.MechanicalDefinitionId ||
            x.Grant.EffectiveValue is null ||
            !JsonElement.DeepEquals(x.Grant.EffectiveValue.Value, root.Grant.EffectiveValue.Value) ||
            (x.Grant.ResolvedChoices?.Count ?? 0) > 0 ||
            x.Grant.Parameters is null))
            return false;
        var keys = root.Grant.Parameters?.Keys.ToHashSet(StringComparer.Ordinal);
        if (keys is null || keys.Count == 0 ||
            entries.Any(x => !keys.SetEquals(x.Grant.Parameters!.Keys)) ||
            !keys.SetEquals(values.Keys) || !keys.SetEquals(sources.Keys) ||
            (decision is not null &&
                (authorizedValues.EnumerateObject().Count() != keys.Count ||
                 authorizedSources.EnumerateObject().Count() != keys.Count)))
            return false;

        foreach (var key in keys)
        {
            var from = sources[key];
            if (!involved.Contains(from, StringComparer.Ordinal) ||
                !JsonElement.DeepEquals(values[key], active[from].Grant.Parameters![key]))
                return false;
        }

        if (decision is not null)
        {
            foreach (var key in keys)
                if (!authorizedValues.TryGetProperty(key, out var permitted) ||
                    !JsonElement.DeepEquals(values[key], permitted) ||
                    JsonString(authorizedSources, key) != sources[key])
                    return false;
        }

        result = root.Grant with
        {
            Parameters = values.ToDictionary(x => x.Key, x => x.Value.Clone(),
                StringComparer.Ordinal),
            ParameterSources = sources.ToDictionary(x => x.Key, x => x.Value,
                StringComparer.Ordinal),
            Authorization = decision is null ?
                new(NativeOriginAdjudication.Supported, NativeRuleReference: authoredRule) :
                new(NativeOriginAdjudication.DmAuthorized, TableDecisionId: decision.Id)
        };
        return true;
    }

    /// <summary>
    /// A native trait definition may explicitly permit particular parameters to
    /// be independently chosen from otherwise identical configured offers. This
    /// authoring is on the canonical mechanic, not on a named Species pairing.
    /// All fields, including non-recombinable ones, are still provenance-checked
    /// by TryComposeReplacement. Source eligibility is checked elsewhere.
    /// </summary>
    private bool NativeTraitCompositionAuthorizes(
        string? definitionId, IReadOnlyList<string> involved,
        IReadOnlyDictionary<string, NativeOriginSelectedGrant> active,
        NativeOriginResolution resolution)
    {
        var winner = resolution.ReplacementCandidateId;
        var sources = resolution.ReplacementParameterSources;
        if (definitionId is null || winner is null || sources is null ||
            !involved.Contains(winner, StringComparer.Ordinal) ||
            !catalog.TryGetById(definitionId, out var entity) || entity is null ||
            entity.Category != "origin-trait" ||
            !entity.Definition.TryGetProperty("originComposition", out var composition) ||
            composition.ValueKind != JsonValueKind.Object ||
            !composition.TryGetProperty("recombinableParameters", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Array ||
            involved.Any(id => active[id].Candidate.Identity.MechanicalDefinitionId != definitionId))
            return false;
        var allowed = parameters.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        // No unrelated field may be taken from a different contributor,
        // even if both variants happen to have the same JSON value.
        return allowed.Count != 0 && sources.All(x =>
            x.Value == winner || allowed.Contains(x.Key));
    }

    /// <summary>
    /// A bare native rule pointer is only sufficient for passive traits whose
    /// other authored fields are presentation. Do not infer equivalence across
    /// arbitrary spell, resource, resistance or conditional trait definitions.
    /// </summary>
    private string? NativePassiveRule(NativeOriginCandidate candidate)
    {
        if (candidate.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
            candidate.Identity.MechanicalDefinitionId is not { } id ||
            !catalog.TryGetById(id, out var canonical) ||
            canonical is null || canonical.Category != "origin-trait" ||
            !canonical.Definition.TryGetProperty("trait", out var trait) ||
            trait.ValueKind != JsonValueKind.Object ||
            !trait.EnumerateObject().All(x =>
                x.Name is "id" or "name" or "description" or "rule") ||
            JsonString(trait, "rule") is not { } ruleId ||
            !catalog.TryGetById(ruleId, out var rule) ||
            rule is null || rule.Category != "rule")
            return null;
        return ruleId;
    }

    /// <summary>
    /// A native typed sense is overlap evidence, not a stacking or superiority
    /// policy. These checks read canonical trait definitions, not proposal
    /// annotations, localized labels or untrusted candidate names.
    /// </summary>
    private string? NativeSense(NativeOriginCandidate candidate)
    {
        if (candidate.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
            candidate.Identity.MechanicalDefinitionId is not { } id ||
            !catalog.TryGetById(id, out var definition) ||
            definition is null || definition.Category != "origin-trait" ||
            !definition.Definition.TryGetProperty("trait", out var trait) ||
            trait.ValueKind != JsonValueKind.Object ||
            !trait.TryGetProperty("sense", out var sense) ||
            sense.ValueKind != JsonValueKind.String)
            return null;
        return sense.GetString();
    }

    /// <summary>
    /// Native movement-mode overlap uses only canonical authoring, never a
    /// submitted trait label, candidate description, or selected speed.
    /// Different modes stay independent; a same-mode collision is not a
    /// movement-speed stacking policy or a first-party inheritance grant.
    /// </summary>
    private string? NativeMovementMode(NativeOriginCandidate candidate)
    {
        if (candidate.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
            candidate.Identity.MechanicalDefinitionId is not { } id ||
            !catalog.TryGetById(id, out var definition) ||
            definition is null || definition.Category != "origin-trait" ||
            !definition.Definition.TryGetProperty("trait", out var trait) ||
            trait.ValueKind != JsonValueKind.Object ||
            !trait.TryGetProperty("movementMode", out var mode) ||
            mode.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(mode.GetString()) ||
            !trait.TryGetProperty("speedFeet", out var speed) ||
            speed.ValueKind != JsonValueKind.Number ||
            !speed.TryGetInt32(out var feet) || feet <= 0)
            return null;
        return mode.GetString();
    }

    /// <summary>
    /// Identify durable type-specific resistance by its executable canonical
    /// shape. Keep event-scoped defenses out of this overlap classification:
    /// Cloudy Escape's reaction resistance is not an inherited permanent pool.
    /// Direct "poison": 50 legacy maps and authored percent/damageType records
    /// are both recognized. Source-bound Dragonborn type is read only from an
    /// already authorized, actually manifested parameter.
    /// </summary>
    private IReadOnlyList<string> NativeResistanceKeys(NativeOriginSelectedGrant selected)
    {
        if (selected.Candidate.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
            selected.Candidate.Identity.MechanicalDefinitionId is not { } id ||
            !catalog.TryGetById(id, out var entity) ||
            entity is null || entity.Category != "origin-trait" ||
            !entity.Definition.TryGetProperty("trait", out var authored) ||
            authored.ValueKind != JsonValueKind.Object ||
            authored.TryGetProperty("activation", out _) ||
            authored.TryGetProperty("duration", out _) ||
            !authored.TryGetProperty("damageResistance", out var resistance) ||
            resistance.ValueKind != JsonValueKind.Object ||
            resistance.TryGetProperty("scope", out _) ||
            resistance.TryGetProperty("duration", out _) ||
            selected.Grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } effective ||
            !effective.TryGetProperty("damageResistance", out var effectiveResistance) ||
            !JsonElement.DeepEquals(resistance, effectiveResistance))
            return [];

        if (resistance.TryGetProperty("percent", out var percent))
        {
            if (percent.ValueKind != JsonValueKind.Number ||
                !percent.TryGetInt32(out var amount) || amount is <= 0 or > 100)
                return [];
            if (resistance.TryGetProperty("damageType", out var damageType) &&
                damageType.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(damageType.GetString()))
                return [damageType.GetString()!];
            if (resistance.TryGetProperty("damageTypeFrom", out var sourceField) &&
                sourceField.ValueKind == JsonValueKind.String &&
                selected.Grant.Parameters?.TryGetValue("damageType", out var bound) == true &&
                bound.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(bound.GetString()) &&
                authored.TryGetProperty("originBindings", out var bindings) &&
                bindings.ValueKind == JsonValueKind.Object &&
                bindings.TryGetProperty("damageType", out var binding) &&
                binding.ValueKind == JsonValueKind.Object &&
                JsonString(binding, "lookup") == "leaf" &&
                JsonString(binding, "sourceField") == "damageType")
                return [bound.GetString()!];
            return [];
        }

        // This older native record shape is a typed damage map, not an
        // percentage object. Ignore partially specified or mixed value maps.
        var entries = resistance.EnumerateObject().ToArray();
        if (entries.Length == 0 || entries.Any(p =>
            string.IsNullOrWhiteSpace(p.Name) ||
            p.Value.ValueKind != JsonValueKind.Number ||
            !p.Value.TryGetInt32(out var amount) || amount is <= 0 or > 100))
            return [];
        return entries.Select(p => p.Name).ToArray();
    }

    private static (string, string) OrderedPair(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    private bool NativeRelationshipAuthorizes(string id, NativeOriginResolutionAction action,
        string? ruleId)
    {
        // Catalog relationships currently name actions, not replacement values
        // or parameter-source bindings. A bare `replace` label grants nothing.
        if (action == NativeOriginResolutionAction.Replace) return false;
        if (ruleId is null || !catalog.TryGetById(ruleId, out var rule) || rule is null ||
            rule.Category != "rule" ||
            !rule.Definition.TryGetProperty("originRelationships", out var items) ||
            items.ValueKind != JsonValueKind.Array) return false;
        return items.EnumerateArray().Any(item =>
            JsonString(item, "id") == id &&
            JsonString(item, "action") == ActionName(action));
    }

    private static bool SameMechanic(NativeOriginSelectedGrant a, NativeOriginSelectedGrant b)
    {
        if (a.Candidate.Identity != b.Candidate.Identity ||
            (a.Grant.EffectiveValue?.GetRawText() ?? "") !=
            (b.Grant.EffectiveValue?.GetRawText() ?? "")) return false;
        static string Parameters(IReadOnlyDictionary<string, JsonElement>? source) =>
            string.Join("|", (source ?? new Dictionary<string, JsonElement>())
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => x.Key + "=" + x.Value.GetRawText()));
        static string Choices(IReadOnlyList<NativeOriginChoiceSelection>? source) =>
            string.Join("|", (source ?? []).OrderBy(x => x.RequirementId, StringComparer.Ordinal)
                .Select(x => x.RequirementId + "=" +
                    string.Join(",", x.SelectedOptionIds.OrderBy(y => y, StringComparer.Ordinal))));
        return Parameters(a.Grant.Parameters) == Parameters(b.Grant.Parameters) &&
            Choices(a.Grant.ResolvedChoices) == Choices(b.Grant.ResolvedChoices);
    }

    private static string ActionName(NativeOriginResolutionAction action) => action switch
    {
        NativeOriginResolutionAction.RetainFirst => "retain-first",
        NativeOriginResolutionAction.RetainSecond => "retain-second",
        NativeOriginResolutionAction.ConsolidateEvidence => "consolidate-evidence",
        NativeOriginResolutionAction.RetainBoth => "retain-both",
        NativeOriginResolutionAction.Replace => "replace",
        _ => "defer"
    };

    private static string? JsonString(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String ? property.GetString() : null;
}
