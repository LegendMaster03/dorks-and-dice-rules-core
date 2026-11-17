namespace RulesCore.Mechanics;

/// <summary>
/// Combines only explicitly selected, disjoint characteristic slots from
/// multiple already verified transformations. The latest primary character
/// remains the fallback for every unspecified property. This is not a
/// general-purpose grant, Class, Hit Point, or resource transfer rule.
/// </summary>
internal static class NativeCharacterManifestationSlotComposer
{
    internal static bool ValidSlot(NativeManifestationCharacteristicSlot? slot)
    {
        if (slot is null || !Enum.IsDefined(slot.Characteristic))
            return false;
        if (slot.Characteristic == NativeInfluenceCharacteristic.BaseWalkSpeed)
            return slot.Key is null;
        return !string.IsNullOrWhiteSpace(slot.Key) &&
            slot.Key.Length <= 160 && !slot.Key.Any(char.IsControl);
    }

    internal static NativeManifestationEffectiveCharacter Compose(
        NativeCharacterState state,
        NativeManifestationLedger ledger,
        INativeCharacterManifestationAuthority authority,
        IReadOnlyList<NativeManifestationInstance> contributing,
        NativeCharacterInfluenceResolution baseline,
        bool verifyCommittedLedger)
    {
        NativeManifestationEffectiveCharacter Fail(string code, string detail) =>
            new("unresolved", NativeCharacterSheetResolver.PrimarySheetId, null,
                null, [], [], [], [], [], [],
                [new NativeIssue(code, "manifestations.composition", detail)]);

        // This limit bounds repeated full native form resolution; it is not a
        // gameplay limit. Large composite views need a different evaluation
        // strategy rather than hidden ordering or a long callback loop.
        if (contributing.Count is < 2 or > 16 ||
            baseline.EffectiveMechanics is null)
            return Fail("manifestation-composition-unresolved",
                "Simultaneous partial transformations exceed supported composition bounds.");

        var claimed = new HashSet<NativeManifestationCharacteristicSlot>();
        var sheetById = state.AlternateSheets.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var mechanics = NativeCharacterSheetResolver.CopyMechanics(baseline.EffectiveMechanics);
        var sheetProgression = new Dictionary<string, NativeSheetResolvedProgression>(
            StringComparer.Ordinal);
        var sheetGrants = new Dictionary<string, NativeSheetResolvedGrant>(StringComparer.Ordinal);
        var influenceProgression = new Dictionary<string, NativeInfluenceResolvedProgression>(
            StringComparer.Ordinal);
        var influenceGrants = new Dictionary<string, NativeInfluenceResolvedGrant>(
            StringComparer.Ordinal);
        var resources = new Dictionary<string, NativeManifestationEffectiveResource>(
            StringComparer.Ordinal);
        var applied = new Dictionary<string, NativeInfluenceResolvedEffect>(StringComparer.Ordinal);
        var issues = new List<NativeIssue>(baseline.Issues);
        foreach (var item in baseline.AppliedEffects)
            applied[item.InfluenceId + ":" + item.Effect.Id] = item;

        foreach (var instance in contributing)
        {
            if (instance.ContributionSlots is not { Count: > 0 })
                return Fail("manifestation-slots-required",
                    "Every simultaneous form requires exact, source-authored contribution slots.");

            var form = NativeCharacterManifestationResolver.ResolveSelected(
                state, ledger, authority, instance, verifyCommittedLedger);
            if (form.EffectiveMechanics is null ||
                form.Status is "invalid" or "unresolved")
                return new("unresolved", NativeCharacterSheetResolver.PrimarySheetId,
                    null, null, [], [], [], [], [], [], form.Issues);

            var localSlots = instance.ContributionSlots.ToHashSet();
            foreach (var effect in form.AppliedEffects)
                if (!localSlots.Contains(
                    new NativeManifestationCharacteristicSlot(effect.Effect.Characteristic,
                        effect.Effect.Key)))
                    return Fail("manifestation-unrepresented-effect",
                        "An active form influence writes a characteristic outside its declared slots.");

            foreach (var slot in instance.ContributionSlots)
            {
                if (!claimed.Add(slot))
                    return Fail("manifestation-slot-overlap",
                        "Two simultaneous forms write the same characteristic slot.");

                if (baseline.AppliedEffects.Any(item =>
                        item.Effect.Characteristic == slot.Characteristic &&
                        string.Equals(item.Effect.Key, slot.Key, StringComparison.Ordinal)))
                    return Fail("manifestation-primary-effect-overlap",
                        "A primary influence and a partial form both write this slot without a composition rule.");

                if (!HasAuthoredWriter(state, sheetById, instance.SheetId, slot, form.AppliedEffects))
                    return Fail("manifestation-slot-unbound",
                        "A declared slot has no matching inherited form definition or active form effect.");

                var patch = SelectSlot(form.EffectiveMechanics, slot);
                if (patch is null)
                    return Fail("manifestation-slot-value-unresolved",
                        "The selected form does not provide a resolvable value for its declared slot.");
                mechanics = NativeCharacterSheetResolver.Apply(mechanics, patch);
            }

            issues.AddRange(form.Issues);
            foreach (var item in form.SheetProgression)
                sheetProgression.TryAdd(item.Record.Id, item);
            foreach (var grant in form.SheetGrants)
            {
                if (sheetGrants.TryGetValue(grant.Entitlement.Id, out var existing) &&
                    (!string.Equals(existing.OwnerSheetId, grant.OwnerSheetId, StringComparison.Ordinal) ||
                     !string.Equals(existing.ProgressionId, grant.ProgressionId, StringComparison.Ordinal) ||
                     existing.Applicability != grant.Applicability ||
                     !Equals(existing.ApplicabilityBasis, grant.ApplicabilityBasis)))
                    return Fail("manifestation-grant-interaction-unresolved",
                        "A shared sheet grant has incompatible applicability between simultaneous forms.");
                sheetGrants.TryAdd(grant.Entitlement.Id, grant);
            }
            foreach (var item in form.InfluenceProgression)
                influenceProgression.TryAdd(item.Record.Id, item);
            foreach (var grant in form.InfluenceGrants)
            {
                if (influenceGrants.TryGetValue(grant.Entitlement.Id, out var existing) &&
                    (!string.Equals(existing.OwnerInfluenceId, grant.OwnerInfluenceId,
                         StringComparison.Ordinal) ||
                     existing.Applicability != grant.Applicability ||
                     !Equals(existing.ApplicabilityBasis, grant.ApplicabilityBasis)))
                    return Fail("manifestation-influence-grant-interaction-unresolved",
                        "An influence grant has incompatible applicability between simultaneous forms.");
                influenceGrants.TryAdd(grant.Entitlement.Id, grant);
            }
            foreach (var resource in form.Resources)
            {
                if (resources.TryGetValue(resource.Id, out var existing) &&
                    existing != resource)
                    return Fail("manifestation-resource-interaction-unresolved",
                        "Two simultaneous forms declare incompatible resource visibility or maxima.");
                resources.TryAdd(resource.Id, resource);
            }
            foreach (var effect in form.AppliedEffects)
                applied.TryAdd(effect.InfluenceId + ":" + effect.Effect.Id, effect);
        }

        // The composite is intentionally partial. The displayed primary
        // Class advancements remain current, but the effects of combining
        // active forms on Class features, HP and Origin entitlements do not
        // become implicitly authorized by disjoint numeric slots.
        issues.Add(new("manifestation-composite-partial", "manifestations",
            "Multiple explicit form slots are combined; Class, Hit Point and Origin interaction policies remain partial."));
        return new("partial", NativeCharacterSheetResolver.PrimarySheetId,
            null, NativeCharacterSheetResolver.CopyMechanics(mechanics with
            {
                CurrentResources = null
            }),
            sheetProgression.Values.OrderBy(x => x.OwnerSheetId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Id, StringComparer.Ordinal).ToArray(),
            sheetGrants.Values.OrderBy(x => x.OwnerSheetId, StringComparer.Ordinal)
                .ThenBy(x => x.Entitlement.Id, StringComparer.Ordinal).ToArray(),
            influenceProgression.Values.OrderBy(x => x.OwnerInfluenceId, StringComparer.Ordinal)
                .ThenBy(x => x.Record.Id, StringComparer.Ordinal).ToArray(),
            influenceGrants.Values.OrderBy(x => x.OwnerInfluenceId, StringComparer.Ordinal)
                .ThenBy(x => x.Entitlement.Id, StringComparer.Ordinal).ToArray(),
            applied.Values.OrderBy(x => x.InfluenceId, StringComparer.Ordinal)
                .ThenBy(x => x.Effect.Id, StringComparer.Ordinal).ToArray(),
            resources.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            issues.Distinct().OrderBy(x => x.Path, StringComparer.Ordinal)
                .ThenBy(x => x.Code, StringComparer.Ordinal).ToArray(),
            contributing.Select(x => x.Id).ToArray());
    }

