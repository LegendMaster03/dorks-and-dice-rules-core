using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Projects exact native climb, swim and burrow effects from the authorized
/// initial Origin. A movement mode's terrain exclusions and source identity
/// must survive without an invented mode bonus, breath grant or eligibility.
/// </summary>
public sealed class NativeOriginMovementModeProjectorTests
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
        throw new InvalidOperationException("First-party native catalog missing.");
    }

    private static NativeOriginConstructionResult Direct(string ancestry) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(new NativeOriginProposal("direct-" + ancestry,
                NativeOriginIntent.Direct, new("rule.creature-origination", "biological"),
                [], [], [], SelectedAncestryId: ancestry),
                AutoManifestDirectDefaults: true));

    private sealed class LocalAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "movement-projection" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "verified-movement";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    private static NativeTableRuleDecision Decision(string id, string kind,
        string target, object outcome) =>
        new(id, new("dm", "verified-movement"),
            new("campaign", "movement-projection"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private static NativeOriginConstructionResult Mixed(string[] ancestries,
        string[] selectedTraitIds, bool retainBothClimbs = false)
    {
        var catalog = Catalog();
        var contributors = ancestries.Select((ancestry, i) =>
            new NativeOriginContributor("source-" + i,
                new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, ancestry))).ToArray();
        var proposal = new NativeOriginProposal("mixed-movement-projection",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            contributors, [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var scope = new NativeTableDecisionScope("campaign", "movement-projection");
        var offers = engine.Evaluate(new(proposal)).EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "source-0" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);
        var traits = selectedTraitIds.Select(id =>
            Assert.Single(offers, x => x.Identity.MechanicalDefinitionId == id)).ToArray();
        Assert.All(traits, x =>
            Assert.Equal(NativeOriginEligibility.Eligible, x.SelectionBasis.Eligibility));

        var manifestations = foundation.Select((x, i) =>
            new NativeOriginManifestation("physical-" + i, x.Id,
                SelectedValue: x.OfferedValue)).Concat(traits.Select((x, i) =>
            new NativeOriginManifestation("mode-" + i, x.Id))).ToArray();
        var decisions = traits.Select((x, i) => Decision("permit-" + i, "candidate",
            x.Id, new { eligibility = "eligible" })).ToList();
        NativeOriginRelationship[] relations = [];
        NativeOriginResolution[] resolutions = [];
        if (retainBothClimbs)
        {
            Assert.Equal(2, traits.Length);
            var keep = Decision("dual-climb", "relationship", "two-climbs",
                new { action = "retain-both", candidateIds = traits.Select(x => x.Id).ToArray() });
            decisions.Add(keep);
            relations =
            [
                new("two-climbs", NativeOriginRelationshipKind.Equivalent,
                    traits.Select(x => x.Id).ToArray())
            ];
            resolutions =
            [
                new("two-climbs", NativeOriginResolutionAction.RetainBoth,
                    new(NativeOriginAdjudication.DmAuthorized,
                        TableDecisionId: keep.Id))
            ];
        }

        return engine.Evaluate(new(proposal with
        {
            Manifestations = manifestations, TableDecisions = decisions,
            Relationships = relations, Resolutions = resolutions
        }, TrustedScope: scope), new LocalAuthority());
    }

    [Theory]
    [InlineData("ancestry.dragonborn.copper",
        "trait.natural-climber", "climb")]
    [InlineData("ancestry.dragonborn.white",
        "trait.coldwater-swimmer", "swim")]
    [InlineData("ancestry.dragonborn.brass",
        "trait.sand-burrower", "burrow")]
    public void ConventionalAncestryProjectsOnlyItsActualNativeMovementMode(
        string ancestry, string traitId, string expectedMode)
    {
        var catalog = Catalog();
        var result = Direct(ancestry);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var view = NativeOriginMovementModeProjector.Project(catalog, result);
        Assert.Equal("resolved", view.Status);
        Assert.True(view.PreviewOnly);
        var chosen = Assert.Single(view.Modes);
        Assert.Equal(traitId, chosen.DefinitionId);
        Assert.Equal(ancestry, chosen.SourceId);
        Assert.Equal(expectedMode, chosen.Mode);
        Assert.Equal(15, chosen.SpeedFeet);
        Assert.Equal(expectedMode, chosen.CompleteEffect
            .GetProperty("movementMode").GetString());
        Assert.Equal(15, chosen.CompleteEffect.GetProperty("speedFeet").GetInt32());
        Assert.StartsWith("/traits/", chosen.OccurrencePath);
        Assert.Equal("unresolved",
            NativeOriginMovementModeProjector.ProjectApprovedMixed(
                catalog, result).Status);
    }

    [Fact]
    public void SwimAndBurrowPreserveEnvironmentalLimitations()
    {
        var catalog = Catalog();
        var white = NativeOriginMovementModeProjector.Project(catalog,
            Direct("ancestry.dragonborn.white"));
        var swim = Assert.Single(white.Modes);
        Assert.False(swim.CompleteEffect.GetProperty("grantsWaterBreathing")
            .GetBoolean());

        var brass = NativeOriginMovementModeProjector.Project(catalog,
            Direct("ancestry.dragonborn.brass"));
        var burrow = Assert.Single(brass.Modes);
        Assert.Equal("loose-natural-sand", burrow.CompleteEffect
            .GetProperty("allowedMaterials")[0].GetString());
        Assert.False(burrow.CompleteEffect.GetProperty("negatesSuffocation")
            .GetBoolean());
        Assert.False(burrow.CompleteEffect
            .GetProperty("leavesPermanentTraversableTunnel").GetBoolean());
    }

    [Fact]
    public void MixedSelectedMovementGrantsKeepSeparateModesAndOriginalSources()
    {
        var catalog = Catalog();
        var result = Mixed(
            [
                "ancestry.dragonborn.red", "ancestry.dragonborn.white",
                "ancestry.dragonborn.brass"
            ],
            [
                "trait.natural-climber",
                "trait.coldwater-swimmer",
                "trait.sand-burrower"
            ]);
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code)));
        var view = NativeOriginMovementModeProjector.ProjectApprovedMixed(
            catalog, result);
        Assert.Equal("resolved", view.Status);
        Assert.Equal(new[] { "burrow", "climb", "swim" },
            view.Modes.Select(x => x.Mode).ToArray());
        Assert.All(view.Modes, x => Assert.Equal(15, x.SpeedFeet));
        Assert.Equal("ancestry.dragonborn.brass", view.Modes[0].SourceId);
        Assert.Equal("ancestry.dragonborn.red", view.Modes[1].SourceId);
        Assert.Equal("ancestry.dragonborn.white", view.Modes[2].SourceId);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile));
        var restored = NativeOriginMovementModeProjector.ProjectApprovedMixed(
            catalog, result with { Profile = saved });
        Assert.Equal("resolved", restored.Status);
        Assert.Equal(view.Modes.Select(x => x.CompleteEffect.GetRawText()),
            restored.Modes.Select(x => x.CompleteEffect.GetRawText()));
    }

    [Fact]
    public void TableApprovedTwoClimbModesAreNotFusedOrGivenExtraSpeed()
    {
        var catalog = Catalog();
        var result = Mixed(
            ["ancestry.dragonborn.red", "ancestry.dragonborn.copper"],
            [
                "trait.natural-climber",
                "trait.natural-climber"
            ], retainBothClimbs: true);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var view = NativeOriginMovementModeProjector.ProjectApprovedMixed(
            catalog, result);
        Assert.Equal("resolved", view.Status);
        Assert.Equal(2, view.Modes.Count);
        Assert.All(view.Modes, x =>
        {
            Assert.Equal("climb", x.Mode);
            Assert.Equal(15, x.SpeedFeet);
            Assert.Single(result.Profile!.Underlying.InherentGrants.Where(y =>
                y.Id == x.GrantId));
        });
        Assert.Equal(2, view.Modes.Select(x => x.DefinitionId).Distinct().Count());
    }

    [Fact]
    public void ForgedCandidateAndAlteredMovementValueFailClosed()
    {
        var catalog = Catalog();
        var original = Direct("ancestry.dragonborn.copper");
        var profile = original.Profile!;
        var grant = Assert.Single(profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.natural-climber");
        var modified = JsonNode.Parse(grant.EffectiveValue!.Value.GetRawText())!;
        modified["speedFeet"] = 10000;
        var altered = grant with
        {
            EffectiveValue = JsonSerializer.SerializeToElement(modified)
        };
        Assert.Equal("unresolved",
            NativeOriginMovementModeProjector.Project(catalog,
                Replace(original, altered)).Status);
        Assert.Equal("unresolved",
            NativeOriginMovementModeProjector.Project(catalog,
                Replace(original, grant with
                {
                    SourceCandidateIds = ["forged-source"]
                })).Status);
    }

    [Fact]
    public void IncompleteOriginAndAbsentMovementAreNotEffectiveGrants()
    {
        var catalog = Catalog();
        var directOrc = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(
            new NativeOriginProposal("orc-no-movement",
                NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"),
                [], [], [], SelectedSpeciesId: "species.orc"));
        Assert.Equal("none",
            NativeOriginMovementModeProjector.Project(catalog, directOrc).Status);

        var copper = Direct("ancestry.dragonborn.copper");
        var incomplete = copper with
        {
            Status = NativeOriginConstructionStatus.Incomplete
        };
        var unresolved = NativeOriginMovementModeProjector.Project(
            catalog, incomplete);
        Assert.Equal("unresolved", unresolved.Status);
        Assert.Empty(unresolved.Modes);
    }

    private static NativeOriginConstructionResult Replace(
        NativeOriginConstructionResult result, NativeOriginEffectiveGrant replacement)
    {
        var profile = result.Profile!;
        var changed = profile.Current with
        {
            InherentGrants = profile.Current.InherentGrants.Select(x =>
                x.Id == replacement.Id ? replacement : x).ToArray()
        };
        return result with
        {
            Profile = profile with { Underlying = changed, Current = changed }
        };
    }
}
