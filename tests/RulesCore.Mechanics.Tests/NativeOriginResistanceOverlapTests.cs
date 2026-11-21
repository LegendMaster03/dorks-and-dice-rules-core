using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Real-catalog permanent resistance reconciliation. A same-damage overlap
/// cannot silently double-count percentages, duplicate whole traits or erase
/// unrelated saving-throw benefits and activation restrictions.
/// </summary>
public sealed class NativeOriginResistanceOverlapTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var root = Path.Combine(current.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                    "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001"));
            current = current.Parent;
        }
        throw new InvalidOperationException("The native catalog is missing.");
    }

    private static NativeOriginContributor Parent(string id, string source) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, source));

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null : new("campaign", "resistance-test"),
                AutoManifestDirectDefaults: false), authority);

    private static NativeTableRuleDecision Decision(string id, string kind,
        string target, object outcome) =>
        new(id, new("dm", "verified-resistance"),
            new("campaign", "resistance-test"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision d,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "resistance-test" &&
            d.Authority.ActorId == "dm" &&
            d.Authority.AuthorizationEvidenceId == "verified-resistance";

        public bool AuthorizesCatalogChoice(string catalog, string option,
            string evidence) => false;
    }

    private static (NativeOriginProposal Proposal, NativeOriginCandidate First,
        NativeOriginCandidate Second, IReadOnlyList<NativeTableRuleDecision> Permissions)
        Select(string donorA, string traitA, string donorB, string traitB)
    {
        var proposal = new NativeOriginProposal("two-resistances",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [Parent("a", donorA), Parent("b", donorB)], [], []);
        var candidates = Evaluate(proposal).EvaluatedProposal.Candidates;
        var a = Assert.Single(candidates, x => x.ContributorId == "a" &&
            x.Identity.MechanicalDefinitionId == traitA);
        var b = Assert.Single(candidates, x => x.ContributorId == "b" &&
            x.Identity.MechanicalDefinitionId == traitB);
        var physical = candidates.Where(x => x.ContributorId == "a" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        Assert.Equal(3, physical.Length);
        var selected = physical.Select((x, i) =>
            x.Identity.IntrinsicSlotId == "size" && x.OfferedValue is null
                ? new NativeOriginManifestation("physical-" + i, x.Id,
                    Choices: [new NativeOriginChoiceSelection("size", ["medium"])])
                : new NativeOriginManifestation("physical-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat(
            [
                new("first-resistance", a.Id),
                new("second-resistance", b.Id)
            ]).ToArray();
        var permits = new[]
        {
            Decision("permit-a", "candidate", a.Id, new { eligibility = "eligible" }),
            Decision("permit-b", "candidate", b.Id, new { eligibility = "eligible" })
        };
        return (proposal with
        {
            Manifestations = selected,
            TableDecisions = permits
        }, a, b, permits);
    }

    [Fact]
    public void DwarfAndAbyssalPoisonOverlapRequiresWholeTraitDisposition()
    {
        var (proposal, dwarf, tiefling, permits) = Select("species.dwarf",
            "trait.poison-resilience", "ancestry.tiefling.abyssal",
            "trait.abyssal-resistance");
        var unresolved = Evaluate(proposal, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, unresolved.Status);
        Assert.Contains(unresolved.Issues, x =>
            x.Code == "same-damage-resistance-reconciliation-required" &&
            x.Path == "poison");
        Assert.DoesNotContain(unresolved.Profile?.Underlying.InherentGrants ?? [], x =>
            x.Identity.MechanicalDefinitionId is
                "trait.poison-resilience" or
                "trait.abyssal-resistance");

        // The selected Dwarf option retains its full poison-resistance
        // *and* poison-saving-throw benefits. A type overlap does not
        // turn a composite authored trait into separate free fragments.
        var disposition = Decision("keep-dwarf", "relationship", "poison-overlap",
            new { action = "retain-first" });
        var result = Evaluate(proposal with
        {
            TableDecisions = permits.Append(disposition).ToArray(),
            Relationships =
            [
                new("poison-overlap", NativeOriginRelationshipKind.Substitution,
                    [dwarf.Id, tiefling.Id])
            ],
            Resolutions =
            [
                new("poison-overlap", NativeOriginResolutionAction.RetainFirst,
                    new(NativeOriginAdjudication.DmAuthorized,
                        TableDecisionId: disposition.Id))
            ]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal(4, result.Profile!.Underlying.InherentGrants.Count);
        var inherited = Assert.Single(result.Profile.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId ==
                "trait.poison-resilience");
        var value = inherited.EffectiveValue!.Value;
        Assert.Equal(50, value.GetProperty("damageResistance")
            .GetProperty("poison").GetInt32());
        Assert.Equal("poison-effects", value.GetProperty("savingThrows")
            .GetProperty("advantageAgainst")[0].GetString());
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.abyssal-resistance");
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void TwoPermanentFireResistancesRequireExplicitDisposition()
    {
        var (proposal, infernal, goliath, decisions) = Select(
            "ancestry.tiefling.infernal",
            "trait.infernal-resistance",
            "ancestry.goliath.fire", "trait.born-of-flame");
        var unresolved = Evaluate(proposal, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, unresolved.Status);
        Assert.Contains(unresolved.Issues, x =>
            x.Code == "same-damage-resistance-reconciliation-required" &&
            x.Path == "fire");

        var dm = Decision("keep-goliath", "relationship", "fire-overlap",
            new { action = "retain-second" });
        var result = Evaluate(proposal with
        {
            TableDecisions = decisions.Append(dm).ToArray(),
            Relationships =
            [
                new("fire-overlap", NativeOriginRelationshipKind.Substitution,
                    [infernal.Id, goliath.Id])
            ],
            Resolutions =
            [
                new("fire-overlap", NativeOriginResolutionAction.RetainSecond,
                    new(NativeOriginAdjudication.DmAuthorized, TableDecisionId: dm.Id))
            ]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var resistance = Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.born-of-flame");
        Assert.Equal(50, resistance.EffectiveValue!.Value.GetProperty("damageResistance")
            .GetProperty("percent").GetInt32());
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.infernal-resistance");
    }

    [Fact]
    public void BoundDragonbornDamageTypeIsIncludedInResistanceOverlap()
    {
        var (proposal, dragonborn, infernal, _) = Select(
            "ancestry.dragonborn.red", "trait.damage-type-resistance",
            "ancestry.tiefling.infernal",
            "trait.infernal-resistance");
        var result = Evaluate(proposal, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "same-damage-resistance-reconciliation-required" &&
            x.Path == "fire");
        Assert.Equal("fire", Assert.Single(result.EvaluatedProposal.Candidates, x =>
            x.Id == dragonborn.Id).OfferedParameters!["damageType"].GetString());
        Assert.NotEqual(dragonborn.Identity.MechanicalDefinitionId,
            infernal.Identity.MechanicalDefinitionId);
    }

    [Fact]
    public void DifferentDamageTypesAndTemporaryReactionDefenseRemainIndependent()
    {
        var (proposal, chthonic, goliath, _) = Select(
            "ancestry.tiefling.chthonic",
            "trait.chthonic-resistance",
            "ancestry.goliath.fire", "trait.born-of-flame");
        var distinct = Evaluate(proposal, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, distinct.Status);
        Assert.Contains(distinct.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == chthonic.Identity.MechanicalDefinitionId);
        Assert.Contains(distinct.Profile.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == goliath.Identity.MechanicalDefinitionId);
        Assert.DoesNotContain(distinct.Issues,
            x => x.Code == "same-damage-resistance-reconciliation-required");

        // Cloudy Escape has a reaction-scoped 50-percent defense but no
        // permanent type-based resistance. It does not compete with Fire.
        var (reactionProposal, fire, cloudy, _) = Select(
            "ancestry.tiefling.infernal",
            "trait.infernal-resistance",
            "ancestry.goliath.cloud", "trait.cloudy-escape");
        var reaction = Evaluate(reactionProposal, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, reaction.Status);
        Assert.Contains(reaction.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == fire.Identity.MechanicalDefinitionId);
        Assert.Contains(reaction.Profile.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == cloudy.Identity.MechanicalDefinitionId);
        Assert.DoesNotContain(reaction.Issues,
            x => x.Code == "same-damage-resistance-reconciliation-required");
    }
}