    private static bool HasAuthoredWriter(
        NativeCharacterState state,
        IReadOnlyDictionary<string, NativePersistentCharacterSheet> sheets,
        string sheetId, NativeManifestationCharacteristicSlot slot,
        IReadOnlyList<NativeInfluenceResolvedEffect> effects)
    {
        if (effects.Any(x => x.Effect.Characteristic == slot.Characteristic &&
            string.Equals(x.Effect.Key, slot.Key, StringComparison.Ordinal)))
            return true;
        var cursor = sheetId;
        while (cursor != NativeCharacterSheetResolver.PrimarySheetId)
        {
            var sheet = sheets[cursor];
            var patch = sheet.Overrides;
            if (patch is not null && HasSlot(patch, slot)) return true;
            cursor = sheet.ParentId;
        }
        return false;
    }

    private static bool HasSlot(
        NativeCharacterSheetOverrides patch, NativeManifestationCharacteristicSlot slot)
    {
        var key = slot.Key!;
        return slot.Characteristic switch
        {
            NativeInfluenceCharacteristic.AbilityScore =>
                patch.AbilityScores?.Entries.ContainsKey(key) == true,
            NativeInfluenceCharacteristic.DefenseContribution =>
                patch.DefenseContributions?.Entries.ContainsKey(key) == true,
            NativeInfluenceCharacteristic.HitPointGain =>
                patch.HitPointGains?.Entries.ContainsKey(key) == true,
            NativeInfluenceCharacteristic.BaseWalkSpeed => patch.BaseWalkSpeed is not null,
            NativeInfluenceCharacteristic.ConditionId =>
                patch.ConditionIds is { } conditions &&
                (conditions.Replace ||
                 conditions.Added.Contains(key, StringComparer.Ordinal) ||
                 conditions.Removed?.Contains(key, StringComparer.Ordinal) == true),
            NativeInfluenceCharacteristic.SkillRanks or
                NativeInfluenceCharacteristic.SkillProficient or
                NativeInfluenceCharacteristic.SkillMiscModifier or
                NativeInfluenceCharacteristic.SkillAbility =>
                patch.Skills?.Entries.ContainsKey(key) == true,
            NativeInfluenceCharacteristic.CompetencyRanks or
                NativeInfluenceCharacteristic.CompetencyProficient or
                NativeInfluenceCharacteristic.CompetencyMiscModifier or
                NativeInfluenceCharacteristic.CompetencyAbility =>
                patch.Competencies?.Entries.ContainsKey(key) == true,
            _ => false
        };
    }

