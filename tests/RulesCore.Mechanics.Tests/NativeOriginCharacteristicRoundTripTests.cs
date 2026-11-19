using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Source-to-candidate round trips, not claims that unresolved creation choices
/// or unapproved Species interactions yield finished playable characters.
/// </summary>
public sealed class NativeOriginCharacteristicRoundTripTests
{
    private static readonly NativeOriginMethodReference Biological =
        new("rule.creature-origination", "biological");

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
        throw new InvalidOperationException("Native first-party catalog is missing.");
    }

    private static NativeOriginProposal Direct(NativeRuleEntity source) =>
        new("roundtrip:" + source.Id, NativeOriginIntent.Direct, Biological, [], [], [],
            SelectedSpeciesId: source.Category == "species" ? source.Id : null,
            SelectedAncestryId: source.Category == "ancestry" ? source.Id : null);

    private static bool Selectable(NativeRuleEntity source) =>
        !source.Definition.TryGetProperty("selectable", out var choice) ||
        choice.ValueKind != JsonValueKind.False;

    private static IReadOnlyList<NativeRuleEntity> Lineage(
        NativeCatalogSnapshot catalog, NativeRuleEntity selected)
    {
        var chain = new List<NativeRuleEntity>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        NativeRuleEntity current = selected;
        while (true)
        {
            Assert.True(seen.Add(current.Id), "Cycle in catalog lineage: " + selected.Id);
            chain.Add(current);
            if (current.Category == "species") break;
            var parentId = current.Definition.GetProperty("parent").GetString()!;
            Assert.True(catalog.TryGetById(parentId, out var parent) && parent is not null,
                "Missing catalog parent: " + parentId);
            current = parent!;
        }
        chain.Reverse();
        return chain;
    }

    private static IReadOnlyList<NativeRuleEntity> NativeSelections(
        NativeCatalogSnapshot catalog, bool selectableOnly)
    {
        var species = catalog.Search(category: "species", limit: 100).Items;
        var ancestries = catalog.Search(category: "ancestry", limit: 100).Items;
        // Keep the nine conventional roots in the inventory, including roots
        // that are available only via a selectable descendant Ancestry.
        Assert.Equal(9, species.Length);
        Assert.Equal(34, ancestries.Length);
        return species.Concat(ancestries)
            .Where(x => !selectableOnly || Selectable(x))
            .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void AllNineSpeciesAndAllNativeAncestryNodesRetainEveryTypedOccurrence()
    {
        var catalog = Catalog();
        var deconstructor = new NativeOriginSourceDeconstructor(catalog);
        foreach (var selected in NativeSelections(catalog, selectableOnly: false))
        {
            // Unselectable intermediate ancestry families are still authored
            // source nodes. Inspect them without declaring them playable.
            var proposal = Direct(selected);
            var (deconstructed, issues) = deconstructor.Deconstruct(proposal);
            var (repeated, repeatedIssues) = deconstructor.Deconstruct(proposal);
            Assert.Equal(JsonSerializer.Serialize(deconstructed.Candidates),
                JsonSerializer.Serialize(repeated.Candidates));
            Assert.Equal(JsonSerializer.Serialize(issues),
                JsonSerializer.Serialize(repeatedIssues));

            var lineage = Lineage(catalog, selected);
            foreach (var entity in lineage)
            {
                var candidates = deconstructed.Candidates.Where(candidate =>
                    candidate.Identity.Occurrence.Source.Id == entity.Id).ToArray();
                var expected = new List<string>();
                foreach (var property in new[]
                {
                    "creatureType", "size", "walkingSpeedFeet", "darkvisionFeet"
                })
                    if (entity.Definition.TryGetProperty(property, out var value) &&
                        value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                        expected.Add("/" + property);
                if (entity.Definition.TryGetProperty("sizeChoice", out var sizeChoice) &&
                    sizeChoice.ValueKind == JsonValueKind.Object &&
                    sizeChoice.TryGetProperty("from", out var sizes) &&
                    sizes.ValueKind == JsonValueKind.Array)
                    expected.Add("/sizeChoice");
                if (entity.Definition.TryGetProperty("spellcastingAbilityChoice", out var ability) &&
                    ability.ValueKind == JsonValueKind.Array)
                    expected.Add("/spellcastingAbilityChoice");
                if (entity.Definition.TryGetProperty("traits", out var traits) &&
                    traits.ValueKind == JsonValueKind.Array)
                    for (var index = 0; index < traits.GetArrayLength(); index++)
                        expected.Add("/traits/" + index);
                Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
                    candidates.Select(x => x.Identity.Occurrence.OccurrencePath)
                        .OrderBy(x => x, StringComparer.Ordinal));

                foreach (var candidate in candidates)
                {
                    Assert.Equal(NativeOriginSourceKind.Native,
                        candidate.Identity.Occurrence.Source.Kind);
                    var path = candidate.Identity.Occurrence.OccurrencePath;
                    if (path.StartsWith("/traits/", StringComparison.Ordinal))
                    {
                        var index = int.Parse(path["/traits/".Length..]);
                        var authored = entity.Definition.GetProperty("traits")[index];
                        Assert.Equal(NativeOriginCharacteristicKind.Trait, candidate.Identity.Kind);
                        Assert.True(candidate.OfferedValue is { } value &&
                            JsonElement.DeepEquals(authored, value),
                            "Trait payload changed: " + selected.Id + " " + entity.Id + path);
                        var canonical = catalog.OriginTraitDefinitionId(entity.Id, index);
                        if (canonical is not null)
                            Assert.Equal(canonical, candidate.Identity.MechanicalDefinitionId);
                    }
                    else if (candidate.OfferedValue is { } value)
                    {
                        Assert.True(JsonElement.DeepEquals(
                            entity.Definition.GetProperty(path[1..]), value),
                            "Intrinsic value changed: " + selected.Id + " " + entity.Id + path);
                    }
                    else
                    {
                        Assert.NotEmpty(candidate.RequiredChoices ?? []);
                    }
                }
            }
            if (selected.Category == "ancestry" && Selectable(selected))
                Assert.DoesNotContain(issues, x =>
                    x.Code is "ancestry-parent-cycle" or "ancestry-parent-unavailable");
        }
    }

    [Fact]
    public void EverySelectableConventionalConfigurationReconstructsDeterministicSourceGrants()
    {
        var catalog = Catalog();
        var construction = new NativeOriginConstructionEngine(catalog);
        var selected = NativeSelections(catalog, selectableOnly: true);
        // Includes ordinary standalone Species and selectable leaf Ancestries.
        Assert.Equal(32, selected.Count);
        foreach (var entity in selected)
        {
            var request = new NativeOriginConstructionRequest(Direct(entity));
            var first = construction.Evaluate(request);
            var second = construction.Evaluate(request);
            Assert.NotEqual(NativeOriginConstructionStatus.Invalid, first.Status);
            Assert.NotNull(first.Profile);
            Assert.Equal(JsonSerializer.Serialize(first.EvaluatedProposal),
                JsonSerializer.Serialize(second.EvaluatedProposal));
            Assert.Equal(JsonSerializer.Serialize(first.Profile),
                JsonSerializer.Serialize(second.Profile));
            Assert.Equal(JsonSerializer.Serialize(first.Issues),
                JsonSerializer.Serialize(second.Issues));
            Assert.Equal(first.Profile!.Underlying, first.Profile.Current);

            foreach (var grant in first.Profile.Underlying.InherentGrants)
            {
                // A grant may consolidate several occurrences; every source
                // must remain traceable to an actually discovered candidate.
                Assert.NotEmpty(grant.SourceCandidateIds);
                foreach (var id in grant.SourceCandidateIds)
                {
                    var source = Assert.Single(first.EvaluatedProposal.Candidates
                        .Where(candidate => candidate.Id == id));
                    Assert.Equal(NativeOriginSourceKind.Native,
                        source.Identity.Occurrence.Source.Kind);
                }
                // Complete raw trait definitions include nested effects,
                // parameters, resource and level-progression authoring.
                var matchingSource = first.EvaluatedProposal.Candidates.FirstOrDefault(x =>
                    x.Identity == grant.Identity &&
                    grant.SourceCandidateIds.Contains(x.Id, StringComparer.Ordinal));
                if (matchingSource?.Identity.Kind == NativeOriginCharacteristicKind.Trait &&
                    matchingSource.OfferedValue is { } offered &&
                    grant.EffectiveValue is { } effective)
                    Assert.True(JsonElement.DeepEquals(offered, effective),
                        "Selected trait payload changed: " + entity.Id + " " + grant.Id);
            }
            // A missing player choice is not completed by this losslessness test.
            Assert.DoesNotContain(first.Profile.Underlying.InherentGrants, grant =>
                grant.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                grant.EffectiveValue is null);
        }
    }

    [Fact]
    public void UnavailableParentIsUnresolvedButNotAnInventedCycle()
    {
        using var fixture = new SyntheticCatalog("species.fixture.missing");
        var deconstructor = new NativeOriginSourceDeconstructor(fixture.Catalog);
        var leaf = Direct(fixture.Entity("ancestry.fixture.leaf"));
        var directIssues = deconstructor.Deconstruct(leaf).Issues;
        Assert.Contains(directIssues, x => x.Code == "ancestry-parent-unavailable");
        Assert.DoesNotContain(directIssues, x => x.Code == "ancestry-parent-cycle");

        var mixed = new NativeOriginProposal("mixed", NativeOriginIntent.Mixed, Biological,
            [new NativeOriginContributor("donor", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, "ancestry.fixture.leaf"))], [], []);
        var contributorIssues = deconstructor.Deconstruct(mixed).Issues;
        Assert.Contains(contributorIssues, x => x.Code == "contributor-lineage-unavailable");
        Assert.DoesNotContain(contributorIssues, x => x.Code == "contributor-lineage-cycle");
    }

    [Fact]
    public void GenuineParentCyclesRemainInvalidForDirectAndContributorPaths()
    {
        using var fixture = new SyntheticCatalog("ancestry.fixture.leaf");
        var deconstructor = new NativeOriginSourceDeconstructor(fixture.Catalog);
        var directIssues = deconstructor.Deconstruct(
            Direct(fixture.Entity("ancestry.fixture.leaf"))).Issues;
        Assert.Contains(directIssues, x => x.Code == "ancestry-parent-cycle" &&
            x.Severity == NativeOriginIssueSeverity.Invalid);

        var mixed = new NativeOriginProposal("mixed", NativeOriginIntent.Mixed, Biological,
            [new NativeOriginContributor("donor", new(NativeOriginInputKind.BiologicalParent),
                new(NativeOriginSourceKind.Native, "ancestry.fixture.leaf"))], [], []);
        var contributorIssues = deconstructor.Deconstruct(mixed).Issues;
        Assert.Contains(contributorIssues, x => x.Code == "contributor-lineage-cycle" &&
            x.Severity == NativeOriginIssueSeverity.Invalid);
    }

    [Fact]
    public void MalformedHomebrewBindingsAndCountsRemainVisibleButCanNotCreateGrants()
    {
        using var fixture = new SyntheticCatalog("species.fixture");
        var deconstructor = new NativeOriginSourceDeconstructor(fixture.Catalog);
        var (proposal, issues) = deconstructor.Deconstruct(
            Direct(fixture.Entity("species.fixture")));
        Assert.Empty(issues);
        var candidate = Assert.Single(proposal.Candidates.Where(x =>
            x.Identity.Occurrence.OccurrencePath == "/traits/0"));
        var raw = fixture.Entity("species.fixture").Definition.GetProperty("traits")[0];
        Assert.True(JsonElement.DeepEquals(raw, candidate.OfferedValue!.Value));
        Assert.True(JsonElement.DeepEquals(raw.GetProperty("engineData"),
            candidate.OfferedValue.Value.GetProperty("engineData")));
        Assert.Contains(candidate.UnresolvedEvidence!, x => x.Contains("originBindings",
            StringComparison.Ordinal));
        Assert.Contains(candidate.UnresolvedEvidence!, x => x.Contains("choiceCount",
            StringComparison.Ordinal));
        var result = new NativeOriginConstructionEngine(fixture.Catalog)
            .Evaluate(new(Direct(fixture.Entity("species.fixture"))));
        Assert.Contains(result.Issues, x => x.Code == "unsupported-source-binding");
        Assert.DoesNotContain(result.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.Occurrence.OccurrencePath == "/traits/0");
    }

    [Fact]
    public void UnfamiliarCustomCharacteristicRetainsRawPayloadWithoutGrantingEligibility()
    {
        using var fixture = new SyntheticCatalog("species.fixture");
        var value = JsonSerializer.SerializeToElement(new
        {
            unknownEffect = new { steps = new[] { 2, 4, 8 } },
            levelEffects = new[] { new { level = 7, benefit = "custom" } }
        });
        var identity = new NativeOriginCharacteristicIdentity(
            NativeOriginCharacteristicKind.Other,
            new(new(NativeOriginSourceKind.Custom, "homebrew.origin"), "/mechanics/new"),
            "custom.unknown-mechanic");
        var candidate = new NativeOriginCandidate("homebrew", identity,
            new(NativeOriginEligibility.Eligible, "untrusted-claim"),
            OfferedValue: value,
            OfferedParameters: new Dictionary<string, JsonElement>
            {
                ["charges"] = JsonSerializer.SerializeToElement(new { daily = 3 })
            });
        var origin = new NativeOriginProposal("custom", NativeOriginIntent.Custom, Biological,
            [], [candidate], [new("pick", candidate.Id)]);
        var result = new NativeOriginSourceDeconstructor(fixture.Catalog).Deconstruct(origin);
        Assert.Empty(result.Issues);
        var preserved = Assert.Single(result.Proposal.Candidates);
        Assert.Equal(NativeOriginEligibility.Unreviewed, preserved.SelectionBasis.Eligibility);
        Assert.True(JsonElement.DeepEquals(value, preserved.OfferedValue!.Value));
        Assert.True(JsonElement.DeepEquals(candidate.OfferedParameters!["charges"],
            preserved.OfferedParameters!["charges"]));
    }

    private sealed class SyntheticCatalog : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "rc-origin-roundtrip-" + Guid.NewGuid().ToString("N"));

        public NativeCatalogSnapshot Catalog { get; }

        public SyntheticCatalog(string familyParent)
        {
            var package = Path.Combine(_root, "first-party-001");
            var native = Path.Combine(package, "native");
            Directory.CreateDirectory(native);
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(native, "species.json"),
                """
                {"id":"species.fixture","name":"Fixture","kind":"species",
                 "selectable":true,"creatureType":"humanoid","size":"medium",
                 "walkingSpeedFeet":30,
                 "traits":[{"id":"opaque","name":"Opaque",
                   "engineData":{"nested":[1,{"unknown":true}]},
                   "choiceCount":"2",
                   "originBindings":[{"sourceField":"x"}]}]}
                """);
            File.WriteAllText(Path.Combine(native, "family.json"),
                JsonSerializer.Serialize(new
                {
                    id = "ancestry.fixture.mid", name = "Mid", kind = "ancestry",
                    parent = familyParent
                }));
            File.WriteAllText(Path.Combine(native, "leaf.json"),
                """
                {"id":"ancestry.fixture.leaf","name":"Leaf","kind":"ancestry",
                 "parent":"ancestry.fixture.mid","selectable":true}
                """);
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }

        public NativeRuleEntity Entity(string id)
        {
            Assert.True(Catalog.TryGetById(id, out var entity));
            return entity!;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
