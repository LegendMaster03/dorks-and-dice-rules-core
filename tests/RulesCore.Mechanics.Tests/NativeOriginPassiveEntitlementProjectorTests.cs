using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Real first-party saving-throw and skill bonus definitions. These tests
/// exercise only read-only Origin inspection, never a save/check executor or
/// a substitute for native construction and scoped table authorization.
/// </summary>
public sealed class NativeOriginPassiveEntitlementProjectorTests
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

    private static NativeOriginConstructionResult Direct(
        NativeCatalogSnapshot catalog, string speciesId) =>
        new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(
            new NativeOriginProposal("passive-" + speciesId,
                NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"),
                [], [], [], SelectedSpeciesId: speciesId));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "passive-projection" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "passive-test-only";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    private static NativeOriginConstructionResult Mixed(
        NativeCatalogSnapshot catalog, params string[] selectedTraits) =>
        MixedWithSources(catalog, "species.elf", "species.dwarf", selectedTraits);

    private static NativeOriginConstructionResult MixedWithSources(
        NativeCatalogSnapshot catalog, string firstSource, string secondSource,
        params string[] selectedTraits)
    {
        var proposal = new NativeOriginProposal("mixed-passive",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("elf", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, firstSource)),
                new("second", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, secondSource))
            ], [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var offers = engine.Evaluate(new(proposal)).EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "elf" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);

        var chosen = selectedTraits.Select(id =>
            Assert.Single(offers, x => x.Identity.MechanicalDefinitionId == id)).ToArray();
        var permits = chosen.Where(x =>
            x.SelectionBasis.Eligibility != NativeOriginEligibility.Eligible)
            .Select((x, i) => new NativeTableRuleDecision(
                "permit-" + i, new("dm", "passive-test-only"),
                new("campaign", "passive-projection"), new("candidate", x.Id),
                JsonSerializer.SerializeToElement(new { eligibility = "eligible" })))
            .ToArray();

        var manifestations = foundation.Select((x, i) =>
            new NativeOriginManifestation("foundation-" + i, x.Id,
                SelectedValue: x.OfferedValue))
            .Concat(chosen.Select((x, i) =>
                new NativeOriginManifestation("selected-" + i, x.Id)))
            .ToArray();
        return engine.Evaluate(new(proposal with
        {
            Manifestations = manifestations,
            TableDecisions = permits
        }, TrustedScope: new("campaign", "passive-projection")),
            new TestAuthority());
    }

    private static NativeOriginConstructionResult WithGrant(
        NativeOriginConstructionResult result, NativeOriginEffectiveGrant replacement)
    {
        var configuration = result.Profile!.Underlying with
        {
            InherentGrants = result.Profile.Underlying.InherentGrants
                .Select(x => x.Id == replacement.Id ? replacement : x).ToArray()
        };
        return result with
        {
            Profile = result.Profile with
            {
                Underlying = configuration,
                Current = configuration
            }
        };
    }

    [Fact]
    public void ElfProjectsTwoSkillBonusesAndScopedFeySavingThrowAdvantage()
    {
        var catalog = Catalog();
        var completed = Direct(catalog, "species.elf");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var view = NativeOriginPassiveEntitlementProjector.Project(catalog, completed);

        Assert.Equal("resolved", view.Status);
        Assert.True(view.PreviewOnly);
        Assert.Empty(view.UnresolvedReasons);
        var keen = Assert.Single(view.Traits, x =>
            x.DefinitionId == "trait.elf.keen-senses");
        Assert.Equal("species.elf", keen.SourceId);
        Assert.StartsWith("grant:", keen.GrantId);
        Assert.StartsWith("/traits/", keen.OccurrencePath);
        Assert.Single(keen.SourceCandidateIds);
        Assert.Null(keen.SavingThrows);
        Assert.Equal(new[] { "skill.listen", "skill.spot" },
            keen.SkillBonuses.Select(x => x.SkillId));
        Assert.All(keen.SkillBonuses, x => Assert.Equal(2, x.Amount));

        var fey = Assert.Single(view.Traits, x =>
            x.DefinitionId == "trait.elf.fey-ancestry");
        Assert.Empty(fey.SkillBonuses);
        Assert.Equal(new[] { "condition.charmed" }, fey.SavingThrows!.Against);
        Assert.Empty(fey.SavingThrows.Abilities);
        Assert.Null(fey.SavingThrows.Scope);
        Assert.Equal("fey-ancestry",
            fey.CompleteEffect.GetProperty("id").GetString());
        Assert.Equal("unresolved",
            NativeOriginPassiveEntitlementProjector
                .ProjectApprovedMixed(catalog, completed).Status);
    }

    [Fact]
    public void DwarfKeepsWholeResistanceTraitWithoutProjectingDamageExecution()
    {
        var catalog = Catalog();
        var completed = Direct(catalog, "species.dwarf");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var view = NativeOriginPassiveEntitlementProjector.Project(catalog, completed);
        var resilience = Assert.Single(view.Traits);
        Assert.Equal("trait.dwarf.dwarven-resilience", resilience.DefinitionId);
        Assert.Equal(new[] { "poison-effects" }, resilience.SavingThrows!.Against);
        Assert.Equal(50, resilience.CompleteEffect.GetProperty("damageResistance")
            .GetProperty("poison").GetInt32());
        Assert.Empty(resilience.SkillBonuses);
    }

    [Fact]
    public void GnomeMagicOnlyScopeAndHalflingFixedSkillBonusStayDistinct()
    {
        var catalog = Catalog();
        var gnome = Direct(catalog, "species.gnome");
        var halfling = MixedWithSources(catalog, "species.elf",
            "species.halfling", "trait.halfling.keen-hearing");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, gnome.Status);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, halfling.Status);

        var cunning = Assert.Single(
            NativeOriginPassiveEntitlementProjector.Project(catalog, gnome).Traits);
        Assert.Equal(new[] { "intelligence", "wisdom", "charisma" },
            cunning.SavingThrows!.Abilities);
        Assert.Equal("magic-only", cunning.SavingThrows.Scope);
        Assert.Empty(cunning.SavingThrows.Against);

        var hearing = Assert.Single(NativeOriginPassiveEntitlementProjector
            .ProjectApprovedMixed(catalog, halfling).Traits);
        Assert.Equal("trait.halfling.keen-hearing", hearing.DefinitionId);
        var bonus = Assert.Single(hearing.SkillBonuses);
        Assert.Equal("skill.listen", bonus.SkillId);
        Assert.Equal(2, bonus.Amount);
    }

    [Fact]
    public void ConventionalHalflingAdmissionRemainsUnresolvedWithoutNewPolicy()
    {
        var catalog = Catalog();
        var halfling = Direct(catalog, "species.halfling");
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, halfling.Status);
        Assert.Contains(halfling.Issues, x =>
            x.Code == "unresolved-source-evidence");
        var view = NativeOriginPassiveEntitlementProjector.Project(catalog, halfling);
        Assert.Equal("unresolved", view.Status);
        Assert.Empty(view.Traits);
        Assert.Empty(view.OutsideScopeTraits);
    }

    [Fact]
    public void SelectedMixedSourcesStaySeparateAcrossSerialization()
    {
        var catalog = Catalog();
        var mixed = Mixed(catalog, "trait.elf.keen-senses",
            "trait.dwarf.dwarven-resilience");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, mixed.Status);
        var direct = NativeOriginPassiveEntitlementProjector.Project(catalog, mixed);
        Assert.Equal("unresolved", direct.Status);
        Assert.Empty(direct.Traits);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(mixed.Profile));
        Assert.NotNull(saved);
        var projection = NativeOriginPassiveEntitlementProjector.ProjectApprovedMixed(
            catalog, mixed with { Profile = saved });
        Assert.Equal("resolved", projection.Status);
        Assert.Equal(2, projection.Traits.Count);
        Assert.Contains(projection.Traits, x =>
            x.SourceId == "species.elf" &&
            x.DefinitionId == "trait.elf.keen-senses");
        Assert.Contains(projection.Traits, x =>
            x.SourceId == "species.dwarf" &&
            x.DefinitionId == "trait.dwarf.dwarven-resilience");
        Assert.All(projection.Traits, x => Assert.Single(x.SourceCandidateIds));
        Assert.Empty(projection.UnresolvedReasons);
    }

    [Fact]
    public void UnselectedMixedTraitsDoNotBecomeInheritedPassives()
    {
        var catalog = Catalog();
        var mixed = Mixed(catalog, "trait.elf.keen-senses");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, mixed.Status);
        var view = NativeOriginPassiveEntitlementProjector.ProjectApprovedMixed(
            catalog, mixed);
        Assert.Equal("resolved", view.Status);
        Assert.Single(view.Traits);
        Assert.DoesNotContain(view.Traits, x =>
            x.DefinitionId == "trait.dwarf.dwarven-resilience");
    }

    [Fact]
    public void ModifiedPayloadAndFabricatedSourceEvidenceFailClosed()
    {
        var catalog = Catalog();
        var completed = Direct(catalog, "species.elf");
        var keen = Assert.Single(completed.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.elf.keen-senses");
        var changed = JsonNode.Parse(keen.EffectiveValue!.Value.GetRawText())!;
        changed["skillBonuses"]!["skill.listen"] = 100;
        var altered = WithGrant(completed, keen with
        {
            EffectiveValue = JsonSerializer.SerializeToElement(changed)
        });
        Assert.Equal("unresolved",
            NativeOriginPassiveEntitlementProjector.Project(catalog, altered).Status);

        var fakeSource = WithGrant(completed, keen with
        {
            SourceCandidateIds = ["fabricated-offer"]
        });
        Assert.Equal("unresolved",
            NativeOriginPassiveEntitlementProjector.Project(catalog, fakeSource).Status);

        var fakeDefinition = WithGrant(completed, keen with
        {
            Identity = keen.Identity with
            {
                MechanicalDefinitionId = "trait.nonexistent.fake"
            }
        });
        Assert.Equal("unresolved",
            NativeOriginPassiveEntitlementProjector.Project(catalog, fakeDefinition).Status);

        var missingSelection = completed with
        {
            EvaluatedProposal = completed.EvaluatedProposal with
            {
                Manifestations = completed.EvaluatedProposal.Manifestations
                    .Where(x => !keen.SourceCandidateIds.Contains(
                        x.CandidateId, StringComparer.Ordinal)).ToArray()
            }
        };
        Assert.Equal("unresolved",
            NativeOriginPassiveEntitlementProjector.Project(catalog, missingSelection).Status);

        var fey = Assert.Single(completed.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.elf.fey-ancestry");
        var forgedScope = JsonNode.Parse(fey.EffectiveValue!.Value.GetRawText())!;
        forgedScope["savingThrows"]!["scope"] = "all-threats";
        Assert.Equal("unresolved", NativeOriginPassiveEntitlementProjector.Project(
            catalog, WithGrant(completed, fey with
            {
                EffectiveValue = JsonSerializer.SerializeToElement(forgedScope)
            })).Status);
    }

    [Fact]
    public void UnsupportedParametersPoolsAndMissingManifestationFailClosed()
    {
        var catalog = Catalog();
        var completed = Direct(catalog, "species.elf");
        var keen = Assert.Single(completed.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.elf.keen-senses");

        var parameter = WithGrant(completed, keen with
        {
            Parameters = new Dictionary<string, JsonElement>
            {
                ["skill"] = JsonSerializer.SerializeToElement("skill.stealth")
            }
        });
        Assert.Equal("unresolved",
            NativeOriginPassiveEntitlementProjector.Project(catalog, parameter).Status);

        var pool = WithGrant(completed, keen with
        {
            ResourceOwners = new Dictionary<string, string>
            {
                ["uses"] = "grant:imaginary-resource"
            }
        });
        Assert.Equal("unresolved",
            NativeOriginPassiveEntitlementProjector.Project(catalog, pool).Status);

        var parameterSource = WithGrant(completed, keen with
        {
            ParameterSources = new Dictionary<string, string>
            {
                ["skill"] = "invented-contributor"
            }
        });
        Assert.Equal("unresolved", NativeOriginPassiveEntitlementProjector
            .Project(catalog, parameterSource).Status);

        var incomplete = completed with
        {
            Status = NativeOriginConstructionStatus.Incomplete
        };
        Assert.Equal("unresolved",
            NativeOriginPassiveEntitlementProjector.Project(catalog, incomplete).Status);
    }

    [Fact]
    public void MissingCanonicalDefinitionAndAlteredOccurrenceCanNotDisappear()
    {
        var catalog = Catalog();
        var completed = Direct(catalog, "species.elf");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var keen = Assert.Single(completed.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.elf.keen-senses");

        // A payload without savingThrows/skillBonuses is still a native trait,
        // not evidence that there are no projected entitlements.
        var unknown = WithGrant(completed, keen with
        {
            Identity = keen.Identity with
            {
                MechanicalDefinitionId = "trait.missing.unrecognized"
            },
            EffectiveValue = JsonSerializer.SerializeToElement(new
            {
                id = "unknown-native", description = "not a recognized passive"
            })
        });
        Assert.Equal("unresolved",
            NativeOriginPassiveEntitlementProjector.Project(catalog, unknown).Status);

        foreach (var altered in new[]
        {
            keen with { Identity = keen.Identity with
                { Occurrence = keen.Identity.Occurrence with
                    { OccurrencePath = "/traits/2" } } },
            keen with { Identity = keen.Identity with
                { Occurrence = keen.Identity.Occurrence with
                    { OccurrencePath = "/traits/01" } } },
            keen with { Identity = keen.Identity with
                { Occurrence = keen.Identity.Occurrence with
                    { Source = new(NativeOriginSourceKind.Native, "species.gnome") } } },
            keen with { Identity = keen.Identity with
                { IntrinsicSlotId = "darkvisionFeet" } },
            keen with { Identity = keen.Identity with
                { Kind = NativeOriginCharacteristicKind.Other } },
            keen with { Identity = keen.Identity with
                {
                    Kind = NativeOriginCharacteristicKind.Intrinsic,
                    MechanicalDefinitionId = "trait.unknown.reclassified"
                } },
            keen with { ManifestationId = "fabricated-manifestation" },
        })
        {
            var result = NativeOriginPassiveEntitlementProjector.Project(
                catalog, WithGrant(completed, altered));
            Assert.Equal("unresolved", result.Status);
            Assert.Empty(result.Traits);
        }
    }

    [Fact]
    public void MissingCanonicalDefinitionOfAnUnrelatedTraitIsNotAnAllClear()
    {
        var catalog = Catalog();
        var orc = Direct(catalog, "species.orc");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, orc.Status);
        var baseline = NativeOriginPassiveEntitlementProjector.Project(catalog, orc);
        Assert.Equal("none", baseline.Status);
        Assert.Contains(baseline.OutsideScopeTraits, x =>
            x.DefinitionId == "trait.orc.adrenaline-rush");
        Assert.Empty(baseline.Traits);

        var rush = Assert.Single(orc.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.orc.adrenaline-rush");
        var edited = WithGrant(orc, rush with
        {
            Identity = rush.Identity with
            {
                MechanicalDefinitionId = "trait.unknown.action"
            }
        });
        var view = NativeOriginPassiveEntitlementProjector.Project(catalog, edited);
        Assert.Equal("unresolved", view.Status);
        Assert.Empty(view.Traits);
        Assert.Empty(view.OutsideScopeTraits);
    }

    [Fact]
    public void MultipleNativeParentsRetainOneTraitWithEveryContributingCandidate()
    {
        var catalog = Catalog();
        var proposal = new NativeOriginProposal("twin-keen-senses",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("first", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf")),
                new("second", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf"))
            ], [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var offers = engine.Evaluate(new(proposal)).EvaluatedProposal.Candidates;
        var keen = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.elf.keen-senses")
            .ToArray();
        Assert.Equal(2, keen.Length);
        var foundation = offers.Where(x => x.ContributorId == "first" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        Assert.Equal(3, foundation.Length);
        var permits = keen.Where(x =>
            x.SelectionBasis.Eligibility != NativeOriginEligibility.Eligible)
            .Select((x, i) => new NativeTableRuleDecision("permit-keen-" + i,
                new("dm", "passive-test-only"), new("campaign", "passive-projection"),
                new("candidate", x.Id),
                JsonSerializer.SerializeToElement(new { eligibility = "eligible" })))
            .ToArray();
        var manifests = foundation.Select((x, i) =>
            new NativeOriginManifestation("foundation-" + i, x.Id,
                SelectedValue: x.OfferedValue))
            .Concat(keen.Select((x, i) =>
                new NativeOriginManifestation("keen-" + i, x.Id))).ToArray();
        var result = engine.Evaluate(new(proposal with
        {
            Manifestations = manifests, TableDecisions = permits
        }, TrustedScope: new("campaign", "passive-projection")),
            new TestAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var projected = NativeOriginPassiveEntitlementProjector.ProjectApprovedMixed(
            catalog, result);
        Assert.Equal("resolved", projected.Status);
        var trait = Assert.Single(projected.Traits);
        Assert.Equal("trait.elf.keen-senses", trait.DefinitionId);
        Assert.Equal(2, trait.SourceCandidateIds.Count);
        Assert.Equal(keen.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal),
            trait.SourceCandidateIds);

        var restored = result with
        {
            Profile = JsonSerializer.Deserialize<NativeOriginProfile>(
                JsonSerializer.Serialize(result.Profile))
        };
        var another = NativeOriginPassiveEntitlementProjector.ProjectApprovedMixed(
            catalog, restored);
        Assert.Equal(JsonSerializer.Serialize(projected),
            JsonSerializer.Serialize(another));
    }

    [Fact]
    public void OrcOutsideScopeTraitRequiresExactSelectionAndManifestation()
    {
        var catalog = Catalog();
        var orc = Direct(catalog, "species.orc");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, orc.Status);
        var rush = Assert.Single(orc.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.orc.adrenaline-rush");
        var baseline = NativeOriginPassiveEntitlementProjector.Project(catalog, orc);
        Assert.Equal("none", baseline.Status);
        Assert.Empty(baseline.Traits);
        var opaque = Assert.Single(baseline.OutsideScopeTraits, x =>
            x.DefinitionId == "trait.orc.adrenaline-rush");
        Assert.Equal(rush.Id, opaque.GrantId);
        Assert.Equal(rush.ManifestationId, opaque.ManifestationId);
        Assert.Equal(rush.SourceCandidateIds, opaque.SourceCandidateIds);
        Assert.Equal("species.orc", opaque.SourceId);
        Assert.True(JsonElement.DeepEquals(rush.EffectiveValue!.Value,
            opaque.CompleteEffect));

        var other = Assert.Single(orc.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.orc.savage-attacks");
        var forged = new[]
        {
            rush with { SourceCandidateIds = ["fabricated-candidate"] },
            rush with { SourceCandidateIds = [] },
            rush with { SourceCandidateIds =
                [rush.SourceCandidateIds[0], rush.SourceCandidateIds[0]] },
            rush with { SourceCandidateIds = [other.SourceCandidateIds[0]] },
            rush with { SourceCandidateIds =
                [rush.SourceCandidateIds[0], other.SourceCandidateIds[0]] },
            rush with { ManifestationId = "fabricated-manifestation" },
            rush with { Authorization = new NativeOriginDecisionBasis(
                NativeOriginAdjudication.Supported, "species.elf") },
            rush with { Authorization = null }
        };

        foreach (var modified in forged)
        {
            var view = NativeOriginPassiveEntitlementProjector.Project(
                catalog, WithGrant(orc, modified));
            Assert.Equal("unresolved", view.Status);
            Assert.Empty(view.Traits);
            Assert.Empty(view.OutsideScopeTraits);
        }

        var removedManifestation = orc with
        {
            EvaluatedProposal = orc.EvaluatedProposal with
            {
                Manifestations = orc.EvaluatedProposal.Manifestations
                    .Where(x => x.Id != rush.ManifestationId).ToArray()
            }
        };
        Assert.Equal("unresolved", NativeOriginPassiveEntitlementProjector
            .Project(catalog, removedManifestation).Status);

        var forgedManifestationIdentity = orc with
        {
            EvaluatedProposal = orc.EvaluatedProposal with
            {
                Manifestations = orc.EvaluatedProposal.Manifestations
                    .Select(x => x.Id == rush.ManifestationId
                        ? x with { CandidateId = other.SourceCandidateIds[0] }
                        : x).ToArray()
            }
        };
        Assert.Equal("unresolved", NativeOriginPassiveEntitlementProjector
            .Project(catalog, forgedManifestationIdentity).Status);

        var changedCandidateIdentity = orc with
        {
            EvaluatedProposal = orc.EvaluatedProposal with
            {
                Candidates = orc.EvaluatedProposal.Candidates
                    .Select(x => x.Id == rush.SourceCandidateIds[0]
                        ? x with { Identity = other.Identity }
                        : x).ToArray()
            }
        };
        Assert.Equal("unresolved", NativeOriginPassiveEntitlementProjector
            .Project(catalog, changedCandidateIdentity).Status);
    }

    [Fact]
    public void MixedOrcOutsideScopeTraitRequiresContributorAuthorization()
    {
        var catalog = Catalog();
        var mixed = MixedWithSources(catalog, "species.elf", "species.orc",
            "trait.orc.adrenaline-rush", "trait.elf.keen-senses");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, mixed.Status);
        var baseline = NativeOriginPassiveEntitlementProjector.ProjectApprovedMixed(
            catalog, mixed);
        Assert.Equal("resolved", baseline.Status);
        Assert.Single(baseline.Traits);
        var rush = Assert.Single(mixed.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.orc.adrenaline-rush");
        var opaque = Assert.Single(baseline.OutsideScopeTraits, x =>
            x.DefinitionId == "trait.orc.adrenaline-rush");
        Assert.Equal(rush.ManifestationId, opaque.ManifestationId);
        Assert.Equal(rush.SourceCandidateIds, opaque.SourceCandidateIds);

        var elf = Assert.Single(mixed.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.elf.keen-senses");
        foreach (var forged in new[]
        {
            rush with { SourceCandidateIds = ["invented-parent"] },
            rush with { SourceCandidateIds =
                [rush.SourceCandidateIds[0], rush.SourceCandidateIds[0]] },
            rush with { SourceCandidateIds =
                [rush.SourceCandidateIds[0], elf.SourceCandidateIds[0]] },
            rush with { Authorization = new NativeOriginDecisionBasis(
                NativeOriginAdjudication.Supported, "species.orc") },
            rush with { ManifestationId = elf.ManifestationId }
        })
        {
            var result = NativeOriginPassiveEntitlementProjector.ProjectApprovedMixed(
                catalog, WithGrant(mixed, forged));
            Assert.Equal("unresolved", result.Status);
            Assert.Empty(result.Traits);
            Assert.Empty(result.OutsideScopeTraits);
        }

        var altered = mixed with
        {
            EvaluatedProposal = mixed.EvaluatedProposal with
            {
                Manifestations = mixed.EvaluatedProposal.Manifestations
                    .Where(x => x.Id != rush.ManifestationId).ToArray()
            }
        };
        Assert.Equal("unresolved", NativeOriginPassiveEntitlementProjector
            .ProjectApprovedMixed(catalog, altered).Status);
        var roundTrip = mixed with
        {
            Profile = JsonSerializer.Deserialize<NativeOriginProfile>(
                JsonSerializer.Serialize(mixed.Profile))
        };
        var replay = NativeOriginPassiveEntitlementProjector.ProjectApprovedMixed(
            catalog, roundTrip);
        Assert.Equal(JsonSerializer.Serialize(baseline),
            JsonSerializer.Serialize(replay));

        var unresolved = mixed with
        {
            Status = NativeOriginConstructionStatus.Incomplete
        };
        var denied = NativeOriginPassiveEntitlementProjector.ProjectApprovedMixed(
            catalog, unresolved);
        Assert.Equal("unresolved", denied.Status);
        Assert.Empty(denied.Traits);
        Assert.Empty(denied.OutsideScopeTraits);
    }

    [Fact]
    public void UnrelatedSpeciesAndNoSelectableNativePassivesRemainAbsent()
    {
        var catalog = Catalog();
        var orc = Direct(catalog, "species.orc");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, orc.Status);
        var view = NativeOriginPassiveEntitlementProjector.Project(catalog, orc);
        Assert.Equal("none", view.Status);
        Assert.Empty(view.Traits);
        Assert.Empty(view.UnresolvedReasons);
    }
}
