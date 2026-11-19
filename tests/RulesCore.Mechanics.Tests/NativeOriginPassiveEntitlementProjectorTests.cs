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
        NativeCatalogSnapshot catalog, params string[] selectedTraits)
    {
        var proposal = new NativeOriginProposal("mixed-passive",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("elf", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf")),
                new("dwarf", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.dwarf"))
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
        var halfling = Direct(catalog, "species.halfling");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, gnome.Status);
        Assert.True(halfling.Status == NativeOriginConstructionStatus.Authorized,
            "Halfling: " + string.Join("; ", halfling.Issues.Select(x =>
                x.Code + ":" + x.Path)));

        var cunning = Assert.Single(
            NativeOriginPassiveEntitlementProjector.Project(catalog, gnome).Traits);
        Assert.Equal(new[] { "intelligence", "wisdom", "charisma" },
            cunning.SavingThrows!.Abilities);
        Assert.Equal("magic-only", cunning.SavingThrows.Scope);
        Assert.Empty(cunning.SavingThrows.Against);

        var hearing = Assert.Single(
            NativeOriginPassiveEntitlementProjector.Project(catalog, halfling).Traits);
        Assert.Equal("trait.halfling.keen-hearing", hearing.DefinitionId);
        var bonus = Assert.Single(hearing.SkillBonuses);
        Assert.Equal("skill.listen", bonus.SkillId);
        Assert.Equal(2, bonus.Amount);
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
