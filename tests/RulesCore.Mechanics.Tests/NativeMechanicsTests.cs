using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeMechanicsTests
{
    [Fact]
    public void TrainingStructureSurvivesUnresolvedNumericalValues()
    {
        using var fixture = new NativeFixture();
        var request = fixture.Input() with { Skills = null };
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(request);
        var family = Assert.Single(projection.Training!, entry => entry.Id == "skill.perform");
        Assert.True(family.IsFamily);
        Assert.False(family.SupportsRanks);
        Assert.Equal("charisma", family.GoverningAbility);
        var member = Assert.Single(projection.Training!, entry => entry.Id == "skill.perform-act");
        Assert.Equal(family.Id, member.FamilyId);
        Assert.True(member.SupportsRanks);
        var composite = Assert.Single(projection.Training!, entry => entry.Id == "skill.perception");
        Assert.Equal(new[] { "skill.listen", "skill.spot" }, composite.ComponentIds);
        Assert.Equal("arithmetic-mean", composite.Composition);
        Assert.False(composite.SupportsRanks);
        Assert.DoesNotContain("skill.perception.total", projection.Values.Keys);
    }

    [Fact]
    public void CompetencyChecksAddIndependentRanksAndProficiency()
    {
        using var fixture = new NativeFixture();
        var request = fixture.Input() with
        {
            Competencies = new Dictionary<string, NativeSkillTraining>
            {
                ["competency.smithing"] = new(Ranks: 2, Proficient: true, Ability: "intelligence"),
                ["competency.cooking"] = new(Ranks: 2, Ability: "wisdom"),
                ["competency.alchemy"] = new(Proficient: true, Ability: "intelligence"),
                ["competency.navigation"] = new(Ability: "wisdom")
            }
        };
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(request);
        // Intelligence -1, Wisdom +2, proficiency bonus +2.
        Assert.Equal(3, projection.Values["competency.smithing.total"].Value);
        Assert.Equal(4, projection.Values["competency.cooking.total"].Value);
        Assert.Equal(1, projection.Values["competency.alchemy.total"].Value);
        Assert.Equal(2, projection.Values["competency.navigation.total"].Value);
        Assert.DoesNotContain(projection.Issues, issue =>
            issue.Code == "training-profile-conflict");
    }

    [Fact]
    public void CompetencyStillRequiresAnActivityAbilityAndSkillsKeepTheirExistingBoundary()
    {
        using var fixture = new NativeFixture();
        var request = fixture.Input() with
        {
            Skills = new Dictionary<string, NativeSkillTraining>
            {
                ["skill.listen"] = new(Ranks: 1, Proficient: true)
            },
            Competencies = new Dictionary<string, NativeSkillTraining>
            {
                ["competency.smithing"] = new(Ranks: 1, Proficient: true)
            }
        };
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(request);
        Assert.Equal("unsupported", projection.Values["skill.listen.total"].Status);
        Assert.Equal("unsupported", projection.Values["competency.smithing.total"].Status);
        Assert.Contains(projection.Issues, issue =>
            issue.Code == "training-profile-conflict" && issue.Path == "skill.listen");
        Assert.Contains(projection.Issues, issue =>
            issue.Code == "training-ability-unresolved" &&
            issue.Path == "competency.smithing");
        Assert.DoesNotContain(projection.Issues, issue =>
            issue.Code == "training-profile-conflict" &&
            issue.Path == "competency.smithing");
    }

    [Fact]
    public void LegacyCompetencyFacetsNeverChooseAnActivityAbility()
    {
        // Historical facets are not part of the native Competency's ability contract.
        using var fixture = new NativeFixture(legacyCompetencyFacet: true);
        var request = fixture.Input() with
        {
            Competencies = new Dictionary<string, NativeSkillTraining>
            {
                ["competency.smithing"] = new(Ranks: 2, Proficient: true)
            }
        };
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(request);
        Assert.Equal("unsupported", projection.Values["competency.smithing.total"].Status);
        Assert.Contains(projection.Issues, issue =>
            issue.Code == "training-ability-unresolved" &&
            issue.Path == "competency.smithing");
    }

    [Fact]
    public void InvalidCompetencyGrantBatchDoesNotExposePartialTraining()
    {
        using var fixture = new NativeFixture();
        var result = NativeCompetencyTraining.CombineValidated(fixture.Catalog,
        [
            new("feature.valid", "valid", "competency.smithing", Ranks: 1),
            new("feature.invalid", "invalid", "competency.alchemy", Ranks: -1)
        ]);
        Assert.Equal("invalid", result.Status);
        Assert.Empty(result.Training);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "invalid-competency-grant-effect" &&
            issue.Path == "invalid");
    }

    [Fact]
    public void ValidatedCompetencyGrantsAccumulateRanksAndProficiencyIndependently()
    {
        using var fixture = new NativeFixture();
        var grants = new NativeCompetencyTrainingGrant[]
        {
            new("upbringing.dwarf.general", "dwarf-craft", "competency.smithing", Ranks: 1),
            new("background.smith", "background-craft", "competency.smithing", Proficient: true),
            new("feat.training", "feat-one", "competency.smithing", Ranks: 1),
            new("feat.training", "feat-two", "competency.smithing", Ranks: 1),
            new("upbringing.goliath.general", "clan-craft", "competency.cooking", Ranks: 2)
        };
        var result = NativeCompetencyTraining.CombineValidated(fixture.Catalog, grants);
        Assert.Equal("resolved", result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(3, result.Training["competency.smithing"].Ranks);
        Assert.True(result.Training["competency.smithing"].Proficient);
        Assert.Equal(2, result.Training["competency.cooking"].Ranks);
        Assert.False(result.Training["competency.cooking"].Proficient);
        Assert.True(NativeCompetencyTraining.CheckPrerequisite(
            fixture.Catalog, result.Training,
            new("competency.smithing", MinimumRanks: 3, RequiresProficiency: true)).Satisfied);
        Assert.True(NativeCompetencyTraining.CheckPrerequisite(
            fixture.Catalog, result.Training,
            new("competency.cooking", MinimumRanks: 2)).Satisfied);
        Assert.False(NativeCompetencyTraining.CheckPrerequisite(
            fixture.Catalog, result.Training,
            new("competency.cooking", MinimumRanks: 2, RequiresProficiency: true)).Satisfied);
        Assert.False(NativeCompetencyTraining.CheckPrerequisite(
            fixture.Catalog, result.Training,
            new("competency.smithing", MinimumRanks: 4)).Satisfied);
    }

    [Fact]
    public void RepeatedProficienciesAddRanksAndPreserveOccurrenceAudit()
    {
        using var fixture = new NativeFixture();
        var grants = new NativeCompetencyTrainingGrant[]
        {
            new("upbringing", "c", "competency.alchemy", Proficient: true),
            new("background", "a", "competency.alchemy", Proficient: true),
            new("feature", "b", "competency.alchemy", Ranks: 2, Proficient: true),
            new("feature", "rank", "competency.alchemy", Ranks: 1)
        };
        var result = NativeCompetencyTraining.CombineValidated(fixture.Catalog, grants);
        Assert.Equal("resolved", result.Status);
        Assert.Equal(new NativeCompetencyTrainingState(5, true),
            result.Training["competency.alchemy"]);
        Assert.Equal(2, result.DuplicateResolutions!.Count);
        Assert.All(result.DuplicateResolutions, entry =>
            Assert.Equal("add-rank", entry.Outcome));
        var reversed = NativeCompetencyTraining.CombineValidated(
            fixture.Catalog, grants.Reverse().ToArray());
        Assert.Equal(result.Training["competency.alchemy"],
            reversed.Training["competency.alchemy"]);
        Assert.Equal(result.DuplicateResolutions, reversed.DuplicateResolutions);

        var single = NativeCompetencyTraining.CombineValidated(fixture.Catalog,
        [
            new("background", "only", "competency.alchemy", Proficient: true)
        ]);
        Assert.Equal(0, single.Training["competency.alchemy"].Ranks);
        var repeatedOccurrence = NativeCompetencyTraining.CombineValidated(
            fixture.Catalog,
        [
            new("feat", "same", "competency.alchemy", Ranks: 1),
            new("feat", "same", "competency.alchemy", Ranks: 1)
        ]);
        Assert.Equal("invalid", repeatedOccurrence.Status);
        Assert.Empty(repeatedOccurrence.Training);
        Assert.Contains(repeatedOccurrence.Issues, issue =>
            issue.Code == "duplicate-competency-grant-occurrence");

        var wrongKind = NativeCompetencyTraining.CombineValidated(fixture.Catalog,
        [
            new("feature", "once", "skill.listen", Ranks: 1)
        ]);
        Assert.Equal("invalid", wrongKind.Status);
        Assert.Empty(wrongKind.Training);
        Assert.Contains(wrongKind.Issues, issue =>
            issue.Code == "unknown-competency-grant-target");
    }

    [Fact]
    public void RepeatedProficiencyCanSelectAuthorizedDifferentCompetencies()
    {
        using var fixture = new NativeFixture();
        var grants = new NativeCompetencyTrainingGrant[]
        {
            new("upbringing", "a", "competency.smithing", Proficient: true),
            new("background", "b", "competency.smithing", Ranks: 1,
                Proficient: true,
                DuplicateProficiencyAlternativeCompetencyId: "competency.cooking"),
            new("feat", "c", "competency.smithing", Proficient: true,
                DuplicateProficiencyAlternativeCompetencyId: "competency.alchemy")
        };
        var mode = NativeCompetencyDuplicateProficiencyPolicy.ChooseAnotherProficiency;
        var result = NativeCompetencyTraining.CombineValidated(fixture.Catalog, grants, mode);
        Assert.Equal("resolved", result.Status);
        Assert.Equal(new NativeCompetencyTrainingState(1, true),
            result.Training["competency.smithing"]);
        Assert.Equal(new NativeCompetencyTrainingState(0, true),
            result.Training["competency.cooking"]);
        Assert.Equal(new NativeCompetencyTrainingState(0, true),
            result.Training["competency.alchemy"]);
        Assert.Contains(result.DuplicateResolutions!, entry =>
            entry.SourceId == "background" && entry.OccurrenceId == "b" &&
            entry.AlternativeCompetencyId == "competency.cooking");

        var reversed = NativeCompetencyTraining.CombineValidated(
            fixture.Catalog, grants.Reverse().ToArray(), mode);
        Assert.Equal("resolved", reversed.Status);
        foreach (var item in result.Training)
            Assert.Equal(item.Value, reversed.Training[item.Key]);

        var noChoice = NativeCompetencyTraining.CombineValidated(fixture.Catalog,
        [
            new("one", "a", "competency.smithing", Proficient: true),
            new("two", "b", "competency.smithing", Proficient: true)
        ], mode);
        Assert.Equal("invalid", noChoice.Status);
        Assert.Empty(noChoice.Training);

        foreach (var target in new[]
            { "competency.smithing", "competency.missing", "skill.listen" })
        {
            var invalid = NativeCompetencyTraining.CombineValidated(fixture.Catalog,
            [
                new("one", "a", "competency.smithing", Proficient: true),
                new("two", "b", "competency.smithing", Proficient: true,
                    DuplicateProficiencyAlternativeCompetencyId: target)
            ], mode);
            Assert.Equal("invalid", invalid.Status);
            Assert.Empty(invalid.Training);
        }
        var repeatedTarget = NativeCompetencyTraining.CombineValidated(fixture.Catalog,
        [
            new("one", "a", "competency.smithing", Proficient: true),
            new("two", "b", "competency.smithing", Proficient: true,
                DuplicateProficiencyAlternativeCompetencyId: "competency.cooking"),
            new("three", "c", "competency.smithing", Proficient: true,
                DuplicateProficiencyAlternativeCompetencyId: "competency.cooking")
        ], mode);
        Assert.Equal("invalid", repeatedTarget.Status);

        var alternateInRankMode = NativeCompetencyTraining.CombineValidated(
            fixture.Catalog, grants);
        Assert.Equal("invalid", alternateInRankMode.Status);
        var unknownMode = NativeCompetencyTraining.CombineValidated(
            fixture.Catalog, [], (NativeCompetencyDuplicateProficiencyPolicy)99);
        Assert.Equal("invalid", unknownMode.Status);
    }

    [Fact]
    public void EmphasisSpecialCancellationOverridesReroll()
    {
        Assert.Equal("counteract", NativeRolls.Resolve(NativeRollMode.Emphasis, 1, 20,
            "counteract").EffectKey);
        Assert.Equal("invalid-dice", NativeRolls.Resolve(NativeRollMode.Normal, 21).Status);
    }

    [Fact]
    public void FourDefenseProfilesPreserveApprovedDifferences()
    {
        var parts = new Dictionary<string, int> {
            ["armor"] = 4, ["shield"] = 2, ["dexterity"] = 3 };
        Assert.Equal(19, NativeDefenses.Evaluate(NativeDefenseProfile.Normal, parts));
        Assert.Equal(13, NativeDefenses.Evaluate(NativeDefenseProfile.Touch, parts));
        Assert.Equal(16, NativeDefenses.Evaluate(NativeDefenseProfile.FlatFooted, parts));
        Assert.Equal(10, NativeDefenses.Evaluate(NativeDefenseProfile.TouchFlatFooted, parts));
        parts["dexterity"] = -2;
        Assert.Equal(8, NativeDefenses.Evaluate(NativeDefenseProfile.TouchFlatFooted, parts));
    }

    [Fact]
    public void RestDoesNotInventUnspecifiedRecovery()
    {
        var sorcery = new NativeResource("resource.sorcery-points", 5, 1, "long-rest");
        Assert.Equal(1, NativeResourceResolution.Recover(sorcery, 1, NativeRecoveryEvent.ShortRest).Current);
        Assert.Equal(5, NativeResourceResolution.Recover(sorcery, 1, NativeRecoveryEvent.LongRest).Current);
        Assert.Equal("insufficient-resource", NativeResourceResolution.Spend(sorcery, 1, 2).Status);
        Assert.Equal("unsupported", NativeResourceResolution.Recover(
            sorcery with { Recovery = "unsupported" }, 1, NativeRecoveryEvent.LongRest).Status);
        var pact = new NativeResource("resource.pact-magic", 2, 0, "short-or-long-rest", 3);
        Assert.Equal(2, NativeResourceResolution.Recover(pact, 0,
            NativeRecoveryEvent.ShortRest).Current);
        Assert.Equal(2, NativeResourceResolution.Recover(pact, 0,
            NativeRecoveryEvent.LongRest).Current);
        Assert.Equal("invalid-input", NativeResourceResolution.Recover(pact, 0,
            (NativeRecoveryEvent)42).Status);
    }

    [Fact]
    public void CharacterIsCalculatedFromNativeProgressionAndOwnSnapshot()
    {
        using var fixture = new NativeFixture();
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(fixture.Input());
        Assert.Equal(1, projection.ContractVersion);
        Assert.Equal(3, projection.Values["ability.dexterity.modifier"].Value);
        Assert.Equal(2, projection.Values["proficiency.bonus"].Value);
        Assert.Equal(5, projection.Values["save.dexterity"].Value);
        Assert.Equal(3, projection.Values["initiative"].Value);
        Assert.Equal(19, projection.Values["ac.normal"].Value);
        Assert.Equal(13, projection.Values["ac.touch"].Value);
        Assert.Equal(16, projection.Values["ac.flat-footed"].Value);
        Assert.Equal(10, projection.Values["ac.touch-flat-footed"].Value);
        Assert.Contains("class-feature.demo.known", projection.FeatureIds);
        Assert.Equal(3, projection.Resources.Single(r => r.Id == "resource.sorcery-points").Maximum);
        Assert.Equal(11, projection.Values["skill.listen.total"].Value);
        Assert.Equal(7, projection.Values["skill.perception.total"].Value);
    }

    [Fact]
    public void ApprovedBlindedEffectsSuppressPositiveDexterityAndHalveMovement()
    {
        using var fixture = new NativeFixture();
        var request = fixture.Input() with {
            ConditionIds = ["condition.blinded"], BaseWalkSpeed = 30
        };
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(request);
        Assert.Equal(14, projection.Values["ac.normal"].Value);
        Assert.Equal(8, projection.Values["ac.touch"].Value);
        Assert.Equal(15, projection.Values["movement.walk"].Value);
        // An applied condition is not automatically a complete attack/check evaluator.
        Assert.Equal("unsupported", projection.Values["initiative"].Status);
    }

    [Fact]
    public void WetRetainsIndependentCharacterNumbers()
    {
        using var fixture = new NativeFixture();
        var baseline = new NativeCharacterProjector(fixture.Catalog).Project(fixture.Input());
        var wet = new NativeCharacterProjector(fixture.Catalog).Project(
            fixture.Input() with { ConditionIds = ["condition.wet"] });
        foreach (var key in new[] { "ability.dexterity.score", "ability.dexterity.modifier",
                     "save.dexterity", "proficiency.bonus", "skill.listen.total" })
            Assert.Equal(baseline.Values[key], wet.Values[key]);
    }

    [Fact]
    public void UnknownConditionsAreNotSilentlyIgnored()
    {
        using var fixture = new NativeFixture();
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(
            fixture.Input() with { ConditionIds = ["condition.unknown"], BaseWalkSpeed = 30 });
        Assert.Equal("unsupported", projection.Values["ac.normal"].Status);
        Assert.Equal("unsupported", projection.Values["movement.walk"].Status);
        Assert.Contains(projection.Issues, issue => issue.Code == "condition-unavailable");
        Assert.Equal("unsupported", projection.Values["ability.dexterity.modifier"].Status);
        Assert.Equal(2, projection.Values["proficiency.bonus"].Value);
    }

    [Fact]
    public void TypedNativeRollEffectsRespectAttackRolesAndCancelOpposites()
    {
        using var fixture = new NativeFixture();
        var byAffected = new NativeRollContext("attack-roll", "attacks-by-affected-creature");
        var againstAffected = new NativeRollContext("attack-roll", "attacks-against-affected-creature");
        Assert.Equal(NativeRollMode.Disadvantage, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.blinded"], byAffected).Mode);
        Assert.Equal(NativeRollMode.Normal, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.blinded"], againstAffected).Mode);
        Assert.Equal(NativeRollMode.Advantage, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.petrified"], againstAffected).Mode);
        Assert.Equal(NativeRollMode.Normal, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.poisoned", "condition.blessed"],
            new NativeRollContext("ability-check", "affected-creature")).Mode);
    }

    [Fact]
    public void MissingSaveAbilityCannotConcealDexterityDisadvantage()
    {
        using var fixture = new NativeFixture();
        var omitted = NativeConditionEffects.RollMode(fixture.Catalog, ["condition.restrained"],
            new NativeRollContext("saving-throw", "affected-creature"));
        Assert.Equal("unsupported", omitted.Status);
        Assert.Null(omitted.Mode);
        Assert.Contains(omitted.Issues, i => i.Code == "roll-ability-required");
        Assert.Equal(NativeRollMode.Disadvantage, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.restrained"],
            new NativeRollContext("saving-throw", "affected-creature", "dexterity")).Mode);
        Assert.Equal(NativeRollMode.Normal, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.restrained"],
            new NativeRollContext("saving-throw", "affected-creature", "wisdom")).Mode);
    }

    [Fact]
    public void ConditionalRollEffectsRemainUnresolvedWithoutEventFacts()
    {
        using var fixture = new NativeFixture();
        var evaluation = NativeConditionEffects.RollMode(fixture.Catalog,
            ["condition.frightened"], new NativeRollContext("attack-roll", "affected-creature"));
        Assert.Equal("unsupported", evaluation.Status);
        Assert.Null(evaluation.Mode);
        Assert.Contains(evaluation.Issues, issue => issue.Code == "conditional-roll-unresolved");
    }


    [Fact]
    public void FrightenedPredicateUsesKnownLineOfSightRatherThanAssumingTrue()
    {
        using var fixture = new NativeFixture();
        var roll = new NativeRollContext("attack-roll", "attacks-by-affected-creature");
        var visible = new NativeEffectContext(Flags: new Dictionary<string, bool> {
            ["fearSourceWithinLineOfSight"] = true });
        var hidden = new NativeEffectContext(Flags: new Dictionary<string, bool> {
            ["fearSourceWithinLineOfSight"] = false });
        Assert.Equal(NativeRollMode.Disadvantage, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.frightened"], roll, visible).Mode);
        Assert.Equal(NativeRollMode.Normal, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.frightened"], roll, hidden).Mode);
        Assert.Equal("unsupported", NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.frightened"], roll).Status);
    }

    [Fact]
    public void WetSaveMatchesDamageTypeAndSourceKindAndCancelsOpposites()
    {
        using var fixture = new NativeFixture();
        var roll = new NativeRollContext("saving-throw", "affected-creature");
        NativeEffectContext Facts(string sourceKind, params string[] damageTypes) => new(
            Qualifiers: new Dictionary<string, string> {
                ["sourceKind"] = sourceKind, ["purpose"] = "ordinary-save" },
            SourceDamageTypes: damageTypes);
        Assert.Equal(NativeRollMode.Disadvantage, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.wet"], roll, Facts("spell", "cold")).Mode);
        Assert.Equal(NativeRollMode.Advantage, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.wet"], roll, Facts("effect", "fire")).Mode);
        Assert.Equal(NativeRollMode.Normal, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.wet"], roll, Facts("spell", "fire", "lightning")).Mode);
        Assert.Equal(NativeRollMode.Normal, NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.wet"], roll, Facts("weapon", "cold")).Mode);
        Assert.Equal("unsupported", NativeConditionEffects.RollMode(
            fixture.Catalog, ["condition.wet"], roll, new NativeEffectContext(
                SourceDamageTypes: ["fire"])).Status);
    }

    [Fact]
    public void ConditionalProneDefenseUsesIncomingAttackType()
    {
        using var fixture = new NativeFixture();
        var parts = new Dictionary<string, int> { ["armor"] = 4, ["dexterity"] = 3 };
        var melee = new NativeEffectContext(Qualifiers: new Dictionary<string, string> {
            ["incomingAttackType"] = "melee" });
        var ranged = new NativeEffectContext(Qualifiers: new Dictionary<string, string> {
            ["incomingAttackType"] = "ranged" });
        Assert.Equal(13, NativeConditionEffects.ArmorClass(
            fixture.Catalog, ["condition.prone"], NativeDefenseProfile.Normal,
            parts, melee).Result.Value);
        Assert.Equal(21, NativeConditionEffects.ArmorClass(
            fixture.Catalog, ["condition.prone"], NativeDefenseProfile.Normal,
            parts, ranged).Result.Value);
        Assert.Equal("unsupported", NativeConditionEffects.ArmorClass(
            fixture.Catalog, ["condition.prone"], NativeDefenseProfile.Normal,
            parts).Result.Status);
    }

    [Fact]
    public void ConditionalEntangledSpeedDistinguishesAnchoredFromUnanchored()
    {
        using var fixture = new NativeFixture();
        var tied = new NativeEffectContext(Flags: new Dictionary<string, bool> { ["anchored"] = true });
        var loose = new NativeEffectContext(Flags: new Dictionary<string, bool> { ["anchored"] = false });
        Assert.Equal(0, NativeConditionEffects.WalkSpeed(
            fixture.Catalog, ["condition.entangled"], 30, tied).Result.Value);
        Assert.Equal(15, NativeConditionEffects.WalkSpeed(
            fixture.Catalog, ["condition.entangled"], 30, loose).Result.Value);
        Assert.Equal("unsupported", NativeConditionEffects.WalkSpeed(
            fixture.Catalog, ["condition.entangled"], 30).Result.Status);
    }

    [Fact]
    public void UnknownPredicateIsUnresolvedEvenWithUnrelatedFacts()
    {
        using var fixture = new NativeFixture();
        var result = NativeConditionEffects.RollMode(fixture.Catalog, ["condition.unknown-predicate"],
            new NativeRollContext("attack-roll", "attacks-by-affected-creature"),
            new NativeEffectContext(Flags: new Dictionary<string, bool> { ["anchored"] = true }));
        Assert.Equal("unsupported", result.Status);
        Assert.Null(result.Mode);
        Assert.Contains(result.Issues, i => i.Code == "conditional-roll-unresolved");
    }

    [Fact]
    public void UnavailableAndVersionMismatchedMechanicsDoNotBecomeZero()
    {
        using var fixture = new NativeFixture();
        var wrong = fixture.Input() with { Fingerprint = "outdated" };
        var rejection = new NativeCharacterProjector(fixture.Catalog).Project(wrong);
        Assert.Equal("invalid", rejection.Status);
        Assert.Contains(rejection.Issues, issue => issue.Code == "ruleset-version-mismatch");
        var missing = fixture.Input() with {
            Advancements = [new NativeAdvancement("class.untranslated", 1, "a")],
            DefenseContributions = null,
            Skills = null
        };
        var result = new NativeCharacterProjector(fixture.Catalog).Project(missing);
        Assert.Equal("partial", result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "unsupported-advancement");
        Assert.Equal("unsupported", result.Values["advancement.total-level"].Status);
        Assert.False(result.Values.ContainsKey("ac.normal"));
    }

    [Fact]
    public void UntrustedOversizedNumericInputsFailWithoutOverflowing()
    {
        using var fixture = new NativeFixture();
        var projector = new NativeCharacterProjector(fixture.Catalog);
        var defenseOverflow = projector.Project(fixture.Input() with {
            DefenseContributions = new Dictionary<string, int> { ["armor"] = int.MaxValue }
        });
        Assert.Equal("invalid", defenseOverflow.Status);
        Assert.Contains(defenseOverflow.Issues, i => i.Code == "invalid-input-limits");
        var hitPointOverflow = projector.Project(fixture.Input() with {
            HitPointGains = new Dictionary<string, int> {
                ["first"] = int.MaxValue, ["second"] = int.MaxValue,
                ["third"] = int.MaxValue }
        });
        Assert.Equal("invalid", hitPointOverflow.Status);
        Assert.Contains(hitPointOverflow.Issues, i => i.Code == "invalid-input-limits");
    }

    [Fact]
    public void RejectedDuplicateAdvancementWithholdsAllClassDerivedBenefits()
    {
        using var fixture = new NativeFixture();
        var request = fixture.Input() with
        {
            Advancements = [
                new NativeAdvancement("class.demo", 3, "same-occurrence"),
                new NativeAdvancement("class.demo", 3, "same-occurrence")]
        };
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(request);
        Assert.Equal("partial", projection.Status);
        Assert.Equal("unsupported", projection.Values["advancement.total-level"].Status);
        Assert.Equal("unsupported", projection.Values["proficiency.bonus"].Status);
        Assert.Equal("unsupported", projection.Values["save.dexterity"].Status);
        Assert.Equal("unsupported", projection.Values["skill.listen.total"].Status);
        Assert.Equal(3, projection.Values["ability.dexterity.modifier"].Value);
        Assert.Empty(projection.FeatureIds);
        Assert.Empty(projection.Resources);
        Assert.Empty(projection.RequiredChoices);
        Assert.Contains(projection.Issues, i => i.Code == "duplicate-advancement-occurrence");
    }

    [Fact]
    public void MissingAdvancementIdentityDoesNotCauseAnUnhandledLookupFailure()
    {
        using var fixture = new NativeFixture();
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(
            fixture.Input() with
            {
                Advancements = [new NativeAdvancement(null!, 3, "class-a")],
                SelectedOptions = [null!]
            });
        Assert.Equal("partial", projection.Status);
        Assert.Empty(projection.FeatureIds);
        Assert.Empty(projection.Resources);
        Assert.Contains(projection.Issues, issue => issue.Code == "unsupported-advancement");
    }

    [Fact]
    public void NativeProgressionGapIsRejectedBeforeBenefitsAreGranted()
    {
        using var fixture = new NativeFixture(progressions: "gap");
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(fixture.Input());
        Assert.Equal("unsupported", projection.Values["advancement.total-level"].Status);
        Assert.Empty(projection.FeatureIds);
        Assert.Empty(projection.Resources);
        Assert.Contains(projection.Issues, i => i.Code == "progression-gap");
    }

    [Fact]
    public void DuplicateNativeProgressionRowsYieldAnIssueInsteadOfThrowing()
    {
        using var fixture = new NativeFixture(progressions: "duplicate");
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(fixture.Input());
        Assert.Equal("partial", projection.Status);
        Assert.Equal("unsupported", projection.Values["advancement.total-level"].Status);
        Assert.Contains(projection.Issues, i => i.Code == "progression-invalid");
    }

    [Fact]
    public void MultipleClassesDoNotUseMaximumClassSpecificProficiency()
    {
        using var fixture = new NativeFixture(secondClass: true);
        var request = fixture.Input() with
        {
            Advancements = [
                new NativeAdvancement("class.demo", 3, "class-a"),
                new NativeAdvancement("class.secondary", 1, "class-b")]
        };
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(request);
        Assert.Equal(4, projection.Values["advancement.total-level"].Value);
        Assert.Equal("unsupported", projection.Values["proficiency.bonus"].Status);
        Assert.Equal("unsupported", projection.Values["save.dexterity"].Status);
        Assert.Contains(projection.Issues, i => i.Code == "multiclass-proficiency-unresolved");
    }

    [Fact]
    public async Task ParallelProjectionsAreIdentical()
    {
        using var fixture = new NativeFixture();
        var projector = new NativeCharacterProjector(fixture.Catalog);
        var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() => projector.Project(fixture.Input())));
        var results = await Task.WhenAll(tasks);
        Assert.All(results, item => Assert.Equal(results[0].Status, item.Status));
        Assert.All(results, item => Assert.Equal(results[0].Fingerprint, item.Fingerprint));
        Assert.All(results, item => Assert.Equal(results[0].Issues.Select(i => i.Code),
            item.Issues.Select(i => i.Code)));
        Assert.All(results, item => Assert.Equal(results[0].Values["ac.normal"], item.Values["ac.normal"]));
    }

    private sealed class NativeFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "rc-mechanics-" + Guid.NewGuid().ToString("N"));
        public NativeCatalogSnapshot Catalog { get; }
        public NativeFixture(string progressions = "valid", bool secondClass = false,
            bool legacyCompetencyFacet = false)
        {
            var root = Path.Combine(_root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(root, "native"));
            File.WriteAllText(Path.Combine(root, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(root, "native", "class.json"), """
                {"id":"class.demo","name":"Demo","kind":"class","definition":{
                  "savingThrowProficiencies":["dexterity"],
                  "progression":[
                    {"level":1,"proficiencyBonus":2,"featureGrants":["class-feature.demo.known"]},
                    {"level":2,"proficiencyBonus":2,"featureGrants":[]},
                    {"level":3,"proficiencyBonus":2,"featureGrants":[],"sorceryPointMaximum":3}
                  ]}}
                """);
            if (progressions is "gap" or "duplicate")
            {
                var file = Path.Combine(root, "native", "class.json");
                var original = File.ReadAllText(file);
                var replacement = progressions == "gap"
                    ? original.Replace("{\"level\":2,", "{\"level\":4,", StringComparison.Ordinal)
                    : original.Replace("{\"level\":2,", "{\"level\":1,", StringComparison.Ordinal);
                File.WriteAllText(file, replacement);
            }
            if (secondClass)
                File.WriteAllText(Path.Combine(root, "native", "secondary.json"), """
                    {"id":"class.secondary","name":"Secondary","kind":"class","definition":{
                    "savingThrowProficiencies":["wisdom"],
                    "progression":[{"level":1,"proficiencyBonus":2,"featureGrants":[]}]}}
                    """);
            File.WriteAllText(Path.Combine(root, "native", "skills.json"), """
                {"skills":[
                  {"id":"skill.perform","name":"Perform","kind":"skill-family","defaultAbility":"charisma"},
                  {"id":"skill.perform-act","name":"Perform (Act)","kind":"skill","family":"skill.perform","defaultAbility":"charisma"},
                  {"id":"skill.listen","name":"Listen","kind":"skill","defaultAbility":"wisdom"},
                  {"id":"skill.spot","name":"Spot","kind":"skill","defaultAbility":"wisdom"},
                  {"id":"skill.perception","name":"Perception","kind":"skill","defaultAbility":"wisdom",
                   "childSkills":["skill.listen","skill.spot"],
                   "composition":{"operation":"arithmetic-mean","rounding":"truncate-toward-zero"}}
                ]}
                """);
            File.WriteAllText(Path.Combine(root, "native", "competencies.json"), """
                {"competencies":[
                  {"id":"competency.smithing","name":"Smithing","kind":"competency",
                   "profile":{"proficiency":"binary","level":"progressive","abilityResolution":"per-activity"}},
                  {"id":"competency.cooking","name":"Cooking","kind":"competency",
                   "profile":{"proficiency":"binary","level":"progressive","abilityResolution":"per-activity"}},
                  {"id":"competency.alchemy","name":"Alchemy","kind":"competency",
                   "profile":{"proficiency":"binary","level":"progressive","abilityResolution":"per-activity"}},
                  {"id":"competency.navigation","name":"Navigation","kind":"competency",
                   "profile":{"proficiency":"binary","level":"progressive","abilityResolution":"per-activity"}}
                ]}
                """);
            if (legacyCompetencyFacet)
            {
                // Simulate an obsolete facet that must not supply a task's Ability.
                var competencyFile = Path.Combine(root, "native", "competencies.json");
                var original = File.ReadAllText(competencyFile);
                const string marker = "\"id\":\"competency.smithing\",\"name\":\"Smithing\",\"kind\":\"competency\",";
                var replaced = original.Replace(marker,
                    marker + "\"facets\":[{\"governingAbility\":\"intelligence\"}],",
                    StringComparison.Ordinal);
                if (replaced == original)
                    throw new InvalidOperationException("Competency fixture marker not found.");
                File.WriteAllText(competencyFile, replaced);
            }
            File.WriteAllText(Path.Combine(root, "native", "conditions.json"), """
                {"conditions":[
                  {"id":"condition.blinded","name":"Blinded","kind":"condition", "effects":[
                    {"type":"roll-mode","appliesTo":"attack-roll","mode":"disadvantage"},
                    {"type":"speed","operation":"multiply","factor":0.5},
                    {"type":"defense-adjustment","operation":"suppress-contributions",
                     "contributions":["positive-dexterity","dodge"]},
                    {"type":"defense-adjustment","operation":"add-modifier",
                     "defense":"armor-class","value":-2}
                  ]},
                  {"id":"condition.poisoned","name":"Poisoned","kind":"condition","effects":[
                    {"type":"roll-mode","appliesTo":"ability-check","mode":"disadvantage"}]},
                  {"id":"condition.restrained","name":"Restrained","kind":"condition","effects":[
                    {"type":"roll-mode","appliesTo":"saving-throw","ability":"dexterity",
                     "mode":"disadvantage"}]},
                  {"id":"condition.blessed","name":"Blessed","kind":"condition","effects":[
                    {"type":"roll-mode","appliesTo":"ability-check","mode":"advantage"}]},
                  {"id":"condition.petrified","name":"Petrified","kind":"condition","effects":[
                    {"type":"roll-mode","appliesTo":"attack-roll","mode":"advantage",
                     "subject":"attacks-against-affected-creature"}]},
                  {"id":"condition.frightened","name":"Frightened","kind":"condition","effects":[
                    {"type":"roll-mode","appliesTo":"attack-roll","mode":"disadvantage",
                     "when":{"fearSourceWithinLineOfSight":true}}]},
                  {"id":"condition.prone","name":"Prone","kind":"condition","effects":[
                    {"type":"defense-adjustment","operation":"add-modifier","defense":"armor-class",
                     "value":-4,"when":{"incomingAttackType":"melee"},
                     "nonStackingGroup":"prone-or-helpless-melee-exposure"},
                    {"type":"defense-adjustment","operation":"add-modifier","defense":"armor-class",
                     "value":4,"when":{"incomingAttackType":"ranged"}}]},
                  {"id":"condition.entangled","name":"Entangled","kind":"condition","effects":[
                    {"type":"speed","operation":"multiply","factor":0.5,"when":{"anchored":false}},
                    {"type":"speed","operation":"set","value":0,"when":{"anchored":true}}]},
                  {"id":"condition.wet","name":"Wet","kind":"condition","effects":[
                    {"type":"roll-mode","appliesTo":"saving-throw","mode":"disadvantage",
                     "when":{"sourceKind":["spell","effect"],"sourceDealsAnyDamageType":["cold","lightning"]}},
                    {"type":"roll-mode","appliesTo":"saving-throw","mode":"disadvantage",
                     "when":{"purpose":"resist-extreme-cold"}},
                    {"type":"roll-mode","appliesTo":"saving-throw","mode":"advantage",
                     "when":{"sourceKind":["spell","effect"],"sourceDealsDamageType":"fire"}}]},
                  {"id":"condition.unknown-predicate","name":"Unknown Predicate","kind":"condition","effects":[
                    {"type":"roll-mode","appliesTo":"attack-roll","mode":"advantage",
                     "when":{"unsupportedFuturePredicate":true}}]}
                ]}
                """);
            File.WriteAllText(Path.Combine(root, "native", "feature.json"),
                """{"id":"class-feature.demo.known","name":"Known","kind":"classFeature"}""");
            Catalog = NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }

        public NativeCharacterRequest Input() => new(
            "first-party-001", Catalog.Fingerprint,
            new Dictionary<string, int> {
                ["strength"] = 10, ["dexterity"] = 16, ["constitution"] = 12,
                ["intelligence"] = 8, ["wisdom"] = 14, ["charisma"] = 12 },
            [new NativeAdvancement("class.demo", 3, "class-a")],
            Skills: new Dictionary<string, NativeSkillTraining> {
                ["skill.listen"] = new(9), ["skill.spot"] = new(0),
                ["skill.perception"] = new(MiscModifier: 1) },
            DefenseContributions: new Dictionary<string, int> { ["armor"] = 4, ["shield"] = 2 });

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
