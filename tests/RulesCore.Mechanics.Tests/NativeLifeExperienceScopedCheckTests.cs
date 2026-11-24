using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeLifeExperienceScopedCheckTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var root = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native", "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("First-party native ruleset missing.");
    }

    [Theory]
    [InlineData("upbringing.dwarf.general", "stonework-history", 0, false, true)]
    [InlineData("upbringing.dwarf.general", "stonework-history", 3, true, true)]
    [InlineData("upbringing.dwarf.general", "stonework-history", 6, true, true)]
    [InlineData("upbringing.gnome.rock", "magic-alchemy-technology-objects", 0, false, false)]
    [InlineData("upbringing.gnome.rock", "magic-alchemy-technology-objects", 3, true, true)]
    [InlineData("upbringing.gnome.rock", "magic-alchemy-technology-objects", 6, true, true)]
    public void HistoryReplacesOrdinaryProficiencyEvenIfUntrained(
        string upbringing, string scope, int ordinaryContribution, bool normallyProficient, bool expectedProficient)
    {
        var sources = new Dictionary<string, string> { [upbringing] = "verified-cultural-source" };
        if (upbringing == "upbringing.gnome.rock") sources["upbringing.gnome.general"] = "verified-parent";
        var input = new NativeScopedSkillCheckInput(upbringing, sources, "skill.history", "intelligence",
            [scope], 3, ordinaryContribution, 4 + ordinaryContribution, normallyProficient);
        var resolver = new NativeScopedSkillCheckResolver(Catalog());
        var result = resolver.Resolve(input);
        Assert.Equal("resolved", result.Status);
        Assert.Equal(10, result.Total);
        Assert.Equal(6, result.ProficiencyContribution);
        Assert.Equal(0, result.FlatBonus);
        Assert.Equal(expectedProficient, result.ConsideredProficientOnCheck);
        Assert.Equal("verified-cultural-source", Assert.Single(result.AppliedEffects).OccurrenceId);
        Assert.Equal(ordinaryContribution, input.OrdinaryProficiencyContribution);
        var unrelated = resolver.Resolve(input with { ActivityScopeIds = ["unrelated-history"] });
        Assert.Equal(4 + ordinaryContribution, unrelated.Total);
        Assert.Equal(normallyProficient, unrelated.ConsideredProficientOnCheck);
        Assert.Empty(unrelated.AppliedEffects);
        Assert.Equal("unresolved", resolver.Resolve(input with
        { CompetingProficiencyReplacement = true }).Status);
    }

    [Theory]
    [InlineData("upbringing.elf.wood", "forest-foraging")]
    [InlineData("upbringing.elf.drow", "underground-tracking")]
    public void SurvivalBonusesStackWithTrainingButNotMerelyLocation(string upbringing, string scope)
    {
        var sources = new Dictionary<string, string>
        {
            [upbringing] = "verified-child", ["upbringing.elf.general"] = "verified-parent"
        };
        var resolver = new NativeScopedSkillCheckResolver(Catalog());
        var input = new NativeScopedSkillCheckInput(upbringing, sources, "skill.survival", "intelligence",
            [scope], 3, 3, 7, true);
        var result = resolver.Resolve(input);
        Assert.Equal("resolved", result.Status);
        Assert.Equal(9, result.Total);
        Assert.Equal(3, result.ProficiencyContribution);
        Assert.Equal(2, result.FlatBonus);
        Assert.True(result.ConsideredProficientOnCheck);
        Assert.Equal("flat-bonus", Assert.Single(result.AppliedEffects).Kind);
        var untrained = resolver.Resolve(input with
        { NormallyProficient = false, OrdinaryProficiencyContribution = 0, OrdinaryTotal = 4 });
        Assert.Equal(6, untrained.Total);
        Assert.False(untrained.ConsideredProficientOnCheck);
        var mereLocation = resolver.Resolve(input with { ActivityScopeIds = ["in-forest", "underground"] });
        Assert.Equal(7, mereLocation.Total);
        Assert.Empty(mereLocation.AppliedEffects);
        sources.Remove("upbringing.elf.general");
        Assert.Equal("invalid", resolver.Resolve(input).Status);
    }

    [Fact]
    public void OnlyOneEffectiveUpbringingAndAnExactVerifiedOccurrenceChainAreAccepted()
    {
        var resolver = new NativeScopedSkillCheckResolver(Catalog());
        var input = new NativeScopedSkillCheckInput("upbringing.elf.wood",
            new Dictionary<string, string>
            {
                ["upbringing.elf.wood"] = "wood", ["upbringing.elf.general"] = "elf",
                ["upbringing.elf.drow"] = "drow"
            }, "skill.survival", "wisdom", ["forest-navigation", "underground-navigation"],
            3, 0, 3, false);
        Assert.Equal("invalid", resolver.Resolve(input).Status);
        var invalidParent = input with
        { UpbringingId = "upbringing.elf.general",
            SourceOccurrenceIds = new Dictionary<string, string> { ["upbringing.elf.general"] = "elf" } };
        Assert.Equal("invalid", resolver.Resolve(invalidParent).Status);
    }

    [Fact]
    public void EveryNativeSelectableLifeExperienceCanBeDiscoveredWithoutInventingGrants()
    {
        var catalog = Catalog();
        var resolver = new NativeLifeExperienceResolver(catalog);
        var backgrounds = catalog.Search(category: "background", offset: 0, limit: 100).Items
            .Where(entity => entity.Definition.GetProperty("selectable").GetBoolean()).ToArray();
        var upbringings = catalog.Search(category: "upbringing", offset: 0, limit: 100).Items
            .Where(entity => entity.Definition.GetProperty("selectable").GetBoolean()).ToArray();
        Assert.NotEmpty(backgrounds);
        Assert.NotEmpty(upbringings);
        // Each source is discovered against the active catalog, without
        // inventing a completed selection or requiring proof of inventory.
        foreach (var background in backgrounds)
        {
            var result = resolver.Resolve(new("upbringing.elf.wood", background.Id));
            Assert.NotEqual("invalid", result.Status);
            Assert.Contains(result.Sources, source => source.SourceId == background.Id);
            Assert.NotEmpty(result.StartingItems);
            Assert.Null(result.EarnedProjection);
        }
        foreach (var upbringing in upbringings)
        {
            var result = resolver.Resolve(new(upbringing.Id, "background.folk-hero"));
            Assert.NotEqual("invalid", result.Status);
            Assert.Contains(result.Sources, source => source.SourceId == upbringing.Id && !source.Inherited);
            Assert.Equal(result.Sources.Count, result.Sources.Select(source => source.SourceId).Distinct().Count());
            Assert.Null(result.EarnedProjection);
        }
    }

    [Fact]
    public void DuplicateSpecificToolsRetainExactSourceProvenanceAndRequireADistinctTool()
    {
        var resolver = new NativeLifeExperienceResolver(Catalog());
        var selections = new Dictionary<string, IReadOnlyList<string>>
        {
            ["upbringing.street-raised/street-raised-formative-skill"] = ["skill.perception"],
            ["upbringing.street-raised/street-raised-community-competency"] = ["thieves-tools"],
            ["background.spy/spy-field-skills"] = ["skill.deception", "skill.stealth"],
            ["background.spy/spy-tradecraft-competency"] = ["thieves-tools"]
        };
        var proposal = new NativeLifeExperienceProposal("upbringing.street-raised", "background.spy", selections);
        var discovery = resolver.Resolve(proposal);
        var choice = Assert.Single(discovery.Choices, c => c.Kind == "duplicate-tool");
        Assert.DoesNotContain("equipment.tool.thieves-tools", choice.OptionIds);
        Assert.Contains("equipment.tool.flute", choice.OptionIds);
        Assert.DoesNotContain(choice.OptionIds, id => id.StartsWith("equipment.group.", StringComparison.Ordinal));
        selections[choice.Key] = ["equipment.tool.flute"];
        var result = resolver.Resolve(proposal);
        Assert.NotEqual("invalid", result.Status);
        var replacement = Assert.Single(result.EquipmentReplacements);
        Assert.Equal("equipment.tool.thieves-tools", replacement.OriginalQualificationId);
        Assert.Equal("equipment.tool.flute", replacement.ReplacementQualificationId);
        var qualifications = result.CandidateEquipment!.Qualifications;
        Assert.Equal(2, qualifications.Count);
        var original = Assert.Single(qualifications, item => item.QualificationId == "equipment.tool.thieves-tools");
        Assert.Single(original.Provenance);
        var alternate = Assert.Single(qualifications, item => item.QualificationId == "equipment.tool.flute");
        Assert.Equal(replacement.Provenance, Assert.Single(alternate.Provenance));
        Assert.DoesNotContain(result.CandidateGrants, grant => grant.Kind == "competency-training");
        Assert.Null(result.EarnedProjection);

        selections[choice.Key] = ["equipment.tool.thieves-tools"];
        Assert.Equal("invalid", resolver.Resolve(proposal).Status);
    }

    [Theory]
    [InlineData("calligraphy", "competency.calligraphy")]
    [InlineData("painting", "competency.painting")]
    [InlineData("cooking", "competency.cooking")]
    public void AristocraticCompetencyBranchesDoNotLeakGamingSetQualification(string selected, string expected)
    {
        var resolver = new NativeLifeExperienceResolver(Catalog());
        var selections = new Dictionary<string, IReadOnlyList<string>>
        {
            ["upbringing.aristocratic/aristocratic-formative-skill"] = ["skill.history"],
            ["upbringing.aristocratic/aristocratic-formative-competency"] = [selected]
        };
        var proposal = new NativeLifeExperienceProposal("upbringing.aristocratic", "background.folk-hero", selections);
        var result = resolver.Resolve(proposal);
        Assert.NotEqual("invalid", result.Status);
        Assert.DoesNotContain(result.Choices, choice => choice.Key.EndsWith("/aristocratic-gaming-set"));
        Assert.Empty(result.CandidateEquipment!.Qualifications);
        var grant = Assert.Single(result.CandidateGrants, grant => grant.Kind == "competency-training");
        Assert.Equal(expected, grant.TargetId);
        Assert.True(grant.Proficient);
        Assert.Equal(0, grant.Ranks);
        selections["upbringing.aristocratic/equipment/aristocratic-gaming-set"] = ["equipment.tool.flute"];
        Assert.Equal("invalid", resolver.Resolve(proposal).Status);
    }

    [Fact]
    public void LifeExperienceDuplicateSkillChoiceIsSourceQualifiedAndNeverAnExtraRank()
    {
        var resolver = new NativeLifeExperienceResolver(Catalog());
        var selections = new Dictionary<string, IReadOnlyList<string>>
        {
            ["background.soldier/equipment/soldier-gaming-set"] = ["equipment.tool.dice-set"]
        };
        var proposal = new NativeLifeExperienceProposal("upbringing.giant-foundling", "background.soldier", selections);
        var discovery = resolver.Resolve(proposal);
        var duplicate = Assert.Single(discovery.Choices, choice => choice.Kind == "duplicate-skill");
        Assert.DoesNotContain("skill.intimidation", duplicate.OptionIds);
        Assert.DoesNotContain("skill.athletics", duplicate.OptionIds);
        Assert.DoesNotContain("skill.survival", duplicate.OptionIds);
        selections[duplicate.Key] = ["skill.insight"];
        var result = resolver.Resolve(proposal);
        Assert.NotEqual("invalid", result.Status);
        var replaced = Assert.Single(result.CandidateGrants, grant => grant.OriginalTargetId == "skill.intimidation");
        Assert.Equal("skill.insight", replaced.TargetId);
        Assert.True(replaced.Proficient);
        Assert.Equal(0, replaced.Ranks);
        Assert.Single(result.CandidateGrants, grant => grant.Kind == "skill-training" && grant.TargetId == "skill.intimidation");
        Assert.Null(result.EarnedProjection);
    }
}
