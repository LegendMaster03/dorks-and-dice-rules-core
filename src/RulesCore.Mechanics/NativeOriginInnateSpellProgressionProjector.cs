using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Projects already-selected native Origin spells at a character level without
/// granting any new spell, allocating slots, or spending a per-rest use pool.
/// Each entry retains its original grant/source so identical spell names from
/// different Origins do not silently combine their free-use allowances.
/// </summary>
public static class NativeOriginInnateSpellProgressionProjector
{
    public sealed record Spell(
        string GrantId, string TraitId, string SourceId, string SpellId,
        string Availability, int UnlockCharacterLevel,
        string? SpellcastingAbility, bool AtWill,
        bool SlotCasting, int? FreeUsesPerRecharge, string? FreeUsesFormula,
        string? Recharge, int? FreeCastSpellLevel);

    public sealed record View(
        string Status, int? CharacterLevel, IReadOnlyList<Spell> Spells,
        IReadOnlyList<string> UnresolvedReasons, bool PreviewOnly = true);

    /// <summary>
    /// The supplied level is a preview input, not authenticated advancement.
    /// Locked future spells remain in the result to preserve the entire course.
    /// </summary>
    public static View Project(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult origin, int? characterLevel)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(origin);
        if (origin.Status != NativeOriginConstructionStatus.Authorized ||
            origin.Profile is not { } profile || origin.Issues.Count != 0 ||
            profile.IndependentGrants.Count != 0 ||
            (profile.PersistentInfluences?.Count ?? 0) != 0 ||
            profile.ActiveTemporaryTransformation is not null ||
            (profile.OtherActiveTemporaryTransformations?.Count ?? 0) != 0 ||
            !NativeOriginContractValidator.SameConfiguration(
                profile.Current, profile.Underlying))
            return Unresolved("origin-not-authorized-current-baseline");

        var mixed = origin.EvaluatedProposal.Intent == NativeOriginIntent.Mixed;
        if (mixed)
        {
            if (NativeOriginApprovedMixedIntrinsicProjector.Project(origin).Status !=
                "resolved")
                return Unresolved("mixed-foundation-unverified");
        }
        else if (origin.EvaluatedProposal.Intent != NativeOriginIntent.Direct)
            return Unresolved("unsupported-origin-intent");
        if (characterLevel is null)
            return Unresolved("character-level-required");
        if (characterLevel is <= 0 or > 1000)
            return Unresolved("invalid-character-level");

