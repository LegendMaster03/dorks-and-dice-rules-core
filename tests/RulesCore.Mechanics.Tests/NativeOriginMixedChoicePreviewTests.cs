using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Tests only source-authored candidate previews. They do not establish
/// a character's acquisition, approve client requests, or apply any grants.
/// </summary>
public sealed class NativeOriginMixedChoicePreviewTests
{
    private static readonly Lazy<NativeCatalogSnapshot> SharedCatalog =
        new(() => NativeCatalogLoader.Load(RepositoryLocation()));

    private static NativeOriginMixedChoicePreviewer Previewer =>
        new(SharedCatalog.Value);

    [Theory]
    [InlineData("background.spy", "spy-tradecraft-competency", "disguise",
        "competency.disguise", 1, false)]
    [InlineData("background.spy", "spy-tradecraft-competency", "forgery",
        "competency.forgery", 1, false)]
    [InlineData("background.spy", "spy-tradecraft-competency", "calligraphy",
        "competency.calligraphy", 1, false)]
    [InlineData("upbringing.street-raised", "street-raised-community-competency",
        "disguise", "competency.disguise", 0, true)]
    [InlineData("upbringing.street-raised", "street-raised-community-competency",
        "cooking", "competency.cooking", 0, true)]
    [InlineData("upbringing.street-raised", "street-raised-community-competency",
        "forgery", "competency.forgery", 0, true)]
    [InlineData("upbringing.aristocratic", "aristocratic-formative-competency",
        "cooking", "competency.cooking", 0, true)]
    [InlineData("upbringing.aristocratic", "aristocratic-formative-competency",
        "calligraphy", "competency.calligraphy", 0, true)]
    [InlineData("upbringing.aristocratic", "aristocratic-formative-competency",
        "painting", "competency.painting", 0, true)]
    [InlineData("background.entertainer", "entertainer-performance-method",
        "disguise-kit", "competency.disguise", 1, false)]
    [InlineData("background.guild-merchant", "guild-merchant-professional-choice",
        "navigation", "competency.navigation", 1, false)]
    public void CompetencyBranchesPreviewExactlyTheAuthoredRankOrProficiency(
        string source, string benefit, string option, string competency, int ranks, bool trained)
    {
        var result = Previewer.Preview(Request(source, benefit, option));
        Assert.Equal("preview", result.Status);
        Assert.Empty(result.Issues);
        Assert.Empty(result.RequiredEquipmentChoices);
        var candidate = Assert.Single(result.CandidateEffects);
        Assert.Equal("competency", candidate.Domain);
        Assert.Equal(competency, candidate.TargetId);
        Assert.Equal(ranks, candidate.Ranks);
        Assert.Equal(trained, candidate.Proficient);
        Assert.Equal(source, candidate.SourceId);
        Assert.Equal("asserted-not-authorized", candidate.SourceOccurrenceId);
        Assert.Equal(benefit, candidate.BenefitId);
        Assert.Equal(option, candidate.OptionId);
    }

    [Theory]
    [InlineData("background.spy", "spy-tradecraft-competency", "thieves-tools",
        "spy-tradecraft-thieves-tools")]
    [InlineData("upbringing.street-raised", "street-raised-community-competency",
        "thieves-tools", "street-raised-thieves-tools")]
    public void ThievesToolsBranchesAreFixedAndDoNotRequireAnotherSelection(
        string source, string benefit, string option, string grantId)
    {
        var result = Previewer.Preview(Request(source, benefit, option));
        Assert.Equal("preview", result.Status);
        Assert.Empty(result.RequiredEquipmentChoices);
        var candidate = Assert.Single(result.CandidateEffects);
        Assert.Equal("equipment-proficiency", candidate.Domain);
        Assert.Equal("equipment.tool.thieves-tools", candidate.TargetId);
        Assert.Equal(grantId, candidate.GrantId);
        Assert.Equal(option, candidate.OptionId);
    }

    [Fact]
    public void MerchantLanguageIsOnlyAConstrainedChoiceNotNavigationTraining()
    {
        var result = Previewer.Preview(Request("background.guild-merchant",
            "guild-merchant-professional-choice", "additional-trade-language"));
        Assert.Equal("preview", result.Status);
        var candidate = Assert.Single(result.CandidateEffects);
        Assert.Equal("language-choice", candidate.Domain);
        Assert.Null(candidate.TargetId);
        Assert.Equal(1, candidate.Count);
        Assert.Equal("learned-through-commercial-dealings", candidate.ChoiceConstraint);
    }

