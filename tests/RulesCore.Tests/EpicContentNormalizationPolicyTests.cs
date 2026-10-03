using System.Text;
using System.Text.Json;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Tests;

public sealed class EpicContentNormalizationPolicyTests
{
    [Fact]
    public void FiveEToolsEpicBoonFeatNormalizesToEpicFeatWithoutChangingRawSource()
    {
        const string raw = """
            {"name":"Boon of Combat Prowess","source":"XPHB","category":"EB","prerequisite":[{"level":19}]}
            """;
        var record = Record("feat", "Boon of Combat Prowess", raw, raw);
        var normalized = EpicContentNormalizationPolicy.Apply(
            Representation(FiveEToolsSourceFormatAdapter.Format, record),
            record);

        Assert.Equal("feat", normalized.EntityType);
        Assert.Equal("Boon of Combat Prowess", normalized.Name);
        Assert.Equal(raw, normalized.RawJson);

        using var content = JsonDocument.Parse(normalized.ContentJson!);
        Assert.Equal("Epic", content.RootElement.GetProperty("category").GetString());
        var epic = content.RootElement.GetProperty("_rulesCore").GetProperty("epic");
        Assert.Equal("epic", epic.GetProperty("tier").GetString());
        Assert.Equal("feat", epic.GetProperty("kind").GetString());
        Assert.Equal("Epic Feat", epic.GetProperty("canonicalTerm").GetString());
        Assert.Equal("Epic Boon Feat", epic.GetProperty("sourceTerm").GetString());
        Assert.Equal("EB", epic.GetProperty("sourceCategory").GetString());

        var category = Assert.Single(
            RuleBrowserSummaryProjector.Project("feat", content.RootElement)
                .Where(value => value.Key == "category"));
        Assert.Equal("Epic Feat", category.Value);
    }

    [Fact]
    public void LegacyEpicBaseClassBecomesContinuationProgression()
    {
        const string raw = """
            {
              "name":"Epic Barbarian",
              "source":"SRD35",
              "uniqueId":"legacy-epic-barbarian",
              "documentUri":"https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/epic/epic-classes.md",
              "headingLevel":2,
              "originalHeading":"Epic Barbarian",
              "body":"Hit Die: d12. Skill Points at Each Additional Level: 4 + Int modifier. Bonus feats continue after 20th level."
            }
            """;
        const string translated = """
            {"name":"Epic Barbarian","source":"SRD35","entries":["Epic progression."],"_rulesCore":{"context":{"sourceFormat":"legacy-srd-snapshot","nativeEntityType":"class","edition":"3.5e"}}}
            """;
        var record = Record("class", "Epic Barbarian", raw, translated);
        var normalized = EpicContentNormalizationPolicy.Apply(
            Representation(LegacySrdSourceFormatAdapter.Format, record),
            record);

        Assert.Equal(RuleConceptEntityTypes.ClassProgression, normalized.EntityType);
        Assert.Equal("Barbarian", normalized.Name);
        Assert.Equal(raw, normalized.RawJson);
        Assert.True(EpicContentNormalizationPolicy.IsReviewedIdentityMigration(
            "class",
            "Epic Barbarian",
            normalized));

        using var content = JsonDocument.Parse(normalized.ContentJson!);
        Assert.Equal("Barbarian", content.RootElement.GetProperty("name").GetString());
        var epic = content.RootElement.GetProperty("_rulesCore").GetProperty("epic");
        Assert.Equal("progression", epic.GetProperty("kind").GetString());
        Assert.Equal(20, epic.GetProperty("startsAfterClassLevel").GetInt32());
        var continuation = epic.GetProperty("continuationOf");
        Assert.Equal("class", continuation.GetProperty("entityType").GetString());
        Assert.Equal("Barbarian", continuation.GetProperty("name").GetString());

        var fields = RuleBrowserSummaryProjector.Project(
            RuleConceptEntityTypes.ClassProgression,
            content.RootElement);
        Assert.Contains(fields, value => value.Key == "tier" && value.Value == "Epic");
        Assert.Contains(fields, value => value.Key == "continues" && value.Value == "Barbarian");
    }

