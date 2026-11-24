using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Real native Dragonborn movement modes: distinct source occurrences of a
/// shared gene still require reconciliation, and different modes must not stack
/// or receive invented speed and terrain permissions.
/// Tests use exact in-process DM decisions only for traits that remain outside
/// the approved first-party biological inheritance set.
/// </summary>
public sealed class NativeOriginMovementOverlapTests
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
        throw new InvalidOperationException("First-party native catalog is missing.");
    }

    private static NativeOriginContributor Donor(string id, string ancestry) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, ancestry));

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null,
        bool directDefaults = false) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null : new("campaign", "movement-test"),
                AutoManifestDirectDefaults: directDefaults), authority);

    private static NativeTableRuleDecision Decision(string id, string kind,
        string target, object outcome) =>
        new(id, new("dm", "verified-movement-evidence"),
            new("campaign", "movement-test"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "movement-test" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "verified-movement-evidence";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    private static (NativeOriginProposal Proposal, NativeOriginCandidate Red,
        NativeOriginCandidate Copper) TwoClimbs()
    {
        var initial = new NativeOriginProposal("two-climb-modes",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [
                Donor("red", "ancestry.dragonborn.red"),
                Donor("copper", "ancestry.dragonborn.copper")
            ], [], []);
        var offers = Evaluate(initial).EvaluatedProposal.Candidates;
        var red = Assert.Single(offers, x => x.ContributorId == "red" &&
            x.Identity.MechanicalDefinitionId ==
                "trait.natural-climber");
        var copper = Assert.Single(offers, x => x.ContributorId == "copper" &&
            x.Identity.MechanicalDefinitionId ==
                "trait.natural-climber");
        Assert.Equal(NativeOriginEligibility.Eligible,
            red.SelectionBasis.Eligibility);
        Assert.Equal(NativeOriginEligibility.Eligible,
            copper.SelectionBasis.Eligibility);
        var foundation = offers.Where(x => x.ContributorId == "red" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or
                "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);
        return (initial with
        {
            Manifestations = foundation.Select((x, i) =>
                new NativeOriginManifestation("physical-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat(
                [
                    new("climb-red", red.Id),
                    new("climb-copper", copper.Id)
                ]).ToArray(),
            TableDecisions =
            [
                Decision("allow-red", "candidate", red.Id,
                    new { eligibility = "eligible" }),
                Decision("allow-copper", "candidate", copper.Id,
                    new { eligibility = "eligible" })
            ]
        }, red, copper);
    }

    [Fact]
    public void SharedClimbGeneFromDifferentSourcesRequiresExplicitDisposition()
    {
        var (proposal, red, copper) = TwoClimbs();
        var result = Evaluate(proposal, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "shared-definition-reconciliation-required" &&
            x.Path == "trait.natural-climber");
        Assert.DoesNotContain(result.Profile?.Underlying.InherentGrants ?? [], x =>
            x.Identity.MechanicalDefinitionId == "trait.natural-climber");
        Assert.Contains(result.EvaluatedProposal.Candidates, x => x.Id == red.Id);
        Assert.Contains(result.EvaluatedProposal.Candidates, x => x.Id == copper.Id);
    }

    [Theory]
    [InlineData(true, "trait.natural-climber")]
    [InlineData(false, "trait.natural-climber")]
    public void ScopedChoiceRetainsOneCompleteAuthoredClimbTrait(
        bool keepFirst, string expectedTrait)
    {
        var (proposal, red, copper) = TwoClimbs();
        var action = keepFirst
            ? NativeOriginResolutionAction.RetainFirst
            : NativeOriginResolutionAction.RetainSecond;
        var decision = Decision("retain-one-climb", "relationship", "climb-choice",
            new { action = keepFirst ? "retain-first" : "retain-second" });
        var result = Evaluate(proposal with
        {
            Relationships =
            [
                new("climb-choice", NativeOriginRelationshipKind.Substitution,
                    [red.Id, copper.Id])
            ],
            Resolutions =
            [
                new("climb-choice", action,
                    new(NativeOriginAdjudication.DmAuthorized,
                        TableDecisionId: decision.Id))
            ],
            TableDecisions = proposal.TableDecisions!.Append(decision).ToArray()
        }, new TestAuthority());
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Detail)));
        var climb = Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == expectedTrait);
        Assert.Single(climb.SourceCandidateIds);
        Assert.Equal(15, climb.EffectiveValue!.Value.GetProperty("speedFeet").GetInt32());
        Assert.Equal("climb", climb.EffectiveValue.Value
            .GetProperty("movementMode").GetString());
        Assert.Contains("spider-climb", climb.EffectiveValue.Value
            .GetProperty("doesNotGrant").EnumerateArray()
            .Select(x => x.GetString()));
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.natural-climber" &&
                x.Id != climb.Id);

        var roundTripped = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile));
        var restored = Assert.Single(roundTripped!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == expectedTrait);
        Assert.Equal(climb.EffectiveValue.Value.GetRawText(),
            restored.EffectiveValue!.Value.GetRawText());
    }

    [Fact]
    public void ExplicitCoexistenceKeepsTwoModesWithoutCombiningTheirSpeed()
    {
        var (proposal, red, copper) = TwoClimbs();
        var decision = Decision("two-independent-climbs", "relationship",
            "two-climb-modes", new { action = "retain-both", candidateIds = new[] { red.Id, copper.Id } });
        var eligible = proposal with
        {
            Relationships =
            [
                new("two-climb-modes", NativeOriginRelationshipKind.Equivalent,
                    [red.Id, copper.Id])
            ],
            Resolutions =
            [
                new("two-climb-modes", NativeOriginResolutionAction.RetainBoth,
                    new(NativeOriginAdjudication.DmAuthorized,
                        TableDecisionId: decision.Id))
            ],
            TableDecisions = proposal.TableDecisions!.Append(decision).ToArray()
        };
        var result = Evaluate(eligible, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var selected = result.Profile!.Underlying.InherentGrants.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.natural-climber").ToArray();
        Assert.Equal(2, selected.Length);
        Assert.All(selected, x =>
        {
            Assert.Equal("climb",
                x.EffectiveValue!.Value.GetProperty("movementMode").GetString());
            Assert.Equal(15, x.EffectiveValue.Value.GetProperty("speedFeet").GetInt32());
            Assert.Single(x.SourceCandidateIds);
        });
        Assert.DoesNotContain(result.Issues, x =>
            x.Code == "same-movement-mode-reconciliation-required");

        // No saved relationship labels can masquerade as an authenticated table
        // decision. The absent authority must fail the same submitted proposal.
        var unauthenticated = Evaluate(result.EvaluatedProposal);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, unauthenticated.Status);
        Assert.DoesNotContain(unauthenticated.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.MechanicalDefinitionId == "trait.natural-climber");
    }

    [Fact]
    public void DistinctClimbSwimAndBurrowModesDoNotConflictOrConferBreathing()
    {
        var baseProposal = new NativeOriginProposal("separate-movement-modes",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [
                Donor("red", "ancestry.dragonborn.red"),
                Donor("white", "ancestry.dragonborn.white"),
                Donor("brass", "ancestry.dragonborn.brass")
            ], [], []);
        var offered = Evaluate(baseProposal).EvaluatedProposal.Candidates;
        var foundational = offered.Where(x => x.ContributorId == "red" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundational.Length);
        var ids = new[]
        {
            "trait.natural-climber",
            "trait.coldwater-swimmer",
            "trait.sand-burrower"
        };
        var movement = ids.Select(id => Assert.Single(offered,
            x => x.Identity.MechanicalDefinitionId == id)).ToArray();
        Assert.All(movement, x => Assert.Equal(
            NativeOriginEligibility.Eligible, x.SelectionBasis.Eligibility));
        var result = Evaluate(baseProposal with
        {
            Manifestations = foundational.Select((x, i) =>
                new NativeOriginManifestation("physical-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat(movement.Select((x, i) =>
                new NativeOriginManifestation("movement-" + i, x.Id))).ToArray(),
            TableDecisions = movement.Select((x, i) =>
                Decision("allow-movement-" + i, "candidate", x.Id,
                    new { eligibility = "eligible" })).ToArray()
        }, new TestAuthority());

        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.DoesNotContain(result.Issues, x =>
            x.Code == "same-movement-mode-reconciliation-required");
        var chosen = result.Profile!.Underlying.InherentGrants.Where(x =>
            ids.Contains(x.Identity.MechanicalDefinitionId)).ToArray();
        Assert.Equal(3, chosen.Length);
        Assert.Equal(new[] { "burrow", "climb", "swim" },
            chosen.Select(x => x.EffectiveValue!.Value.GetProperty("movementMode")
                .GetString()).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        var swim = Assert.Single(chosen, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.coldwater-swimmer");
        Assert.False(swim.EffectiveValue!.Value
            .GetProperty("grantsWaterBreathing").GetBoolean());
        var burrow = Assert.Single(chosen, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.sand-burrower");
        Assert.Equal("loose-natural-sand", burrow.EffectiveValue!.Value
            .GetProperty("allowedMaterials")[0].GetString());
        Assert.False(burrow.EffectiveValue.Value
            .GetProperty("negatesSuffocation").GetBoolean());
    }

    [Fact]
    public void NormalCopperDragonbornKeepsItsOwnUnmodifiedClimbTrait()
    {
        var direct = Evaluate(new NativeOriginProposal("direct-copper",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: "ancestry.dragonborn.copper"),
            directDefaults: true);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, direct.Status);
        var climb = Assert.Single(direct.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.natural-climber");
        Assert.Equal(15, climb.EffectiveValue!.Value.GetProperty("speedFeet").GetInt32());
        Assert.Equal("ancestry.dragonborn.copper",
            climb.Identity.Occurrence.Source.Id);
    }
}
