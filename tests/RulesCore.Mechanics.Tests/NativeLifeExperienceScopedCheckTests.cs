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
