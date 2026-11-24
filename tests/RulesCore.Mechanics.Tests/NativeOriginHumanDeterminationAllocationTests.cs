using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved Human determination and Origin-feat price reconciliation.
/// The missing public Origin-feat catalog is represented by an explicitly
/// scoped test authority; this test is not first-party feat eligibility.
/// </summary>
public sealed class NativeOriginHumanDeterminationAllocationTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var root = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Native ruleset missing.");
    }

    private static NativeOriginProposal Human() =>
        new("human-determination-price", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedSpeciesId: "species.human");

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) => false;

        public bool AuthorizesCatalogChoice(
            string catalogId, string optionId, string evidenceId) =>
            evidenceId == "human-determination-test-only" &&
            ((catalogId == "any-native-skill" &&
                optionId == "skill.intimidation") ||
             (catalogId == "feat:origin" &&
                optionId == "feat.test-origin"));
    }

    private static NativeOriginConstructionResult SelectedHuman(int allowance = 18)
    {
        var catalog = Catalog();
        var engine = new NativeOriginConstructionEngine(catalog);
        var preview = engine.Evaluate(new(Human())).EvaluatedProposal;
        var size = Assert.Single(preview.Candidates, x =>
            x.Identity.IntrinsicSlotId == "size");
        var skill = Assert.Single(preview.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.skillful");
        var feat = Assert.Single(preview.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.versatile");
        var determination = Assert.Single(preview.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.human-determination");
        Assert.Equal(NativeOriginEligibility.Eligible,
            determination.SelectionBasis.Eligibility);

        var manifests = new NativeOriginManifestation[]
        {
            new("human-size", size.Id,
                Choices: [new(Assert.Single(size.RequiredChoices!).Id, ["medium"])]),
            new("human-skill", skill.Id,
                Choices: [new(Assert.Single(skill.RequiredChoices!).Id,
                    ["skill.intimidation"], "human-determination-test-only")]),
            new("human-origin-feat", feat.Id,
                Choices: [new(Assert.Single(feat.RequiredChoices!).Id,
                    ["feat.test-origin"], "human-determination-test-only")])
        };
        return engine.Evaluate(new(Human() with { Manifestations = manifests },
            TrustedScope: new("campaign", "human-determination-test"),
            AutoManifestDirectDefaults: true,
            RequireCreationAllocation: true,
            CreationAllocationPolicy:
                NativeOriginFirstPartyBiologicalPricing.Create(allowance)),
            new TestAuthority());
    }

    [Fact]
    public void CompleteHumanIsAuthorizedAtSeventeenPoints()
    {
        var result = SelectedHuman();
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Detail)));
        Assert.Equal("within-allowance", result.CreationAllocation!.Status);
        Assert.Equal(17, result.CreationAllocation.CommittedLoad);
        Assert.Equal("species.human", result.EvaluatedProposal.SelectedSpeciesId);
        Assert.Null(result.EvaluatedProposal.SelectedAncestryId);

        var grants = result.Profile!.Underlying.InherentGrants;
        foreach (var id in new[]
        {
            "trait.skillful", "trait.versatile",
            "trait.adaptable-learning", "trait.human-determination"
        })
            Assert.Single(grants, x => x.Identity.MechanicalDefinitionId == id);

        var charges = result.CreationAllocation.Charges;
        Assert.Equal(6, Assert.Single(charges, x =>
            x.CharacteristicKeys.Contains("trait:trait.versatile")).Load);
        Assert.Equal(4, Assert.Single(charges, x =>
            x.CharacteristicKeys.Contains("trait:trait.human-determination")).Load);
    }

    [Fact]
    public void LowerBudgetRejectsTheSameWholeHuman()
    {
        var result = SelectedHuman(16);
        Assert.Equal(NativeOriginConstructionStatus.Conflicting, result.Status);
        Assert.Equal("over-allowance", result.CreationAllocation!.Status);
        Assert.Equal(17, result.CreationAllocation.CommittedLoad);
        Assert.Contains(result.Issues, x => x.Code == "creation-allocation-exceeded");
    }

    [Fact]
    public void HumanDeterminationIsPreRollOncePerShortOrLongRest()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.human-determination", out var entity));
        var trait = entity!.Definition.GetProperty("trait");
        var advantage = trait.GetProperty("rollAdvantage");
        Assert.Equal(new[]
        {
            "attack-roll", "ability-check", "saving-throw"
        }, advantage.GetProperty("eligibleRollTypes")
            .EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal("before-roll", advantage
            .GetProperty("declarationTiming").GetString());
        Assert.Equal("advantage", advantage.GetProperty("outcome").GetString());
        Assert.Equal("none", advantage.GetProperty("actionCost").GetString());
        Assert.False(advantage.GetProperty("stacksWithExistingAdvantage")
            .GetBoolean());
        var uses = trait.GetProperty("uses");
        Assert.Equal(1, uses.GetProperty("value").GetInt32());
        Assert.Equal("short-or-long-rest", uses.GetProperty("recharge").GetString());
        Assert.False(trait.TryGetProperty("abilityScoreIncrease", out _));
    }

}
