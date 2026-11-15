using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Native Class projection and sheet-owned synthetic entitlements are tested
/// together without encoding any werewolf gameplay or granting new Class levels.
/// </summary>
public sealed class NativeCharacterSheetProjectorTests
{
    private sealed class Trust : INativeCharacterSheetAuthority
    {
        public bool AuthorizesPrimary(string characterId, NativeCharacterRequest primary) =>
            characterId == "character.one";
        public bool AuthorizesSheet(string characterId, NativePersistentCharacterSheet sheet) =>
            characterId == "character.one";
        public bool AuthorizesProgression(string characterId, string ownerSheetId,
            NativeSheetEarnedProgression progression) =>
            characterId == "character.one";
        public bool AuthorizesGrant(string characterId, string ownerSheetId,
            NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant) =>
            characterId == "character.one";
        public bool AuthorizesApplicability(string characterId, string sheetId,
            NativeSheetGrantApplicability decision) =>
            characterId == "character.one";
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "rc-sheet-inheritance-" + Guid.NewGuid().ToString("N"));
        public NativeCatalogSnapshot Catalog { get; }

        public Fixture()
        {
            var dir = Path.Combine(_root, "first-party-001");
            Directory.CreateDirectory(Path.Combine(dir, "native"));
            File.WriteAllText(Path.Combine(dir, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(dir, "native", "class.json"), """
                {"id":"class.demo","name":"Demo","kind":"class","definition":{
                "savingThrowProficiencies":["dexterity"],
                "progression":[
                    {"level":1,"proficiencyBonus":2,"featureGrants":["class-feature.demo.known"]},
                    {"level":2,"proficiencyBonus":2,"featureGrants":[]},
                    {"level":3,"proficiencyBonus":2,"featureGrants":[]}
                ]}}
                """);
            File.WriteAllText(Path.Combine(dir, "native", "feature.json"),
                """{"id":"class-feature.demo.known","name":"Known","kind":"classFeature"}""");
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    private static NativeOriginSourceReference Source() =>
        new(NativeOriginSourceKind.Native, "synthetic-source", "v1");
    private static NativeOriginSourceOccurrence Occurrence() =>
        new(Source(), "advancements/synthetic");
    private static NativeOriginDecisionBasis Allowed() =>
        new(NativeOriginAdjudication.Supported, NativeRuleReference: "rule.synthetic");

    private static NativeSheetEarnedProgression Earn(
        string progressionId, string grantId, NativeSheetLevelReference levelReference) =>
        new(progressionId, "progression." + progressionId,
            Occurrence(), "receipt." + progressionId,
            [new(grantId, "trait." + grantId, Occurrence(), "receipt." + grantId,
                levelReference)]);

    private static NativeCharacterState State(NativeCatalogSnapshot catalog, int classLevel = 1)
    {
        var source = Source();
        return new("character.one",
            new NativeCharacterRequest("first-party-001", catalog.Fingerprint,
                new Dictionary<string, int>
                {
                    ["strength"] = 10, ["dexterity"] = 14, ["constitution"] = 12,
                    ["intelligence"] = 12, ["wisdom"] = 13, ["charisma"] = 8
                },
                [new NativeAdvancement("class.demo", classLevel, "primary.class.demo")],
                BaseWalkSpeed: 30),
            [
                new NativePersistentCharacterSheet("werewolf",
                    NativeCharacterSheetResolver.PrimarySheetId, "state.werewolf", source, "receipt.werewolf",
                    Progression:
                    [
                        Earn("shared", "grant.shared",
                            new(NativeSheetLevelSource.TotalClassLevels))
                    ],
                    GrantApplicability:
                    [
                        new("grant.shared", NativeOriginGrantApplicabilityState.Applicable, Allowed())
                    ]),
                new NativePersistentCharacterSheet("wolf", "werewolf", "state.wolf",
                    source, "receipt.wolf",
                    Overrides: new(AbilityScores: new(false, new Dictionary<string, int> { ["strength"] = 18 })),
                    Progression:
                    [
                        Earn("wolf", "grant.wolf",
                            new(NativeSheetLevelSource.SpecificClassLevel, "class.demo"))
                    ],
                    GrantApplicability:
                    [
                        new("grant.wolf", NativeOriginGrantApplicabilityState.Applicable, Allowed())
                    ]),
                new NativePersistentCharacterSheet("hybrid", "werewolf", "state.hybrid",
                    source, "receipt.hybrid")
            ]);
    }

    private static NativeCharacterSheetProjection Project(
        NativeCharacterState state, NativeCatalogSnapshot catalog, string sheet) =>
        NativeCharacterSheetProjector.Project(
            state, sheet, new Trust(), new NativeCharacterProjector(catalog));

    [Fact]
    public void NativeClassAdvancementUpdatesInheritedLevelReferencesWithoutRebuildingSheets()
    {
        using var fixture = new Fixture();
        var first = State(fixture.Catalog);
        var advanced = first with
        {
            PrimaryMechanics = first.PrimaryMechanics with
            {
                Advancements = [new NativeAdvancement("class.demo", 2, "primary.class.demo")]
            }
        };

        var before = Project(first, fixture.Catalog, "wolf");
        var after = Project(advanced, fixture.Catalog, "wolf");
        Assert.Same(first.AlternateSheets, advanced.AlternateSheets);
        Assert.Equal(1, before.BaseMechanics!.Values["advancement.total-level"].Value);
        Assert.Equal(2, after.BaseMechanics!.Values["advancement.total-level"].Value);
        Assert.Equal(18, after.BaseMechanics.Values["ability.strength.score"].Value);
        Assert.Equal(2, after.UsableCapabilities.Count);
        Assert.All(before.UsableCapabilities, grant =>
            Assert.Equal(1, grant.ReferencedClassLevel!.Value));
        Assert.All(after.UsableCapabilities, grant =>
            Assert.Equal(2, grant.ReferencedClassLevel!.Value));
        Assert.Equal(new[] { "grant.shared", "grant.wolf" },
            after.UsableCapabilities.Select(item => item.Grant.Entitlement.Id));
        // The native Class projector knows these features, but a form can
        // not claim they are currently usable until its policy is authorized.
        Assert.Contains("class-feature.demo.known",
            new NativeCharacterProjector(fixture.Catalog)
                .Project(advanced.PrimaryMechanics).FeatureIds);
        Assert.DoesNotContain("class-feature.demo.known", after.BaseMechanics.FeatureIds);
        Assert.Contains(after.Issues, issue =>
            issue.Code == "sheet-class-applicability-unresolved");
        Assert.Equal("werewolf", after.UsableCapabilities[0].Grant.OwnerSheetId);
        Assert.Equal("wolf", after.UsableCapabilities[1].Grant.OwnerSheetId);
        Assert.Single(Project(first, fixture.Catalog, "hybrid").UsableCapabilities);
    }

    [Fact]
    public void SuppressedOrUnresolvedGrantsAreNotIncludedInUsableCapabilities()
    {
        using var fixture = new Fixture();
        var original = State(fixture.Catalog);
        var wolf = original.AlternateSheets[1] with
        {
            GrantApplicability =
            [
                new("grant.shared", NativeOriginGrantApplicabilityState.Suppressed, Allowed()),
                new("grant.wolf", NativeOriginGrantApplicabilityState.Unresolved, Allowed())
            ]
        };
        var changed = original with
        {
            AlternateSheets = [original.AlternateSheets[0], wolf, original.AlternateSheets[2]]
        };
        var effective = Project(changed, fixture.Catalog, "wolf");
        Assert.Empty(effective.UsableCapabilities);
        Assert.Equal(2, effective.InapplicableCapabilities.Count);
        Assert.Equal("partial", effective.Status);
        Assert.Contains(effective.Issues, issue =>
            issue.Code == "sheet-grant-applicability-unresolved");
        var hybrid = Project(changed, fixture.Catalog, "hybrid");
        Assert.Single(hybrid.UsableCapabilities);
        Assert.Equal("grant.shared", hybrid.UsableCapabilities[0].Grant.Entitlement.Id);
    }

    [Fact]
    public void InvalidNativeClassProgressionWithholdsLevelBoundCapabilities()
    {
        using var fixture = new Fixture();
        var state = State(fixture.Catalog) with
        {
            PrimaryMechanics = State(fixture.Catalog).PrimaryMechanics with
            {
                Advancements = [new NativeAdvancement("class.unavailable", 3, "unavailable")]
            }
        };
        var view = Project(state, fixture.Catalog, "wolf");
        Assert.Empty(view.UsableCapabilities);
        Assert.Equal(2, view.InapplicableCapabilities.Count);
        Assert.Contains(view.Issues, issue => issue.Code == "sheet-level-reference-unresolved");
    }

    [Fact]
    public void AFormMayNotClaimAnUnownedClassLevelOrCreateClassAdvancement()
    {
        using var fixture = new Fixture();
        var state = State(fixture.Catalog);
        var wolf = state.AlternateSheets[1];
        var ownProgress = wolf.Progression![0];
        wolf = wolf with
        {
            Progression =
            [
                ownProgress with
                {
                    Grants =
                    [
                        ownProgress.Grants[0] with
                        {
                            LevelReference = new(NativeSheetLevelSource.SpecificClassLevel,
                                "class.other")
                        }
                    ]
                }
            ]
        };
        state = state with
        {
            AlternateSheets = [state.AlternateSheets[0], wolf, state.AlternateSheets[2]]
        };
        var view = Project(state, fixture.Catalog, "wolf");
        Assert.Single(view.UsableCapabilities);
        Assert.Contains(view.InapplicableCapabilities,
            grant => grant.Entitlement.Id == "grant.wolf");
        Assert.Single(state.PrimaryMechanics.Advancements);
        Assert.Contains(view.Issues, issue => issue.Code == "sheet-level-reference-unresolved");
    }
}
