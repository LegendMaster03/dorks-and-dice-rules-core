using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Synthetic authored interactions with real first-party biological gene offers.
/// No numerical cost or playable combination is introduced into native content.
/// </summary>
public sealed class NativeOriginInheritedCombinationTests
{
    private const string ScopeId = "inherited-combination-test";

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
        throw new InvalidOperationException("First-party native ruleset missing.");
    }

    private static NativeCatalogSnapshot CatalogWithAdditionalDefinition(
        DirectoryInfo temporary, string mechanics)
    {
        var source = new DirectoryInfo(AppContext.BaseDirectory);
        while (source is not null && !Directory.Exists(Path.Combine(source.FullName, "data", "rulesets")))
            source = source.Parent;
        if (source is null)
            throw new InvalidOperationException("First-party native ruleset missing.");
        var previous = Path.Combine(source.FullName, "data", "rulesets", "first-party-001");
        var package = Directory.CreateDirectory(Path.Combine(temporary.FullName, "first-party-001"));
        File.Copy(Path.Combine(previous, "manifest.json"), Path.Combine(package.FullName, "manifest.json"));
        foreach (var file in Directory.EnumerateFiles(Path.Combine(previous, "native"), "*.json",
                     SearchOption.AllDirectories))
        {
            var destination = Path.Combine(package.FullName, Path.GetRelativePath(previous, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
        File.WriteAllText(Path.Combine(package.FullName, "native", "unfamiliar.json"), mechanics);
        return NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(temporary.FullName, "first-party-001"));
    }

    private sealed class Authority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision, NativeTableDecisionScope scope) =>
            scope == new NativeTableDecisionScope("campaign", ScopeId) &&
            decision.Scope == scope && decision.Authority?.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "approved";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId, string evidenceId) =>
            false;
    }

    private sealed class ParentAuthority(NativeOriginProfile parent)
        : INativeOriginManifestedContributorAuthority
    {
        public NativeOriginVerifiedManifestedContributor? ResolveCurrent(
            NativeOriginSourceReference source, NativeTableDecisionScope scope) =>
            scope == new NativeTableDecisionScope("campaign", ScopeId) &&
            source.Id == parent.Id && source.Version == "revision-1"
                ? new(parent, "revision-1") : null;
    }

    private static NativeOriginProposal Proposal(int parents = 2) =>
        new("combine-" + parents, NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            Enumerable.Range(0, parents).Select(i =>
                new NativeOriginContributor("parent-" + i,
                    new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.orc"))).ToArray(),
            [], []);

    private static NativeOriginConstructionResult Evaluate(
        NativeCatalogSnapshot catalog, NativeOriginProposal proposal,
        INativeOriginAuthority? rules = null,
        INativeOriginManifestedContributorAuthority? parents = null) =>
        new NativeOriginConstructionEngine(catalog).Evaluate(
            new(proposal, new("campaign", ScopeId), AutoManifestDirectDefaults: false),
            rules, parents);

    private static NativeOriginManifestation Express(NativeOriginCandidate candidate,
        int position) =>
        new("express-" + position, candidate.Id,
            SelectedValue: candidate.Identity.Kind ==
                NativeOriginCharacteristicKind.Intrinsic
                ? candidate.OfferedValue : null);

    private static NativeOriginCandidate[] Foundations(
        IEnumerable<NativeOriginCandidate> offers, string owner) =>
        offers.Where(x => x.ContributorId == owner &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();

    private static NativeTableRuleDecision Rule(string decisionId = "rule-double-power",
        string interactionId = "double-power", string result = "trait.amphibious",
        object? selector = null)
    {
        var input = selector ?? new { mechanicalDefinitionId = "trait.powerful-build" };
        return new NativeTableRuleDecision(decisionId,
            new("dm", "approved"), new("campaign", ScopeId),
            new("origin-synergy", interactionId),
            JsonSerializer.SerializeToElement(new
            {
                id = interactionId, inputEvidence = "inherited-gene",
                resultTraitId = result,
                inputs = new[] { new { anyOf = new[] { input }, count = 2 } }
            }));
    }

    private static (NativeOriginProposal Proposal, NativeOriginCandidate[] Genes)
        Choose(NativeCatalogSnapshot catalog, bool directExpression = false,
            IReadOnlyList<NativeTableRuleDecision>? tableRules = null)
    {
        var proposal = Proposal();
        var offers = Evaluate(catalog, proposal).EvaluatedProposal.Candidates;
        var foundation = Foundations(offers, "parent-0");
        Assert.Equal(3, foundation.Length);
        var genes = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build")
            .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        Assert.Equal(2, genes.Length);
        var manifests = directExpression ? foundation.Append(genes[0]).ToArray() : foundation;
        return (proposal with
        {
            Manifestations = manifests.Select(Express).ToArray(),
            BiologicalTransmission = new(
                foundation.Select(x => x.Id).Concat(genes.Select(x => x.Id)).ToArray(),
                Combinations: [new("table-decision:rule-double-power",
                    "double-power", genes.Select(x => x.Id).ToArray())]),
            TableDecisions = tableRules
        }, genes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitTwoGeneExpressionReplacesRatherThanStacks(bool directExpression)
    {
        var catalog = Catalog();
        var (proposal, genes) = Choose(catalog, directExpression, [Rule()]);
        var result = Evaluate(catalog, proposal, new Authority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Empty(result.Issues);

        var grants = result.Profile!.Underlying.InherentGrants;
        var combined = Assert.Single(grants, g =>
            g.Identity.MechanicalDefinitionId == "trait.amphibious");
        Assert.NotNull(combined.Synergy);
        Assert.Empty(combined.Synergy.Inputs);
        Assert.Equal(2, combined.Synergy.InheritedInputs!.Count);
        Assert.Equal(genes.Select(x => x.Id).OrderBy(x => x),
            combined.SourceCandidateIds.OrderBy(x => x));
        Assert.DoesNotContain(grants, g =>
            g.Identity.MechanicalDefinitionId == "trait.powerful-build");
        Assert.Null(combined.ResourceOwners);
        Assert.Equal(2, result.Profile.Underlying.BiologicalGenes!.Count(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build"));
        Assert.All(result.BiologicalCombination!.InheritedInstances!
            .Where(x => x.Gene.Identity.MechanicalDefinitionId == "trait.powerful-build"),
            x => Assert.Equal(new[] { combined.Id }, x.ExpressionGrantIds));

        var repeat = Evaluate(catalog, result.EvaluatedProposal, new Authority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, repeat.Status);
        Assert.Equal(JsonSerializer.Serialize(combined),
            JsonSerializer.Serialize(Assert.Single(repeat.Profile!.Underlying.InherentGrants,
                g => g.Synergy?.InheritedInputs is { Count: 2 })));
    }

    [Fact]
    public void NativeInteractionDiscoveryUsesOnlyDistinctConfiguredInheritedInputs()
    {
        var temporary = Directory.CreateTempSubdirectory("origin-native-interaction-");
        try
        {
            const string owner = "rule.fixture.inherited-interaction";
            var catalog = CatalogWithAdditionalDefinition(temporary, JsonSerializer.Serialize(new
            {
                id = owner, kind = "rule", name = "Test-only inherited interaction",
                originSynergies = new[] { new
                {
                    id = "double-power", inputEvidence = "inherited-gene",
                    resultTraitId = "trait.amphibious",
                    inputs = new[] { new
                    {
                        anyOf = new[] { new { mechanicalDefinitionId = "trait.powerful-build" } },
                        count = 2
                    } }
                } }
            }));
            var (proposal, genes) = Choose(catalog);
            var latent = proposal with
            {
                BiologicalTransmission = proposal.BiologicalTransmission! with { Combinations = null }
            };
            var one = latent with
            {
                BiologicalTransmission = latent.BiologicalTransmission! with
                {
                    CandidateIds = latent.BiologicalTransmission.CandidateIds
                        .Where(id => id != genes[0].Id).ToArray()
                }
            };
            Assert.Empty(Evaluate(catalog, one).BiologicalCombination!.InteractionOptions!);
            var discovered = Evaluate(catalog, latent);
            var option = Assert.Single(discovered.BiologicalCombination!.InteractionOptions!);
            Assert.Equal(owner, option.RuleOwnerId);
            Assert.Equal("double-power", option.InteractionId);
            Assert.Equal("trait.amphibious", option.ResultTraitId);
            Assert.Equal(2, Assert.Single(option.Inputs).Count);
            Assert.Equal(genes.Select(g => g.Id).OrderBy(id => id, StringComparer.Ordinal),
                option.Inputs[0].CandidateIds);
            Assert.DoesNotContain(discovered.Profile!.Underlying.InherentGrants,
                g => g.Identity.MechanicalDefinitionId == "trait.amphibious");
            var selected = latent with
            {
                BiologicalTransmission = latent.BiologicalTransmission! with
                {
                    Combinations = [new(option.RuleOwnerId, option.InteractionId, option.Inputs[0].CandidateIds)]
                }
            };
            var expressed = Evaluate(catalog, selected);
            Assert.True(expressed.IsMechanicallyAuthorized,
                string.Join("; ", expressed.Issues.Select(i => i.Code + ": " + i.Detail)));
            Assert.Single(expressed.Profile!.Underlying.InherentGrants, g =>
                g.Identity.MechanicalDefinitionId == "trait.amphibious");
            // The ordinary authored discovery never includes an unauthenticated
            // or even scope-verified table-only interaction.
            var tableOnly = Evaluate(Catalog(), latent with { TableDecisions = [Rule()] }, new Authority());
            Assert.Empty(tableOnly.BiologicalCombination!.InteractionOptions!);
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    [Fact]
    public void CombinedExpressionMustSatisfyAnAuthenticatedFinalParameterLink()
    {
        var catalog = Catalog();
        var combination = Rule();
        var (proposal, _) = Choose(catalog, tableRules: [combination]);
        var constraint = new NativeOriginConstraint("linked-combination",
            NativeOriginRelationshipKind.LinkedParameters,
            [new(MechanicalDefinitionId: "trait.amphibious")],
            [new(MechanicalDefinitionId: "trait.ancestral-aptitude")],
            LinkedParameterPaths: new Dictionary<string, string> { ["variant"] = "variant" },
            TableDecisionId: "linked-combination-decision");
        var ruling = new NativeTableRuleDecision(constraint.TableDecisionId!,
            new("dm", "approved"), new("campaign", ScopeId), new("constraint", constraint.Id),
            JsonSerializer.SerializeToElement(new { enforce = true }));

        var result = Evaluate(catalog, proposal with
        {
            Constraints = [constraint],
            TableDecisions = [combination, ruling]
        }, new Authority());

        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "effective-linked-parameter-counterpart-missing");
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants,
            grant => grant.Identity.MechanicalDefinitionId == "trait.amphibious");
        Assert.All(result.BiologicalCombination!.InheritedInstances!.Where(
            x => x.Gene.Identity.MechanicalDefinitionId == "trait.powerful-build"),
            x => Assert.True(x.IsLatent));
    }

    [Theory]
    [InlineData("""{"exampleWaterTopologyEffect":{"rangeFeet":20}}""")]
    [InlineData("""{"damage":{"dice":"1d6","exampleWaterTopologyEffect":true}}""")]
    public void AnUnfamiliarCanonicalEffectCannotBeAnInheritedExpression(string trait)
    {
        const string resultId = "trait.fixture.unfamiliar";
        var temporary = Directory.CreateTempSubdirectory("origin-unfamiliar-");
        try
        {
            using var payload = JsonDocument.Parse(trait);
            var catalog = CatalogWithAdditionalDefinition(temporary, JsonSerializer.Serialize(new
            {
                id = resultId, kind = "origin-trait", name = "Unfamiliar Effect",
                trait = payload.RootElement
            }));
            var (proposal, _) = Choose(catalog, tableRules: [Rule(result: resultId)]);
            var pricedKeys = new[] { "intrinsic:creatureType", "intrinsic:size",
                "intrinsic:walkingSpeedFeet", "trait:" + resultId };
            var policy = new NativeOriginAllocationPolicy("unfamiliar-fixture",
                "rule.creature-origination", "biological", 4,
                pricedKeys.Select(key => new NativeOriginAllocationPrice(key, 1, [key])).ToArray());
            var result = new NativeOriginConstructionEngine(catalog).Evaluate(new(proposal,
                new("campaign", ScopeId), AutoManifestDirectDefaults: false,
                RequireCreationAllocation: true, CreationAllocationPolicy: policy),
                new Authority());
            Assert.Equal(NativeOriginConstructionStatus.Unresolved, result.Status);
            Assert.Equal("unresolved", result.CreationAllocation!.Status);
            Assert.Contains(result.Issues, x => x.Code == "synergy-authoring-invalid");
            Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants,
                grant => grant.Identity.MechanicalDefinitionId == resultId);
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    [Fact]
    public void AnAuthoredSameCanonicalKindReplacesTheOriginalGrant()
    {
        var catalog = Catalog();
        var (proposal, _) = Choose(catalog, directExpression: true,
            tableRules: [Rule(result: "trait.powerful-build")]);
        var result = Evaluate(catalog, proposal, new Authority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var effective = Assert.Single(result.Profile!.Underlying.InherentGrants, g =>
            g.Identity.MechanicalDefinitionId == "trait.powerful-build");
        Assert.Equal(2, effective.Synergy!.InheritedInputs!.Count);
        Assert.DoesNotContain(result.Profile.Underlying.InherentGrants, g =>
            g.Identity.MechanicalDefinitionId == "trait.amphibious");
    }

    [Fact]
    public void MerelyOfferedOrTransmittedGenesDoNotTriggerAnAuthoredRule()
    {
        var catalog = Catalog();
        var (proposal, _) = Choose(catalog, tableRules: [Rule()]);
        var noSelection = proposal with
        {
            BiologicalTransmission = proposal.BiologicalTransmission! with
            {
                Combinations = null
            }
        };
        var result = Evaluate(catalog, noSelection, new Authority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, g =>
            g.Identity.MechanicalDefinitionId == "trait.amphibious");
        Assert.All(result.BiologicalCombination!.InheritedInstances!
            .Where(x => x.Gene.Identity.MechanicalDefinitionId == "trait.powerful-build"),
            x => Assert.True(x.IsLatent));
    }

    [Fact]
    public void UnverifiedRulesAndFabricatedInstanceIdsCannotAuthorizeAnExpression()
    {
        var catalog = Catalog();
        var (proposal, genes) = Choose(catalog, tableRules: [Rule()]);
        var unverified = Evaluate(catalog, proposal);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, unverified.Status);
        Assert.True(unverified.Profile is null || !unverified.Profile.Underlying.InherentGrants
            .Any(g => g.Identity.MechanicalDefinitionId == "trait.amphibious"));

        var missing = Evaluate(catalog, proposal with { TableDecisions = [] },
            new Authority());
        Assert.DoesNotContain(missing.Profile!.Underlying.InherentGrants, g =>
            g.Identity.MechanicalDefinitionId == "trait.amphibious");

        var forged = Evaluate(catalog, proposal with
        {
            BiologicalTransmission = proposal.BiologicalTransmission! with
            {
                Combinations =
                [
                    new("table-decision:rule-double-power", "double-power",
                        [genes[0].Id, "fake-inherited-gene"])
                ]
            }
        }, new Authority());
        Assert.Contains(forged.Issues, x =>
            x.Code == "inherited-combination-instance-unverified");
        Assert.True(forged.Profile is null || !forged.Profile.Underlying.InherentGrants
            .Any(g => g.Identity.MechanicalDefinitionId == "trait.amphibious"));
    }

    [Fact]
    public void ConfigurationSelectorsMustMatchAllDistinctInstances()
    {
        var catalog = Catalog();
        var selector = new
        {
            mechanicalDefinitionId = "trait.powerful-build",
            parameterKey = "variant", expectedValue = "armored"
        };
        var (proposal, _) = Choose(catalog, tableRules: [Rule(selector: selector)]);
        var result = Evaluate(catalog, proposal, new Authority());
        Assert.Contains(result.Issues, x =>
            x.Code == "inherited-combination-input-mismatch");
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, g =>
            g.Identity.MechanicalDefinitionId == "trait.amphibious");
    }

    [Fact]
    public void CompetingAndRepeatedInteractionsFailClosed()
    {
        var catalog = Catalog();
        var other = Rule("rule-double-power-two", "double-power-two");
        var (proposal, genes) = Choose(catalog, tableRules: [Rule(), other]);
        var duplicate = Evaluate(catalog, proposal with
        {
            BiologicalTransmission = proposal.BiologicalTransmission! with
            {
                Combinations =
                [
                    new("table-decision:rule-double-power", "double-power",
                        [genes[0].Id, genes[0].Id])
                ]
            }
        }, new Authority());
        Assert.Contains(duplicate.Issues, x =>
            x.Code == "inherited-combination-selection-invalid");

        var competing = Evaluate(catalog, proposal with
        {
            BiologicalTransmission = proposal.BiologicalTransmission! with
            {
                Combinations =
                [
                    new("table-decision:rule-double-power", "double-power",
                        genes.Select(x => x.Id).ToArray()),
                    new("table-decision:rule-double-power-two", "double-power-two",
                        genes.Select(x => x.Id).ToArray())
                ]
            }
        }, new Authority());
        Assert.Contains(competing.Issues, x =>
            x.Code == "inherited-combination-conflict");
        Assert.True(competing.Profile is null || !competing.Profile.Underlying.InherentGrants
            .Any(g => g.Identity.MechanicalDefinitionId == "trait.amphibious"));
    }

    [Fact]
    public void ForgedStoredGenotypeCannotSatisfyReplayedSynergyEvidence()
    {
        var catalog = Catalog();
        var rule = Rule();
        var (proposal, _) = Choose(catalog, tableRules: [rule]);
        var result = Evaluate(catalog, proposal, new Authority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var original = result.Profile!;
        var savedGenes = original.Underlying.BiologicalGenes!.ToArray();
        var index = Array.FindIndex(savedGenes, gene =>
            gene.Identity.MechanicalDefinitionId == "trait.powerful-build");
        Assert.True(index >= 0);
        savedGenes[index] = savedGenes[index] with { Id = "forged-gene-instance" };
        var underlying = original.Underlying with { BiologicalGenes = savedGenes };
        var tampered = original with { Underlying = underlying, Current = underlying };

        var review = new NativeOriginContractValidator(catalog).ReviewProfileVerified(
            tampered, result.EvaluatedProposal, [rule], 1);
        Assert.Contains(review.Issues, x => x.Code == "synergy-provenance-invalid");
    }

    [Fact]
    public void VerifiedMixedHeritageParentSuppliesExactInheritedInstances()
    {
        var catalog = Catalog();
        var original = Proposal();
        var offers = Evaluate(catalog, original).EvaluatedProposal.Candidates;
        var foundation = Foundations(offers, "parent-0");
        var powers = offers.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build").ToArray();
        var parent = Evaluate(catalog, original with
        {
            Manifestations = foundation.Append(powers[0]).Select(Express).ToArray(),
            BiologicalTransmission = new(
                foundation.Select(x => x.Id).Concat(powers.Select(x => x.Id)).ToArray())
        });
        Assert.Equal(NativeOriginConstructionStatus.Authorized, parent.Status);
        var donor = new NativeOriginContributor("carrier",
            new(NativeOriginInputKind.BiologicalParent),
            new(NativeOriginSourceKind.ManifestedProfile, parent.Profile!.Id, "revision-1"));
        var proposal = new NativeOriginProposal("descendant-combination",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"), [donor], [], []);
        var discovered = Evaluate(catalog, proposal, parents: new ParentAuthority(parent.Profile))
            .EvaluatedProposal.Candidates;
        var carry = discovered.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.powerful-build").ToArray();
        Assert.Equal(2, carry.Length);
        var foundations = Foundations(discovered, "carrier");
        var result = Evaluate(catalog, proposal with
        {
            Manifestations = foundations.Select(Express).ToArray(),
            BiologicalTransmission = new(
                foundations.Select(x => x.Id).Concat(carry.Select(x => x.Id)).ToArray(),
                Combinations: [new("table-decision:rule-double-power", "double-power",
                    carry.Select(x => x.Id).ToArray())]),
            TableDecisions = [Rule()]
        }, new Authority(), new ParentAuthority(parent.Profile));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var combined = Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.amphibious");
        Assert.All(combined.Synergy!.InheritedInputs!, evidence =>
        {
            Assert.Equal("carrier", evidence.Gene.InheritedFrom!.ContributorId);
            Assert.Equal(parent.Profile.Id,
                evidence.Gene.InheritedFrom.ContributorProfileId);
        });

        var unverified = Evaluate(catalog, result.EvaluatedProposal, new Authority());
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, unverified.Status);
        Assert.True(unverified.Profile is null || !unverified.Profile.Underlying.InherentGrants
            .Any(g => g.Identity.MechanicalDefinitionId == "trait.amphibious"));
    }

    [Fact]
    public void InheritedCombinationUsesConfiguredCreationPriceWithoutChargingLatentInputs()
    {
        var catalog = Catalog();
        var (proposal, _) = Choose(catalog, tableRules: [Rule()]);
        var baseline = Evaluate(catalog, proposal, new Authority());
        Assert.Equal(NativeOriginConstructionStatus.Authorized, baseline.Status);
        var effective = baseline.Profile!.Underlying.InherentGrants;
        var combined = Assert.Single(effective, grant =>
            grant.Identity.MechanicalDefinitionId == "trait.amphibious");
        Assert.NotNull(combined.EffectiveValue);
        Assert.DoesNotContain(effective, grant =>
            grant.Identity.MechanicalDefinitionId == "trait.powerful-build");

        // All values are synthetic accounting fixtures, not gameplay prices.
        // Price the single *result* grant; neither inherited source instance
        // receives a separate charge when it is latent or consumed.
        const string combinedKey = "trait:trait.amphibious";
        var keys = effective.Select(grant =>
            grant.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                ? "intrinsic:" + grant.Identity.IntrinsicSlotId
                : "trait:" + grant.Identity.MechanicalDefinitionId)
            .Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        Assert.Contains(combinedKey, keys);
        var prices = keys.Select(key =>
            key == combinedKey
                ? new NativeOriginAllocationPrice("combined-expression", 1, [key],
                    Configurations:
                    [
                        new("verified-combined-value", 7, 10,
                        [
                            new(combinedKey, "value",
                                NativeOriginAllocationComparison.Equals,
                                combined.EffectiveValue)
                        ])
                    ])
                : new NativeOriginAllocationPrice("foundation:" + key, 1, [key]))
            .ToArray();
        var expectedLoad = keys.Length - 1 + 7;
        var policy = new NativeOriginAllocationPolicy("integration-fixture",
            "rule.creature-origination", "biological", expectedLoad, prices);

        NativeOriginConstructionResult Create(NativeOriginProposal selected,
            NativeOriginAllocationPolicy configured) =>
            new NativeOriginConstructionEngine(catalog).Evaluate(
                new(selected, new("campaign", ScopeId),
                    AutoManifestDirectDefaults: false,
                    RequireCreationAllocation: true,
                    CreationAllocationPolicy: configured),
                new Authority());

        var accepted = Create(proposal, policy);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, accepted.Status);
        Assert.Equal("within-allowance", accepted.CreationAllocation!.Status);
        Assert.Equal(expectedLoad, accepted.CreationAllocation.CommittedLoad);
        var charge = Assert.Single(accepted.CreationAllocation.Charges,
            x => x.PriceId == "combined-expression");
        Assert.Equal("verified-combined-value", charge.ConfigurationId);
        Assert.Equal(7, charge.Load);
        Assert.Single(charge.GrantIds);
        Assert.DoesNotContain(accepted.CreationAllocation.Components,
            x => x.Key == "trait:trait.powerful-build");

        var overBudget = Create(proposal, policy with { Allowance = expectedLoad - 1 });
        Assert.Equal(NativeOriginConstructionStatus.Conflicting, overBudget.Status);
        Assert.Equal("over-allowance", overBudget.CreationAllocation!.Status);

        var unpriced = Create(proposal, policy with
        {
            Prices = prices.Where(x => x.Id != "combined-expression").ToArray()
        });
        Assert.Equal(NativeOriginConstructionStatus.Unresolved, unpriced.Status);
        Assert.Equal("unresolved", unpriced.CreationAllocation!.Status);
        Assert.Contains(unpriced.CreationAllocation.Issues, x =>
            x.StartsWith("allocation-load-unpriced:" + combinedKey, StringComparison.Ordinal));

        var noSelection = proposal with
        {
            BiologicalTransmission = proposal.BiologicalTransmission! with
            {
                Combinations = null
            }
        };
        var latent = Create(noSelection, policy);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, latent.Status);
        Assert.DoesNotContain(latent.CreationAllocation!.Charges,
            x => x.PriceId == "combined-expression");
        Assert.DoesNotContain(latent.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == "trait.amphibious");
    }
}
