using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Candidate costs are deliberately illustrative. These tests prove accounting
/// mechanics, not a balance decision, a live biological budget, or trait prices.
/// </summary>
public sealed class NativeOriginAllocationPreviewTests
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
        throw new InvalidOperationException("Native catalog missing.");
    }

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null : new("campaign", "allocation-test"),
                AutoManifestDirectDefaults: false), authority);

    private static NativeOriginContributor Parent(string id, string source) =>
        new(id, new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.Native, source));

    private static NativeOriginAllocationPolicy Hypothetical(int allowance,
        params NativeOriginAllocationPrice[] additional)
    {
        var foundation = new[]
        {
            new NativeOriginAllocationPrice("creature", 1, ["intrinsic:creatureType"]),
            new NativeOriginAllocationPrice("size", 1, ["intrinsic:size"]),
            new NativeOriginAllocationPrice("walk", 1, ["intrinsic:walkingSpeedFeet"])
        };
        return new("candidate-alpha", "rule.creature-origination", "biological",
            allowance, foundation.Concat(additional).ToArray());
    }

    private static NativeOriginConstructionResult ElfProfile(
        int parents = 1, bool selectAllVisionOffers = false, bool includeTrance = false)
    {
        var proposal = new NativeOriginProposal("elf-preview-" + parents,
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            Enumerable.Range(1, parents)
                .Select(i => Parent("elf-" + i, "species.elf")).ToArray(), [], []);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var physical = offered.Where(x => x.ContributorId == "elf-1" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var vision = offered.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.low-light-vision").ToArray();
        var trance = Assert.Single(offered, x => x.ContributorId == "elf-1" &&
            x.Identity.MechanicalDefinitionId == "trait.trance");
        var choices = physical.Select((x, i) =>
            new NativeOriginManifestation("physical-" + i, x.Id,
                SelectedValue: x.OfferedValue)).ToList();
        var chosenVision = selectAllVisionOffers ? vision : vision.Take(1).ToArray();
        choices.AddRange(chosenVision.Select((x, i) =>
            new NativeOriginManifestation("vision-" + i, x.Id)));
        if (includeTrance)
            choices.Add(new("trance", trance.Id));
        var result = Evaluate(proposal with { Manifestations = choices });
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            "Issues: " + string.Join("; ", result.Issues.Select(x => x.Code)));
        return result;
    }

    [Fact]
    public void NoApprovedAllocationPolicyDoesNotSilentlyTreatAnyTraitAsFree()
    {
        var result = ElfProfile(includeTrance: true);
        var unconfigured = NativeOriginAllocationPreview.Assess(Catalog(), result);
        Assert.Equal("not-configured", unconfigured.Status);
        Assert.Null(unconfigured.CommittedLoad);
        Assert.Equal(5, unconfigured.Components.Count);
        Assert.All(unconfigured.Components, x => Assert.Null(x.AssignedPriceId));
        Assert.Contains("biological-allocation-policy-not-approved",
            unconfigured.Issues);
        Assert.True(unconfigured.PreviewOnly);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
    }

    [Fact]
    public void HypotheticalCostsPriceEveryEffectiveGrantAndDoNotAuthorizeAnything()
    {
        var result = ElfProfile(includeTrance: true);
        var sight = new NativeOriginAllocationPrice("vision", 3,
            ["trait:trait.low-light-vision"]);
        var trance = new NativeOriginAllocationPrice("trance", 2,
            ["trait:trait.trance"]);
        var within = NativeOriginAllocationPreview.Assess(Catalog(), result,
            Hypothetical(8, sight, trance));
        Assert.Equal("within-allowance", within.Status);
        Assert.Equal(8, within.CommittedLoad);
        Assert.Equal(5, within.Charges.Count);
        Assert.All(within.Components, x => Assert.NotNull(x.AssignedPriceId));
        Assert.Empty(within.Issues);
        Assert.Equal("candidate-alpha", within.PolicyRevision);

        var over = NativeOriginAllocationPreview.Assess(Catalog(), result,
            Hypothetical(7, sight, trance));
        Assert.Equal("over-allowance", over.Status);
        Assert.Equal(8, over.CommittedLoad);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);

        var unpriced = NativeOriginAllocationPreview.Assess(Catalog(), result,
            Hypothetical(80, sight));
        Assert.Equal("unresolved", unpriced.Status);
        Assert.Null(unpriced.CommittedLoad);
        Assert.Contains(unpriced.Issues, x =>
            x.Contains("allocation-load-unpriced:trait:trait.trance:",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ThreeDonorsOfOneCanonicalVisionRemainOneBurdenAndOnePool()
    {
        var single = ElfProfile();
        var repeated = ElfProfile(parents: 3, selectAllVisionOffers: true);
        Assert.Equal(4, repeated.Profile!.Underlying.InherentGrants.Count);
        Assert.Equal(3, Assert.Single(repeated.Profile.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == "trait.low-light-vision")
            .SourceCandidateIds.Count);
        var policy = Hypothetical(6, new NativeOriginAllocationPrice("vision", 3,
            ["trait:trait.low-light-vision"]));
        var one = NativeOriginAllocationPreview.Assess(Catalog(), single, policy);
        var many = NativeOriginAllocationPreview.Assess(Catalog(), repeated, policy);
        Assert.Equal(6, one.CommittedLoad);
        Assert.Equal(one.CommittedLoad, many.CommittedLoad);
        Assert.Equal(one.Charges.Count, many.Charges.Count);
        Assert.Equal(1, many.Charges.Count(x => x.PriceId == "vision"));
    }

    private static NativeTableRuleDecision Permit(string id, string kind, string target,
        object outcome) =>
        new(id, new("dm", "verified-allocation"),
            new("campaign", "allocation-test"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "allocation-test" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "verified-allocation";
        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    [Fact]
    public void InseparableNativeDrowAdaptationHasOneBundleCharge()
    {
        var proposal = new NativeOriginProposal("drow-allocation",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [Parent("drow", "ancestry.elf.drow")], [], []);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var vision = Assert.Single(offered, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
        var sensitivity = Assert.Single(offered, x =>
            x.Identity.MechanicalDefinitionId == "trait.sunlight-sensitivity");
        var physical = offered.Where(x => x.ContributorId == "drow" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var completed = Evaluate(proposal with
        {
            Manifestations = physical.Select((x, i) =>
                new NativeOriginManifestation("physical-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat(
                [
                    new("vision", vision.Id, SelectedValue: vision.OfferedValue),
                    new("sunlight", sensitivity.Id)
                ]).ToArray(),
            TableDecisions =
            [
                Permit("allow-vision", "candidate", vision.Id,
                    new { eligibility = "eligible" }),
                Permit("allow-sunlight", "candidate", sensitivity.Id,
                    new { eligibility = "eligible" })
            ]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var policy = Hypothetical(7, new NativeOriginAllocationPrice("drow-adaptation", 4,
        [
            "intrinsic:darkvisionFeet",
            "trait:trait.sunlight-sensitivity"
        ]));
        var assessment = NativeOriginAllocationPreview.Assess(Catalog(), completed, policy);
        Assert.Equal("within-allowance", assessment.Status);
        Assert.Equal(7, assessment.CommittedLoad);
        var bundle = Assert.Single(assessment.Charges,
            x => x.PriceId == "drow-adaptation");
        Assert.Equal(2, bundle.GrantIds.Count);
        Assert.Equal(4, assessment.Charges.Count);
        Assert.Equal(5, assessment.Components.Count);
        Assert.All(assessment.Components, x => Assert.NotNull(x.AssignedPriceId));
        Assert.Empty(assessment.Issues);

        // A configured bundle replaces its whole charge; an independently
        // authored interaction is additive once, not once per bundle member.
        var darkvision = Assert.Single(completed.Profile!.Underlying.InherentGrants,
            x => x.Identity.IntrinsicSlotId == "darkvisionFeet");
        var configuredBundle = new NativeOriginAllocationPrice("drow-adaptation", 4,
        [
            "intrinsic:darkvisionFeet",
            "trait:trait.sunlight-sensitivity"
        ],
            Configurations:
            [
                new("configured-vision-and-sensitivity", 5, 10,
                [
                    new("intrinsic:darkvisionFeet", "value",
                        NativeOriginAllocationComparison.Equals,
                        darkvision.EffectiveValue),
                    new("trait:trait.sunlight-sensitivity", "value",
                        NativeOriginAllocationComparison.Present)
                ])
            ]);
        var combinedPolicy = Hypothetical(12, configuredBundle) with
        {
            InteractionPrices =
            [
                new NativeOriginAllocationInteractionPrice("adaptation-and-size", 2,
                    ["intrinsic:darkvisionFeet", "intrinsic:size"])
            ]
        };
        var combined = NativeOriginAllocationPreview.Assess(
            Catalog(), completed, combinedPolicy);
        Assert.Equal("within-allowance", combined.Status);
        Assert.Equal(10, combined.CommittedLoad);
        Assert.Equal(5, combined.Charges.Count);
        Assert.Equal("configured-vision-and-sensitivity",
            Assert.Single(combined.Charges, x => x.PriceId == "drow-adaptation")
                .ConfigurationId);
        Assert.Equal(2, Assert.Single(combined.Charges,
            x => x.PriceId == "adaptation-and-size").Load);
    }

    [Fact]
    public void FutureLevelUnlockIsCommittedAtCharacterCreationNotOnlyAtLevelFour()
    {
        var proposal = new NativeOriginProposal("stone-committed-progression",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [Parent("stone", "ancestry.goliath.stone")], [], []);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var cavernous = Assert.Single(offered, x =>
            x.Identity.MechanicalDefinitionId == "trait.cavernous-sight");
        var foundation = offered.Where(x => x.Identity.IntrinsicSlotId is
            "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var completed = Evaluate(proposal with
        {
            Manifestations = foundation.Select((x, i) =>
                new NativeOriginManifestation("foundation-" + i, x.Id,
                    SelectedValue: x.OfferedValue))
                .Append(new("future-vision", cavernous.Id)).ToArray(),
            TableDecisions =
            [
                Permit("allow-cavernous", "candidate", cavernous.Id,
                    new { eligibility = "eligible" })
            ]
        }, new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var policy = Hypothetical(8, new NativeOriginAllocationPrice("cavernous", 6,
            ["trait:trait.cavernous-sight"]));
        var preview = NativeOriginAllocationPreview.Assess(Catalog(), completed, policy);
        Assert.Equal("over-allowance", preview.Status);
        Assert.Equal(9, preview.CommittedLoad);
        Assert.Equal(4, Assert.Single(preview.Components,
            x => x.Key == "trait:trait.cavernous-sight")
            .UnlockLevel);
        Assert.Contains(preview.Charges, x =>
            x.PriceId == "cavernous" && x.Load == 6);
    }

    [Fact]
    public void MalformedOrIncompleteHypotheticalPriceCoverageFailsClosed()
    {
        var result = ElfProfile(includeTrance: true);
        var vision = new NativeOriginAllocationPrice("vision", 3,
            ["trait:trait.low-light-vision"]);
        var duplicate = new NativeOriginAllocationPrice("duplicate", 1,
            ["trait:trait.low-light-vision"]);
        Assert.Equal("invalid-policy", NativeOriginAllocationPreview
            .Assess(Catalog(), result, Hypothetical(12, vision, duplicate)).Status);
        Assert.Equal("invalid-policy", NativeOriginAllocationPreview
            .Assess(Catalog(), result, Hypothetical(12,
                new NativeOriginAllocationPrice("free", 0,
                    ["trait:trait.trance"]))).Status);
        Assert.Equal("invalid-policy", NativeOriginAllocationPreview
            .Assess(Catalog(), result, Hypothetical(-1, vision)).Status);
        Assert.Equal("unresolved", NativeOriginAllocationPreview
            .Assess(Catalog(), result, Hypothetical(100, vision)).Status);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
    }

    [Fact]
    public void UnknownNativeDefinitionDoesNotBecomePricedByAClaimedLedgerEntry()
    {
        var original = ElfProfile();
        var known = Assert.Single(original.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.low-light-vision");
        var altered = original.Profile.Underlying with
        {
            InherentGrants = original.Profile.Underlying.InherentGrants.Select(x =>
                x.Id == known.Id
                    ? x with { Identity = x.Identity with
                        { MechanicalDefinitionId = "trait.unsupported.user-authored" } }
                    : x).ToArray()
        };
        var forged = original with
        {
            Profile = original.Profile with
            {
                Underlying = altered, Current = altered
            }
        };
        var ledger = Hypothetical(10,
            new NativeOriginAllocationPrice("untrusted", 2,
                ["trait:trait.unsupported.user-authored"]));
        var assessed = NativeOriginAllocationPreview.Assess(Catalog(), forged, ledger);
        Assert.Equal("unresolved", assessed.Status);
        Assert.Null(assessed.CommittedLoad);
        Assert.Contains(assessed.Issues, x =>
            x.StartsWith("allocation-definition-unresolved:",
                StringComparison.Ordinal));
        Assert.True(assessed.PreviewOnly);
    }

    [Fact]
    public void IncompleteOriginCannotPassAllocationWithNoEffectiveGrants()
    {
        var proposal = new NativeOriginProposal("no-manifestations",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [Parent("elf", "species.elf")], [], []);
        var unfinished = Evaluate(proposal);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, unfinished.Status);
        var assessed = NativeOriginAllocationPreview.Assess(Catalog(), unfinished,
            Hypothetical(100));
        Assert.Equal("unresolved", assessed.Status);
        Assert.Null(assessed.CommittedLoad);
        Assert.Contains("origin-not-authorized-for-allocation",
            assessed.Issues);
        Assert.Empty(assessed.Charges);
    }

    [Fact]
    public void AllocationAssessmentNeverAppliesBiologicalBudgetToOtherMethods()
    {
        var selected = new NativeOriginProposal("direct-elf",
            NativeOriginIntent.Direct, new("rule.creature-origination", "biological"),
            [], [], [], SelectedSpeciesId: "species.elf");
        var direct = new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(selected));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, direct.Status);
        var preview = NativeOriginAllocationPreview.Assess(Catalog(), direct,
            Hypothetical(1));
        Assert.Equal("not-applicable", preview.Status);
        Assert.Null(preview.CommittedLoad);
        Assert.True(preview.PreviewOnly);
    }

    private static NativeOriginAllocationPriceCondition EqualsValue(
        string member, string path, object value) =>
        new(member, path, NativeOriginAllocationComparison.Equals,
            JsonSerializer.SerializeToElement(value));

    // Synthetic grant configurations below exercise policy selection only.
    // They are not new authorized species traits or construction-engine offers.
    private static NativeOriginConstructionResult ChangeGrant(
        NativeOriginConstructionResult result, string key,
        Func<NativeOriginEffectiveGrant, NativeOriginEffectiveGrant> modify)
    {
        var underlying = result.Profile!.Underlying;
        var grants = underlying.InherentGrants.Select(grant =>
        {
            var grantKey = grant.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                ? "intrinsic:" + grant.Identity.IntrinsicSlotId
                : "trait:" + grant.Identity.MechanicalDefinitionId;
            return grantKey == key ? modify(grant) : grant;
        }).ToArray();
        var changed = underlying with { InherentGrants = grants };
        return result with
        {
            Profile = result.Profile with { Underlying = changed, Current = changed }
        };
    }

    [Fact]
    public void ConfiguredIntrinsicValuesUseEffectiveSizeAndSpeedNotOneGenericPrice()
    {
        var original = ElfProfile();
        var size = Assert.Single(original.Profile!.Underlying.InherentGrants,
            x => x.Identity.IntrinsicSlotId == "size").EffectiveValue!.Value;
        var walk = Assert.Single(original.Profile.Underlying.InherentGrants,
            x => x.Identity.IntrinsicSlotId == "walkingSpeedFeet").EffectiveValue!.Value;
        var policy = Hypothetical(30, new NativeOriginAllocationPrice("vision", 3,
            ["trait:trait.low-light-vision"]));
        policy = policy with
        {
            Prices = policy.Prices.Select(x => x.Id switch
            {
                "size" => x with
                {
                    Configurations =
                    [
                        new("source-size", 4, 10,
                            [EqualsValue("intrinsic:size", "value", size)]),
                        new("alternative-size", 5, 10,
                            [EqualsValue("intrinsic:size", "value", "Small")])
                    ]
                },
                "walk" => x with
                {
                    Configurations =
                    [
                        new("source-walk", 6, 10,
                            [EqualsValue("intrinsic:walkingSpeedFeet", "value", walk)]),
                        new("alternative-walk", 7, 10,
                            [EqualsValue("intrinsic:walkingSpeedFeet", "value", 35)])
                    ]
                },
                _ => x
            }).ToArray()
        };

        var normal = NativeOriginAllocationPreview.Assess(Catalog(), original, policy);
        Assert.Equal("within-allowance", normal.Status);
        Assert.Equal(14, normal.CommittedLoad);
        Assert.Contains(normal.Charges, x =>
            x.PriceId == "size" && x.ConfigurationId == "source-size" && x.Load == 4);
        Assert.Contains(normal.Components, x =>
            x.Key == "intrinsic:walkingSpeedFeet" &&
            x.AssignedConfigurationId == "source-walk");

        var changed = ChangeGrant(original, "intrinsic:size", x => x with
        {
            EffectiveValue = JsonSerializer.SerializeToElement("Small")
        });
        changed = ChangeGrant(changed, "intrinsic:walkingSpeedFeet", x => x with
        {
            EffectiveValue = JsonSerializer.SerializeToElement(35)
        });
        var alternative = NativeOriginAllocationPreview.Assess(Catalog(), changed, policy);
        Assert.Equal("within-allowance", alternative.Status);
        Assert.Equal(16, alternative.CommittedLoad);
        Assert.Contains(alternative.Charges, x =>
            x.PriceId == "size" && x.ConfigurationId == "alternative-size");
        Assert.Contains(alternative.Charges, x =>
            x.PriceId == "walk" && x.ConfigurationId == "alternative-walk");
    }

    [Fact]
    public void MissingConfiguredPriceFailsClosedUnlessFallbackIsExplicit()
    {
        var original = ElfProfile();
        var policy = Hypothetical(100, new NativeOriginAllocationPrice("vision", 3,
            ["trait:trait.low-light-vision"]));
        var size = Assert.Single(policy.Prices, x => x.Id == "size") with
        {
            Configurations =
            [
                new("only-small", 2, 0,
                    [EqualsValue("intrinsic:size", "value", "Small")])
            ]
        };
        policy = policy with
        {
            Prices = policy.Prices.Select(x => x.Id == "size" ? size : x).ToArray()
        };
        var missing = NativeOriginAllocationPreview.Assess(Catalog(), original, policy);
        Assert.Equal("unresolved", missing.Status);
        Assert.Null(missing.CommittedLoad);
        Assert.Contains(missing.Issues, x =>
            x.StartsWith("allocation-configuration-unpriced:size:", StringComparison.Ordinal));

        var fallback = policy with
        {
            Prices = policy.Prices.Select(x => x.Id == "size"
                ? x with { AllowUnmatchedConfigurationFallback = true } : x).ToArray()
        };
        var explicitFallback = NativeOriginAllocationPreview.Assess(
            Catalog(), original, fallback);
        Assert.Equal("within-allowance", explicitFallback.Status);
        Assert.Equal(6, explicitFallback.CommittedLoad);
        Assert.Null(Assert.Single(explicitFallback.Charges,
            x => x.PriceId == "size").ConfigurationId);
    }

    [Fact]
    public void HighestExplicitPriorityWinsButTiedMatchesAreInvalid()
    {
        var original = ElfProfile();
        var sizeValue = Assert.Single(original.Profile!.Underlying.InherentGrants,
            x => x.Identity.IntrinsicSlotId == "size").EffectiveValue!.Value;
        var entry = new NativeOriginAllocationPrice("size", 1, ["intrinsic:size"],
            Configurations:
            [
                new("generic-present", 2, 0,
                    [new("intrinsic:size", "value", NativeOriginAllocationComparison.Present)]),
                new("specific", 4, 3,
                    [EqualsValue("intrinsic:size", "value", sizeValue)])
            ]);
        var source = Hypothetical(20, new NativeOriginAllocationPrice("vision", 3,
            ["trait:trait.low-light-vision"]));
        NativeOriginAllocationPolicy Use(NativeOriginAllocationPrice chosen) =>
            source with
            {
                Prices = source.Prices.Select(x => x.Id == "size" ? chosen : x).ToArray()
            };

        var selected = NativeOriginAllocationPreview.Assess(Catalog(), original, Use(entry));
        Assert.Equal("within-allowance", selected.Status);
        Assert.Equal(9, selected.CommittedLoad);
        Assert.Equal("specific", Assert.Single(selected.Charges,
            x => x.PriceId == "size").ConfigurationId);

        var tied = entry with
        {
            Configurations = entry.Configurations!.Append(
                new NativeOriginAllocationPriceConfiguration("also-highest", 8, 3,
                [
                    new("intrinsic:size", "value",
                        NativeOriginAllocationComparison.Present)
                ])).ToArray()
        };
        var ambiguous = NativeOriginAllocationPreview.Assess(
            Catalog(), original, Use(tied));
        Assert.Equal("invalid-policy", ambiguous.Status);
        Assert.Null(ambiguous.CommittedLoad);
        Assert.Contains("allocation-configuration-ambiguous:size", ambiguous.Issues);

        var contradictory = entry with
        {
            Configurations =
            [
                new("contradictory", 1, 0,
                [
                    new("intrinsic:size", "value", NativeOriginAllocationComparison.Present),
                    new("intrinsic:size", "value", NativeOriginAllocationComparison.Absent)
                ])
            ]
        };
        Assert.Equal("invalid-policy", NativeOriginAllocationPreview.Assess(
            Catalog(), original, Use(contradictory)).Status);
    }

    [Fact]
    public void WholeTraitConfigurationCanMatchNestedMechanicsChoicesAndBoundResources()
    {
        var original = ElfProfile();
        var chosenTrait = ChangeGrant(original, "trait:trait.low-light-vision",
            grant => grant with
            {
                EffectiveValue = JsonSerializer.SerializeToElement(new
                {
                    movementMode = "fly",
                    speedFeet = 40,
                    duration = new { minutes = 1 },
                    uses = new { count = 2, recharge = "long-rest" },
                    naturalArmor = new { formula = "13 + Dexterity modifier" }
                }),
                Parameters = new Dictionary<string, JsonElement>
                {
                    ["spellPackage"] = JsonSerializer.SerializeToElement(new
                    {
                        progression = new[] { new { spell = "detect-magic", level = 3 } }
                    })
                },
                ResolvedChoices =
                [
                    new NativeOriginChoiceSelection("selection", ["spell.detect-magic"])
                ],
                ResourceOwners = new Dictionary<string, string>
                {
                    ["uses"] = "grant:shared-resource-owner"
                }
            });
        var actualDefinition = Catalog();
        Assert.True(actualDefinition.TryGetById("trait.low-light-vision",
            out var canonical));
        var canonicalId = canonical!.Definition.GetProperty("trait")
            .GetProperty("id").Clone();
        var price = new NativeOriginAllocationPrice("vision", 1,
            ["trait:trait.low-light-vision"],
            Configurations:
            [
                new("limited-flight-and-spell-package", 8, 10,
                [
                    EqualsValue("trait:trait.low-light-vision",
                        "value/movementMode", "fly"),
                    EqualsValue("trait:trait.low-light-vision",
                        "value/uses/recharge", "long-rest"),
                    EqualsValue("trait:trait.low-light-vision",
                        "value/naturalArmor/formula", "13 + Dexterity modifier"),
                    EqualsValue("trait:trait.low-light-vision",
                        "parameters/spellPackage/progression/0/spell", "detect-magic"),
                    EqualsValue("trait:trait.low-light-vision",
                        "choices/selection", new[] { "spell.detect-magic" }),
                    EqualsValue("trait:trait.low-light-vision",
                        "definition/id", canonicalId),
                    new("trait:trait.low-light-vision", "value/duration",
                        NativeOriginAllocationComparison.Present),
                    new("trait:trait.low-light-vision", "resourceOwners/uses",
                        NativeOriginAllocationComparison.Present)
                ]),
                new("unlimited-no-activation", 11, 10,
                [
                    new("trait:trait.low-light-vision", "value/duration",
                        NativeOriginAllocationComparison.Absent),
                    new("trait:trait.low-light-vision", "value/uses",
                        NativeOriginAllocationComparison.Absent)
                ])
            ]);
        var policy = Hypothetical(30, price);
        var limited = NativeOriginAllocationPreview.Assess(Catalog(), chosenTrait, policy);
        Assert.Equal("within-allowance", limited.Status);
        Assert.Equal(11, limited.CommittedLoad);
        Assert.Equal("limited-flight-and-spell-package",
            Assert.Single(limited.Charges, x => x.PriceId == "vision").ConfigurationId);

        var unlimited = ChangeGrant(chosenTrait, "trait:trait.low-light-vision",
            grant => grant with
            {
                EffectiveValue = JsonSerializer.SerializeToElement(new
                {
                    movementMode = "fly",
                    speedFeet = 40
                }),
                ResourceOwners = null
            });
        var always = NativeOriginAllocationPreview.Assess(Catalog(), unlimited, policy);
        Assert.Equal("within-allowance", always.Status);
        Assert.Equal(14, always.CommittedLoad);
        Assert.Equal("unlimited-no-activation",
            Assert.Single(always.Charges, x => x.PriceId == "vision").ConfigurationId);

        // An absent property on a real object can be a permanent ability.
        // An entirely missing effective payload is not proof of permanence.
        var unknown = ChangeGrant(chosenTrait, "trait:trait.low-light-vision",
            grant => grant with { EffectiveValue = null });
        var unresolved = NativeOriginAllocationPreview.Assess(Catalog(), unknown, policy);
        Assert.Equal("unresolved", unresolved.Status);
        Assert.Null(unresolved.CommittedLoad);
        Assert.Contains(unresolved.Issues, x =>
            x.StartsWith("allocation-configuration-unpriced:vision:",
                StringComparison.Ordinal));

        var permittingFallback = policy with
        {
            Prices = policy.Prices.Select(x => x.Id == "vision"
                ? x with { AllowUnmatchedConfigurationFallback = true } : x).ToArray()
        };
        var stillUnresolved = NativeOriginAllocationPreview.Assess(
            Catalog(), unknown, permittingFallback);
        Assert.Equal("unresolved", stillUnresolved.Status);
        Assert.Null(stillUnresolved.CommittedLoad);

        var malformed = ChangeGrant(chosenTrait, "trait:trait.low-light-vision",
            grant => grant with
            {
                EffectiveValue = JsonSerializer.SerializeToElement(new
                {
                    movementMode = "fly",
                    uses = "unexpected-scalar",
                    duration = new { minutes = 1 }
                })
            });
        var malformedFallback = NativeOriginAllocationPreview.Assess(
            Catalog(), malformed, permittingFallback);
        Assert.Equal("unresolved", malformedFallback.Status);
        Assert.Null(malformedFallback.CommittedLoad);
    }

    // These instances exist only to exercise the pricing ledger. The normal
    // construction evaluator still rejects expression beyond its native cap.
    // Synthesizing an Origin result here never grants playable authorization.
    private static NativeOriginConstructionResult PricingOnlyRepeatedVision(
        int count, IReadOnlyList<string>? configurations = null)
    {
        var original = ElfProfile();
        var existing = original.Profile!.Underlying.InherentGrants;
        var vision = Assert.Single(existing, x =>
            x.Identity.MechanicalDefinitionId == "trait.low-light-vision");
        var copies = Enumerable.Range(0, count).Select(i => vision with
        {
            Id = "pricing-only-vision-" + i,
            ManifestationId = "pricing-only-manifestation-" + i,
            Parameters = configurations is null ? vision.Parameters :
                new Dictionary<string, JsonElement>
                {
                    ["variant"] = JsonSerializer.SerializeToElement(configurations[i])
                }
        }).ToArray();
        var edited = original.Profile.Underlying with
        {
            InherentGrants = existing.Where(x => x.Id != vision.Id)
                .Concat(copies).ToArray()
        };
        return original with
        {
            Profile = original.Profile with
            {
                Underlying = edited, Current = edited
            }
        };
    }

    [Fact]
    public void IndependentEffectiveOccurrencesOfOneGeneAreEachCharged()
    {
        var ledger = Hypothetical(9, new NativeOriginAllocationPrice("sight", 2,
            ["trait:trait.low-light-vision"]));
        var one = NativeOriginAllocationPreview.Assess(Catalog(),
            PricingOnlyRepeatedVision(1), ledger);
        var two = NativeOriginAllocationPreview.Assess(Catalog(),
            PricingOnlyRepeatedVision(2), ledger);
        var three = NativeOriginAllocationPreview.Assess(Catalog(),
            PricingOnlyRepeatedVision(3), ledger);

        Assert.Equal(5, one.CommittedLoad);
        Assert.Equal(7, two.CommittedLoad);
        Assert.Equal(9, three.CommittedLoad);
        Assert.Equal(2, two.Charges.Count(x => x.PriceId == "sight"));
        Assert.Equal(3, three.Charges.Count(x => x.PriceId == "sight"));
        Assert.Equal(3, three.Components.Count(x =>
            x.Key == "trait:trait.low-light-vision" &&
            x.AssignedPriceId == "sight"));
    }

    [Fact]
    public void SeparateSameGeneConfigurationsEachUseTheirOwnBasePrice()
    {
        var key = "trait:trait.low-light-vision";
        var price = new NativeOriginAllocationPrice("sight", 1, [key],
            Configurations:
            [
                new("variant-a", 2, 1,
                    [EqualsValue(key, "parameters/variant", "a")]),
                new("variant-b", 5, 1,
                    [EqualsValue(key, "parameters/variant", "b")])
            ]);
        var profile = PricingOnlyRepeatedVision(2, ["a", "b"]);
        var policy = Hypothetical(11, price) with
        {
            MultiplicityPremiums =
            [
                new("sight-extra", key, PerAdditionalInstanceLoad: 1)
            ]
        };
        var assessed = NativeOriginAllocationPreview.Assess(Catalog(), profile, policy);
        Assert.Equal("within-allowance", assessed.Status);
        Assert.Equal(11, assessed.CommittedLoad);
        Assert.Equal(new[] { 2, 5 }, assessed.Charges
            .Where(x => x.PriceId == "sight")
            .Select(x => x.Load).OrderBy(x => x).ToArray());
        Assert.Equal("sight-extra", Assert.Single(assessed.Charges,
            x => x.PriceId == "sight-extra").PriceId);

        var missing = NativeOriginAllocationPreview.Assess(Catalog(),
            PricingOnlyRepeatedVision(3, ["a", "b", "c"]), policy);
        Assert.Equal("unresolved", missing.Status);
        Assert.Null(missing.CommittedLoad);
        Assert.Contains(missing.Issues, x =>
            x.StartsWith("allocation-configuration-unpriced:sight:",
                StringComparison.Ordinal));
        Assert.DoesNotContain(missing.Charges, x => x.PriceId == "sight-extra");
    }

    [Fact]
    public void MarginalPremiumScalesWithInstancesAndNotWithDonorCount()
    {
        var key = "trait:trait.low-light-vision";
        var policy = Hypothetical(12,
            new NativeOriginAllocationPrice("sight", 2, [key])) with
        {
            MultiplicityPremiums =
            [
                new("sight-multiplicity", key,
                    PerAdditionalInstanceLoad: 1, PerPairLoad: 2)
            ]
        };
        var one = NativeOriginAllocationPreview.Assess(Catalog(),
            PricingOnlyRepeatedVision(1), policy);
        var two = NativeOriginAllocationPreview.Assess(Catalog(),
            PricingOnlyRepeatedVision(2), policy);
        var three = NativeOriginAllocationPreview.Assess(Catalog(),
            PricingOnlyRepeatedVision(3), policy);
        Assert.Equal(5, one.CommittedLoad);
        Assert.Equal(10, two.CommittedLoad);
        Assert.Equal(17, three.CommittedLoad);
        Assert.Equal("over-allowance", three.Status);
        Assert.DoesNotContain(one.Charges, x => x.PriceId == "sight-multiplicity");
        Assert.Equal(3, Assert.Single(two.Charges,
            x => x.PriceId == "sight-multiplicity").Load);
        Assert.Equal(8, Assert.Single(three.Charges,
            x => x.PriceId == "sight-multiplicity").Load);
        // Three donor offers coalescing into one effective benefit still
        // produce one base charge and no multiplicity surcharge.
        var sameBenefit = NativeOriginAllocationPreview.Assess(Catalog(),
            ElfProfile(parents: 3, selectAllVisionOffers: true), policy);
        Assert.Equal(5, sameBenefit.CommittedLoad);
        Assert.DoesNotContain(sameBenefit.Charges, x =>
            x.PriceId == "sight-multiplicity");
    }

    [Fact]
    public void InvalidMultiplicityPricingFailsClosedAndDoesNotWrap()
    {
        const string key = "trait:trait.low-light-vision";
        var ordinary = Hypothetical(20,
            new NativeOriginAllocationPrice("sight", 2, [key]));
        var pair = PricingOnlyRepeatedVision(2);
        NativeOriginAllocationPolicy Configure(
            params NativeOriginAllocationMultiplicityPremium[] premiums) =>
            ordinary with { MultiplicityPremiums = premiums };
        foreach (var bad in new[]
        {
            Configure(new NativeOriginAllocationMultiplicityPremium("free", key, 0, 0)),
            Configure(new NativeOriginAllocationMultiplicityPremium("unknown", "trait:trait.unpriced", 1)),
            Configure(new NativeOriginAllocationMultiplicityPremium("sight", key, 1)),
            Configure(new NativeOriginAllocationMultiplicityPremium("duplicated", key, 1),
                new NativeOriginAllocationMultiplicityPremium("other", key, 2)),
            Configure(new NativeOriginAllocationMultiplicityPremium("negative", key, -1))
        })
        {
            var assessed = NativeOriginAllocationPreview.Assess(Catalog(), pair, bad);
            Assert.Equal("invalid-policy", assessed.Status);
            Assert.Null(assessed.CommittedLoad);
        }

        var huge = NativeOriginAllocationPreview.Assess(Catalog(),
            PricingOnlyRepeatedVision(3),
            Configure(new NativeOriginAllocationMultiplicityPremium("too-expensive",
                key, int.MaxValue, PerPairLoad: int.MaxValue)));
        Assert.Equal("invalid-policy", huge.Status);
        Assert.Null(huge.CommittedLoad);
        Assert.Contains("allocation-multiplicity-overflow:too-expensive",
            huge.Issues);
    }
}
