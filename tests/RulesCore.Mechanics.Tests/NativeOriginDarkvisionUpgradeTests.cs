using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Uses the actual level-four Stone Goliath Cavernous Sight rule. An authored
/// conditional range upgrade is not an automatic Species inheritance or a
/// general stacking rule for unrelated native senses.
/// </summary>
public sealed class NativeOriginDarkvisionUpgradeTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var root = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Native catalog is missing.");
    }

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null,
        bool defaults = false) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null : new("campaign", "darkvision-upgrade"),
                AutoManifestDirectDefaults: defaults), authority);

    private static NativeTableRuleDecision Decision(string id, string kind, string target,
        object outcome) =>
        new(id, new("dm", "verified-upgrade"),
            new("campaign", "darkvision-upgrade"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "darkvision-upgrade" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "verified-upgrade";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    [Fact]
    public void DirectStoneGoliathGetsStandaloneDarkvisionOnlyWhenLevelFourUnlocks()
    {
        var proposal = new NativeOriginProposal("direct-stone-vision",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedAncestryId: "ancestry.goliath.stone");
        var candidates = Evaluate(proposal, defaults: true).EvaluatedProposal.Candidates;
        var aptitude = Assert.Single(candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.giant-ancestry-aptitude");
        var cavernous = Assert.Single(candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.stone.cavernous-sight");
        Assert.Equal(NativeOriginEligibility.Eligible, cavernous.SelectionBasis.Eligibility);
        var completed = Evaluate(proposal with
        {
            Manifestations = [new("aptitude", aptitude.Id,
                Choices: [new("abilityChoice", ["strength"])])]
        }, defaults: true);
        Assert.True(completed.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", completed.Issues.Select(x => x.Code)));
        Assert.Contains(completed.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == cavernous.Identity.MechanicalDefinitionId);

        var unknownLevel = NativeOriginDarkvisionProjector.Project(Catalog(), completed);
        Assert.Equal("unresolved", unknownLevel.Status);
        Assert.Contains("darkvision-extension-requires-character-level",
            unknownLevel.UnresolvedReasons);
        var level3 = NativeOriginDarkvisionProjector.Project(Catalog(), completed, 3);
        Assert.Equal("none", level3.Status);
        Assert.Null(level3.Darkvision);
        var level4 = NativeOriginDarkvisionProjector.Project(Catalog(), completed, 4);
        Assert.Equal("resolved", level4.Status);
        Assert.Equal(60, level4.Darkvision!.RangeFeet);
        Assert.Equal(0, level4.Darkvision.BaseRangeFeet);
        Assert.Equal("ancestry.goliath.stone", level4.Darkvision.SourceId);
        var source = Assert.Single(level4.Darkvision.Extensions!);
        Assert.Equal(60, source.AddedRangeFeet);
        Assert.Equal(4, source.RequiredCharacterLevel);
        Assert.Equal("ancestry.goliath.stone", source.SourceId);
        Assert.Equal("grant:auto:" + cavernous.Id, source.GrantId);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.Project(Catalog(), completed, 0).Status);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), completed, 4).Status);
    }

    [Fact]
    public void MixedDrowStoneDarkvisionAddsOnlyTheAuthoredRangeWhenUnlocked()
    {
        var proposal = new NativeOriginProposal("mixed-drow-stone",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [
                new("drow", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.elf.drow")),
                new("stone", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.goliath.stone"))
            ], [], []);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var source = Assert.Single(offered, x => x.ContributorId == "drow" &&
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
        var drawback = Assert.Single(offered, x => x.ContributorId == "drow" &&
            x.Identity.MechanicalDefinitionId == "trait.elf.drow.sunlight-sensitivity");
        var upgrade = Assert.Single(offered, x => x.ContributorId == "stone" &&
            x.Identity.MechanicalDefinitionId == "trait.goliath.stone.cavernous-sight");
        var foundation = offered.Where(x => x.ContributorId == "drow" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        Assert.Equal(3, foundation.Length);
        var permissions = new[]
        {
            Decision("allow-drow-darkvision", "candidate", source.Id,
                new { eligibility = "eligible" }),
            Decision("allow-drow-sunlight", "candidate", drawback.Id,
                new { eligibility = "eligible" }),
            Decision("allow-stone-cavernous", "candidate", upgrade.Id,
                new { eligibility = "eligible" })
        };
        var completed = Evaluate(proposal with
        {
            Manifestations = foundation.Select((x, i) =>
                new NativeOriginManifestation("foundation-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat(
            [
                new("selected-darkvision", source.Id, SelectedValue: source.OfferedValue),
                new("selected-sunlight", drawback.Id),
                new("selected-cavernous", upgrade.Id)
            ]).ToArray(),
            TableDecisions = permissions
        }, new TestAuthority());

        Assert.True(completed.Status == NativeOriginConstructionStatus.Authorized,
            "Status: " + completed.Status + " / " +
            string.Join("; ", completed.Issues.Select(x => x.Code + ":" + x.Detail)));
        var at3 = NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), completed, 3);
        Assert.Equal("resolved", at3.Status);
        Assert.Equal(120, at3.Darkvision!.RangeFeet);
        Assert.Empty(at3.Darkvision.Extensions!);

        var at4 = NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), completed, 4);
        Assert.Equal("resolved", at4.Status);
        Assert.Equal(180, at4.Darkvision!.RangeFeet);
        Assert.Equal(120, at4.Darkvision.BaseRangeFeet);
        Assert.Equal("ancestry.elf.drow", at4.Darkvision.SourceId);
        Assert.Equal("grant:selected-darkvision", at4.Darkvision.GrantId);
        var extra = Assert.Single(at4.Darkvision.Extensions!);
        Assert.Equal(60, extra.AddedRangeFeet);
        Assert.Equal("ancestry.goliath.stone", extra.SourceId);
        Assert.Equal("grant:selected-cavernous", extra.GrantId);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), completed).Status);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.Project(Catalog(), completed, 4).Status);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(completed.Profile));
        Assert.Contains(saved!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == upgrade.Identity.MechanicalDefinitionId);
        Assert.Contains(saved.Underlying.InherentGrants, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
    }

    [Fact]
    public void UnselectedGoliathUpgradeDoesNotModifyAuthorizedDrowRange()
    {
        var proposal = new NativeOriginProposal("direct-drow-range",
            NativeOriginIntent.Direct, new("rule.creature-origination", "biological"),
            [], [], [], SelectedAncestryId: "ancestry.elf.drow");
        var casting = Assert.Single(Evaluate(proposal, defaults: true)
            .EvaluatedProposal.Candidates, x =>
                x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var completed = Evaluate(proposal with
        {
            Manifestations = [new("drow-casting", casting.Id,
                Choices: [new("spellcastingAbility", ["wisdom"])])]
        }, defaults: true);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var result = NativeOriginDarkvisionProjector.Project(Catalog(), completed);
        Assert.Equal("resolved", result.Status);
        Assert.Equal(120, result.Darkvision!.RangeFeet);
        Assert.Equal(120, result.Darkvision.BaseRangeFeet);
        Assert.Empty(result.Darkvision.Extensions!);
    }

    [Fact]
    public void MixedStoneCavernousDoesNotAcquireFirstPartyInheritancePermission()
    {
        var proposal = new NativeOriginProposal("no-implicit-upgrade",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [
                new("stone", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.goliath.stone"))
            ], [], []);
        var evaluated = Evaluate(proposal).EvaluatedProposal;
        var upgrade = Assert.Single(evaluated.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.goliath.stone.cavernous-sight");
        Assert.Equal(NativeOriginEligibility.Unreviewed,
            upgrade.SelectionBasis.Eligibility);
        var attempts = evaluated.Candidates.Where(x =>
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .Select((x, i) => new NativeOriginManifestation("foundation-" + i,
                x.Id, SelectedValue: x.OfferedValue))
            .Append(new("unsanctioned-upgrade", upgrade.Id)).ToArray();
        var rejected = Evaluate(proposal with { Manifestations = attempts });
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, rejected.Status);
        Assert.DoesNotContain(rejected.Profile?.Underlying.InherentGrants ?? [], x =>
            x.Identity.MechanicalDefinitionId == upgrade.Identity.MechanicalDefinitionId);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), rejected, 4).Status);
    }
}
