using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Test-only native definitions exercise the production construction pipeline.
/// They are not playable Construct or biological Species content.
/// </summary>
public sealed class NativeOriginSynergyTests
{
    private const string Rules = """
        [
          {"id":"locomotion-stability","resultTraitId":"trait.fixture.stability",
           "inputs":[
             {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:locomotion"}],"count":1},
             {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:stabilizer"}],"count":1}]},
          {"id":"powered-flight","resultTraitId":"trait.fixture.flight",
           "inputs":[
             {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":1},
             {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:muscle"}],"count":1}]},
          {"id":"three-parts","resultTraitId":"trait.fixture.guardian",
           "inputs":[
             {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:locomotion"}],"count":1},
             {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:stabilizer"}],"count":1},
             {"anyOf":[{"intrinsicSlotId":"size"}],"count":1}]},
          {"id":"alternate-actuation","resultTraitId":"trait.fixture.response",
           "inputs":[
             {"anyOf":[
               {"mechanicalDefinitionId":"species.fixture#trait:kinetic"},
               {"mechanicalDefinitionId":"species.fixture#trait:alchemical"}],"count":1},
             {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:stabilizer"}],"count":1}]},
          {"id":"transitive","resultTraitId":"trait.fixture.aerial-stability",
           "inputs":[
             {"anyOf":[{"mechanicalDefinitionId":"trait.fixture.stability"}],"count":1},
             {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":1}]},
          {"id":"cannot-reuse-one-grant","resultTraitId":"trait.fixture.duplicate",
           "inputs":[
             {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:locomotion"}],"count":2}]}
        ]
        """;

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "rules-core-synergy-" + Guid.NewGuid().ToString("N"));

        public NativeCatalogSnapshot Catalog { get; }

        public Fixture(string synergies = Rules)
        {
            var native = Path.Combine(_root, "first-party-001", "native");
            Directory.CreateDirectory(Path.Combine(native, "traits"));
            File.WriteAllText(Path.Combine(_root, "first-party-001", "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(native, "creature-types.json"), """
                {"id":"creature-type.humanoid","kind":"creatureType","name":"Humanoid",
                 "parent":"creature","originationMethods":["biological"]}
                """);
            File.WriteAllText(Path.Combine(native, "species.json"), """
                {"id":"species.fixture","kind":"species","name":"Fixture","selectable":true,
                 "creatureType":"humanoid","size":"medium","walkingSpeedFeet":30,
                 "traits":[
                   {"id":"locomotion","name":"Locomotion","systemTag":"motion"},
                   {"id":"stabilizer","name":"Stabilization","systemTag":"balance"},
                   {"id":"wings","name":"Wings","systemTag":"wing"},
                   {"id":"muscle","name":"Flight Musculature","systemTag":"muscle"},
                   {"id":"kinetic","name":"Kinetic Actuator","systemTag":"kinetic"},
                   {"id":"alchemical","name":"Alchemical Actuator","systemTag":"alchemical"}]}
                """);
            File.WriteAllText(Path.Combine(native, "rules.json"),
                """{"id":"rule.fixture","kind":"rule","name":"Fixture Rule","methodDefinitions":[{"id":"biological","name":"Biological"}],"originSynergies":""" +
                synergies + "}");
            File.WriteAllText(Path.Combine(native, "traits", "results.json"), """
                [
                  {"id":"trait.fixture.stability","kind":"origin-trait","name":"Stability","trait":{"id":"stability","name":"Stability","systemTag":"stability"}},
                  {"id":"trait.fixture.flight","kind":"origin-trait","name":"Flight","trait":{"id":"flight","name":"Flight","systemTag":"flight"}},
                  {"id":"trait.fixture.guardian","kind":"origin-trait","name":"Guardian","trait":{"id":"guardian","name":"Guardian","systemTag":"guardian"}},
                  {"id":"trait.fixture.response","kind":"origin-trait","name":"Response","trait":{"id":"response","name":"Response","systemTag":"response"}},
                  {"id":"trait.fixture.aerial-stability","kind":"origin-trait","name":"Aerial Stability","trait":{"id":"aerial-stability","name":"Aerial Stability","systemTag":"aerial"}},
                  {"id":"trait.fixture.duplicate","kind":"origin-trait","name":"Duplicate","trait":{"id":"duplicate","name":"Duplicate","systemTag":"duplicate"}},
                  {"id":"trait.fixture.loop-a","kind":"origin-trait","name":"Loop A","trait":{"id":"loop-a","name":"Loop A","systemTag":"loop-a"}},
                  {"id":"trait.fixture.loop-b","kind":"origin-trait","name":"Loop B","trait":{"id":"loop-b","name":"Loop B","systemTag":"loop-b"}},
                  {"id":"trait.fixture.choice","kind":"origin-trait","name":"Choice","trait":{"id":"choice","choices":[{"id":"mode","options":["a","b"]}]}},
                  {"id":"trait.fixture.resource","kind":"origin-trait","name":"Resource","trait":{"id":"resource","uses":3}},
                  {"id":"trait.fixture.select","kind":"origin-trait","name":"Select","trait":{"id":"select","chooseFrom":"feat:all","choiceCount":1}},
                  {"id":"trait.fixture.feat","kind":"origin-trait","name":"Feat","trait":{"id":"feat","grantType":"feat-choice"}}
                ]
                """);
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private static NativeOriginProposal Proposal(string id = "fixture") =>
        new(id, NativeOriginIntent.Direct, new("rule.fixture", "biological"),
            [], [], [], SelectedSpeciesId: "species.fixture");

    private static NativeOriginConstructionResult Evaluate(Fixture fixture,
        params string[] occurrences)
    {
        var engine = new NativeOriginConstructionEngine(fixture.Catalog);
        var proposal = Proposal();
        var discovered = engine.Evaluate(new(proposal, AutoManifestDirectDefaults: false))
            .EvaluatedProposal;
        var selected = discovered.Candidates
            .Where(candidate => occurrences.Contains(
                candidate.Identity.Occurrence.OccurrencePath, StringComparer.Ordinal))
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Select((candidate, i) => new NativeOriginManifestation("manifest-" + i,
                candidate.Id, candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                    ? candidate.OfferedValue : null))
            .ToArray();
        return engine.Evaluate(new(proposal with { Manifestations = selected },
            AutoManifestDirectDefaults: false));
    }

    private static bool Has(NativeOriginConstructionResult result, string definitionId) =>
        result.Profile!.Underlying.InherentGrants.Any(x =>
            x.Identity.MechanicalDefinitionId == definitionId);

    [Fact]
    public void ConstructComponentsProduceOneStableDerivedGrantAndRetractWithMissingInput()
    {
        using var fixture = new Fixture();
        var full = Evaluate(fixture, "/creatureType", "/size",
            "/walkingSpeedFeet", "/traits/0", "/traits/1");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, full.Status);
        Assert.True(Has(full, "trait.fixture.stability"));
        var grant = full.Profile!.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.stability");
        Assert.Equal("rule.fixture", grant.Synergy!.RuleId);
        Assert.Equal(2, grant.Synergy.Inputs.Count);
        Assert.Equal(2, grant.SourceCandidateIds.Count);
        Assert.All(grant.Synergy.Inputs, evidence =>
            Assert.Equal("species.fixture", evidence.Identity.Occurrence.Source.Id));

        var repeated = Evaluate(fixture, "/traits/1", "/walkingSpeedFeet",
            "/traits/0", "/creatureType", "/size");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, repeated.Status);
        Assert.Equal(JsonSerializer.Serialize(full.Profile.Underlying.InherentGrants),
            JsonSerializer.Serialize(repeated.Profile!.Underlying.InherentGrants));

        var missing = Evaluate(fixture, "/creatureType", "/size",
            "/walkingSpeedFeet", "/traits/0");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, missing.Status);
        Assert.False(Has(missing, "trait.fixture.stability"));
        Assert.True(Has(missing, "species.fixture#trait:locomotion"));
        Assert.DoesNotContain(full.Issues, issue => issue.Code == "synergy-provenance-invalid");
    }

    [Fact]
    public void OfferedButUnmanifestedBiologicalCharacteristicsCannotActivateFlight()
    {
        using var fixture = new Fixture();
        var offered = new NativeOriginConstructionEngine(fixture.Catalog)
            .Evaluate(new(Proposal(), AutoManifestDirectDefaults: false));
        Assert.Contains(offered.EvaluatedProposal.Candidates,
            candidate => candidate.Identity.Occurrence.OccurrencePath == "/traits/2");
        Assert.False(Has(offered, "trait.fixture.flight"));

        Assert.False(Has(Evaluate(fixture, "/traits/2"), "trait.fixture.flight"));
        Assert.True(Has(Evaluate(fixture, "/traits/2", "/traits/3"),
            "trait.fixture.flight"));
    }

    [Fact]
    public void AlternativeActuationAndThreePrerequisitesAreDeclarative()
    {
        using var fixture = new Fixture();
        Assert.True(Has(Evaluate(fixture, "/traits/1", "/traits/4"),
            "trait.fixture.response"));
        Assert.True(Has(Evaluate(fixture, "/traits/1", "/traits/5"),
            "trait.fixture.response"));
        Assert.False(Has(Evaluate(fixture, "/traits/1"), "trait.fixture.response"));

        Assert.True(Has(Evaluate(fixture, "/traits/0", "/traits/1", "/size"),
            "trait.fixture.guardian"));
        Assert.False(Has(Evaluate(fixture, "/traits/0", "/traits/1"),
            "trait.fixture.guardian"));
    }

    [Fact]
    public void TransitiveGrantInvalidatesWhenItsIndirectPrerequisiteDisappears()
    {
        using var fixture = new Fixture();
        var complete = Evaluate(fixture, "/traits/0", "/traits/1", "/traits/2");
        Assert.True(Has(complete, "trait.fixture.aerial-stability"));
        var transitive = complete.Profile!.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.aerial-stability");
        Assert.Contains(transitive.Synergy!.Inputs, x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.stability");
        Assert.False(Has(Evaluate(fixture, "/traits/0", "/traits/2"),
            "trait.fixture.aerial-stability"));
    }

    [Fact]
    public void ASourceOccurrenceCannotFillTwoSlotsOrMintDuplicateResults()
    {
        using var fixture = new Fixture();
        var result = Evaluate(fixture, "/traits/0", "/traits/1", "/traits/4", "/traits/5");
        Assert.False(Has(result, "trait.fixture.duplicate"));
        Assert.Single(result.Profile!.Underlying.InherentGrants.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.response"));
        Assert.All(result.Profile.Underlying.InherentGrants.Where(x => x.Synergy is not null),
            x => Assert.Equal(x.SourceCandidateIds.Distinct(StringComparer.Ordinal).Count(),
                x.SourceCandidateIds.Count));
    }

    [Fact]
    public void CyclicAuthoringNeverBootstrapsNewBenefits()
    {
        const string cyclic = """
            [
              {"id":"loop-a","resultTraitId":"trait.fixture.loop-a",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"trait.fixture.loop-b"}],"count":1},
                         {"anyOf":[{"intrinsicSlotId":"size"}],"count":1}]},
              {"id":"loop-b","resultTraitId":"trait.fixture.loop-b",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"trait.fixture.loop-a"}],"count":1},
                         {"anyOf":[{"intrinsicSlotId":"size"}],"count":1}]}
            ]
            """;
        using var fixture = new Fixture(cyclic);
        var result = Evaluate(fixture, "/size");
        Assert.False(Has(result, "trait.fixture.loop-a"));
        Assert.False(Has(result, "trait.fixture.loop-b"));
        Assert.Contains(result.Issues, x => x.Code == "synergy-cycle-unresolved");
    }

    [Fact]
    public void ForgedProvenanceAndUntrustedNativeCandidatesFailClosed()
    {
        using var fixture = new Fixture();
        var result = Evaluate(fixture, "/traits/0", "/traits/1");
        var original = result.Profile!.Underlying.InherentGrants.ToArray();
        var tampered = original.Select(x => x.Synergy is null ? x : x with
        {
            Synergy = x.Synergy with { InteractionId = "forged" }
        }).ToArray();
        var forgedProfile = result.Profile with
        {
            Underlying = result.Profile.Underlying with { InherentGrants = tampered },
            Current = result.Profile.Current with { InherentGrants = tampered }
        };
        var review = new NativeOriginContractValidator(fixture.Catalog)
            .ReviewProfile(forgedProfile, result.EvaluatedProposal);
        Assert.Contains(review.Issues, x => x.Code == "synergy-provenance-invalid");

        var fake = new NativeOriginCandidate("forged",
            new(NativeOriginCharacteristicKind.Trait,
                new(new(NativeOriginSourceKind.Native, "rule.fixture"),
                    "/originSynergies/fake"),
                MechanicalDefinitionId: "trait.fixture.flight"),
            new(NativeOriginEligibility.Eligible, "rule.fixture"));
        var injected = Proposal() with
        {
            Candidates = [fake],
            Manifestations = [new("fake-manifest", "forged")]
        };
        var evaluated = new NativeOriginConstructionEngine(fixture.Catalog)
            .Evaluate(new(injected, AutoManifestDirectDefaults: false));
        Assert.Contains(evaluated.Issues, x => x.Code == "candidate-not-offered");
        Assert.Equal(NativeOriginConstructionStatus.Invalid, evaluated.Status);
        Assert.True(evaluated.Profile is null || !evaluated.Profile.Underlying.InherentGrants
            .Any(x => x.Identity.MechanicalDefinitionId == "trait.fixture.flight"));
    }

    private sealed class SyntheticAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision, NativeTableDecisionScope scope) =>
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "verified" &&
            scope == new NativeTableDecisionScope("campaign", "example") &&
            decision.Scope == scope;

        public bool AuthorizesCatalogChoice(string catalogId, string optionId, string evidenceId) =>
            false;
    }

    [Fact]
    public void BiologicalContributorOffersRequireActualAuthorizedManifestations()
    {
        using var fixture = new Fixture();
        var proposal = new NativeOriginProposal("mixed-fixture", NativeOriginIntent.Mixed,
            new("rule.fixture", "biological"),
            [new("parent", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, "species.fixture"))],
            [], []);
        var engine = new NativeOriginConstructionEngine(fixture.Catalog);
        var discovered = engine.Evaluate(new(proposal, AutoManifestDirectDefaults: false));
        var wings = discovered.EvaluatedProposal.Candidates.Single(x =>
            x.ContributorId == "parent" && x.Identity.Occurrence.OccurrencePath == "/traits/2");
        var muscle = discovered.EvaluatedProposal.Candidates.Single(x =>
            x.ContributorId == "parent" && x.Identity.Occurrence.OccurrencePath == "/traits/3");
        Assert.True(discovered.Profile is null || !discovered.Profile.Underlying.InherentGrants
            .Any(x => x.Identity.MechanicalDefinitionId == "trait.fixture.flight"));

        var permissions = new[] { wings, muscle }.Select((candidate, i) =>
            new NativeTableRuleDecision("permit-" + i,
                new("dm", "verified"), new("campaign", "example"),
                new("candidate", candidate.Id),
                JsonSerializer.SerializeToElement(new { eligibility = "eligible" }))).ToArray();
        var manifests = new[]
        {
            new NativeOriginManifestation("selected-wings", wings.Id),
            new NativeOriginManifestation("selected-muscle", muscle.Id)
        };
        var selected = proposal with { Manifestations = manifests, TableDecisions = permissions };
        var allowed = engine.Evaluate(new(selected, new("campaign", "example"),
            AutoManifestDirectDefaults: false), new SyntheticAuthority());
        Assert.True(Has(allowed, "trait.fixture.flight"));
        Assert.All(allowed.Profile!.Underlying.InherentGrants.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.flight"),
            x => Assert.Equal(2, x.Synergy!.Inputs.Count));

        var withoutMuscle = engine.Evaluate(new(selected with
        {
            Manifestations = [manifests[0]]
        }, new("campaign", "example"), AutoManifestDirectDefaults: false),
            new SyntheticAuthority());
        Assert.False(Has(withoutMuscle, "trait.fixture.flight"));

        var unauthorized = engine.Evaluate(new(selected, new("campaign", "different"),
            AutoManifestDirectDefaults: false), new SyntheticAuthority());
        Assert.True(unauthorized.Profile is null ||
            !unauthorized.Profile.Underlying.InherentGrants.Any(x =>
                x.Identity.MechanicalDefinitionId == "trait.fixture.flight"));
    }

    [Fact]
    public void ReconciledSuppressionPreventsOtherwiseQualifiedSynergy()
    {
        using var fixture = new Fixture();
        var engine = new NativeOriginConstructionEngine(fixture.Catalog);
        var proposal = Proposal("suppressed");
        var offers = engine.Evaluate(new(proposal, AutoManifestDirectDefaults: false))
            .EvaluatedProposal.Candidates;
        var wings = offers.Single(x => x.Identity.Occurrence.OccurrencePath == "/traits/2");
        var muscle = offers.Single(x => x.Identity.Occurrence.OccurrencePath == "/traits/3");
        var manifests = new[]
        {
            new NativeOriginManifestation("wings", wings.Id),
            new NativeOriginManifestation("muscle", muscle.Id)
        };
        var original = engine.Evaluate(new(proposal with { Manifestations = manifests },
            AutoManifestDirectDefaults: false));
        Assert.True(Has(original, "trait.fixture.flight"));

        var constraint = new NativeOriginConstraint("requires-movement",
            NativeOriginRelationshipKind.Requires,
            [new(MechanicalDefinitionId: muscle.Identity.MechanicalDefinitionId)],
            [new(IntrinsicSlotId: "walkingSpeedFeet")],
            Minimum: 1, TableDecisionId: "enforce-movement");
        var decision = new NativeTableRuleDecision("enforce-movement",
            new("dm", "verified"), new("campaign", "example"),
            new("constraint", constraint.Id),
            JsonSerializer.SerializeToElement(new { enforce = true }));
        var suppressed = engine.Evaluate(new(proposal with
        {
            Manifestations = manifests,
            Constraints = [constraint],
            TableDecisions = [decision]
        }, new("campaign", "example"), AutoManifestDirectDefaults: false),
            new SyntheticAuthority());
        Assert.DoesNotContain(suppressed.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == muscle.Identity.MechanicalDefinitionId);
        Assert.False(Has(suppressed, "trait.fixture.flight"));
    }

    private static NativeOriginProposal Selected(Fixture fixture,
        params string[] occurrences)
    {
        var engine = new NativeOriginConstructionEngine(fixture.Catalog);
        var proposal = Proposal();
        var offers = engine.Evaluate(new(proposal, AutoManifestDirectDefaults: false))
            .EvaluatedProposal.Candidates;
        var manifests = offers
            .Where(x => occurrences.Contains(x.Identity.Occurrence.OccurrencePath,
                StringComparer.Ordinal))
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .Select((x, i) => new NativeOriginManifestation("selection-" + i, x.Id,
                x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                    ? x.OfferedValue : null))
            .ToArray();
        return proposal with { Manifestations = manifests };
    }

    private static NativeTableRuleDecision TableSynergy(
        string decisionId = "dm-table-flight", string targetId = "table-flight",
        string authoredId = "table-flight", string resultId = "trait.fixture.guardian")
    {
        var rule = JsonSerializer.SerializeToElement(new
        {
            id = authoredId,
            resultTraitId = resultId,
            inputs = new[]
            {
                new { anyOf = new[] { new { mechanicalDefinitionId =
                    "species.fixture#trait:wings" } }, count = 1 },
                new { anyOf = new[] { new { mechanicalDefinitionId =
                    "species.fixture#trait:muscle" } }, count = 1 }
            }
        });
        return new NativeTableRuleDecision(decisionId,
            new("dm", "verified"), new("campaign", "example"),
            new("origin-synergy", targetId), rule);
    }

    [Fact]
    public void AuthenticatedTableSynergyUsesCanonicalTraitAndExactProvenance()
    {
        using var fixture = new Fixture();
        var decision = TableSynergy();
        var proposal = Selected(fixture, "/traits/2", "/traits/3") with
        {
            TableDecisions = [decision]
        };
        var result = new NativeOriginConstructionEngine(fixture.Catalog).Evaluate(
            new(proposal, new("campaign", "example"),
                AutoManifestDirectDefaults: false), new SyntheticAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var grant = result.Profile!.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.guardian");
        Assert.Equal(NativeOriginSourceKind.Custom, grant.Identity.Occurrence.Source.Kind);
        Assert.Equal("table-decision:dm-table-flight", grant.Synergy!.RuleId);
        Assert.Equal("table-flight", grant.Synergy.InteractionId);
        Assert.Equal(NativeOriginAdjudication.DmAuthorized,
            grant.Authorization!.Adjudication);
        Assert.Equal(decision.Id, grant.Authorization.TableDecisionId);
        Assert.Equal(2, grant.Synergy.Inputs.Count);
        Assert.Equal(2, grant.SourceCandidateIds.Count);
        Assert.All(grant.Synergy.Inputs, input => Assert.StartsWith(
            "species.fixture#trait:", input.Identity.MechanicalDefinitionId));

        // A structural review without an authenticated policy context may
        // inspect the claim, but must never confer table-created benefits.
        var untrustedReview = new NativeOriginContractValidator(fixture.Catalog)
            .ReviewProfile(result.Profile, result.EvaluatedProposal);
        Assert.Contains(untrustedReview.Issues,
            x => x.Code == "synergy-provenance-invalid");

        var noInput = new NativeOriginConstructionEngine(fixture.Catalog).Evaluate(
            new(proposal with { Manifestations = [proposal.Manifestations[0]] },
                new("campaign", "example"), AutoManifestDirectDefaults: false),
            new SyntheticAuthority());
        Assert.False(Has(noInput, "trait.fixture.guardian"));
    }

    [Fact]
    public void UntrustedOrMisScopedTableRulesCannotCreateDerivedBenefits()
    {
        using var fixture = new Fixture();
        var decision = TableSynergy();
        var proposal = Selected(fixture, "/traits/2", "/traits/3") with
        {
            TableDecisions = [decision]
        };
        var engine = new NativeOriginConstructionEngine(fixture.Catalog);
        var untrusted = engine.Evaluate(
            new(proposal, new("campaign", "example"),
                AutoManifestDirectDefaults: false));
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, untrusted.Status);
        Assert.Contains(untrusted.Issues,
            x => x.Code == "synergy-table-decision-unverified");
        Assert.False(Has(untrusted, "trait.fixture.guardian"));

        var misScoped = engine.Evaluate(
            new(proposal, new("campaign", "other"),
                AutoManifestDirectDefaults: false), new SyntheticAuthority());
        Assert.Contains(misScoped.Issues,
            x => x.Code == "synergy-table-decision-unverified");
        Assert.False(Has(misScoped, "trait.fixture.guardian"));

        var forgedEvidence = engine.Evaluate(
            new(proposal with
            {
                TableDecisions = [decision with
                {
                    Authority = new("dm", "forged")
                }]
            }, new("campaign", "example"), AutoManifestDirectDefaults: false),
            new SyntheticAuthority());
        Assert.False(Has(forgedEvidence, "trait.fixture.guardian"));
        Assert.Contains(forgedEvidence.Issues,
            x => x.Code == "synergy-table-decision-unverified");
    }

    [Fact]
    public void MalformedTableAuthoringAndDuplicateTargetsFailClosed()
    {
        using var fixture = new Fixture();
        var engine = new NativeOriginConstructionEngine(fixture.Catalog);
        var proposal = Selected(fixture, "/traits/2", "/traits/3");
        var mismatched = engine.Evaluate(new(proposal with
        {
            TableDecisions = [TableSynergy(authoredId: "not-table-flight")]
        }, new("campaign", "example"), AutoManifestDirectDefaults: false),
            new SyntheticAuthority());
        Assert.Contains(mismatched.Issues,
            x => x.Code == "synergy-authoring-invalid");
        Assert.False(Has(mismatched, "trait.fixture.guardian"));

        var fabricated = engine.Evaluate(new(proposal with
        {
            TableDecisions = [TableSynergy(resultId: "trait.unknown")]
        }, new("campaign", "example"), AutoManifestDirectDefaults: false),
            new SyntheticAuthority());
        Assert.Contains(fabricated.Issues,
            x => x.Code == "synergy-authoring-invalid");
        Assert.False(Has(fabricated, "trait.fixture.guardian"));

        var duplicate = engine.Evaluate(new(proposal with
        {
            TableDecisions = [TableSynergy(),
                TableSynergy(decisionId: "different-decision")]
        }, new("campaign", "example"), AutoManifestDirectDefaults: false),
            new SyntheticAuthority());
        Assert.Equal(NativeOriginConstructionStatus.Invalid, duplicate.Status);
        Assert.Null(duplicate.Profile);
        Assert.Contains(duplicate.Issues,
            x => x.Code == "synergy-table-decision-conflict");
    }

    [Fact]
    public void ReconciledDuplicateContributorOccurrencesDoNotFillTwoInputSlots()
    {
        const string doubleWing = """
            [{"id":"double-wing","resultTraitId":"trait.fixture.guardian",
              "inputs":[{"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":2}]}]
            """;
        using var fixture = new Fixture(doubleWing);
        var proposal = new NativeOriginProposal("two-parents", NativeOriginIntent.Mixed,
            new("rule.fixture", "biological"),
            [
                new("parent-one", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.fixture")),
                new("parent-two", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.fixture"))
            ], [], []);
        var engine = new NativeOriginConstructionEngine(fixture.Catalog);
        var discovered = engine.Evaluate(new(proposal, AutoManifestDirectDefaults: false));
        var wings = discovered.EvaluatedProposal.Candidates.Where(x =>
            x.Identity.Occurrence.OccurrencePath == "/traits/2").ToArray();
        Assert.Equal(2, wings.Length);
        var decisions = wings.Select((candidate, i) => new NativeTableRuleDecision(
            "permit-wings-" + i, new("dm", "verified"), new("campaign", "example"),
            new("candidate", candidate.Id),
            JsonSerializer.SerializeToElement(new { eligibility = "eligible" }))).ToArray();
        var manifests = wings.Select((candidate, i) =>
            new NativeOriginManifestation("selected-wings-" + i, candidate.Id)).ToArray();
        var result = engine.Evaluate(new(proposal with
        {
            Manifestations = manifests,
            TableDecisions = decisions
        }, new("campaign", "example"), AutoManifestDirectDefaults: false),
            new SyntheticAuthority());

        Assert.NotNull(result.Profile);
        var effectiveWings = result.Profile.Underlying.InherentGrants.Where(x =>
            x.Identity.MechanicalDefinitionId == "species.fixture#trait:wings").ToArray();
        Assert.Single(effectiveWings);
        Assert.Equal(2, effectiveWings[0].SourceCandidateIds.Count);
        Assert.False(Has(result, "trait.fixture.guardian"));
    }

    [Fact]
    public void InputManifestationOrderDoesNotChangeSynergyProof()
    {
        using var fixture = new Fixture();
        var proposal = Selected(fixture, "/traits/0", "/traits/1", "/traits/2");
        var engine = new NativeOriginConstructionEngine(fixture.Catalog);
        var first = engine.Evaluate(new(proposal, AutoManifestDirectDefaults: false));
        var reversed = engine.Evaluate(new(proposal with
        {
            Manifestations = proposal.Manifestations.Reverse().ToArray()
        }, AutoManifestDirectDefaults: false));
        Assert.Equal(first.Status, reversed.Status);
        var left = first.Profile!.Underlying.InherentGrants.Where(x => x.Synergy is not null)
            .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        var right = reversed.Profile!.Underlying.InherentGrants.Where(x => x.Synergy is not null)
            .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        Assert.Equal(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right));
        Assert.Contains(left, x => x.Identity.MechanicalDefinitionId ==
            "trait.fixture.aerial-stability");
    }

    [Fact]
    public void UnimplementedAuthoringConditionsCannotBeSilentlyIgnored()
    {
        const string unsupported = """
            [
              {"id":"unsafe-condition","resultTraitId":"trait.fixture.guardian",
               "requiresPoweredMode":true,
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":1},
                         {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:muscle"}],"count":1}]},
              {"id":"unsafe-selector","resultTraitId":"trait.fixture.response",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings",
                                    "unlessSuppressed":true}],"count":1},
                         {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:muscle"}],"count":1}]}
            ]
            """;
        using var fixture = new Fixture(unsupported);
        var result = Evaluate(fixture, "/traits/2", "/traits/3");
        Assert.Contains(result.Issues,
            x => x.Code == "synergy-authoring-invalid");
        Assert.False(Has(result, "trait.fixture.guardian"));
        Assert.False(Has(result, "trait.fixture.response"));
    }

    [Fact]
    public void ConcurrentRulesForTheSameCanonicalTraitRequireAnExplicitPolicy()
    {
        using var fixture = new Fixture();
        // The native stability interaction and an authenticated table
        // interaction both qualify. Sorting rule IDs is not adjudication.
        var proposal = Selected(fixture, "/traits/0", "/traits/1",
            "/traits/2", "/traits/3") with
        {
            TableDecisions = [TableSynergy(resultId: "trait.fixture.stability")]
        };
        var result = new NativeOriginConstructionEngine(fixture.Catalog).Evaluate(
            new(proposal, new("campaign", "example"),
                AutoManifestDirectDefaults: false), new SyntheticAuthority());

        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "synergy-result-ambiguous");
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.stability" &&
            x.Synergy is not null);
        Assert.False(Has(result, "trait.fixture.aerial-stability"));
    }

    [Fact]
    public void LateCompetingResultRetractsEarlierGrantAndTransitiveDependents()
    {
        const string rules = """
            [
              {"id":"a-initial-stability","resultTraitId":"trait.fixture.stability",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:locomotion"}],"count":1},
                         {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:stabilizer"}],"count":1}]},
              {"id":"b-late-stability","resultTraitId":"trait.fixture.stability",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":1},
                         {"anyOf":[{"mechanicalDefinitionId":"trait.fixture.response"}],"count":1}]},
              {"id":"m-dependent","resultTraitId":"trait.fixture.aerial-stability",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"trait.fixture.stability"}],"count":1},
                         {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":1}]},
              {"id":"z-enable-late","resultTraitId":"trait.fixture.response",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":1},
                         {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:muscle"}],"count":1}]}
            ]
            """;
        using var fixture = new Fixture(rules);
        var result = Evaluate(fixture,
            "/traits/0", "/traits/1", "/traits/2", "/traits/3");

        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "synergy-result-ambiguous");
        Assert.False(Has(result, "trait.fixture.stability"));
        Assert.False(Has(result, "trait.fixture.aerial-stability"));
        // The independent synergy does not rely on the disputed outcome.
        Assert.True(Has(result, "trait.fixture.response"));
    }

    [Fact]
    public void RuleKeysRemainDistinctWhenTableAndInteractionIdsContainColons()
    {
        using var fixture = new Fixture("[]");
        var proposal = Selected(fixture, "/traits/2", "/traits/3") with
        {
            // The old OwnerId + ":" + Id encoding mapped both pairs to
            // "table-decision:a:b:c" and threw while building graph edges.
            TableDecisions =
            [
                TableSynergy("a", "b:c", "b:c", "trait.fixture.guardian"),
                TableSynergy("a:b", "c", "c", "trait.fixture.response")
            ]
        };
        var result = new NativeOriginConstructionEngine(fixture.Catalog).Evaluate(
            new(proposal, new("campaign", "example"),
                AutoManifestDirectDefaults: false), new SyntheticAuthority());

        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.True(Has(result, "trait.fixture.guardian"));
        Assert.True(Has(result, "trait.fixture.response"));
        Assert.Equal(2, result.Profile!.Underlying.InherentGrants.Count(x =>
            x.Synergy is not null));
    }

    [Fact]
    public void NewlineDelimitersCannotAliasStableDerivedGrantIdentities()
    {
        using var fixture = new Fixture("[]");
        var proposal = Selected(fixture, "/traits/2", "/traits/3") with
        {
            // The unhashed tuple pairs would otherwise both become
            // "table-decision:a\nb\nc" after naive newline joining.
            TableDecisions =
            [
                TableSynergy("a", "b\nc", "b\nc", "trait.fixture.guardian"),
                TableSynergy("a\nb", "c", "c", "trait.fixture.response")
            ]
        };
        var result = new NativeOriginConstructionEngine(fixture.Catalog).Evaluate(
            new(proposal, new("campaign", "example"),
                AutoManifestDirectDefaults: false), new SyntheticAuthority());

        Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "synergy-authoring-invalid");
        Assert.False(Has(result, "trait.fixture.guardian"));
        Assert.False(Has(result, "trait.fixture.response"));
    }

    [Fact]
    public void CallerManifestationCannotImpersonateDerivedSynergyIdentity()
    {
        using var fixture = new Fixture();
        var legitimate = Evaluate(fixture, "/traits/0", "/traits/1");
        var syntheticId = legitimate.Profile!.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.stability")
            .ManifestationId;
        Assert.StartsWith("synergy:", syntheticId);

        var proposal = Selected(fixture, "/traits/0", "/traits/1");
        var forged = proposal with
        {
            Manifestations = proposal.Manifestations.Select((item, index) =>
                index == 0 ? item with { Id = syntheticId } : item).ToArray()
        };
        var result = new NativeOriginConstructionEngine(fixture.Catalog).Evaluate(
            new(forged, AutoManifestDirectDefaults: false));

        Assert.Equal(NativeOriginConstructionStatus.Invalid, result.Status);
        Assert.Null(result.Profile);
        Assert.Contains(result.Issues, x =>
            x.Code == "reserved-synergy-manifestation-id");
    }

    [Fact]
    public void ChoiceAndResourceDependentResultsAreNotMintedImplicitly()
    {
        const string unsafeResults = """
            [
              {"id":"unsafe-choice","resultTraitId":"trait.fixture.choice",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":1},
                         {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:muscle"}],"count":1}]},
              {"id":"unsafe-resource","resultTraitId":"trait.fixture.resource",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":1},
                         {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:muscle"}],"count":1}]},
              {"id":"unsafe-select","resultTraitId":"trait.fixture.select",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":1},
                         {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:muscle"}],"count":1}]},
              {"id":"unsafe-feat","resultTraitId":"trait.fixture.feat",
               "inputs":[{"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:wings"}],"count":1},
                         {"anyOf":[{"mechanicalDefinitionId":"species.fixture#trait:muscle"}],"count":1}]}
            ]
            """;
        using var fixture = new Fixture(unsafeResults);
        var result = Evaluate(fixture, "/traits/2", "/traits/3");
        Assert.Contains(result.Issues,
            x => x.Code == "synergy-authoring-invalid");
        Assert.False(Has(result, "trait.fixture.choice"));
        Assert.False(Has(result, "trait.fixture.resource"));
        Assert.False(Has(result, "trait.fixture.select"));
        Assert.False(Has(result, "trait.fixture.feat"));
    }

    [Fact]
    public void SerializationRoundTripPreservesDerivedIdentityAndProof()
    {
        using var fixture = new Fixture();
        var result = Evaluate(fixture, "/traits/0", "/traits/1", "/traits/2");
        var roundTrip = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile))!;
        Assert.Equal(JsonSerializer.Serialize(result.Profile), JsonSerializer.Serialize(roundTrip));
        Assert.All(roundTrip.Underlying.InherentGrants.Where(x => x.Synergy is not null),
            x => Assert.All(x.Synergy!.Inputs,
                input => Assert.NotEmpty(input.SourceCandidateIds)));
    }
}
