using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Uses canonical Dwarven Toughness and the real character HP kernel. The
/// nonheritable trait in a mixed Origin is explicitly DM-authorized in tests;
/// this does not establish first-party biological inheritance eligibility.
/// </summary>
public sealed class NativeOriginHitPointProgressionTests
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
        throw new InvalidOperationException("First-party ruleset unavailable.");
    }

    private static NativeOriginProposal Direct(string id = "dwarf-hp") =>
        new(id, NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: "species.dwarf");

    private static NativeOriginProposal Mixed(string id = "mixed-hp",
        int donors = 1) =>
        new(id, NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            Enumerable.Range(0, donors).Select(index =>
                new NativeOriginContributor("dwarf-" + index,
                    new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.dwarf"))).ToArray(),
            [], []);

    private static NativeOriginConstructionResult Evaluate(
        NativeCatalogSnapshot catalog, NativeOriginProposal proposal,
        bool auto = true) =>
        new NativeOriginConstructionEngine(catalog).Evaluate(
            new(proposal, AutoManifestDirectDefaults: auto));

    private sealed class ScopedTestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "test-hp-approval" &&
            scope.Kind == "campaign" && scope.Id == "test-hp";

        public bool AuthorizesCatalogChoice(string catalogId,
            string optionId, string evidenceId) => false;
    }

    private static NativeOriginConstructionResult SelectedMixed(
        NativeCatalogSnapshot catalog, int donorCount = 1)
    {
        var baseProposal = Mixed(donors: donorCount);
        var offers = Evaluate(catalog, baseProposal).EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "dwarf-0" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var toughness = offers.Where(x => x.Identity.MechanicalDefinitionId ==
            "trait.dwarf.dwarven-toughness").ToArray();
        Assert.Equal(donorCount, toughness.Length);
        Assert.All(toughness, x =>
            Assert.NotEqual(NativeOriginEligibility.Eligible,
                x.SelectionBasis.Eligibility));

        var manifests = foundation.Concat(toughness).Select((offer, index) =>
            new NativeOriginManifestation("manifest:" + index, offer.Id,
                SelectedValue: offer.Identity.Kind ==
                    NativeOriginCharacteristicKind.Intrinsic
                    ? offer.OfferedValue : null)).ToArray();
        var decisions = toughness.Select(x => new NativeTableRuleDecision(
            "approve:" + x.Id,
            new("dm", "test-hp-approval"), new("campaign", "test-hp"),
            new("candidate", x.Id),
            JsonSerializer.SerializeToElement(new { eligibility = "eligible" }))).ToArray();
        return new NativeOriginConstructionEngine(catalog).Evaluate(
            new(baseProposal with
            {
                Manifestations = manifests,
                TableDecisions = decisions
            }, TrustedScope: new("campaign", "test-hp"),
                AutoManifestDirectDefaults: false),
            new ScopedTestAuthority());
    }

    private static NativeCharacterProjection Character(
        NativeCatalogSnapshot catalog, int level, bool hasHistory = true)
    {
        var gains = hasHistory
            ? Enumerable.Range(1, level)
                .ToDictionary(i => "level-" + i, _ => 6, StringComparer.Ordinal)
            : null;
        var request = new NativeCharacterRequest(
            catalog.RulesetId, catalog.Fingerprint,
            new Dictionary<string, int>
            {
                ["strength"] = 10, ["dexterity"] = 12, ["constitution"] = 14,
                ["intelligence"] = 8, ["wisdom"] = 15, ["charisma"] = 11
            },
            [new("class.warlock", level, "warlock-level")],
            HitPointGains: gains);
        return new NativeCharacterProjector(catalog).Project(request);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(17)]
    public void ConventionalDwarfGainsExactlyOneMaximumHpPerCharacterLevel(int level)
    {
        var catalog = Catalog();
        var direct = Evaluate(catalog, Direct());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, direct.Status);
        var projected = NativeOriginHitPointProgressionProjector.Project(catalog,
            direct, level);
        Assert.Equal("resolved", projected.Status);
        Assert.Equal(1, projected.PerLevelBonus);
        Assert.Equal(level, projected.MaximumHitPointBonus);
        Assert.Null(projected.CombinedMaximumHitPoints);
        Assert.Equal("trait.dwarf.dwarven-toughness",
            direct.Profile!.Underlying.InherentGrants.Single(
                x => x.Id == projected.GrantId).Identity.MechanicalDefinitionId);

        var character = Character(catalog, level);
        Assert.Equal("resolved", character.Values["advancement.total-level"].Status);
        Assert.Equal("resolved", character.Values["hit-points.maximum"].Status);
        var result = NativeOriginHitPointProgressionProjector.Combine(catalog,
            direct, character);
        Assert.Equal("resolved", result.Status);
        Assert.Equal(level * 8 + level, result.CombinedMaximumHitPoints);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(direct.Profile))!;
        var afterLoad = NativeOriginHitPointProgressionProjector.Combine(catalog,
            direct with { Profile = saved }, character);
        Assert.Equal(result.CombinedMaximumHitPoints,
            afterLoad.CombinedMaximumHitPoints);
    }

    [Fact]
    public void ScopedMixedDwarfManifestsOneFullProgressionWithoutAutoTransmission()
    {
        var catalog = Catalog();
        var mixed = SelectedMixed(catalog);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, mixed.Status);
        var projected = NativeOriginHitPointProgressionProjector.Combine(
            catalog, mixed, Character(catalog, 11));
        Assert.Equal("resolved", projected.Status);
        Assert.Equal(11, projected.MaximumHitPointBonus);
        Assert.Equal(99, projected.CombinedMaximumHitPoints);
        Assert.Equal(4, mixed.Profile!.Underlying.InherentGrants.Count);
        Assert.Equal(NativeOriginAdjudication.DmAuthorized,
            mixed.Profile.Adjudication.Adjudication);
    }

    [Fact]
    public void TwoApprovedSourcesOfSameToughnessConsolidateInsteadOfDoublingHp()
    {
        var catalog = Catalog();
        var mixed = SelectedMixed(catalog, donorCount: 2);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, mixed.Status);
        var toughness = Assert.Single(mixed.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.dwarf.dwarven-toughness");
        Assert.Equal(2, toughness.SourceCandidateIds.Count);
        var projected = NativeOriginHitPointProgressionProjector.Combine(
            catalog, mixed, Character(catalog, 5));
        Assert.Equal("resolved", projected.Status);
        Assert.Equal(5, projected.MaximumHitPointBonus);
        Assert.Equal(45, projected.CombinedMaximumHitPoints);
    }

    [Fact]
    public void MissingLevelOrHitPointHistoryDoesNotManufactureABonus()
    {
        var catalog = Catalog();
        var direct = Evaluate(catalog, Direct());
        var noLevel = NativeOriginHitPointProgressionProjector.Project(
            catalog, direct, null);
        Assert.Equal("unresolved", noLevel.Status);
        Assert.Contains("character-level-required", noLevel.UnresolvedReasons);
        Assert.Equal("unresolved",
            NativeOriginHitPointProgressionProjector.Project(catalog, direct, 0).Status);
        Assert.Equal("unresolved",
            NativeOriginHitPointProgressionProjector.Project(catalog, direct, 1001).Status);
        var missing = NativeOriginHitPointProgressionProjector.Combine(
            catalog, direct, Character(catalog, 5, hasHistory: false));
        Assert.Equal("unresolved", missing.Status);
        Assert.Contains("character-hit-points-unresolved", missing.UnresolvedReasons);
    }

    [Fact]
    public void ForgedProgressionOrNonAuthorizedOriginCannotProduceHp()
    {
        var catalog = Catalog();
        var completed = Evaluate(catalog, Direct());
        var grant = Assert.Single(completed.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.dwarf.dwarven-toughness");
        var forgedValue = JsonSerializer.SerializeToElement(new
        {
            id = "dwarven-toughness",
            hitPointsPerCharacterLevel = 100
        });
        var changed = completed.Profile.Underlying with
        {
            InherentGrants = completed.Profile.Underlying.InherentGrants
                .Select(x => x.Id == grant.Id
                    ? x with { EffectiveValue = forgedValue } : x).ToArray()
        };
        var fake = completed with
        {
            Profile = completed.Profile with
            {
                Underlying = changed, Current = changed
            }
        };
        var attempted = NativeOriginHitPointProgressionProjector.Project(
            catalog, fake, 5);
        Assert.Equal("unresolved", attempted.Status);
        Assert.Contains("hp-progressions-not-verified", attempted.UnresolvedReasons);
        var unauthorized = completed with
        {
            Status = NativeOriginConstructionStatus.Unresolved
        };
        Assert.Equal("unresolved",
            NativeOriginHitPointProgressionProjector.Project(
                catalog, unauthorized, 5).Status);
    }
}