    private static NativeCharacterSheetOverrides? SelectSlot(
        NativeCharacterRequest form, NativeManifestationCharacteristicSlot slot)
    {
        var key = slot.Key!;
        switch (slot.Characteristic)
        {
            case NativeInfluenceCharacteristic.AbilityScore:
                return form.AbilityScores.TryGetValue(key, out var ability)
                    ? new(AbilityScores: new(false, new Dictionary<string, int> { [key] = ability }))
                    : null;
            case NativeInfluenceCharacteristic.DefenseContribution:
                return form.DefenseContributions?.TryGetValue(key, out var defense) == true
                    ? new(DefenseContributions: new(false,
                        new Dictionary<string, int> { [key] = defense }))
                    : null;
            case NativeInfluenceCharacteristic.HitPointGain:
                return form.HitPointGains?.TryGetValue(key, out var hp) == true
                    ? new(HitPointGains: new(false, new Dictionary<string, int> { [key] = hp }))
                    : null;
            case NativeInfluenceCharacteristic.BaseWalkSpeed:
                return new(BaseWalkSpeed: new(form.BaseWalkSpeed));
            case NativeInfluenceCharacteristic.ConditionId:
                var present = form.ConditionIds?.Contains(key, StringComparer.Ordinal) == true;
                return new(ConditionIds: new(false,
                    present ? [key] : [], present ? [] : [key]));
            default:
                var skill = slot.Characteristic is
                    NativeInfluenceCharacteristic.SkillRanks or
                    NativeInfluenceCharacteristic.SkillProficient or
                    NativeInfluenceCharacteristic.SkillMiscModifier or
                    NativeInfluenceCharacteristic.SkillAbility;
                var map = skill ? form.Skills : form.Competencies;
                if (map?.TryGetValue(key, out var training) != true || training is null)
                    return null;
                var change = slot.Characteristic switch
                {
                    NativeInfluenceCharacteristic.SkillRanks or
                        NativeInfluenceCharacteristic.CompetencyRanks =>
                        new NativeSheetTrainingPatch(Ranks: new(training.Ranks)),
                    NativeInfluenceCharacteristic.SkillProficient or
                        NativeInfluenceCharacteristic.CompetencyProficient =>
                        new NativeSheetTrainingPatch(Proficient: new(training.Proficient)),
                    NativeInfluenceCharacteristic.SkillMiscModifier or
                        NativeInfluenceCharacteristic.CompetencyMiscModifier =>
                        new NativeSheetTrainingPatch(MiscModifier: new(training.MiscModifier)),
                    NativeInfluenceCharacteristic.SkillAbility or
                        NativeInfluenceCharacteristic.CompetencyAbility =>
                        new NativeSheetTrainingPatch(Ability: new(training.Ability)),
                    _ => null
                };
                if (change is null) return null;
                var patch = new NativeSheetMapPatch<NativeSheetTrainingPatch>(false,
                    new Dictionary<string, NativeSheetTrainingPatch> { [key] = change });
                return skill ? new(Skills: patch) : new(Competencies: patch);
        }
    }
}