    [Theory]
    [InlineData("background.entertainer", "entertainer-performance-method",
        "musical-instrument", "entertainer-musical-instrument", "equipment.tool.lute")]
    [InlineData("upbringing.aristocratic", "aristocratic-formative-competency",
        "gaming-set", "aristocratic-gaming-set", "equipment.tool.dice-set")]
    public void NestedEquipmentBranchNeedsOneEligibleCanonicalType(
        string source, string benefit, string option, string grantId, string chosenType)
    {
        var preview = Previewer;
        var missing = preview.Preview(Request(source, benefit, option));
        Assert.Equal("needs-choices", missing.Status);
        Assert.Empty(missing.CandidateEffects);
        Assert.Empty(missing.Issues);
        var required = Assert.Single(missing.RequiredEquipmentChoices);
        Assert.Equal(grantId, required.GrantId);
        Assert.Equal(1, required.Count);
        Assert.Contains(chosenType, required.EligibleEquipmentTypeIds);

        var request = Request(source, benefit, option) with
        {
            SelectedEquipmentTypes = new Dictionary<string, IReadOnlyList<string>>
            {
                [grantId] = [chosenType]
            }
        };
        var chosen = preview.Preview(request);
        Assert.Equal("preview", chosen.Status);
        Assert.Empty(chosen.Issues);
        var grant = Assert.Single(chosen.CandidateEffects);
        Assert.Equal("equipment-proficiency", grant.Domain);
        Assert.Equal(chosenType, grant.TargetId);
        Assert.Equal(grantId, grant.GrantId);
    }

    [Fact]
    public void MissingOuterOptionYieldsOnlyChoicesNeverPartialGrants()
    {
        var missing = Previewer.Preview(Request("background.spy",
            "spy-tradecraft-competency", null));
        Assert.Equal("needs-choices", missing.Status);
        Assert.Empty(missing.CandidateEffects);
        Assert.Equal(["calligraphy", "disguise", "forgery", "thieves-tools"],
            missing.EligibleOptionIds);
        Assert.Empty(missing.RequiredEquipmentChoices);
    }

    [Theory]
    [InlineData("background.entertainer", "entertainer-performance-method",
        "musical-instrument", "entertainer-musical-instrument",
        "equipment.tool.dice-set")]
    [InlineData("upbringing.aristocratic", "aristocratic-formative-competency",
        "gaming-set", "aristocratic-gaming-set", "equipment.tool.lute")]
    public void UnrelatedToolTypeCannotBeSmuggledIntoNestedEquipmentSelection(
        string source, string benefit, string option, string grantId, string disallowed)
    {
        var result = Previewer.Preview(Request(source, benefit, option) with
        {
            SelectedEquipmentTypes = new Dictionary<string, IReadOnlyList<string>>
            {
                [grantId] = [disallowed]
            }
        });
        Assert.Equal("invalid", result.Status);
        Assert.Empty(result.CandidateEffects);
        Assert.Contains(result.Issues,
            issue => issue.Code == "invalid-origin-equipment-selection");
    }

    [Fact]
    public void ExtraSelectionOrForgedOptionCannotGrantOtherBranches()
    {
        var spy = Request("background.spy", "spy-tradecraft-competency", "disguise");
        var injected = Previewer.Preview(spy with
        {
            SelectedEquipmentTypes = new Dictionary<string, IReadOnlyList<string>>
            {
                ["spy-tradecraft-thieves-tools"] = ["equipment.tool.thieves-tools"]
            }
        });
        Assert.NotEqual("preview", injected.Status);
        Assert.Empty(injected.CandidateEffects);

        var forged = Previewer.Preview(spy with { OptionId = "thieves-tools-admin" });
        Assert.Equal("invalid", forged.Status);
        Assert.Empty(forged.CandidateEffects);
    }

    [Fact]
    public void FingerprintMismatchAndNonOriginSourcesFailClosed()
    {
        var request = Request("background.spy", "spy-tradecraft-competency", "thieves-tools");
        var stale = Previewer.Preview(request with
        {
            Fingerprint = new string('0', 64)
        });
        Assert.Equal("invalid", stale.Status);
        Assert.Empty(stale.CandidateEffects);
        Assert.Contains(stale.Issues,
            issue => issue.Code == "ruleset-version-mismatch");

        var wrong = Previewer.Preview(request with { SourceId = "class.rogue" });
        Assert.Equal("unavailable", wrong.Status);
        Assert.Empty(wrong.CandidateEffects);
    }

    [Fact]
    public void CrossSourceOptionIdCannotSelectAForeignBenefit()
    {
        var request = Request("background.spy",
            "spy-tradecraft-competency", "cooking");
        var result = Previewer.Preview(request);
        Assert.Equal("invalid", result.Status);
        Assert.Empty(result.CandidateEffects);

        var unrelated = Previewer.Preview(request with
        {
            BenefitId = "street-raised-community-competency",
            OptionId = "thieves-tools"
        });
        Assert.Equal("unavailable", unrelated.Status);
        Assert.Empty(unrelated.CandidateEffects);
    }

    private static NativeOriginMixedChoiceRequest Request(
        string source, string benefit, string? option) =>
        new(SharedCatalog.Value.RulesetId, SharedCatalog.Value.Fingerprint,
            source, "asserted-not-authorized", benefit, option);

    private static NativeRulesetLocation RepositoryLocation()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RulesCore.slnx")))
                return NativeRulesetLayout.Resolve(
                    Path.Combine(dir.FullName, "data", "rulesets"), "first-party-001");
        }
        throw new InvalidOperationException("The tests require a repository checkout.");
    }
}