    [Fact]
    public void LegacyEpicPrestigeProgressionIsSeparatedFromTrueEpicPrestigeClass()
    {
        const string progressionRaw = """
            {
              "name":"Epic Arcane Archer",
              "source":"SRD35",
              "uniqueId":"legacy-epic-arcane-archer",
              "documentUri":"https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/epic/epic-prestige-classes.md",
              "headingLevel":2,
              "originalHeading":"Epic Arcane Archer",
              "body":"Hit Die: d8. Skill Points at Each Additional Level: 4 + Int modifier."
            }
            """;
        const string trueClassRaw = """
            {
              "name":"Epic Infiltrator",
              "source":"SRD35",
              "uniqueId":"legacy-epic-infiltrator",
              "documentUri":"https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/epic/epic-prestige-classes.md",
              "headingLevel":2,
              "originalHeading":"Epic Infiltrator",
              "body":"Hit Die: d6. Skill Points at Each Level: 8 + Int modifier."
            }
            """;
        const string progressionContent = """
            {"name":"Epic Arcane Archer","source":"SRD35","_rulesCore":{"context":{"sourceFormat":"legacy-srd-snapshot","nativeEntityType":"prestigeClass","edition":"3.5e"}}}
            """;
        const string trueClassContent = """
            {"name":"Epic Infiltrator","source":"SRD35","_rulesCore":{"context":{"sourceFormat":"legacy-srd-snapshot","nativeEntityType":"prestigeClass","edition":"3.5e"}}}
            """;

        var progression = Record("prestigeClass", "Epic Arcane Archer", progressionRaw, progressionContent);
        var trueClass = Record("prestigeClass", "Epic Infiltrator", trueClassRaw, trueClassContent);

        var normalizedProgression = EpicContentNormalizationPolicy.Apply(
            Representation(LegacySrdSourceFormatAdapter.Format, progression),
            progression);
        var normalizedTrueClass = EpicContentNormalizationPolicy.Apply(
            Representation(LegacySrdSourceFormatAdapter.Format, trueClass),
            trueClass);

        Assert.Equal(RuleConceptEntityTypes.PrestigeClassProgression, normalizedProgression.EntityType);
        Assert.Equal("Arcane Archer", normalizedProgression.Name);
        Assert.Equal(RuleConceptEntityTypes.PrestigeClass, normalizedTrueClass.EntityType);
        Assert.Equal("Epic Infiltrator", normalizedTrueClass.Name);

        using var content = JsonDocument.Parse(normalizedTrueClass.ContentJson!);
        var epic = content.RootElement.GetProperty("_rulesCore").GetProperty("epic");
        Assert.Equal("epic", epic.GetProperty("tier").GetString());
        Assert.Equal("prestige-class", epic.GetProperty("kind").GetString());
    }

    [Fact]
    public void LegacyAndPcGenEpicFeatsShareCanonicalFeatTerminology()
    {
        const string legacyRaw = """
            {
              "name":"Armor Skin",
              "source":"SRD35",
              "uniqueId":"legacy-armor-skin",
              "documentUri":"https://raw.githubusercontent.com/olimot/srd-v3.5-md/main/epic/epic-feats.md",
              "headingLevel":3,
              "originalHeading":"Armor Skin [Epic]",
              "body":"Benefit: Natural armor improves."
            }
            """;
        const string legacyContent = """
            {"name":"Armor Skin","source":"SRD35","_rulesCore":{"context":{"sourceFormat":"legacy-srd-snapshot","nativeEntityType":"feat","edition":"3.5e"}}}
            """;
        var legacy = Record("feat", "Armor Skin", legacyRaw, legacyContent);

        const string pcGenRaw = """
            {
              "format":"pcgen-data",
              "path":"data/35e/wizards_of_the_coast/rsrd/epic/rsrd_feats_epic.lst",
              "name":"Armor Skin",
              "entityType":"ability",
              "segments":[
                {"Index":0,"LineNumber":1,"Level":null,"Tag":"CATEGORY","Value":"FEAT","Raw":"CATEGORY:FEAT"},
                {"Index":1,"LineNumber":1,"Level":null,"Tag":"TYPE","Value":"Epic","Raw":"TYPE:Epic"}
              ]
            }
            """;
        const string pcGenContent = """
            {"name":"Armor Skin","source":"RSRD","_rulesCore":{"context":{"sourceFormat":"pcgen-data","nativeEntityType":"ability","translatedEntityType":"feat","edition":"3.5e"}}}
            """;
        var pcGen = Record("feat", "Armor Skin", pcGenRaw, pcGenContent);

        AssertEpicFeat(EpicContentNormalizationPolicy.Apply(
            Representation(LegacySrdSourceFormatAdapter.Format, legacy),
            legacy));
        AssertEpicFeat(EpicContentNormalizationPolicy.Apply(
            Representation(PcGenSourceFormatAdapter.Format, pcGen),
            pcGen));
    }

