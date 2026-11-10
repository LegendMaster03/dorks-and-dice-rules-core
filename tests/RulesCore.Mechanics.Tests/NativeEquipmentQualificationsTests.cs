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
        var grants = engine.Resolve(f.Request(new("class.barbarian", "initial")));
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
        var grants = engine.Resolve(f.Request(new("feature.sword", "one")));
        Assert.True(engine.Covers(grants, "equipment.weapon.longsword").Proficient);
        Assert.False(engine.Covers(grants, "equipment.weapon.rapier").Proficient);
    }

    [Fact]
    public void OverlappingAndRepeatedGrantsRemainBinaryWithProvenance()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var grants = engine.Resolve(f.Request(
            new("class.barbarian", "initial"), new("feature.sword", "other"),
            new("feature.second-sword", "third")));
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
        var rogue = engine.Resolve(f.Request(new("class.rogue", "first")));
        Assert.True(engine.Covers(rogue, "equipment.tool.thieves-tools").Proficient);
        Assert.True(engine.Covers(rogue, "item.thieves-tools").Proficient);

        // A learned Skill is not a tool-grant source, and owning a linked item
        // does not create the qualification fact.
        var skillOnly = engine.Resolve(f.Request(new("feature.open-lock", "skill")));
        Assert.False(engine.Covers(skillOnly, "equipment.tool.thieves-tools").Proficient);
        var empty = engine.Resolve(f.Request());
        Assert.False(engine.Covers(empty, "item.thieves-tools").Proficient);
    }

    [Fact]
    public void GamingAndInstrumentChoicesAreLimitedToTheirOwnTypes()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var request = f.Request(new("feature.entertainment", "background"));
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
            new("class.barbarian", "enter-later", "multiclass")));
        Assert.True(engine.Covers(grants, "equipment.shield.shield").Proficient);
        Assert.False(engine.Covers(grants, "equipment.weapon.longsword").Proficient);
        var unavailable = engine.Resolve(f.Request(
            new("feature.sword", "wrong-mode", "multiclass")));
        Assert.Equal("unavailable", unavailable.Status);
        Assert.Empty(unavailable.Qualifications);
    }

    [Fact]
    public void InvalidChoicesDuplicatesAndUnknownReferencesFailClosed()
    {
        using var f = new Fixture();
        var engine = new NativeEquipmentQualifications(f.Catalog);
        var request = f.Request(new("feature.entertainment", "A")) with {
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
            new("class.rogue", "same"), new("feature.sword", "same")));
        Assert.Equal("unavailable", duplicate.Status);
        Assert.Contains(duplicate.Issues,
            issue => issue.Code == "invalid-equipment-source");
        Assert.Equal("unresolved",
            engine.Covers(engine.Resolve(f.Request()), "equipment.weapon.unknown").Status);
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
                    {"id":"equipment.group.shield.all","name":"Shields","kind":"equipment-group","equipmentCategory":"shield"},
                    {"id":"equipment.group.tool.all","name":"Tools","kind":"equipment-group","equipmentCategory":"tool"},
                    {"id":"equipment.group.tool.gaming-set","name":"Gaming Sets","kind":"equipment-group","equipmentCategory":"tool","parentGroupIds":["equipment.group.tool.all"]},
                    {"id":"equipment.group.tool.musical-instrument","name":"Instruments","kind":"equipment-group","equipmentCategory":"tool","parentGroupIds":["equipment.group.tool.all"]}
                  ],
                  "equipmentTypes": [
                    {"id":"equipment.weapon.longsword","name":"Longsword","kind":"equipment-type","equipmentCategory":"weapon","groupIds":["equipment.group.weapon.martial"]},
                    {"id":"equipment.weapon.rapier","name":"Rapier","kind":"equipment-type","equipmentCategory":"weapon","groupIds":["equipment.group.weapon.martial"]},
                    {"id":"equipment.shield.shield","name":"Shield","kind":"equipment-type","equipmentCategory":"shield","groupIds":["equipment.group.shield.all"]},
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
