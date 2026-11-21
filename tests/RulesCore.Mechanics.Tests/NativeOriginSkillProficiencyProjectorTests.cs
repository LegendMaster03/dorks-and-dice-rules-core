using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Exercise native Human Skillful independently of its distinct Adaptable
/// Learning rank pool and unresolved first-party Origin Feat catalog. The test
/// authority does not authorize gameplay outside the in-process fixture.
/// </summary>
public sealed class NativeOriginSkillProficiencyProjectorTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null)
        {
            var root = Path.Combine(folder.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                    "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001"));
            folder = folder.Parent;
        }
        throw new InvalidOperationException("First-party catalog not present.");
    }

    private sealed class LocalChoiceAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "skill-projection-test" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "skill-test-only";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) =>
            evidenceId == "skill-test-only" &&
            (catalogId == "feat:origin" && optionId == "feat.test-origin" ||
             catalogId == "any-native-skill" && optionId is
                 "skill.intimidation" or "skill.stealth" or "skill.profession");
    }

    private static NativeTableRuleDecision Permit(string id, string target) =>
        new(id, new("dm", "skill-test-only"),
            new("campaign", "skill-projection-test"),
            new("candidate", target),
            JsonSerializer.SerializeToElement(new { eligibility = "eligible" }));

    private static NativeOriginConstructionResult Human(string skillId =
        "skill.intimidation")
    {
        var catalog = Catalog();
        var proposal = new NativeOriginProposal("direct-human-skill",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.human");
        var offers = new NativeOriginConstructionEngine(catalog)
            .Evaluate(new(proposal, AutoManifestDirectDefaults: true))
            .EvaluatedProposal.Candidates;
        var size = Assert.Single(offers, x =>
            x.Identity.IntrinsicSlotId == "size");
        var skill = Assert.Single(offers, x =>
            x.Identity.MechanicalDefinitionId == "trait.human.skillful");
        var feat = Assert.Single(offers, x =>
            x.Identity.MechanicalDefinitionId == "trait.human.versatile");
        var selected = proposal with
        {
            Manifestations =
            [
                new("size", size.Id, Choices:
                    [new(size.RequiredChoices!.Single().Id, ["medium"])]),
                new("skill", skill.Id, Choices:
                    [new(skill.RequiredChoices!.Single().Id,
                        [skillId], "skill-test-only")]),
                new("feat", feat.Id, Choices:
                    [new(feat.RequiredChoices!.Single().Id,
                        ["feat.test-origin"], "skill-test-only")])
            ]
        };
        return new NativeOriginConstructionEngine(catalog).Evaluate(
            new(selected, TrustedScope: new("campaign", "skill-projection-test"),
                AutoManifestDirectDefaults: true),
            new LocalChoiceAuthority());
    }

    private static NativeOriginConstructionResult Mixed(
        int humanDonors, string skillId = "skill.intimidation")
    {
        var catalog = Catalog();
        var proposal = new NativeOriginProposal("mixed-human-skill",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            new NativeOriginContributor[]
            {
                new("elf", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf"))
            }.Concat(Enumerable.Range(0, humanDonors)
                .Select(i => new NativeOriginContributor("human-" + i,
                    new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.human"))))
                .ToArray(), [], []);
        var offers = new NativeOriginConstructionEngine(catalog)
            .Evaluate(new(proposal)).EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "elf" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var skills = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.human.skillful").ToArray();
        Assert.Equal(3, foundation.Length);
        Assert.Equal(humanDonors, skills.Length);
        Assert.All(skills, x => Assert.Equal(
            NativeOriginEligibility.Eligible, x.SelectionBasis.Eligibility));
        var decisions = skills.Select(x =>
            Permit("allow-" + x.Id, x.Id)).ToArray();
        var manifest = foundation.Select((x, i) =>
            new NativeOriginManifestation("foundation-" + i, x.Id,
                SelectedValue: x.OfferedValue)).Concat(skills.Select((x, i) =>
            new NativeOriginManifestation("skill-" + i, x.Id,
                Choices: [new(x.RequiredChoices!.Single().Id,
                    [skillId], "skill-test-only")]))).ToArray();

        return new NativeOriginConstructionEngine(catalog).Evaluate(
            new(proposal with
            {
                Manifestations = manifest,
                TableDecisions = decisions
            }, TrustedScope: new("campaign", "skill-projection-test")),
            new LocalChoiceAuthority());
    }

    [Fact]
    public void HumanSkillfulProjectsSelectedSkillWithoutInventingRanksOrFeat()
    {
        var catalog = Catalog();
        var human = Human();
        Assert.True(human.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", human.Issues.Select(x => x.Code + ": " + x.Detail)));
        var view = NativeOriginSkillProficiencyProjector.Project(catalog, human);
        Assert.Equal("resolved", view.Status);
        Assert.True(view.PreviewOnly);
        var selected = Assert.Single(view.Proficiencies);
        Assert.Equal("skill.intimidation", selected.SkillId);
        Assert.Equal("trait.human.skillful", selected.DefinitionId);
        Assert.Equal("species.human", selected.SourceId);
        Assert.Equal("/traits/0", selected.OccurrencePath);
        Assert.Single(selected.SourceCandidateIds);

        var rank = NativeOriginSkillRankEntitlementProjector.Project(
            catalog, human, 1);
        Assert.Equal("resolved", rank.Status);
        Assert.Equal(4, rank.TotalEntitledExtraRanks);
        Assert.DoesNotContain(view.Proficiencies, x =>
            x.SkillId is "feat.test-origin" or "trait.human.adaptable-learning");

        var restored = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(human.Profile));
        var afterRoundTrip = NativeOriginSkillProficiencyProjector.Project(
            catalog, human with { Profile = restored });
        Assert.Equal("resolved", afterRoundTrip.Status);
        var restoredSkill = Assert.Single(afterRoundTrip.Proficiencies);
        Assert.Equal(selected.SkillId, restoredSkill.SkillId);
        Assert.Equal(selected.DefinitionId, restoredSkill.DefinitionId);
        Assert.Equal(selected.SourceId, restoredSkill.SourceId);
        Assert.Equal(selected.SourceCandidateIds, restoredSkill.SourceCandidateIds);
    }

    [Fact]
    public void IncompleteHumanHasNoAvailableSkillProficiency()
    {
        var catalog = Catalog();
        var human = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(new("unselected-human", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"), [], [], [],
                SelectedSpeciesId: "species.human"),
                AutoManifestDirectDefaults: true));
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, human.Status);
        var view = NativeOriginSkillProficiencyProjector.Project(catalog, human);
        Assert.Equal("unresolved", view.Status);
        Assert.Empty(view.Proficiencies);
    }

    [Fact]
    public void ScopedMixedHumanSelectionRetainsOneNativeSkillAndProvenance()
    {
        var catalog = Catalog();
        var result = Mixed(1, "skill.stealth");
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Detail)));
        var view = NativeOriginSkillProficiencyProjector.ProjectApprovedMixed(
            catalog, result);
        Assert.Equal("resolved", view.Status);
        var skill = Assert.Single(view.Proficiencies);
        Assert.Equal("skill.stealth", skill.SkillId);
        Assert.Equal("species.human", skill.SourceId);
        Assert.Equal("trait.human.skillful", skill.DefinitionId);
        Assert.Equal("unresolved",
            NativeOriginSkillProficiencyProjector.Project(catalog, result).Status);
    }

    [Fact]
    public void TwoIdenticalHumanDonorOffersAreOneSkillWithTwoEvidencePaths()
    {
        var catalog = Catalog();
        var result = Mixed(2);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var view = NativeOriginSkillProficiencyProjector.ProjectApprovedMixed(
            catalog, result);
        Assert.Equal("resolved", view.Status);
        var skill = Assert.Single(view.Proficiencies);
        Assert.Equal("skill.intimidation", skill.SkillId);
        Assert.Equal(2, skill.SourceCandidateIds.Count);
    }

    [Fact]
    public void UnrecognizedSkillFamilyMustNotBeGrantedAsSkill()
    {
        var result = Human("skill.profession");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var view = NativeOriginSkillProficiencyProjector.Project(
            Catalog(), result);
        Assert.Equal("unresolved", view.Status);
        Assert.Empty(view.Proficiencies);
        Assert.Contains(view.UnresolvedReasons, x =>
            x.StartsWith("skill-proficiency-not-verified:", StringComparison.Ordinal));
    }

    [Fact]
    public void ForgedSelectionOrChangedCanonicalSkillGrantFailsClosed()
    {
        var catalog = Catalog();
        var human = Human();
        var original = Assert.Single(human.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == "trait.human.skillful");
        var swapped = original with
        {
            ResolvedChoices =
            [
                new("selection", ["skill.stealth"], "skill-test-only")
            ]
        };
        Assert.Equal("unresolved",
            NativeOriginSkillProficiencyProjector.Project(catalog,
                Replace(human, swapped)).Status);

        var damaged = original with
        {
            EffectiveValue = JsonSerializer.SerializeToElement(
                new { grantType = "skill-proficiency", choiceCount = 500 })
        };
        Assert.Equal("unresolved",
            NativeOriginSkillProficiencyProjector.Project(catalog,
                Replace(human, damaged)).Status);

        Assert.Equal("unresolved",
            NativeOriginSkillProficiencyProjector.Project(catalog,
                Replace(human, original with
                {
                    SourceCandidateIds = ["forged"]
                })).Status);
    }

    [Fact]
    public void NonHumanDirectOriginHasNoHumanSkillfulBenefit()
    {
        var catalog = Catalog();
        var elf = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(new("direct-elf", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"), [], [], [],
                SelectedSpeciesId: "species.elf"),
                AutoManifestDirectDefaults: true));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, elf.Status);
        var view = NativeOriginSkillProficiencyProjector.Project(catalog, elf);
        Assert.Equal("none", view.Status);
        Assert.Empty(view.Proficiencies);
    }

    private static NativeOriginConstructionResult Replace(
        NativeOriginConstructionResult result,
        NativeOriginEffectiveGrant changed)
    {
        var profile = result.Profile!;
        var updated = profile.Underlying with
        {
            InherentGrants = profile.Underlying.InherentGrants.Select(x =>
                x.Id == changed.Id ? changed : x).ToArray()
        };
        return result with
        {
            Profile = profile with { Underlying = updated, Current = updated }
        };
    }
}