    [Fact]
    public void ReviewedPcGenRsrdEpicClassRecordsBecomePrestigeClasses()
    {
        const string raw = """
            {
              "format":"pcgen-data",
              "kind":"class-record",
              "path":"data/35e/wizards_of_the_coast/rsrd/epic/rsrd_classes_epic.lst",
              "name":"Legendary Dreadnought",
              "entityType":"class",
              "segments":[
                {"Index":0,"LineNumber":1,"Level":null,"Tag":"TYPE","Value":"Epic.PC","Raw":"TYPE:Epic.PC"},
                {"Index":1,"LineNumber":2,"Level":null,"Tag":"PRELEVEL","Value":"MIN=20","Raw":"PRELEVEL:MIN=20"}
              ]
            }
            """;
        const string translated = """
            {"name":"Legendary Dreadnought","source":"RSRD","hd":{"number":1,"faces":12},"_rulesCore":{"context":{"sourceFormat":"pcgen-data","nativeEntityType":"class","edition":"3.5e"}}}
            """;
        var record = Record("class", "Legendary Dreadnought", raw, translated);
        var normalized = EpicContentNormalizationPolicy.Apply(
            Representation(PcGenSourceFormatAdapter.Format, record),
            record);

        Assert.Equal(RuleConceptEntityTypes.PrestigeClass, normalized.EntityType);
        Assert.Equal("Legendary Dreadnought", normalized.Name);
        Assert.True(EpicContentNormalizationPolicy.IsReviewedIdentityMigration(
            "class",
            "Legendary Dreadnought",
            normalized));
    }

    [Fact]
    public void IdentityMigrationRejectsUnrelatedClassChanges()
    {
        const string raw = """
            {"name":"Ordinary Class","source":"TEST"}
            """;
        const string content = """
            {"name":"Ordinary Class","source":"TEST"}
            """;
        var normalized = Record("prestigeClass", "Ordinary Class", raw, content);

        Assert.False(EpicContentNormalizationPolicy.IsReviewedIdentityMigration(
            "class",
            "Ordinary Class",
            normalized));
    }

    private static void AssertEpicFeat(NormalizedSourceRecord record)
    {
        Assert.Equal("feat", record.EntityType);
        using var content = JsonDocument.Parse(record.ContentJson!);
        Assert.Equal("Epic", content.RootElement.GetProperty("category").GetString());
        var epic = content.RootElement.GetProperty("_rulesCore").GetProperty("epic");
        Assert.Equal("Epic Feat", epic.GetProperty("canonicalTerm").GetString());
    }

    private static NormalizedSourceRecord Record(
        string entityType,
        string name,
        string rawJson,
        string contentJson) =>
        new(
            entityType,
            name,
            "TEST",
            $"test|{Guid.NewGuid():N}",
            rawJson)
        {
            ContentJson = contentJson
        };

    private static NormalizedSourceRepresentation Representation(
        string format,
        NormalizedSourceRecord record) =>
        new(
            format,
            new SourceRepresentationArtifact(
                "fixture.json",
                Encoding.UTF8.GetBytes(record.RawJson),
                $"test:{Guid.NewGuid():N}"),
            [record]);
}
