using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Exercises native physiological effects from completed, actually selected
/// first-party biological Origins. Projection is never inheritance permission.
/// </summary>
public sealed class NativeOriginPhysiologyProjectorTests
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

    private static NativeOriginConstructionResult Mixed(
        NativeCatalogSnapshot catalog, params string[] selectedTraits)
    {
        var proposal = new NativeOriginProposal("physiology-test",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("bronze", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.dragonborn.bronze")),
                new("fire", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.goliath.fire"))
            ], [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var candidates = engine.Evaluate(new(proposal)).EvaluatedProposal.Candidates;

        // One complete physical foundation; selecting a second contributor
        // does not automatically transfer any of its traits or movement modes.
        var foundation = candidates.Where(x => x.ContributorId == "bronze" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);
        var physical = foundation.Select((x, i) =>
            new NativeOriginManifestation("foundation-" + i, x.Id,
                SelectedValue: x.OfferedValue));
        var chosen = selectedTraits.Select((id, i) =>
        {
            var offer = Assert.Single(candidates, x =>
                x.Identity.MechanicalDefinitionId == id);
            Assert.Equal(NativeOriginEligibility.Eligible,
                offer.SelectionBasis.Eligibility);
            return new NativeOriginManifestation("adaptation-" + i, offer.Id);
        });
        return engine.Evaluate(new(proposal with
        {
            Manifestations = physical.Concat(chosen).ToArray()
        }));
    }

    [Fact]
    public void AmphibiousFromRealBronzeContributorGrantsBreathingButNotSwimmingSpeed()
    {
        var catalog = Catalog();
        var result = Mixed(catalog, "trait.shared.amphibious");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var view = NativeOriginPhysiologyProjector.ProjectApprovedMixed(catalog, result);
        Assert.Equal("resolved", view.Status);
        Assert.True(view.PreviewOnly);
        Assert.Empty(view.UnresolvedReasons);
        Assert.Null(view.AltitudeAcclimation);
        var breathing = Assert.IsType<
            NativeOriginPhysiologyProjector.AmphibiousEffect>(view.Amphibious);
        Assert.Equal(new[] { "air", "water" }, breathing.CanBreathe);
        Assert.False(breathing.GrantsSwimSpeed);
        Assert.Equal("ancestry.dragonborn.bronze", breathing.SourceId);
        Assert.StartsWith("grant:", breathing.GrantId);
        Assert.StartsWith("/traits/", breathing.OccurrencePath);
        Assert.Equal("unresolved",
            NativeOriginPhysiologyProjector.Project(catalog, result).Status);
    }

    [Fact]
    public void AltitudeFromRealGoliathContributorPreservesScopeAndExclusions()
    {
        var catalog = Catalog();
        var result = Mixed(catalog, "trait.goliath.altitude-acclimation");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var view = NativeOriginPhysiologyProjector.ProjectApprovedMixed(catalog, result);
        Assert.Equal("resolved", view.Status);
        Assert.Null(view.Amphibious);
        var altitude = Assert.IsType<
            NativeOriginPhysiologyProjector.AltitudeAcclimationEffect>(
                view.AltitudeAcclimation);
        // The donor is the Fire Goliath leaf, but the actual canonical trait
        // comes from its shared Species source. Retain that provenance.
        Assert.Equal("species.goliath", altitude.SourceId);
        Assert.Contains(result.EvaluatedProposal.Candidates, candidate =>
            candidate.ContributorId == "fire" &&
            candidate.Identity.MechanicalDefinitionId ==
                "trait.goliath.altitude-acclimation");
        Assert.Equal(20000, altitude.IncludesElevationAboveFeet);
        Assert.Null(altitude.AltitudeLimitFeet);
        Assert.True(altitude.MaintainsAcclimationAtLowerElevations);
        Assert.Contains("cold-damage-resistance", altitude.DoesNotGrant);
        Assert.Contains("suffocation-immunity", altitude.DoesNotGrant);
    }

    [Fact]
    public void TwoSelectedIndependentAdaptationsSurviveProfileSerialization()
    {
        var catalog = Catalog();
        var result = Mixed(catalog, "trait.shared.amphibious",
            "trait.goliath.altitude-acclimation");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile));
        Assert.NotNull(saved);
        var roundTrip = NativeOriginPhysiologyProjector.ProjectApprovedMixed(
            catalog, result with { Profile = saved });
        Assert.Equal("resolved", roundTrip.Status);
        Assert.Equal("ancestry.dragonborn.bronze", roundTrip.Amphibious!.SourceId);
        Assert.Equal("species.goliath",
            roundTrip.AltitudeAcclimation!.SourceId);
        Assert.Empty(roundTrip.UnresolvedReasons);
    }

    [Fact]
    public void UnselectedOffersDoNotBecomeAutomaticAdaptations()
    {
        var catalog = Catalog();
        var completed = Mixed(catalog);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var view = NativeOriginPhysiologyProjector.ProjectApprovedMixed(catalog, completed);
        Assert.Equal("none", view.Status);
        Assert.Null(view.Amphibious);
        Assert.Null(view.AltitudeAcclimation);
    }

    [Fact]
    public void ConventionalBronzeDragonbornProjectsItsExistingAmphibiousGrant()
    {
        var catalog = Catalog();
        var result = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(new NativeOriginProposal("direct-bronze-physiology",
                NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"),
                [], [], [], SelectedAncestryId: "ancestry.dragonborn.bronze"),
                AutoManifestDirectDefaults: true));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var view = NativeOriginPhysiologyProjector.Project(catalog, result);
        Assert.Equal("resolved", view.Status);
        Assert.Equal("ancestry.dragonborn.bronze", view.Amphibious!.SourceId);
        Assert.Equal(new[] { "air", "water" }, view.Amphibious.CanBreathe);
        Assert.False(view.Amphibious.GrantsSwimSpeed);
        Assert.Null(view.AltitudeAcclimation);
        Assert.Equal("unresolved",
            NativeOriginPhysiologyProjector.ProjectApprovedMixed(catalog, result).Status);
    }

    [Fact]
    public void DirectSpeciesWithoutSelectedAdaptationsRemainsValid()
    {
        var catalog = Catalog();
        var result = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(
            new NativeOriginProposal("orc-physiology", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"),
                [], [], [], SelectedSpeciesId: "species.orc"));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var view = NativeOriginPhysiologyProjector.Project(catalog, result);
        Assert.Equal("none", view.Status);
        Assert.Empty(view.UnresolvedReasons);
        Assert.Equal("unresolved",
            NativeOriginPhysiologyProjector.ProjectApprovedMixed(catalog, result).Status);
    }

    [Fact]
    public void AlteredAmphibiousPayloadCannotClaimAFreeSwimSpeed()
    {
        var catalog = Catalog();
        var completed = Mixed(catalog, "trait.shared.amphibious");
        var trait = Assert.Single(completed.Profile!.Current.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.shared.amphibious");
        var modified = JsonNode.Parse(trait.EffectiveValue!.Value.GetRawText())!;
        modified["grantsSwimSpeed"] = true;
        var altered = Replace(completed, trait with
        {
            EffectiveValue = JsonSerializer.SerializeToElement(modified)
        });
        var view = NativeOriginPhysiologyProjector.ProjectApprovedMixed(catalog, altered);
        Assert.Equal("unresolved", view.Status);
        Assert.Null(view.Amphibious);
        Assert.Null(view.AltitudeAcclimation);
    }

    [Fact]
    public void ForgedCandidateEvidenceAndDuplicateGrantsFailClosed()
    {
        var catalog = Catalog();
        var completed = Mixed(catalog, "trait.shared.amphibious");
        var trait = Assert.Single(completed.Profile!.Current.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.shared.amphibious");
        var forged = Replace(completed, trait with
        {
            SourceCandidateIds = ["invented-offer"]
        });
        Assert.Equal("unresolved",
            NativeOriginPhysiologyProjector.ProjectApprovedMixed(catalog, forged).Status);

        var profile = completed.Profile!;
        var repeated = profile.Current with
        {
            InherentGrants = profile.Current.InherentGrants
                .Append(trait with { Id = "grant:duplicate" }).ToArray()
        };
        var duplicated = completed with
        {
            Profile = profile with
            {
                Underlying = repeated, Current = repeated
            }
        };
        Assert.Equal("unresolved",
            NativeOriginPhysiologyProjector.ProjectApprovedMixed(
                catalog, duplicated).Status);
    }

    private sealed class TestParentAuthority(NativeOriginProfile parent) :
        INativeOriginManifestedContributorAuthority
    {
        public NativeOriginVerifiedManifestedContributor? ResolveCurrent(
            NativeOriginSourceReference source, NativeTableDecisionScope trustedScope) =>
            source.Id == parent.Id &&
            trustedScope.Kind == "campaign" &&
            trustedScope.Id == "physiology-test"
                ? new(parent, "test-revision-1")
                : null;
    }

    [Fact]
    public void VerifiedMixedParentTransmitsOnlyItsManifestedPhysiology()
    {
        var catalog = Catalog();
        var parent = Mixed(catalog, "trait.shared.amphibious",
            "trait.goliath.altitude-acclimation");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, parent.Status);
        var profile = parent.Profile!;
        var proposal = new NativeOriginProposal("physiology-descendant",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("parent", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.ManifestedProfile,
                        profile.Id, "test-revision-1"))
            ], [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var authority = new TestParentAuthority(profile);
        var scope = new NativeTableDecisionScope("campaign", "physiology-test");
        var offers = engine.Evaluate(new(proposal, TrustedScope: scope),
            manifestedContributorAuthority: authority).EvaluatedProposal.Candidates;
        Assert.Equal(profile.Underlying.InherentGrants.Count, offers.Count);
        Assert.All(offers, x =>
            Assert.Equal(NativeOriginEligibility.Eligible,
                x.SelectionBasis.Eligibility));

        var selected = offers.Select((x, i) => new NativeOriginManifestation(
            "child-" + i, x.Id, SelectedValue:
                x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                    ? x.OfferedValue : null)).ToArray();
        var descendant = engine.Evaluate(new(proposal with
        {
            Manifestations = selected
        }, TrustedScope: scope), manifestedContributorAuthority: authority);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, descendant.Status);
        var physiology = NativeOriginPhysiologyProjector.ProjectApprovedMixed(
            catalog, descendant);
        Assert.Equal("resolved", physiology.Status);
        Assert.Equal("ancestry.dragonborn.bronze", physiology.Amphibious!.SourceId);
        Assert.Equal("species.goliath",
            physiology.AltitudeAcclimation!.SourceId);
        foreach (var grant in descendant.Profile!.Underlying.InherentGrants.Where(x =>
            x.Identity.MechanicalDefinitionId is
                "trait.shared.amphibious" or "trait.goliath.altitude-acclimation"))
        {
            var inherited = Assert.Single(grant.InheritedFrom!);
            Assert.Equal(profile.Id, inherited.ContributorProfileId);
            Assert.Equal("test-revision-1", inherited.ContributorProfileRevision);
            Assert.Equal("parent", inherited.ContributorId);
        }
    }

    [Fact]
    public void InvalidOrIncompleteMixedConstructionHasNoExecutableAdaptation()
    {
        var catalog = Catalog();
        var completed = Mixed(catalog, "trait.shared.amphibious");
        var incomplete = completed with
        {
            Status = NativeOriginConstructionStatus.Incomplete
        };
        var view = NativeOriginPhysiologyProjector.ProjectApprovedMixed(
            catalog, incomplete);
        Assert.Equal("unresolved", view.Status);
        Assert.Null(view.Amphibious);
    }

    private static NativeOriginConstructionResult Replace(
        NativeOriginConstructionResult result, NativeOriginEffectiveGrant replacement)
    {
        var profile = result.Profile!;
        var changed = profile.Current with
        {
            InherentGrants = profile.Current.InherentGrants
                .Select(x => x.Id == replacement.Id ? replacement : x).ToArray()
        };
        return result with
        {
            Profile = profile with { Underlying = changed, Current = changed }
        };
    }
}
