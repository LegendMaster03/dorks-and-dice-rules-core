using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Tests cross-definition overlap against actual native sources. The reconciler
/// must not infer that two sense modes are interchangeable, merge their resource
/// pools or silently give the offspring both abilities.
/// </summary>
public sealed class NativeOriginSenseOverlapTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var root = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                    "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root,
                    "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Native catalog is missing.");
    }

    private static NativeOriginContributor Contributor(string id, string source) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, source));

    private static NativeOriginProposal Proposal() =>
        new("dwarf-rock-selected-senses", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                Contributor("dwarf", "species.dwarf"),
                Contributor("rock", "ancestry.gnome.rock")
            ], [], []);

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null : new("campaign", "sense-test"),
                AutoManifestDirectDefaults: false), authority);

    private static NativeTableRuleDecision Permit(string id, string kind,
        string target, object outcome) =>
        new(id, new("dm", "trusted-sense-test"),
            new("campaign", "sense-test"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "sense-test" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "trusted-sense-test";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    private static (NativeOriginProposal Base, NativeOriginCandidate Stonebound,
        NativeOriginCandidate Earthbound, IReadOnlyList<NativeTableRuleDecision> Permits)
        BothSenses()
    {
        var original = Proposal();
        var offered = Evaluate(original).EvaluatedProposal.Candidates;
        var stone = Assert.Single(offered, x =>
            x.ContributorId == "dwarf" &&
            x.Identity.MechanicalDefinitionId == "trait.dwarf.stonebound-tremorsense");
        var earth = Assert.Single(offered, x =>
            x.ContributorId == "rock" &&
            x.Identity.MechanicalDefinitionId ==
                "trait.gnome.rock.earthbound-tremorsense");
        var physical = offered.Where(x => x.ContributorId == "dwarf" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, physical.Length);
        var baseSelections = physical.Select((x, i) =>
            new NativeOriginManifestation("physical-" + i, x.Id,
                SelectedValue: x.OfferedValue)).Concat(
            [
                new("selected-stonebound", stone.Id),
                new("selected-earthbound", earth.Id)
            ]).ToArray();
        var permissions = new[]
        {
            Permit("eligible-stone", "candidate", stone.Id,
                new { eligibility = "eligible" }),
            Permit("eligible-earth", "candidate", earth.Id,
                new { eligibility = "eligible" })
        };
        return (original with
        {
            Manifestations = baseSelections,
            TableDecisions = permissions
        }, stone, earth, permissions);
    }

    [Fact]
    public void OverlappingTremorsenseMustBeReconciledRatherThanSilentlyCombined()
    {
        var (proposal, stone, earth, _) = BothSenses();
        var result = Evaluate(proposal, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "same-sense-reconciliation-required" &&
            x.Path == "tremorsense");
        Assert.DoesNotContain(result.Profile?.Underlying.InherentGrants ?? [], x =>
            x.Identity.MechanicalDefinitionId is
                "trait.dwarf.stonebound-tremorsense" or
                "trait.gnome.rock.earthbound-tremorsense");
        Assert.Contains(result.EvaluatedProposal.Candidates,
            x => x.Id == stone.Id);
        Assert.Contains(result.EvaluatedProposal.Candidates,
            x => x.Id == earth.Id);
    }

    [Fact]
    public void PlayerChosenStoneboundOnlyPreservesItsFullNativeMechanic()
    {
        var (proposal, stone, earth, decisions) = BothSenses();
        var disposition = Permit("sense-stone-only", "relationship", "one-sense",
            new { action = "retain-first" });
        var resolved = proposal with
        {
            TableDecisions = decisions.Append(disposition).ToArray(),
            Relationships =
            [
                new("one-sense", NativeOriginRelationshipKind.Substitution,
                    [stone.Id, earth.Id])
            ],
            Resolutions =
            [
                new("one-sense", NativeOriginResolutionAction.RetainFirst,
                    new(NativeOriginAdjudication.DmAuthorized,
                        TableDecisionId: disposition.Id))
            ]
        };
        var result = Evaluate(resolved, new TestAuthority());
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Detail)));
        var grants = result.Profile!.Underlying.InherentGrants;
        Assert.Equal(4, grants.Count);
        var selected = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.dwarf.stonebound-tremorsense");
        Assert.DoesNotContain(grants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.gnome.rock.earthbound-tremorsense");
        var value = selected.EffectiveValue!.Value;
        Assert.Equal("tremorsense", value.GetProperty("sense").GetString());
        Assert.Equal(60, value.GetProperty("rangeFeet").GetInt32());
        Assert.Equal("bonus-action",
            value.GetProperty("activation").GetString());
        Assert.Equal(10, value.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("stone", value.GetProperty("requiresContact").GetString());
        Assert.Equal("proficiency-bonus", value.GetProperty("uses")
            .GetProperty("value").GetString());
        Assert.Equal("long-rest", value.GetProperty("uses")
            .GetProperty("recharge").GetString());
        Assert.Equal([stone.Id], selected.SourceCandidateIds);
        Assert.Empty(result.Issues);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile));
        var restored = Assert.Single(saved!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.dwarf.stonebound-tremorsense");
        Assert.Equal(value.GetRawText(), restored.EffectiveValue!.Value.GetRawText());
    }

    [Fact]
    public void ExplicitlySelectingPassiveEarthboundDoesNotInheritActivatedStonebound()
    {
        var (proposal, stone, earth, decisions) = BothSenses();
        var disposition = Permit("sense-earth-only", "relationship", "one-sense",
            new { action = "retain-second" });
        var result = Evaluate(proposal with
        {
            TableDecisions = decisions.Append(disposition).ToArray(),
            Relationships =
            [
                new("one-sense", NativeOriginRelationshipKind.Substitution,
                    [stone.Id, earth.Id])
            ],
            Resolutions =
            [
                new("one-sense", NativeOriginResolutionAction.RetainSecond,
                    new(NativeOriginAdjudication.DmAuthorized,
                        TableDecisionId: disposition.Id))
            ]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var sense = Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.gnome.rock.earthbound-tremorsense");
        var value = sense.EffectiveValue!.Value;
        Assert.Equal(15, value.GetProperty("rangeFeet").GetInt32());
        Assert.Equal("passive", value.GetProperty("activation").GetString());
        Assert.Equal(2, value.GetProperty("requiresSharedContact").GetArrayLength());
        Assert.False(value.TryGetProperty("uses", out _));
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.dwarf.stonebound-tremorsense");
    }

    [Fact]
    public void ExplicitScopedCoexistenceCanRetainBothButNeitherModeIsUpgraded()
    {
        var (proposal, stone, earth, decisions) = BothSenses();
        var disposition = Permit("allow-two-modes", "relationship", "dual-mode",
            new { action = "retain-both" });
        var result = Evaluate(proposal with
        {
            TableDecisions = decisions.Append(disposition).ToArray(),
            Relationships =
            [
                new("dual-mode", NativeOriginRelationshipKind.Equivalent,
                    [stone.Id, earth.Id])
            ],
            Resolutions =
            [
                new("dual-mode", NativeOriginResolutionAction.RetainBoth,
                    new(NativeOriginAdjudication.DmAuthorized,
                        TableDecisionId: disposition.Id))
            ]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var senses = result.Profile!.Underlying.InherentGrants.Where(x =>
            x.Identity.MechanicalDefinitionId is "trait.dwarf.stonebound-tremorsense"
                or "trait.gnome.rock.earthbound-tremorsense").ToArray();
        Assert.Equal(2, senses.Length);
        Assert.Equal(60, senses.Single(x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.dwarf.stonebound-tremorsense")
            .EffectiveValue!.Value.GetProperty("rangeFeet").GetInt32());
        Assert.Equal("passive", senses.Single(x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.gnome.rock.earthbound-tremorsense")
            .EffectiveValue!.Value.GetProperty("activation").GetString());
        Assert.DoesNotContain(result.Issues, x =>
            x.Code == "same-sense-reconciliation-required");

        // A serialized but unauthenticated disposition cannot turn the
        // identical proposed selections into a first-party dual-mode grant.
        var withoutAuthority = Evaluate(result.EvaluatedProposal);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized,
            withoutAuthority.Status);
        Assert.DoesNotContain(withoutAuthority.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.MechanicalDefinitionId is
                "trait.dwarf.stonebound-tremorsense" or
                "trait.gnome.rock.earthbound-tremorsense");
    }

    [Fact]
    public void AnUnrelatedPassiveVisionGrantDoesNotConflictWithTremorsense()
    {
        var original = new NativeOriginProposal("stone-and-low-light",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                Contributor("dwarf", "species.dwarf"),
                Contributor("elf", "species.elf")
            ], [], []);
        var candidates = Evaluate(original).EvaluatedProposal.Candidates;
        var stone = Assert.Single(candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.dwarf.stonebound-tremorsense");
        var vision = Assert.Single(candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision");
        var foundation = candidates.Where(x => x.ContributorId == "dwarf" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var result = Evaluate(original with
        {
            Manifestations = foundation.Select((x, i) =>
                new NativeOriginManifestation("physical-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat(
                [
                    new("stone", stone.Id),
                    new("vision", vision.Id)
                ]).ToArray(),
            TableDecisions =
            [
                Permit("eligible-stone", "candidate", stone.Id,
                    new { eligibility = "eligible" })
            ]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Contains(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == stone.Identity.MechanicalDefinitionId);
        Assert.Contains(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == vision.Identity.MechanicalDefinitionId);
        Assert.DoesNotContain(result.Issues, x =>
            x.Code == "same-sense-reconciliation-required");
    }
}