        var spells = new List<Spell>();
        var reasons = new List<string>();
        // Build a canonical index as well as examining saved effective values.
        // A forged profile cannot remove its spell property to evade validation.
        foreach (var grant in profile.Underlying.InherentGrants)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait)
                continue;
            var id = grant.Identity.MechanicalDefinitionId;
            NativeRuleEntity? definition = null;
            if (id is not null)
                catalog.TryGetById(id, out definition);
            var hasCanonical = definition is not null &&
                definition.Category == "origin-trait" &&
                definition.Definition.TryGetProperty("trait", out var canonical) &&
                canonical.ValueKind == JsonValueKind.Object &&
                (canonical.TryGetProperty("spell", out _) ||
                 canonical.TryGetProperty("spellList", out _) ||
                 canonical.TryGetProperty("spellChoices", out _));
            var hasEffective = grant.EffectiveValue is { ValueKind: JsonValueKind.Object } value &&
                (value.TryGetProperty("spell", out _) ||
                 value.TryGetProperty("spellList", out _) ||
                 value.TryGetProperty("spellChoices", out _));
            if (!hasCanonical && !hasEffective) continue;
            if (!hasCanonical || id is null ||
                grant.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
                !catalog.TryGetById(grant.Identity.Occurrence.Source.Id, out var source) ||
                source is null || source.Category is not ("species" or "ancestry") ||
                definition is null ||
                !definition.Definition.TryGetProperty("trait", out var authored) ||
                grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } current ||
                !JsonElement.DeepEquals(authored, current) ||
                !VerifiedSelectedGrant(origin, grant, mixed))
            {
                reasons.Add("spell-grant-not-verified:" + grant.Id);
                continue;
            }

            // Finite authored spell choices are fixed at creation. Open spell
            // lists retain their separate catalog-eligibility/replacement gate.
            var fixedSpell = Text(authored, "spell", out var spellId);
            if (authored.TryGetProperty("spellChoices", out var menu))
            {
                if (fixedSpell || authored.TryGetProperty("spellList", out _) ||
                    !VerifiedFiniteSpellChoice(origin, grant, menu, out spellId))
                {
                    reasons.Add("spell-choice-unverified:" + grant.Id);
                    continue;
                }
            }
            else if (!fixedSpell)
            {
                reasons.Add("spell-list-projection-deferred:" + grant.Id);
                continue;
            }
            else if (authored.TryGetProperty("spellList", out _))
            {
                reasons.Add("spell-definition-ambiguous:" + grant.Id);
                continue;
            }
            if (NativeOriginUnlockLevel.Read(authored) is not int unlock)
            {
                reasons.Add("spell-level-invalid:" + grant.Id);
                continue;
            }
            var ability = default(string);
            if (authored.TryGetProperty("originBindings", out var bindings))
            {
                if (bindings.ValueKind != JsonValueKind.Object ||
                    bindings.EnumerateObject().Count() != 1 ||
                    !bindings.TryGetProperty("spellcastingAbility", out var binding) ||
                    TextValue(binding, "lookup") != "manifested-intrinsic" ||
                    TextValue(binding, "intrinsicSlot") != "spellcastingAbility" ||
                    TextValue(binding, "scope") is not ("source" or "descendant") ||
                    grant.Parameters?.TryGetValue("spellcastingAbility", out var choice) != true ||
                    choice.ValueKind != JsonValueKind.String ||
                    (ability = choice.GetString()) is not
                        ("strength" or "dexterity" or "constitution" or
                         "intelligence" or "wisdom" or "charisma"))
                {
                    reasons.Add("spell-ability-unverified:" + grant.Id);
                    continue;
                }
            }

            var atWill = True(authored, "atWill");
            var slots = True(authored, "slotCasting");
            if (authored.TryGetProperty("atWill", out var atWillField) &&
                atWillField.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                authored.TryGetProperty("slotCasting", out var slotField) &&
                slotField.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                reasons.Add("spell-casting-modes-invalid:" + grant.Id);
                continue;
            }
            int? freeUses = null;
            string? formula = null;
            if (authored.TryGetProperty("freeUses", out var use))
            {
                if (use.ValueKind == JsonValueKind.Number &&
                    use.TryGetInt32(out var count) && count is > 0 and <= 1000000)
                    freeUses = count;
                else if (use.ValueKind == JsonValueKind.String &&
                    use.GetString() == "proficiency-bonus")
                    formula = use.GetString();
                else
                {
                    reasons.Add("spell-free-use-formula-unsupported:" + grant.Id);
                    continue;
                }
            }
            string? recharge = null;
            if (authored.TryGetProperty("recharge", out var recovery))
            {
                recharge = recovery.ValueKind == JsonValueKind.String
                    ? recovery.GetString() : null;
                if (recharge is not ("long-rest" or "short-or-long-rest"))
                {
                    reasons.Add("spell-recharge-unsupported:" + grant.Id);
                    continue;
                }
            }
            if (freeUses is not null || formula is not null)
            {
                if (recharge is null || atWill)
                {
                    reasons.Add("spell-free-use-recharge-invalid:" + grant.Id);
                    continue;
                }
            }
            else if (!atWill && !slots)
            {
                reasons.Add("spell-casting-resource-unresolved:" + grant.Id);
                continue;
            }
            int? freeLevel = null;
            if (authored.TryGetProperty("freeCastSpellLevel", out var castLevel))
            {
                if (!castLevel.TryGetInt32(out var levelValue) ||
                    levelValue is < 1 or > 30 || freeUses is null)
                {
                    reasons.Add("spell-free-cast-level-invalid:" + grant.Id);
                    continue;
                }
                freeLevel = levelValue;
            }
            spells.Add(new(grant.Id, id, grant.Identity.Occurrence.Source.Id,
                spellId!, characterLevel >= unlock ? "available" : "locked",
                unlock, ability, atWill, slots,
                freeUses, formula, recharge, freeLevel));
        }

        // Validate derived intrinsic parameters from the actual construction
        // proposal, not from a saved field alone. A valid serialized result is
        // still just a preview; production source authentication is separate.
        if (spells.Count > 0)
        {
            var review = new NativeOriginContractValidator(catalog)
                .ReviewProfile(profile, origin.EvaluatedProposal);
            foreach (var issue in review.Issues.Where(x =>
                x.Code == "profile-intrinsic-binding-invalid"))
                reasons.Add(issue.Code + ":" + issue.Path);
        }

        // Two different source grants providing the same spell must not
        // silently share a new resource pool or stack free casts.
        foreach (var pair in spells.GroupBy(x => x.SpellId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1))
            reasons.Add("spell-overlap-reconciliation-required:" + pair.Key);

        if (reasons.Count > 0)
            return new("unresolved", characterLevel, spells, reasons.Distinct().ToArray());
        return new(spells.Count == 0 ? "none" : "resolved", characterLevel,
            spells.OrderBy(x => x.UnlockCharacterLevel)
                .ThenBy(x => x.GrantId, StringComparer.Ordinal).ToArray(), []);
    }

    /// <summary>
    /// Resolve total level and any symbolic proficiency-bonus use count from
    /// the existing native character kernel. No current/spent use counters
    /// or spell-slot grants are inferred from this projection.
    /// </summary>
    public static View ForCharacter(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult origin, NativeCharacterProjection character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (character.RulesetId != catalog.RulesetId ||
            character.Fingerprint != catalog.Fingerprint ||
            !character.Values.TryGetValue("advancement.total-level", out var level) ||
            level.Status != "resolved" || level.Value is not > 0)
            return Unresolved("character-advancement-unresolved");

        var projected = Project(catalog, origin, level.Value);
        if (projected.Status is not ("resolved" or "none"))
            return projected;
        if (!projected.Spells.Any(x => x.FreeUsesFormula is not null))
            return projected;
        if (!character.Values.TryGetValue("proficiency.bonus", out var proficiency) ||
            proficiency.Status != "resolved" ||
            proficiency.Value is null or <= 0 or > 1000000)
            return projected with { Status = "unresolved",
                UnresolvedReasons = ["character-proficiency-unresolved"] };
        return projected with
        {
            Spells = projected.Spells.Select(x => x.FreeUsesFormula ==
                "proficiency-bonus"
                ? x with { FreeUsesPerRecharge = proficiency.Value }
                : x).ToArray()
        };
    }

    private static bool VerifiedSelectedGrant(NativeOriginConstructionResult origin,
        NativeOriginEffectiveGrant grant, bool mixed)
    {
        if (mixed)
            return NativeOriginApprovedMixedIntrinsicProjector
                .HasVerifiedContributingEvidence(origin, grant);
        if (grant.Authorization?.Adjudication != NativeOriginAdjudication.Supported ||
            grant.Authorization.NativeRuleReference != grant.Identity.Occurrence.Source.Id ||
            grant.SourceCandidateIds is not { Count: 1 })
            return false;
        var candidateId = grant.SourceCandidateIds[0];
        return origin.EvaluatedProposal.Candidates.Any(x =>
            x.Id == candidateId && x.ContributorId is null &&
            x.Identity == grant.Identity &&
            x.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
            x.OfferedValue is { } offered &&
            JsonElement.DeepEquals(offered, grant.EffectiveValue!.Value) &&
            origin.EvaluatedProposal.Manifestations.Count(m =>
                m.CandidateId == candidateId) == 1);
    }

    private static bool VerifiedFiniteSpellChoice(NativeOriginConstructionResult origin,
        NativeOriginEffectiveGrant grant, JsonElement menu, out string? spellId)
    {
        spellId = null;
        if (menu.ValueKind != JsonValueKind.Array || menu.GetArrayLength() == 0 ||
            menu.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(x.GetString())))
            return false;
        var options = menu.EnumerateArray().Select(x => x.GetString()!).ToArray();
        if (options.Distinct(StringComparer.Ordinal).Count() != options.Length ||
            grant.ResolvedChoices is not { Count: 1 } choices ||
            choices[0] is null || choices[0].RequirementId != "spell" ||
            choices[0].SelectedOptionIds is not { Count: 1 } selected ||
            !options.Contains(selected[0], StringComparer.Ordinal))
            return false;
        var chosen = selected[0];
        foreach (var candidateId in grant.SourceCandidateIds)
        {
            var offeredCandidates = origin.EvaluatedProposal.Candidates
                .Where(x => x.Id == candidateId).ToArray();
            var manifestations = origin.EvaluatedProposal.Manifestations
                .Where(x => x.CandidateId == candidateId).ToArray();
            if (offeredCandidates.Length != 1 || manifestations.Length != 1)
                return false;
            var candidate = offeredCandidates[0];
            var manifestation = manifestations[0];
            if (candidate.RequiredChoices is { Count: > 0 } requirements)
            {
                if (requirements.Count != 1 || requirements[0] is null ||
                    requirements[0].Id != "spell" ||
                    requirements[0].Timing != NativeOriginChoiceTiming.Creation ||
                    requirements[0].Minimum != 1 || requirements[0].Maximum != 1 ||
                    requirements[0].FiniteOptions is not { } offered ||
                    !options.SequenceEqual(offered, StringComparer.Ordinal) ||
                    !Matches(manifestation.Choices))
                    return false;
            }
            else
            {
                // An authenticated manifested parent carries its committed choice;
                // a descendant must not reopen that parent's spell menu.
                var parents = origin.EvaluatedProposal.Contributors.Where(x =>
                    x.Id == candidate.ContributorId &&
                    x.Source.Kind == NativeOriginSourceKind.ManifestedProfile).ToArray();
                if (parents.Length != 1 || parents[0].ManifestedProfile is not { } snapshot ||
                    (manifestation.Choices?.Count ?? 0) != 0)
                    return false;
                var parent = parents[0];
                var matchingGenes = (snapshot.BiologicalGenes ?? []).Where(g =>
                    candidateId == "nested:" + parent.Id + ":gene:" + g.Id &&
                    g.Identity == candidate.Identity && Matches(g.ResolvedChoices)).Count();
                var matchingGrants = snapshot.InherentGrants.Count(g =>
                    candidateId == "nested:" + parent.Id + ":" + g.Id &&
                    g.Identity == candidate.Identity && Matches(g.ResolvedChoices));
                if (matchingGenes + matchingGrants != 1)
                    return false;
            }
        }
        spellId = chosen;
        return true;

        bool Matches(IReadOnlyList<NativeOriginChoiceSelection>? evidence) =>
            evidence is { Count: 1 } && evidence[0] is not null &&
            evidence[0].RequirementId == "spell" &&
            evidence[0].SelectedOptionIds is { Count: 1 } selectedEvidence &&
            selectedEvidence[0] == chosen;
    }

    private static bool Text(JsonElement source, string key, out string? value)
    {
        value = TextValue(source, key);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? TextValue(JsonElement source, string key) =>
        source.ValueKind == JsonValueKind.Object &&
        source.TryGetProperty(key, out var field) &&
        field.ValueKind == JsonValueKind.String ? field.GetString() : null;

    private static bool True(JsonElement source, string key) =>
        source.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.True;

    private static View Unresolved(string reason) =>
        new("unresolved", null, [], [reason]);
}
