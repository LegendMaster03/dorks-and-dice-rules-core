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
                  {"id":"trait.fixture.loop-b","kind":"origin-trait","name":"Loop B","trait":{"id":"loop-b","name":"Loop B","systemTag":"loop-b"}}
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
        var full = Evaluate(fixture, "/traits/0", "/traits/1");
        Assert.True(Has(full, "trait.fixture.stability"));
        var grant = full.Profile!.Underlying.InherentGrants.Single(x =>
            x.Identity.MechanicalDefinitionId == "trait.fixture.stability");
        Assert.Equal("rule.fixture", grant.Synergy!.RuleId);
        Assert.Equal(2, grant.Synergy.Inputs.Count);
        Assert.Equal(2, grant.SourceCandidateIds.Count);
        Assert.All(grant.Synergy.Inputs, evidence =>
            Assert.Equal("species.fixture", evidence.Identity.Occurrence.Source.Id));

        var repeated = Evaluate(fixture, "/traits/1", "/traits/0");
        Assert.Equal(JsonSerializer.Serialize(full.Profile.Underlying.InherentGrants),
            JsonSerializer.Serialize(repeated.Profile!.Underlying.InherentGrants));

        var missing = Evaluate(fixture, "/traits/0");
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
