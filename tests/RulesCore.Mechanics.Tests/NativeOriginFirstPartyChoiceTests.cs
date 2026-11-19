using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Native catalog membership can be proved from the immutable first-party
/// snapshot. Character-specific feat prerequisites and DM decisions cannot.
/// </summary>
public sealed class NativeOriginFirstPartyChoiceTests
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
        throw new InvalidOperationException("The selected-native ruleset is absent.");
    }

    private static (NativeOriginProposal Proposal, NativeOriginCandidate Size,
        NativeOriginCandidate Skill, NativeOriginCandidate Feat) Human(NativeCatalogSnapshot catalog)
    {
        var baseProposal = new NativeOriginProposal("human-native-choices",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedSpeciesId: "species.human");
        var discovered = new NativeOriginSourceDeconstructor(catalog)
            .Deconstruct(baseProposal).Proposal.Candidates;
        var size = Assert.Single(discovered, x =>
            x.Identity.IntrinsicSlotId == "size" &&
            x.RequiredChoices?.Any(req => req.Id == "size") == true);
        var skill = Assert.Single(discovered, x =>
            x.RequiredChoices?.Any(req => req.OptionCatalogId == "any-native-skill") == true);
        var feat = Assert.Single(discovered, x =>
            x.RequiredChoices?.Any(req => req.OptionCatalogId == "feat:origin") == true);
        return (baseProposal, size, skill, feat);
    }

    private static NativeOriginManifestation Select(
        NativeOriginCandidate candidate, string selectedOption) =>
        new("selected:" + candidate.Id, candidate.Id,
            Choices: [new(candidate.RequiredChoices!.Single().Id, [selectedOption])]);

    [Fact]
    public void NativeSkillChoiceUsesServerCheckedCatalogMembershipOnly()
    {
        var catalog = Catalog();
        var (proposal, size, skill, _) = Human(catalog);
        var selected = proposal with
        {
            Manifestations = [Select(size, "medium"),
                Select(skill, "skill.intimidation")]
        };

        var result = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(selected);
        // Skill and finite size are now actual first-party choices. The Human
        // Origin feat remains mandatory and is not invented or waived.
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code is "choice-authorization-unresolved" or "choice-catalog-proof-required");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "required-origin-selection-missing");
        Assert.Contains(result.Profile!.Underlying.InherentGrants, grant =>
            grant.Identity.Occurrence.OccurrencePath == "/traits/0" &&
            grant.ResolvedChoices?.Any(choice =>
                choice.SelectedOptionIds.Contains("skill.intimidation")) == true);
        Assert.Contains(result.Profile.Underlying.InherentGrants, grant =>
            grant.Identity.IntrinsicSlotId == "size" &&
            grant.EffectiveValue?.GetString() == "medium");

        var proof = result.EvaluatedProposal.Manifestations
            .Single(manifest => manifest.CandidateId == skill.Id).Choices!.Single();
        Assert.Equal("native-catalog:" + catalog.RulesetId + ":" + catalog.Fingerprint,
            proof.CatalogEligibilityEvidenceId);
    }

    [Theory]
    [InlineData("competency.alchemy")]
    [InlineData("skill.profession")]
    [InlineData("skill.nonexistent")]
    [InlineData("species.orc")]
    public void SkillChoiceDoesNotAcceptUnknownOrDifferentCatalogs(string selectedId)
    {
        var catalog = Catalog();
        var (proposal, size, skill, _) = Human(catalog);
        var request = proposal with
        {
            Manifestations = [Select(size, "medium"), Select(skill, selectedId)]
        };
        var result = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(request);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "choice-authorization-unresolved");
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, grant =>
            grant.Identity.Occurrence.OccurrencePath == "/traits/0");
    }

    [Fact]
    public void UnimplementedFeatEligibilityRemainsUnresolved()
    {
        var catalog = Catalog();
        var (proposal, size, skill, feat) = Human(catalog);
        var result = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(
            proposal with
            {
                Manifestations = [Select(size, "medium"),
                    Select(skill, "skill.intimidation"),
                    Select(feat, "feat.some-origin")]
            });
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "choice-authorization-unresolved");
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, grant =>
            grant.Identity.Occurrence.OccurrencePath == "/traits/1");
        Assert.Null(result.EvaluatedProposal.Manifestations
            .Single(manifest => manifest.CandidateId == feat.Id)
            .Choices!.Single().CatalogEligibilityEvidenceId);
    }

    [Fact]
    public void MixedConstructionStillRequiresActualTableAuthority()
    {
        var catalog = Catalog();
        var proposal = new NativeOriginProposal("mixed", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [new("parent", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, "species.human"))],
            [], []);
        var result = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(proposal);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, grant =>
            grant.Identity.Occurrence.Source.Id == "species.human");
    }
}
