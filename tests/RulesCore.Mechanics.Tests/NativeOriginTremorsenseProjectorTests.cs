using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Verifies read-only native sense projections against the real catalog and
/// constructor. Explicitly approved coexistence does not fuse activation or
/// resource pools and serialization is not a replacement for authorization.
/// </summary>
public sealed class NativeOriginTremorsenseProjectorTests
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
        throw new InvalidOperationException("Native catalog unavailable.");
    }

    private static NativeOriginConstructionResult Direct(string species) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(new NativeOriginProposal("direct-" + species,
                NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"),
                [], [], [], SelectedSpeciesId: species),
                AutoManifestDirectDefaults: true));

    private static NativeOriginContributor Contributor(string id, string source) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, source));

    private static NativeTableRuleDecision Permit(string id, string kind,
        string target, object outcome) =>
        new(id, new("dm", "trusted-sense-test"),
            new("campaign", "sense-projection"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "sense-projection" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "trusted-sense-test";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    private static NativeOriginConstructionResult ApprovedBoth()
    {
        var catalog = Catalog();
        var original = new NativeOriginProposal("both-selected-senses",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [Contributor("dwarf", "species.dwarf"),
                Contributor("rock", "ancestry.gnome.rock")], [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var offered = engine.Evaluate(new(original)).EvaluatedProposal.Candidates;
        var stone = Assert.Single(offered, x => x.ContributorId == "dwarf" &&
            x.Identity.MechanicalDefinitionId == "trait.dwarf.stonebound-tremorsense");
        var earth = Assert.Single(offered, x => x.ContributorId == "rock" &&
            x.Identity.MechanicalDefinitionId ==
                "trait.gnome.rock.earthbound-tremorsense");
        var foundation = offered.Where(x => x.ContributorId == "dwarf" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);

        var coexistence = Permit("retain-both", "relationship", "dual-sense",
            new { action = "retain-both" });
        var completedProposal = original with
        {
            Manifestations = foundation.Select((x, i) =>
                new NativeOriginManifestation("foundation-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat(
                [
                    new("selected-stone", stone.Id),
                    new("selected-earth", earth.Id)
                ]).ToArray(),
            TableDecisions =
            [
                Permit("allow-stone", "candidate", stone.Id,
                    new { eligibility = "eligible" }),
                Permit("allow-earth", "candidate", earth.Id,
                    new { eligibility = "eligible" }),
                coexistence
            ],
            Relationships =
            [
                new("dual-sense", NativeOriginRelationshipKind.Equivalent,
                    [stone.Id, earth.Id])
            ],
            Resolutions =
            [
                new("dual-sense", NativeOriginResolutionAction.RetainBoth,
                    new(NativeOriginAdjudication.DmAuthorized,
                        TableDecisionId: coexistence.Id))
            ]
        };
        return engine.Evaluate(new(completedProposal,
            TrustedScope: new("campaign", "sense-projection"),
            AutoManifestDirectDefaults: false), new TestAuthority());
    }

    [Fact]
    public void DirectDwarfPreservesActivatedSenseAndItsOwnUsePool()
    {
        var catalog = Catalog();
        var completed = Direct("species.dwarf");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var projected = NativeOriginTremorsenseProjector.Project(catalog, completed);
        Assert.Equal("resolved", projected.Status);
        Assert.True(projected.PreviewOnly);
        var sense = Assert.Single(projected.Senses);
        Assert.Equal("trait.dwarf.stonebound-tremorsense", sense.DefinitionId);
        Assert.Equal("species.dwarf", sense.SourceId);
        Assert.Equal(60, sense.RangeFeet);
        Assert.Equal("bonus-action", sense.Activation);
        Assert.Equal("stone", sense.RequiresContact);
        Assert.Empty(sense.RequiresSharedContact);
        Assert.Equal(10, sense.DurationMinutes);
        Assert.Equal("proficiency-bonus", sense.UsesFormula);
        Assert.Equal("long-rest", sense.Recharge);
        Assert.Equal(sense.GrantId, sense.ResourcePoolOwnerGrantId);
        var grant = Assert.Single(completed.Profile!.Underlying.InherentGrants,
            x => x.Id == sense.GrantId);
        Assert.Equal(grant.SourceCandidateIds, sense.SourceCandidateIds);
        Assert.True(JsonElement.DeepEquals(grant.EffectiveValue!.Value,
            sense.CompleteEffect));
        Assert.Equal("unresolved",
            NativeOriginTremorsenseProjector.ProjectApprovedMixed(catalog, completed)
                .Status);
    }

    [Fact]
    public void ExplicitMixedCoexistenceRetainsTwoIndependentSenseSemanticsAfterRoundTrip()
    {
        var catalog = Catalog();
        var approved = ApprovedBoth();
        Assert.True(approved.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", approved.Issues.Select(x => x.Code + ":" + x.Detail)));
        var view = NativeOriginTremorsenseProjector.ProjectApprovedMixed(catalog, approved);
        Assert.Equal("resolved", view.Status);
        Assert.Equal(2, view.Senses.Count);
        var stone = Assert.Single(view.Senses, x =>
            x.DefinitionId == "trait.dwarf.stonebound-tremorsense");
        var earth = Assert.Single(view.Senses, x =>
            x.DefinitionId == "trait.gnome.rock.earthbound-tremorsense");
        Assert.Equal("species.dwarf", stone.SourceId);
        Assert.Equal("ancestry.gnome.rock", earth.SourceId);
        Assert.Equal(60, stone.RangeFeet);
        Assert.Equal(15, earth.RangeFeet);
        Assert.Equal("passive", earth.Activation);
        Assert.Null(earth.RequiresContact);
        Assert.Equal(new[] { "earth", "stone" }, earth.RequiresSharedContact);
        Assert.Null(earth.DurationMinutes);
        Assert.Null(earth.UsesFormula);
        Assert.Null(earth.Recharge);
        Assert.Null(earth.ResourcePoolOwnerGrantId);
        Assert.Equal(stone.GrantId, stone.ResourcePoolOwnerGrantId);
        Assert.Equal("proficiency-bonus", stone.UsesFormula);
        Assert.Empty(view.UnresolvedReasons);
        Assert.Equal("unresolved",
            NativeOriginTremorsenseProjector.Project(catalog, approved).Status);

        var restored = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(approved.Profile))!;
        var afterLoad = NativeOriginTremorsenseProjector.ProjectApprovedMixed(
            catalog, approved with { Profile = restored });
        Assert.Equal("resolved", afterLoad.Status);
        Assert.Equal(view.Senses.Select(x => x.GrantId),
            afterLoad.Senses.Select(x => x.GrantId));
        Assert.True(JsonElement.DeepEquals(earth.CompleteEffect,
            afterLoad.Senses.Single(x => x.DefinitionId == earth.DefinitionId)
                .CompleteEffect));

        // Removing the original authority, not changing the saved profile,
        // revokes the construction's ability to produce a verified projection.
        var denied = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(approved.EvaluatedProposal));
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, denied.Status);
        Assert.Equal("unresolved",
            NativeOriginTremorsenseProjector.ProjectApprovedMixed(catalog, denied)
                .Status);
    }

    [Fact]
    public void ForgedPayloadAndCandidateProvenanceFailClosed()
    {
        var catalog = Catalog();
        var completed = Direct("species.dwarf");
        var grant = Assert.Single(completed.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId ==
                "trait.dwarf.stonebound-tremorsense");
        var forgedValue = JsonSerializer.SerializeToElement(new
        {
            id = "stonebound-tremorsense",
            sense = "tremorsense",
            rangeFeet = 999,
            activation = "bonus-action"
        });
        NativeOriginConstructionResult Tampered(NativeOriginEffectiveGrant changedGrant)
        {
            var current = completed.Profile.Underlying with
            {
                InherentGrants = completed.Profile.Underlying.InherentGrants
                    .Select(x => x.Id == grant.Id ? changedGrant : x).ToArray()
            };
            return completed with
            {
                Profile = completed.Profile with
                {
                    Underlying = current, Current = current
                }
            };
        }

        var effect = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with { EffectiveValue = forgedValue }));
        Assert.Equal("unresolved", effect.Status);
        Assert.Contains(effect.UnresolvedReasons,
            x => x.StartsWith("tremorsense-grant-not-verified:",
                StringComparison.Ordinal));

        var source = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with { SourceCandidateIds = ["invented-candidate"] }));
        Assert.Equal("unresolved", source.Status);
        var authority = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with
            {
                Authorization = grant.Authorization! with
                {
                    NativeRuleReference = "species.elf"
                }
            }));
        Assert.Equal("unresolved", authority.Status);
        // The authored modes have no linked-resource or configurable
        // parameter grammar; such claims must not become usable charges.
        var fabricatedPool = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with
            {
                ResourceOwners = new Dictionary<string, string>
                {
                    ["uses"] = "invented-resource-owner"
                }
            }));
        Assert.Equal("unresolved", fabricatedPool.Status);
        var fabricatedParameter = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with
            {
                Parameters = new Dictionary<string, JsonElement>
                {
                    ["rangeFeet"] = JsonSerializer.SerializeToElement(500)
                }
            }));
        Assert.Equal("unresolved", fabricatedParameter.Status);
        Assert.Equal("unresolved", NativeOriginTremorsenseProjector.Project(
            catalog, completed with
            {
                Status = NativeOriginConstructionStatus.Unresolved
            }).Status);
    }

    [Fact]
    public void AbsentSenseIsNoneRatherThanAnInferredGrant()
    {
        var catalog = Catalog();
        var elf = Direct("species.elf");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, elf.Status);
        var view = NativeOriginTremorsenseProjector.Project(catalog, elf);
        Assert.Equal("none", view.Status);
        Assert.Empty(view.Senses);
    }
}
