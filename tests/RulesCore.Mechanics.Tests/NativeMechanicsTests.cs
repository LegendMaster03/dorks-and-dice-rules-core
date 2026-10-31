using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeMechanicsTests
{
    [Theory]
    [InlineData(NativeRollMode.Normal, 8, null, "resolved", 8)]
    [InlineData(NativeRollMode.Advantage, 8, 16, "resolved", 16)]
    [InlineData(NativeRollMode.Disadvantage, 8, 16, "resolved", 8)]
    [InlineData(NativeRollMode.Emphasis, 7, 13, "resolved", 7)]
    [InlineData(NativeRollMode.Emphasis, 4, 16, "resolved", 4)]
    [InlineData(NativeRollMode.Emphasis, 7, 7, "resolved", 7)]
    [InlineData(NativeRollMode.Emphasis, 7, 14, "reroll-required", null)]
    public void RollModesAreDeterministic(NativeRollMode mode, int first, int? second,
        string status, int? selected)
    {
        var actual = NativeRolls.Resolve(mode, first, second);
        Assert.Equal(status, actual.Status);
        Assert.Equal(selected, actual.Selected);
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
    public void UnknownConditionsAreNotSilentlyIgnored()
    {
        using var fixture = new NativeFixture();
        var projection = new NativeCharacterProjector(fixture.Catalog).Project(
            fixture.Input() with { ConditionIds = ["condition.unknown"], BaseWalkSpeed = 30 });
        Assert.Equal("unsupported", projection.Values["ac.normal"].Status);
        Assert.Equal("unsupported", projection.Values["movement.walk"].Status);
        Assert.Contains(projection.Issues, issue => issue.Code == "condition-unavailable");
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
        public NativeFixture(string progressions = "valid", bool secondClass = false)
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
                  {"id":"skill.listen","name":"Listen","kind":"skill","defaultAbility":"wisdom"},
                  {"id":"skill.spot","name":"Spot","kind":"skill","defaultAbility":"wisdom"},
                  {"id":"skill.perception","name":"Perception","kind":"skill","defaultAbility":"wisdom",
                   "childSkills":["skill.listen","skill.spot"],
                   "composition":{"operation":"arithmetic-mean","rounding":"truncate-toward-zero"}}
                ]}
                """);
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
