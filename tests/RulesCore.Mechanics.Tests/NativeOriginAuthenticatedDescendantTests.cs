using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Real-catalog multi-generation tests. LocalSelfOverride is an in-process test
/// authority, not a production permission, web-client bypass or gameplay policy.
/// </summary>
public sealed class NativeOriginAuthenticatedDescendantTests
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
        throw new InvalidOperationException("Native ruleset unavailable.");
    }

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal,
        INativeOriginManifestedContributorAuthority? localSelfOverride = null,
        NativeTableDecisionScope? scope = null) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, TrustedScope: scope ??
                (localSelfOverride is null ? null : new("campaign", "local-test")),
                AutoManifestDirectDefaults: false),
            manifestedContributorAuthority: localSelfOverride);

    private sealed class LocalSelfOverride(NativeOriginProfile authoritative,
        string revision = "test-revision-1") : INativeOriginManifestedContributorAuthority
    {
        public NativeOriginVerifiedManifestedContributor? ResolveCurrent(
            NativeOriginSourceReference source, NativeTableDecisionScope trustedScope) =>
            trustedScope.Kind == "campaign" && trustedScope.Id == "local-test" &&
            source.Id == authoritative.Id
                ? new(authoritative, revision)
                : null;
    }

    private static NativeOriginContributor NativeDonor(string id, string source) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, source));

    private static NativeOriginProposal Proposal(string id,
        IReadOnlyList<NativeOriginContributor> contributors) =>
        new(id, NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"), contributors, [], []);

    private static NativeOriginManifestation Select(
        NativeOriginCandidate candidate, int n) =>
        new("manifest-" + n, candidate.Id,
            SelectedValue: candidate.Identity.Kind ==
                NativeOriginCharacteristicKind.Intrinsic
                ? candidate.OfferedValue : null);

    private static NativeOriginConstructionResult MixedParent()
    {
        var proposal = Proposal("mixed-parent", [
            NativeDonor("human", "species.human"),
            NativeDonor("elf", "species.elf")
        ]);
        var offers = Evaluate(proposal).EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "elf" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);
        var lowLight = Assert.Single(offers, x => x.ContributorId == "elf" &&
            x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision");
        var trance = Assert.Single(offers, x => x.ContributorId == "elf" &&
            x.Identity.MechanicalDefinitionId == "trait.elf.trance");
        var selected = foundation.Append(lowLight).Append(trance).ToArray();
        var result = Evaluate(proposal with
        {
            Manifestations = selected.Select(Select).ToArray()
        });
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        return result;
    }

    private static NativeOriginProposal Child(
        NativeOriginProfile parent, string? revision = "test-revision-1",
        NativeOriginCurrentConfiguration? claimedSnapshot = null,
        string? sourceId = null) =>
        Proposal("descendant", [
            new("inherited-parent",
                new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.ManifestedProfile,
                    sourceId ?? parent.Id, revision),
                claimedSnapshot, ["ancestry.dragonborn.red",
                    "ancestry.elf.high", "species.orc"])
        ]);

    [Fact]
    public void LocalSelfOverrideAllowsOnlyVerifiedUnderlyingManifestations()
    {
        var parent = MixedParent().Profile!;
        var fake = parent.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.elf.trance") with
        {
            Id = "fabricated-form-grant"
        };
        var temporary = parent.Current with
        {
            InherentGrants = parent.Current.InherentGrants.Append(fake).ToArray()
        };
        var authoritative = parent with
        {
            Current = temporary,
            IndependentGrants = [fake]
        };
        var callerSnapshot = temporary with
        {
            InherentGrants = temporary.InherentGrants.Append(fake).ToArray()
        };
        var proposal = Child(parent, claimedSnapshot: callerSnapshot);
        var overrideFixture = new LocalSelfOverride(authoritative);
        var offers = Evaluate(proposal, overrideFixture).EvaluatedProposal.Candidates;
        Assert.Equal(parent.Underlying.InherentGrants.Count, offers.Count);
        Assert.All(offers, x =>
        {
            Assert.Equal(NativeOriginEligibility.Eligible,
                x.SelectionBasis.Eligibility);
            Assert.Equal("rule.creature-origination",
                x.SelectionBasis.RuleReference);
            Assert.Equal("inherited-parent", x.ContributorId);
        });
        Assert.DoesNotContain(offers, x => x.Id.Contains("fabricated-form-grant",
            StringComparison.Ordinal));
        Assert.DoesNotContain(offers, x =>
            x.Identity.Occurrence.Source.Id is "species.orc" or
                "ancestry.dragonborn.red" or "ancestry.elf.high");

        var child = Evaluate(proposal with
        {
            Manifestations = offers.Select(Select).ToArray()
        }, overrideFixture);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, child.Status);
        Assert.Equal(parent.Underlying.InherentGrants.Count,
            child.Profile!.Underlying.InherentGrants.Count);
        Assert.All(child.Profile.Underlying.InherentGrants, grant =>
        {
            var source = Assert.Single(grant.InheritedFrom!);
            Assert.Equal(parent.Id, source.ContributorProfileId);
            Assert.Equal("test-revision-1", source.ContributorProfileRevision);
            Assert.Equal("inherited-parent", source.ContributorId);
            Assert.StartsWith("grant:", source.SourceGrantId);
            Assert.Single(grant.SourceCandidateIds);
            Assert.StartsWith("nested:inherited-parent:",
                grant.SourceCandidateIds.Single());
        });
        Assert.Empty(child.Issues);
        var reloaded = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(child.Profile));
        Assert.All(reloaded!.Underlying.InherentGrants, x =>
            Assert.Equal(parent.Id, Assert.Single(x.InheritedFrom!).ContributorProfileId));
    }

    [Fact]
    public void UntrustedOrStaleSelfClaimsNeverBecomeFirstPartyTransmission()
    {
        var parent = MixedParent().Profile!;
        var proposal = Child(parent, claimedSnapshot: parent.Current);
        var noAuthority = Evaluate(proposal);
        var offered = noAuthority.EvaluatedProposal.Candidates;
        Assert.All(offered, x =>
            Assert.NotEqual(NativeOriginEligibility.Eligible,
                x.SelectionBasis.Eligibility));
        var selected = proposal with
        {
            Manifestations = offered.Select(Select).ToArray()
        };
        var unverified = Evaluate(selected);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, unverified.Status);
        Assert.Contains(unverified.Issues, x =>
            x.Code == "selection-eligibility-unresolved");

        var fixture = new LocalSelfOverride(parent);
        foreach (var bad in new[]
        {
            Child(parent, revision: "stale-version"),
            Child(parent, sourceId: "origin:wrong-profile"),
            Child(parent, claimedSnapshot: parent.Current)
        })
        {
            var scope = bad.Contributors[0].ManifestedProfile is null
                ? new NativeTableDecisionScope("campaign", "local-test")
                : new NativeTableDecisionScope("campaign", "wrong-campaign");
            var failed = Evaluate(bad, fixture, scope);
            Assert.Contains(failed.Issues, x =>
                x.Code == "manifested-contributor-unverified");
            Assert.Empty(failed.EvaluatedProposal.Candidates);
        }
    }

    [Fact]
    public void ComposedBreathRetainsOneConfiguredTraitAndPriorParameterProvenance()
    {
        var source = Proposal("configured-dragonborn", [
            NativeDonor("red", "ancestry.dragonborn.red"),
            NativeDonor("bronze", "ancestry.dragonborn.bronze")
        ]);
        var offers = Evaluate(source).EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "red" &&
            x.Identity.Occurrence.Source.Id == "species.dragonborn" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var breaths = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.dragonborn.breath-weapon")
            .ToArray();
        var red = Assert.Single(breaths, x => x.ContributorId == "red");
        var bronze = Assert.Single(breaths, x => x.ContributorId == "bronze");
        var chosen = new Dictionary<string, JsonElement>
        {
            ["damageType"] = red.OfferedParameters!["damageType"],
            ["area"] = bronze.OfferedParameters!["area"]
        };
        var chosenSources = new Dictionary<string, string>
        {
            ["damageType"] = red.Id,
            ["area"] = bronze.Id
        };
        var parent = Evaluate(source with
        {
            Manifestations = foundation.Select(Select).Concat([
                new NativeOriginManifestation("red-breath", red.Id),
                new NativeOriginManifestation("bronze-breath", bronze.Id)
            ]).ToArray(),
            Relationships = [new("breath-composition",
                NativeOriginRelationshipKind.Substitution, [red.Id, bronze.Id],
                AuthoredRuleReference: "trait.dragonborn.breath-weapon")],
            Resolutions = [new("breath-composition",
                NativeOriginResolutionAction.Replace,
                new(NativeOriginAdjudication.Supported,
                    NativeRuleReference: "trait.dragonborn.breath-weapon"),
                red.Id, chosen, chosenSources)]
        });
        Assert.Equal(NativeOriginConstructionStatus.Authorized, parent.Status);

        var proposal = Child(parent.Profile!);
        var fixture = new LocalSelfOverride(parent.Profile!);
        var inherited = Evaluate(proposal, fixture).EvaluatedProposal.Candidates;
        var breathOffer = Assert.Single(inherited, x =>
            x.Identity.MechanicalDefinitionId == "trait.dragonborn.breath-weapon");
        Assert.Equal("fire", breathOffer.OfferedParameters!["damageType"].GetString());
        Assert.Equal("cone", breathOffer.OfferedParameters["area"]
            .GetProperty("shape").GetString());
        Assert.Equal(4, inherited.Count);
        var child = Evaluate(proposal with
        {
            Manifestations = inherited.Select(Select).ToArray()
        }, fixture);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, child.Status);
        var breath = Assert.Single(child.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.dragonborn.breath-weapon");
        Assert.Equal("fire", breath.Parameters!["damageType"].GetString());
        Assert.Equal("cone", breath.Parameters["area"].GetProperty("shape").GetString());
        Assert.Single(breath.SourceCandidateIds);
        Assert.All(breath.ParameterSources!, x =>
            Assert.Equal(breath.SourceCandidateIds.Single(), x.Value));
        var lineage = Assert.Single(breath.InheritedFrom!);
        Assert.Equal(red.Id, lineage.PriorParameterSources!["damageType"]);
        Assert.Equal(bronze.Id, lineage.PriorParameterSources["area"]);
        Assert.Equal("proficiency-bonus",
            breath.EffectiveValue!.Value.GetProperty("uses")
                .GetProperty("value").GetString());
        Assert.DoesNotContain(child.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.dragonborn.draconic-resistance");
    }


    [Fact]
    public void TwoVerifiedMixedParentsConsolidateOneTraitWithoutLosingEitherLineage()
    {
        var parent = MixedParent().Profile!;
        var first = new NativeOriginContributor("parent-a",
            new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.ManifestedProfile, parent.Id));
        var second = new NativeOriginContributor("parent-b",
            new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.ManifestedProfile, parent.Id));
        var proposal = Proposal("two-mixed-parents", [first, second]);
        var fixture = new LocalSelfOverride(parent);
        var offers = Evaluate(proposal, fixture).EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "parent-a" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        var visions = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision").ToArray();
        Assert.Equal(2, visions.Length);
        var result = Evaluate(proposal with
        {
            Manifestations = foundation.Concat(visions).Select(Select).ToArray()
        }, fixture);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var vision = Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision");
        Assert.Equal(2, vision.SourceCandidateIds.Count);
        Assert.Equal(2, vision.InheritedFrom!.Count);
        Assert.Equal(new[] { "parent-a", "parent-b" },
            vision.InheritedFrom.Select(x => x.ContributorId).ToArray());
        Assert.All(vision.InheritedFrom, x =>
        {
            Assert.Equal(parent.Id, x.ContributorProfileId);
            Assert.Equal("test-revision-1", x.ContributorProfileRevision);
        });
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ASecondGenerationUsesOnlyItsVerifiedParentsCompletedCharacteristics()
    {
        var first = MixedParent().Profile!;
        var firstFixture = new LocalSelfOverride(first);
        var childProposal = Child(first);
        var firstOffers = Evaluate(childProposal, firstFixture).EvaluatedProposal.Candidates;
        var child = Evaluate(childProposal with
        {
            Manifestations = firstOffers.Select(Select).ToArray()
        }, firstFixture);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, child.Status);
        var grandchildProposal = Child(child.Profile!, revision: "test-revision-2");
        var grandchildFixture = new LocalSelfOverride(child.Profile!, "test-revision-2");
        var secondOffers = Evaluate(grandchildProposal,
            grandchildFixture).EvaluatedProposal.Candidates;
        Assert.Equal(first.Underlying.InherentGrants.Count, secondOffers.Count);
        var grandchild = Evaluate(grandchildProposal with
        {
            Manifestations = secondOffers.Select(Select).ToArray()
        }, grandchildFixture);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, grandchild.Status);
        Assert.All(grandchild.Profile!.Underlying.InherentGrants, x =>
        {
            var source = Assert.Single(x.InheritedFrom!);
            Assert.Equal(child.Profile.Id, source.ContributorProfileId);
            Assert.Equal("test-revision-2", source.ContributorProfileRevision);
            Assert.StartsWith("nested:", Assert.Single(source.PriorSourceCandidateIds));
        });
        Assert.Empty(grandchild.Issues);
    }

    [Fact]
    public void NonbiologicalCurrentUnderlyingTypeDoesNotGrantBiologicalTransmission()
    {
        var parent = MixedParent().Profile!;
        var altered = parent.Underlying with
        {
            InherentGrants = parent.Underlying.InherentGrants.Select(g =>
                g.Identity.IntrinsicSlotId == "creatureType"
                    ? g with
                    {
                        EffectiveValue =
                            JsonSerializer.SerializeToElement("construct")
                    }
                    : g).ToArray()
        };
        var configuration = parent with { Underlying = altered, Current = altered };
        var proposal = Child(parent);
        var result = Evaluate(proposal, new LocalSelfOverride(configuration));
        Assert.DoesNotContain(result.EvaluatedProposal.Candidates,
            x => x.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible);
    }

    [Fact]
    public void SerializedLineageForgeriesAreRejectedWithoutTrustingCallerMetadata()
    {
        var parent = MixedParent().Profile!;
        var proposal = Child(parent);
        var fixture = new LocalSelfOverride(parent);
        var offers = Evaluate(proposal, fixture).EvaluatedProposal.Candidates;
        var constructed = Evaluate(proposal with
        {
            Manifestations = offers.Select(Select).ToArray()
        }, fixture);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, constructed.Status);
        var original = constructed.Profile!;
        var grant = Assert.Single(original.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.shared.low-light-vision");
        var prior = Assert.Single(grant.InheritedFrom!);
        var forgedCases = new[]
        {
            prior with { ContributorId = "fake-contributor" },
            prior with { ContributorProfileRevision = "" },
            prior with { SourceGrantId = "grant:missing" },
            prior with { PriorSourceCandidateIds = [] }
        };

        foreach (var fake in forgedCases)
        {
            var changed = grant with { InheritedFrom = [fake] };
            var grants = original.Underlying.InherentGrants.Select(g =>
                g.Id == grant.Id ? changed : g).ToArray();
            var configuration = original.Underlying with { InherentGrants = grants };
            var tampered = original with
            {
                Underlying = configuration,
                Current = configuration
            };
            var review = new NativeOriginContractValidator(Catalog())
                .ReviewProfile(tampered, constructed.EvaluatedProposal);
            Assert.Contains(review.Issues, issue =>
                issue.Code == "inherited-lineage-invalid");
        }
    }

    private sealed class LocalSheetWalkAuthority(NativeOriginProfile expected)
        : INativeCharacterOriginBaselineAuthority
    {
        public bool AuthorizesBaseline(string characterId, NativeOriginProfile profile) =>
            characterId == "descendant-character" && profile.Id == expected.Id;
        public bool AuthorizesGrant(string characterId, NativeOriginProfile profile,
            NativeOriginEffectiveGrant grant) =>
            characterId == "descendant-character" && profile.Id == expected.Id &&
            expected.Underlying.InherentGrants.Any(x => x.Id == grant.Id &&
                x.Identity.IntrinsicSlotId == "walkingSpeedFeet");
    }

    [Fact]
    public void SavedMixedDescendantProjectsVisionAndWalkSpeedWithoutSiteChanges()
    {
        var parent = MixedParent().Profile!;
        var fixture = new LocalSelfOverride(parent);
        var proposal = Child(parent);
        var offers = Evaluate(proposal, fixture).EvaluatedProposal.Candidates;
        var result = Evaluate(proposal with
        {
            Manifestations = offers.Select(Select).ToArray()
        }, fixture);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile))!;
        Assert.NotSame(saved.Current, saved.Underlying);
        var restored = result with { Profile = saved };

        var intrinsic = NativeOriginApprovedMixedIntrinsicProjector.Project(restored);
        Assert.Equal("resolved", intrinsic.Status);
        Assert.Equal("fey", intrinsic.CreatureType!.Value);
        Assert.Equal("medium", intrinsic.Size!.Value);
        Assert.Equal(30, intrinsic.WalkingSpeedFeet!.Value);
        Assert.Equal("unresolved", NativeOriginIntrinsicProjector.Project(restored).Status);

        var vision = NativeOriginVisionProjector.ProjectApprovedMixed(Catalog(), restored);
        Assert.Equal("resolved", vision.Status);
        Assert.Equal("rule.low-light-vision", vision.LowLightVision!.RuleId);
        Assert.Equal("bright",
            NativeOriginVisionProjector.Perceive(vision, "dim").PerceivedLightLevel);

        // The existing sheet kernel receives only the verified underlying walk
        // speed through an in-process authority. No browser or form transition
        // is involved; unrelated Origin mechanics remain unimplemented there.
        var state = new NativeCharacterState("descendant-character",
            new NativeCharacterRequest("first-party-001", "descendant",
                new Dictionary<string, int>
                {
                    ["strength"] = 10, ["dexterity"] = 12, ["constitution"] = 14,
                    ["intelligence"] = 8, ["wisdom"] = 15, ["charisma"] = 11
                }, [new("class.synthetic", 1, "class.level.1")], BaseWalkSpeed: 20),
            []);
        var updated = NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed(
            state, saved, new LocalSheetWalkAuthority(saved));
        Assert.Equal("resolved", updated.Status);
        Assert.Equal(30, updated.State!.PrimaryMechanics.BaseWalkSpeed);
        Assert.Equal(20, state.PrimaryMechanics.BaseWalkSpeed);
        Assert.Empty(updated.Issues);
    }
}
