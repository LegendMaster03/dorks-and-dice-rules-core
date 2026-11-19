using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Actual native Human Adaptable Learning. Only the entitlement trajectory is
/// interpreted: neither a per-level allocation history nor rank caps have been
/// approved. The Human Origin Feat prerequisite is supplied solely by an
/// in-process test authority; it is still unresolved in first-party production.
/// </summary>
public sealed class NativeOriginSkillRankEntitlementTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var root = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(
                    root, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Native catalog unavailable.");
    }

    private static NativeOriginProposal Human() =>
        new("human-ranks", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.human");

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "rank-test-only" &&
            scope.Kind == "campaign" && scope.Id == "human-rank-test";

        public bool AuthorizesCatalogChoice(string catalogId,
            string optionId, string evidenceId) =>
            evidenceId == "rank-test-only" &&
            ((catalogId == "any-native-skill" && optionId == "skill.intimidation") ||
             (catalogId == "feat:origin" && optionId == "feat.test-origin"));
    }

    private static NativeOriginConstructionResult SelectedDirectHuman(
        NativeCatalogSnapshot catalog)
    {
        var baseline = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(Human(), AutoManifestDirectDefaults: true));
        var choices = baseline.EvaluatedProposal.Candidates;
        var size = Assert.Single(choices, x =>
            x.Identity.IntrinsicSlotId == "size");
        var skill = Assert.Single(choices, x =>
            x.Identity.MechanicalDefinitionId == "trait.human.skillful");
        var feat = Assert.Single(choices, x =>
            x.Identity.MechanicalDefinitionId == "trait.human.versatile");
        var adaptable = Assert.Single(choices, x =>
            x.Identity.MechanicalDefinitionId == "trait.human.adaptable-learning");
        Assert.Equal(NativeOriginEligibility.Eligible, adaptable.SelectionBasis.Eligibility);

        var manifestations = new NativeOriginManifestation[]
        {
            new("size", size.Id,
                Choices: [new(size.RequiredChoices!.Single().Id, ["medium"])]),
            new("skill", skill.Id, Choices:
                [new(skill.RequiredChoices!.Single().Id,
                    ["skill.intimidation"], "rank-test-only")]),
            new("feat", feat.Id, Choices:
                [new(feat.RequiredChoices!.Single().Id,
                    ["feat.test-origin"], "rank-test-only")])
        };
        return new NativeOriginConstructionEngine(catalog).Evaluate(
            new(Human() with { Manifestations = manifestations },
                TrustedScope: new("campaign", "human-rank-test"),
                AutoManifestDirectDefaults: true),
            new TestAuthority());
    }

    private static NativeOriginProposal Mixed(int humanDonors = 1) =>
        new("mixed-human-ranks", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            new[] {
                new NativeOriginContributor("elf",
                    new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf"))
            }.Concat(Enumerable.Range(0, humanDonors)
                .Select(i => new NativeOriginContributor("human-" + i,
                    new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.human"))))
                .ToArray(), [], []);

    private static NativeOriginConstructionResult SelectedMixedHuman(
        NativeCatalogSnapshot catalog, int humanDonors = 1)
    {
        var baseProposal = Mixed(humanDonors);
        var offers = new NativeOriginConstructionEngine(catalog)
            .Evaluate(new(baseProposal)).EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "elf" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var ranks = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.human.adaptable-learning").ToArray();
        Assert.Equal(3, foundation.Length);
        Assert.Equal(humanDonors, ranks.Length);
        Assert.All(ranks, x =>
            Assert.NotEqual(NativeOriginEligibility.Eligible,
                x.SelectionBasis.Eligibility));
        var manifest = foundation.Concat(ranks).Select((x, i) =>
            new NativeOriginManifestation("selected-" + i, x.Id,
                SelectedValue: x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                    ? x.OfferedValue : null)).ToArray();
        var decisions = ranks.Select(x => new NativeTableRuleDecision(
            "allow-" + x.Id, new("dm", "rank-test-only"),
            new("campaign", "human-rank-test"),
            new("candidate", x.Id),
            JsonSerializer.SerializeToElement(new { eligibility = "eligible" }))).ToArray();

        return new NativeOriginConstructionEngine(catalog).Evaluate(
            new(baseProposal with
            {
                Manifestations = manifest, TableDecisions = decisions
            }, TrustedScope: new("campaign", "human-rank-test")),
            new TestAuthority());
    }

    private static NativeCharacterProjection Character(
        NativeCatalogSnapshot catalog, int level) =>
        new NativeCharacterProjector(catalog).Project(new NativeCharacterRequest(
            catalog.RulesetId, catalog.Fingerprint,
            new Dictionary<string, int>
            {
                ["strength"] = 10, ["dexterity"] = 12, ["constitution"] = 14,
                ["intelligence"] = 8, ["wisdom"] = 15, ["charisma"] = 11
            },
            [new("class.warlock", level, "warlock")]));

    [Theory]
    [InlineData(1, 4, 4)]
    [InlineData(2, 5, 1)]
    [InlineData(5, 8, 1)]
    [InlineData(10, 13, 1)]
    [InlineData(17, 20, 1)]
    public void HumanRanksAreAFullLevelTrajectoryNotAnImmediateRankAllocation(
        int level, int expectedTotal, int newlyAvailable)
    {
        var catalog = Catalog();
        var result = SelectedDirectHuman(catalog);
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Detail)));
        var preview = NativeOriginSkillRankEntitlementProjector.ForCharacter(
            catalog, result, Character(catalog, level));
        Assert.True(preview.Status == "resolved",
            string.Join("; ", preview.UnresolvedReasons) +
            " / total-level=" + Character(catalog, level)
                .Values["advancement.total-level"].Status +
            " / origin-grants=" + result.Profile!.Underlying.InherentGrants.Count);
        Assert.Equal(level, preview.CharacterLevel);
        Assert.Equal(4, preview.AdditionalRanksAtLevelOne);
        Assert.Equal(1, preview.AdditionalRanksPerLaterLevel);
        Assert.Equal(expectedTotal, preview.TotalEntitledExtraRanks);
        Assert.Equal(newlyAvailable, preview.NewExtraRanksAtThisLevel);
        Assert.True(preview.PreviewOnly);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile))!;
        var restored = NativeOriginSkillRankEntitlementProjector.Project(
            catalog, result with { Profile = saved }, level);
        Assert.Equal(expectedTotal, restored.TotalEntitledExtraRanks);
        Assert.Equal(preview.GrantId, restored.GrantId);
    }

    [Fact]
    public void UnfinishedOrdinaryHumanCannotIssueAnyEntitlements()
    {
        var catalog = Catalog();
        var incomplete = new NativeOriginConstructionEngine(catalog)
            .Evaluate(new(Human(), AutoManifestDirectDefaults: true));
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, incomplete.Status);
        Assert.Equal("unresolved",
            NativeOriginSkillRankEntitlementProjector.Project(catalog,
                incomplete, 5).Status);
        // A hypothetical Origin Feat does not become automatically eligible
        // merely because its identifier appears in a test construction.
        Assert.Contains(incomplete.Issues,
            issue => issue.Code == "required-origin-selection-missing");
    }

    [Fact]
    public void InheritedHumanProgressionNeedsVerifiedScopedEligibility()
    {
        var catalog = Catalog();
        var selected = SelectedMixedHuman(catalog);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, selected.Status);
        var preview = NativeOriginSkillRankEntitlementProjector.ForCharacter(
            catalog, selected, Character(catalog, 8));
        Assert.Equal("resolved", preview.Status);
        Assert.Equal(11, preview.TotalEntitledExtraRanks);
        Assert.Equal(1, preview.NewExtraRanksAtThisLevel);
        Assert.True(preview.PreviewOnly);
    }

    [Fact]
    public void TwoSameSourceDonorsDoNotDoubleTheRankSchedule()
    {
        var catalog = Catalog();
        var selected = SelectedMixedHuman(catalog, 2);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, selected.Status);
        var grant = Assert.Single(selected.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.human.adaptable-learning");
        Assert.Equal(2, grant.SourceCandidateIds.Count);
        var projection = NativeOriginSkillRankEntitlementProjector.Project(
            catalog, selected, 5);
        Assert.Equal("resolved", projection.Status);
        Assert.Equal(8, projection.TotalEntitledExtraRanks);
        Assert.Equal(grant.Id, projection.GrantId);
    }

    [Fact]
    public void ForgedRatesAndInvalidLevelsFailClosed()
    {
        var catalog = Catalog();
        var selected = SelectedDirectHuman(catalog);
        var grant = Assert.Single(selected.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.human.adaptable-learning");
        var changed = selected.Profile.Underlying with
        {
            InherentGrants = selected.Profile.Underlying.InherentGrants
                .Select(x => x.Id == grant.Id
                    ? x with { EffectiveValue = JsonSerializer.SerializeToElement(
                        new { additionalRanksAtCharacterLevel1 = 1000,
                            additionalRanksPerSubsequentCharacterLevel = 1000,
                            allocation = "player-choice",
                            obeysNormalSkillRankLimits = false }) } : x).ToArray()
        };
        var forged = selected with
        {
            Profile = selected.Profile with { Underlying = changed, Current = changed }
        };
        var withheld = NativeOriginSkillRankEntitlementProjector.Project(
            catalog, forged, 5);
        Assert.Equal("unresolved", withheld.Status);
        Assert.Contains("skill-rank-schedule-not-verified",
            withheld.UnresolvedReasons);

        var stripped = selected.Profile.Underlying with
        {
            InherentGrants = selected.Profile.Underlying.InherentGrants
                .Select(x => x.Id == grant.Id
                    ? x with { EffectiveValue = JsonSerializer.SerializeToElement(
                        new { id = "adaptable-learning", description = "omitted" }) }
                    : x).ToArray()
        };
        var removed = NativeOriginSkillRankEntitlementProjector.Project(
            catalog, selected with
            {
                Profile = selected.Profile with
                {
                    Underlying = stripped, Current = stripped
                }
            }, 5);
        Assert.Equal("unresolved", removed.Status);
        Assert.Contains("skill-rank-schedule-not-verified",
            removed.UnresolvedReasons);

        Assert.Equal("unresolved",
            NativeOriginSkillRankEntitlementProjector.Project(
                catalog, selected, null).Status);
        Assert.Equal("unresolved",
            NativeOriginSkillRankEntitlementProjector.Project(
                catalog, selected, 0).Status);
        Assert.Equal("unresolved",
            NativeOriginSkillRankEntitlementProjector.Project(
                catalog, selected, 1001).Status);
    }

    [Fact]
    public void NoSelectedRankScheduleNeverInventsHumanEntitlements()
    {
        var catalog = Catalog();
        var elf = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(new("elf", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"), [], [], [],
                SelectedSpeciesId: "species.elf"),
                AutoManifestDirectDefaults: true));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, elf.Status);
        var preview = NativeOriginSkillRankEntitlementProjector.Project(catalog,
            elf, 5);
        Assert.Equal("none", preview.Status);
        Assert.Equal(0, preview.TotalEntitledExtraRanks);
        Assert.Null(preview.GrantId);
    }
}
