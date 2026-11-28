using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeCombatAttackConditionIntegrationTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(),
            "rulescore-combat-check-" + Guid.NewGuid().ToString("N"));
        public NativeCatalogSnapshot Catalog { get; }

        public Fixture()
        {
            var pkg = Path.Combine(root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(pkg, "native"));
            File.WriteAllText(Path.Combine(pkg, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(pkg, "native", "conditions.json"), """
                {"conditions":[
                  {"id":"condition.attacker-hindered","name":"Attacker Hindered","kind":"condition",
                   "effects":[{"type":"roll-mode","appliesTo":"attack-roll","mode":"disadvantage"}]},
                  {"id":"condition.defender-exposed","name":"Defender Exposed","kind":"condition",
                   "effects":[{"type":"roll-mode","appliesTo":"attack-roll","mode":"advantage",
                    "subject":"attacks-against-affected-creature"}]},
                  {"id":"condition.prone-fixture","name":"Prone Fixture","kind":"condition",
                   "effects":[
                     {"type":"defense-adjustment","operation":"add-modifier",
                      "defense":"armor-class","value":-4,
                      "when":{"incomingAttackType":"melee"}},
                     {"type":"defense-adjustment","operation":"add-modifier",
                      "defense":"armor-class","value":4,
                      "when":{"incomingAttackType":"ranged"}}
                   ]},
                  {"id":"condition.unknown-fixture","name":"Unknown Fixture","kind":"condition",
                   "effects":[{"type":"roll-mode","appliesTo":"attack-roll",
                    "mode":"advantage","when":{"notImplemented":true}}]}
                ]}
                """);
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(root, "first-party-001"));
        }

        public NativeCombatAttackRequest Attack(
            string kind = "melee", NativeDefenseProfile profile = NativeDefenseProfile.Normal,
            IReadOnlyList<string>? attacker = null, IReadOnlyList<string>? defender = null,
            NativeRollMode baseMode = NativeRollMode.Normal,
            NativeEffectContext? defenderFacts = null) =>
            new("first-party-001", Catalog.Fingerprint, kind,
                2, 12, null, profile,
                new Dictionary<string, int> { ["armor"] = 2, ["dexterity"] = 3 },
                attacker, defender, baseMode, DefenderFacts: defenderFacts);

        public void Dispose() => Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void OppositeConditionRollModesCancelBeforeUsingDice()
    {
        using var fixture = new Fixture();
        var resolution = NativeCombatAttackResolver.Resolve(fixture.Catalog,
            fixture.Attack(attacker: ["condition.attacker-hindered"],
                defender: ["condition.defender-exposed"]));
        Assert.Equal("resolved", resolution.Status);
        Assert.Equal(NativeRollMode.Normal, resolution.RollMode);
        Assert.Equal(15, resolution.ArmorClass);
        Assert.False(resolution.Hit);
    }

    [Fact]
    public void ApprovedTouchAndFlatFootedProfilesUseSharedDefenseFormula()
    {
        using var fixture = new Fixture();
        var touch = NativeCombatAttackResolver.Resolve(fixture.Catalog,
            fixture.Attack(profile: NativeDefenseProfile.Touch));
        Assert.Equal(13, touch.ArmorClass);
        Assert.True(touch.Hit);
        var flat = NativeCombatAttackResolver.Resolve(fixture.Catalog,
            fixture.Attack(profile: NativeDefenseProfile.FlatFooted));
        Assert.Equal(12, flat.ArmorClass);
        var combined = NativeCombatAttackResolver.Resolve(fixture.Catalog,
            fixture.Attack(profile: NativeDefenseProfile.TouchFlatFooted));
        Assert.Equal(10, combined.ArmorClass);
    }

    [Fact]
    public void DefenderProneAttackTypeChangesDefenseInCorrectDirection()
    {
        using var fixture = new Fixture();
        var melee = NativeCombatAttackResolver.Resolve(fixture.Catalog,
            fixture.Attack(defender: ["condition.prone-fixture"]));
        var ranged = NativeCombatAttackResolver.Resolve(fixture.Catalog,
            fixture.Attack(kind: "ranged", defender: ["condition.prone-fixture"]));
        Assert.Equal(11, melee.ArmorClass);
        Assert.Equal(19, ranged.ArmorClass);
        Assert.True(melee.Hit);
        Assert.False(ranged.Hit);
    }

    [Fact]
    public void FactsCannotOverrideAuthoritativeAttackType()
    {
        using var fixture = new Fixture();
        var result = NativeCombatAttackResolver.Resolve(fixture.Catalog,
            fixture.Attack(defenderFacts: new NativeEffectContext(
                Qualifiers: new Dictionary<string, string> { ["incomingAttackType"] = "ranged" })));
        Assert.Equal("invalid-input", result.Status);
        Assert.Null(result.Hit);
    }

    [Fact]
    public void UnknownConditionsAndPredicatesDoNotResolveAsNormalHits()
    {
        using var fixture = new Fixture();
        var unknown = NativeCombatAttackResolver.Resolve(fixture.Catalog,
            fixture.Attack(attacker: ["condition.no-such-condition"]));
        Assert.Equal("unsupported", unknown.Status);
        Assert.NotEmpty(unknown.Issues);
        var predicate = NativeCombatAttackResolver.Resolve(fixture.Catalog,
            fixture.Attack(attacker: ["condition.unknown-fixture"]));
        Assert.Equal("unsupported", predicate.Status);
    }

    [Fact]
    public void VersionOrDefenseMismatchIsRejected()
    {
        using var fixture = new Fixture();
        var request = fixture.Attack();
        Assert.Equal("ruleset-version-mismatch",
            NativeCombatAttackResolver.Resolve(fixture.Catalog,
                request with { Fingerprint = "old" }).Status);
        Assert.Equal("invalid-input",
            NativeCombatAttackResolver.Resolve(fixture.Catalog,
                request with { DefenderContributions = new Dictionary<string, int>
                    { ["unverified-ac"] = 2 } }).Status);
    }
}
