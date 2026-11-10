using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeEquipmentQualificationsTests
{
    [Fact]
    public void BarbarianMartialGroupCoversLongswordAndShieldsButNotTools()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var grants = engine.Resolve(f.Request(new NativeEquipmentQualificationSource("class.barbarian", "initial")));
        Assert.Equal("resolved", grants.Status);
        Assert.True(engine.Covers(grants, "equipment.weapon.longsword").Proficient);
        Assert.True(engine.Covers(grants, "equipment.shield.shield").Proficient);
        Assert.False(engine.Covers(grants, "equipment.tool.thieves-tools").Proficient);
    }

    [Fact]
    public void SpecificWeaponDoesNotConferItsEntireGroup()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var grants = engine.Resolve(f.Request(new NativeEquipmentQualificationSource("feature.sword", "one")));
        Assert.True(engine.Covers(grants, "equipment.weapon.longsword").Proficient);
        Assert.False(engine.Covers(grants, "equipment.weapon.rapier").Proficient);
    }

    [Fact]
    public void OverlappingAndRepeatedGrantsRemainBinaryWithProvenance()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var grants = engine.Resolve(f.Request(
            new NativeEquipmentQualificationSource("class.barbarian", "initial"), new NativeEquipmentQualificationSource("feature.sword", "other"),
            new NativeEquipmentQualificationSource("feature.second-sword", "third")));
        Assert.Equal("resolved", grants.Status);
        Assert.Equal(3, grants.Qualifications.Count);
        var coverage = engine.Covers(grants, "equipment.weapon.longsword");
        Assert.Equal("resolved", coverage.Status);
        Assert.True(coverage.Proficient);
        Assert.Equal(3, coverage.Provenance.Count);
        Assert.Equal(3, coverage.Provenance.Select(p => p.SourceOccurrenceId).Distinct().Count());
    }

    [Fact]
    public void RogueThievesToolsIsNotOpenLockSkillAndDoesNotRequirePossession()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var rogue = engine.Resolve(f.Request(new NativeEquipmentQualificationSource("class.rogue", "first")));
        Assert.True(engine.Covers(rogue, "equipment.tool.thieves-tools").Proficient);
        Assert.True(engine.Covers(rogue, "item.thieves-tools").Proficient);

        // A learned Skill is not a tool-grant source, and owning a linked item
        // does not create the qualification fact.
        var skillOnly = engine.Resolve(f.Request(new NativeEquipmentQualificationSource("feature.open-lock", "skill")));
        Assert.False(engine.Covers(skillOnly, "equipment.tool.thieves-tools").Proficient);
        var empty = engine.Resolve(f.Request());
        Assert.False(engine.Covers(empty, "item.thieves-tools").Proficient);
    }

    [Fact]
    public void GamingAndInstrumentChoicesAreLimitedToTheirOwnTypes()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var request = f.Request(new NativeEquipmentQualificationSource("feature.entertainment", "background"));
        var awaiting = engine.Resolve(request);
        Assert.Equal("needs-choices", awaiting.Status);
        Assert.Empty(awaiting.Qualifications);
        Assert.Equal(2, awaiting.RequiredChoices.Count);
        var gaming = Assert.Single(awaiting.RequiredChoices, choice =>
            choice.ChoiceKey == "background/game");
        Assert.Contains("equipment.tool.dice-set", gaming.EligibleQualificationIds);
        Assert.Contains("equipment.tool.playing-card-set", gaming.EligibleQualificationIds);
        Assert.DoesNotContain("equipment.tool.lute", gaming.EligibleQualificationIds);
        var chosen = engine.Resolve(request with {
            SelectedChoices = new Dictionary<string, IReadOnlyList<string>> {
                ["background/game"] = ["equipment.tool.dice-set"],
                ["background/music"] = ["equipment.tool.lute"]
            }
        });
        Assert.Equal("resolved", chosen.Status);
        Assert.True(engine.Covers(chosen, "equipment.tool.dice-set").Proficient);
        Assert.False(engine.Covers(chosen, "equipment.tool.playing-card-set").Proficient);
        Assert.True(engine.Covers(chosen, "equipment.tool.lute").Proficient);
        Assert.False(engine.Covers(chosen, "equipment.tool.flute").Proficient);
    }

    [Fact]
    public void SmithingAssociatedToolsDoNotConferEquipmentTraining()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        Assert.True(f.Catalog.TryGetById("competency.smithing", out var smithing));
        Assert.Equal("Smith's Tools",
            smithing!.Definition.GetProperty("associatedTools")[0].GetString());
        var grants = engine.Resolve(f.Request());
        Assert.False(engine.Covers(grants, "equipment.tool.smiths-tools").Proficient);
        // No separate prerequisite is invented for the Smithing Competency.
    }

    [Fact]
    public void MulticlassEntryUsesItsOwnAuthoredGrantList()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var grants = engine.Resolve(f.Request(
            new NativeEquipmentQualificationSource("class.barbarian", "enter-later", "multiclass")));
        Assert.True(engine.Covers(grants, "equipment.shield.shield").Proficient);
        Assert.False(engine.Covers(grants, "equipment.weapon.longsword").Proficient);
        var unavailable = engine.Resolve(f.Request(
            new NativeEquipmentQualificationSource("feature.sword", "wrong-mode", "multiclass")));
        Assert.Equal("unavailable", unavailable.Status);
        Assert.Empty(unavailable.Qualifications);
    }

    [Fact]
    public void InvalidChoicesDuplicatesAndUnknownReferencesFailClosed()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var request = f.Request(new NativeEquipmentQualificationSource("feature.entertainment", "A")) with {
            SelectedChoices = new Dictionary<string, IReadOnlyList<string>> {
                ["A/game"] = ["equipment.tool.lute"],
                ["A/music"] = ["equipment.tool.lute"]
            }
        };
        var bad = engine.Resolve(request);
        Assert.Equal("unavailable", bad.Status);
        Assert.Empty(bad.Qualifications);
        Assert.Contains(bad.Issues, issue => issue.Code == "invalid-equipment-selection");
        var duplicate = engine.Resolve(f.Request(
            new NativeEquipmentQualificationSource("class.rogue", "same"), new NativeEquipmentQualificationSource("feature.sword", "same")));
        Assert.Equal("unavailable", duplicate.Status);
        Assert.Contains(duplicate.Issues,
            issue => issue.Code == "invalid-equipment-source");
        Assert.Equal("unresolved",
            engine.Covers(engine.Resolve(f.Request()), "equipment.weapon.unknown").Status);
    }

    [Fact]
    public void ParentGroupsCoverChildTypesWithoutDuplicatingTraining()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var grants = engine.Resolve(f.Request(
            new NativeEquipmentQualificationSource("feature.all-tools", "generic")));
        Assert.True(engine.Covers(grants, "equipment.tool.thieves-tools").Proficient);
        Assert.True(engine.Covers(grants, "equipment.tool.dice-set").Proficient);
        Assert.True(engine.Covers(grants, "equipment.tool.lute").Proficient);
        Assert.False(engine.Covers(grants, "equipment.weapon.longsword").Proficient);
    }

    [Fact]
    public void RootGrantsAreRecognizedEvenWhenSourceAlsoHasDefinition()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var grants = engine.Resolve(f.Request(
            new NativeEquipmentQualificationSource("feature.root", "root")));
        Assert.Equal("resolved", grants.Status);
        Assert.True(engine.Covers(grants, "equipment.weapon.longsword").Proficient);
    }

    [Fact]
    public void UnapprovedConditionalGrantsNeverBecomeUnconditional()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var result = engine.Resolve(f.Request(
            new NativeEquipmentQualificationSource("feature.conditional", "conditional")));
        Assert.Equal("unavailable", result.Status);
        Assert.Empty(result.Qualifications);
        Assert.Contains(result.Issues, issue => issue.Code == "unsupported-equipment-grant");
    }

    [Fact]
    public void ExplicitTypeChoiceDoesNotAllowOtherWeaponsInItsCategory()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var request = f.Request(
            new NativeEquipmentQualificationSource("feature.gladiator", "origin"));
        var open = engine.Resolve(request);
        Assert.Equal("needs-choices", open.Status);
        Assert.Equal(["equipment.weapon.longsword"],
            Assert.Single(open.RequiredChoices).EligibleQualificationIds);
        var valid = engine.Resolve(request with
        {
            SelectedChoices = new Dictionary<string, IReadOnlyList<string>>
            {
                ["origin/arena"] = ["equipment.weapon.longsword"]
            }
        });
        Assert.Equal("resolved", valid.Status);
        Assert.True(engine.Covers(valid, "equipment.weapon.longsword").Proficient);
        Assert.False(engine.Covers(valid, "equipment.weapon.rapier").Proficient);

        var invalid = engine.Resolve(request with
        {
            SelectedChoices = new Dictionary<string, IReadOnlyList<string>>
            {
                ["origin/arena"] = ["equipment.weapon.rapier"]
            }
        });
        Assert.Equal("unavailable", invalid.Status);
        Assert.Empty(invalid.Qualifications);
    }

    [Fact]
    public void ExplicitTypeListsMustReferenceExistingMatchingTypes()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var result = engine.Resolve(f.Request(
            new NativeEquipmentQualificationSource("feature.invalid-choice", "origin")));
        Assert.Equal("unavailable", result.Status);
        Assert.Empty(result.Qualifications);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "unknown-equipment-type-choice");
    }

    [Fact]
    public void ArmorQualificationsCoverTheirGroupButNotOtherArmorGroups()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var grants = engine.Resolve(f.Request(
            new NativeEquipmentQualificationSource("feature.light-armor", "armor")));
        Assert.Equal("resolved", grants.Status);
        Assert.True(engine.Covers(grants, "equipment.armor.leather").Proficient);
        Assert.False(engine.Covers(grants, "equipment.armor.chain-mail").Proficient);
    }

    [Theory]
    [InlineData("feature.ambiguous", "ambiguous-equipment-grants")]
    [InlineData("feature.malformed", "invalid-equipment-grants")]
    [InlineData("feature.unknown-block", "unsupported-equipment-grants")]
    [InlineData("feature.multiclass-only", "equipment-grants-not-reconciled")]
    public void UnreconciledSourceBlocksNeverProduceSilentEmptyOrPartialTraining(
        string sourceId, string issueCode)
    {
        using var fixture = new Fixture();
        var engine = new NativeEquipmentQualifications(fixture.Catalog);

        // These sources deliberately contain enough plausible grant content to
        // regress into a false success if the resolver ignores an unsupported
        // field, chooses a preferred location, or falls back to a wrong mode.
        var result = engine.Resolve(fixture.Request(
            new NativeEquipmentQualificationSource(sourceId, "origin-1")));
        Assert.Equal("unavailable", result.Status);
        Assert.Empty(result.Qualifications);
        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
        var coverage = engine.Covers(result, "equipment.weapon.longsword");
        Assert.Equal("unavailable", coverage.Status);
        Assert.Null(coverage.Proficient);
    }

    [Fact]
    public void RepeatedSourcesCannotExpandTheWorkBudgetBeyondTheRequestLimit()
    {
        using var fixture = new Fixture();
        var engine = new NativeEquipmentQualifications(fixture.Catalog);
        var sources = Enumerable.Range(0, 67).Select(i =>
            new NativeEquipmentQualificationSource("feature.many-choices",
                "earned-" + i)).ToArray();

        // Each source is individually valid. The sum of its authored choices
        // exceeds the independent request work limit when repeated 67 times.
        var result = engine.Resolve(fixture.Request(sources));
        Assert.Equal("unavailable", result.Status);
        Assert.Empty(result.Qualifications);
        Assert.Contains(result.Issues, issue => issue.Code == "invalid-equipment-choices");
    }

    [Fact]
    public void FixedGrantOccurrencesAreAlsoBoundedAcrossRepeatedSources()
    {
        using var fixture = new Fixture();
        var engine = new NativeEquipmentQualifications(fixture.Catalog);
        var sources = Enumerable.Range(0, 91).Select(i =>
            new NativeEquipmentQualificationSource("feature.repeat-grants",
                "earned-" + i)).ToArray();

        var result = engine.Resolve(fixture.Request(sources));
        Assert.Equal("unavailable", result.Status);
        Assert.Empty(result.Qualifications);
        Assert.Contains(result.Issues, issue => issue.Code == "invalid-equipment-grants");
    }

    [Fact]
    public void InvalidCatalogGroupReferencesAreRejectedAtLoad()
    {
        Assert.Throws<NativeCatalogLoadException>(() => new Fixture("unknown"));
        Assert.Throws<NativeCatalogLoadException>(() => new Fixture("cycle"));
        Assert.Throws<NativeCatalogLoadException>(() => new Fixture("cross-category"));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "rules-core-equip-qualifications-" + Guid.NewGuid().ToString("N"));

        public NativeCatalogSnapshot Catalog { get; }

        public Fixture(string? invalid = null)
        {
            var package = Path.Combine(_root, "first-party-001");
            var native = Path.Combine(package, "native");
            Directory.CreateDirectory(native);
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            var equipment = """
                {
                  "equipmentGroups": [
                    {"id":"equipment.group.weapon.martial","name":"Martial Weapons","kind":"equipment-group","equipmentCategory":"weapon"},
                    {"id":"equipment.group.armor.light","name":"Light Armor","kind":"equipment-group","equipmentCategory":"armor"},
                    {"id":"equipment.group.armor.heavy","name":"Heavy Armor","kind":"equipment-group","equipmentCategory":"armor"},
                    {"id":"equipment.group.shield.all","name":"Shields","kind":"equipment-group","equipmentCategory":"shield"},
                    {"id":"equipment.group.tool.all","name":"Tools","kind":"equipment-group","equipmentCategory":"tool"},
                    {"id":"equipment.group.tool.gaming-set","name":"Gaming Sets","kind":"equipment-group","equipmentCategory":"tool","parentGroupIds":["equipment.group.tool.all"]},
                    {"id":"equipment.group.tool.musical-instrument","name":"Instruments","kind":"equipment-group","equipmentCategory":"tool","parentGroupIds":["equipment.group.tool.all"]}
                  ],
                  "equipmentTypes": [
                    {"id":"equipment.weapon.longsword","name":"Longsword","kind":"equipment-type","equipmentCategory":"weapon","groupIds":["equipment.group.weapon.martial"]},
                    {"id":"equipment.weapon.rapier","name":"Rapier","kind":"equipment-type","equipmentCategory":"weapon","groupIds":["equipment.group.weapon.martial"]},
                    {"id":"equipment.shield.shield","name":"Shield","kind":"equipment-type","equipmentCategory":"shield","groupIds":["equipment.group.shield.all"]},
                    {"id":"equipment.armor.leather","name":"Leather Armor","kind":"equipment-type","equipmentCategory":"armor","groupIds":["equipment.group.armor.light"]},
                    {"id":"equipment.armor.chain-mail","name":"Chain Mail","kind":"equipment-type","equipmentCategory":"armor","groupIds":["equipment.group.armor.heavy"]},
                    {"id":"equipment.tool.thieves-tools","name":"Thieves' Tools","kind":"equipment-type","equipmentCategory":"tool","groupIds":["equipment.group.tool.all"]},
                    {"id":"equipment.tool.smiths-tools","name":"Smith's Tools","kind":"equipment-type","equipmentCategory":"tool","groupIds":["equipment.group.tool.all"]},
                    {"id":"equipment.tool.dice-set","name":"Dice Set","kind":"equipment-type","equipmentCategory":"tool","groupIds":["equipment.group.tool.gaming-set"]},
                    {"id":"equipment.tool.playing-card-set","name":"Playing Cards","kind":"equipment-type","equipmentCategory":"tool","groupIds":["equipment.group.tool.gaming-set"]},
                    {"id":"equipment.tool.lute","name":"Lute","kind":"equipment-type","equipmentCategory":"tool","groupIds":["equipment.group.tool.musical-instrument"]},
                    {"id":"equipment.tool.flute","name":"Flute","kind":"equipment-type","equipmentCategory":"tool","groupIds":["equipment.group.tool.musical-instrument"]}
                  ]
                }
                """;
            if (invalid == "unknown")
                equipment = equipment.Replace("\"equipment.group.weapon.martial\"]",
                    "\"equipment.group.weapon.missing\"]", StringComparison.Ordinal);
            if (invalid == "cycle")
                equipment = equipment.Replace(
                    "\"equipment.group.tool.all\",\"name\":\"Tools\",\"kind\":\"equipment-group\",\"equipmentCategory\":\"tool\"",
                    "\"equipment.group.tool.all\",\"name\":\"Tools\",\"kind\":\"equipment-group\",\"equipmentCategory\":\"tool\",\"parentGroupIds\":[\"equipment.group.tool.gaming-set\"]",
                    StringComparison.Ordinal);
            if (invalid == "cross-category")
                equipment = equipment.Replace(
                    "\"equipment.group.weapon.martial\"]",
                    "\"equipment.group.armor.light\"]", StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(native, "equipment.json"), equipment);
            File.WriteAllText(Path.Combine(native, "sources.json"),
                """
                {"definitions":[
                  {"id":"class.barbarian","name":"Barbarian","kind":"class","definition":{
                    "equipmentProficiencyGrants":{"fixed":[
                      {"id":"sword-group","targetId":"equipment.group.weapon.martial"},
                      {"id":"shield","targetId":"equipment.group.shield.all"}
                    ],"multiclassFixed":[{"id":"shield","targetId":"equipment.group.shield.all"}]}}},
                  {"id":"class.rogue","name":"Rogue","kind":"class","definition":{
                    "equipmentProficiencyGrants":{"fixed":[
                      {"id":"tools","targetId":"equipment.tool.thieves-tools"}]}}},
                  {"id":"feature.sword","name":"Sword Training","kind":"feature","equipmentProficiencyGrants":{"fixed":[
                    {"id":"sword","targetId":"equipment.weapon.longsword"}]}},
                  {"id":"feature.second-sword","name":"More Sword Training","kind":"feature","equipmentProficiencyGrants":{"fixed":[
                    {"id":"another-sword","targetId":"equipment.weapon.longsword"}]}},
                  {"id":"feature.all-tools","name":"Every Tool","kind":"feature","equipmentProficiencyGrants":{"fixed":[
                    {"id":"all","targetId":"equipment.group.tool.all"}]}},
                  {"id":"feature.root","name":"Root Grant with Definition","kind":"feature",
                    "definition":{"note":"This is not the grant block"},
                    "equipmentProficiencyGrants":{"fixed":[
                      {"id":"sword","targetId":"equipment.weapon.longsword"}]}},
                  {"id":"feature.conditional","name":"Conditional Grant","kind":"feature",
                    "equipmentProficiencyGrants":{"fixed":[
                      {"id":"sword","targetId":"equipment.weapon.longsword",
                       "condition":{"requires":"unverified"}}]}},
                  {"id":"feature.gladiator","name":"Gladiator","kind":"feature","equipmentProficiencyGrants":{"choices":[
                    {"id":"arena","count":1,"fromCategory":"weapon",
                     "fromTypeIds":["equipment.weapon.longsword"]}]}},
                  {"id":"feature.invalid-choice","name":"Invalid Weapon Choice","kind":"feature","equipmentProficiencyGrants":{"choices":[
                    {"id":"arena","count":1,"fromCategory":"weapon",
                     "fromTypeIds":["equipment.weapon.unknown"]}]}},
                  {"id":"feature.light-armor","name":"Light Armor Training","kind":"feature","equipmentProficiencyGrants":{"fixed":[
                    {"id":"light","targetId":"equipment.group.armor.light"}]}},
                  {"id":"feature.ambiguous","name":"Ambiguous Grant Location","kind":"feature",
                    "equipmentProficiencyGrants":{"fixed":[
                      {"id":"sword","targetId":"equipment.weapon.longsword"}]},
                    "definition":{"equipmentProficiencyGrants":{"fixed":[
                      {"id":"other","targetId":"equipment.weapon.rapier"}]}}},
                  {"id":"feature.malformed","name":"Malformed Root Grants","kind":"feature",
                    "equipmentProficiencyGrants":[]},
                  {"id":"feature.unknown-block","name":"Unsupported Future Grants","kind":"feature",
                    "equipmentProficiencyGrants":{"fixed":[
                      {"id":"sword","targetId":"equipment.weapon.longsword"}],
                      "conditionalFixed":[{"id":"conditional","targetId":"equipment.weapon.rapier"}]}},
                  {"id":"feature.multiclass-only","name":"No Standard Grants","kind":"feature",
                    "equipmentProficiencyGrants":{"multiclassFixed":[
                      {"id":"sword","targetId":"equipment.weapon.longsword"}]}},
                  {"id":"feature.many-choices","name":"Several Qualifications","kind":"feature",
                    "equipmentProficiencyGrants":{"choices":[
                      {"id":"game","count":1,"fromCategory":"tool","fromGroupIds":["equipment.group.tool.gaming-set"]},
                      {"id":"music","count":1,"fromCategory":"tool","fromGroupIds":["equipment.group.tool.musical-instrument"]},
                      {"id":"weapon","count":1,"fromCategory":"weapon"}]}},
                  {"id":"feature.open-lock","name":"Open Lock Skill","kind":"feature","equipmentProficiencyGrants":{"fixed":[]}},
                  {"id":"feature.entertainment","name":"Entertainment","kind":"feature","equipmentProficiencyGrants":{"choices":[
                    {"id":"game","count":1,"fromCategory":"tool","fromGroupIds":["equipment.group.tool.gaming-set"]},
                    {"id":"music","count":1,"fromCategory":"tool","fromGroupIds":["equipment.group.tool.musical-instrument"]}
                  ]}},
                  {"id":"skill.open-lock","name":"Open Lock","kind":"skill"},
                  {"id":"competency.smithing","name":"Smithing","kind":"competency","associatedTools":["Smith's Tools"]},
                  {"id":"item.thieves-tools","name":"Owned Thieves' Tools","kind":"item","equipmentTypeId":"equipment.tool.thieves-tools"}
                ]}
                """);
            // A valid source repeated many times must still respect the total
            // work budget. Generate this fixture rather than authoring eleven
            // identical JSON records into every test case.
            var repeats = string.Join(",", Enumerable.Range(1, 11).Select(i =>
                "{\\\"id\\\":\\\"repeat-" + i + "\\\",\\\"targetId\\\":\\\"equipment.weapon.longsword\\\"}"));
            File.WriteAllText(Path.Combine(native, "repeat-grants.json"),
                "{\\\"id\\\":\\\"feature.repeat-grants\\\",\\\"name\\\":\\\"Repeating Grant\\\"," +
                "\\\"kind\\\":\\\"feature\\\",\\\"equipmentProficiencyGrants\\\":{\\\"fixed\\\":[" +
                repeats + "]}}");
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }

        public NativeEquipmentQualificationRequest Request(
            params NativeEquipmentQualificationSource[] sources) =>
            new("first-party-001", Catalog.Fingerprint, sources);

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
