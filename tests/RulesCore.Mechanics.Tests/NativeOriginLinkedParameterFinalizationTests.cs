using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Source-neutral canonical IDs must be used after gene catalog migration.
/// Parameter links compare fully bound biological gene manifestations, not
/// unbound early offers. Links are explicit native or authenticated table
/// constraints; sharing a source or a parameter name creates no implicit link.
/// </summary>
public sealed class NativeOriginLinkedParameterFinalizationTests
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
        throw new InvalidOperationException("Native catalog unavailable.");
    }

    private static NativeOriginProposal Direct(string ancestry) =>
        new("gene-links", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: ancestry);

    private static NativeOriginConstructionResult Evaluate(NativeOriginProposal proposal) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, new("campaign", "gene-link-proof"),
                AutoManifestDirectDefaults: true), new TestAuthority());

    private static NativeTableRuleDecision Enforce(NativeOriginConstraint link) =>
        new(link.TableDecisionId!, new("dm", "linked-gene-proof"),
            new("campaign", "gene-link-proof"),
            new("constraint", link.Id),
            JsonSerializer.SerializeToElement(new { enforce = true }));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "gene-link-proof" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "linked-gene-proof";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    [Fact]
    public void ExplicitGeneLinkAcceptsParametersBoundFromOneManifestedAptitude()
    {
        var draft = Direct("ancestry.goliath.stone");
        var offers = Evaluate(draft).EvaluatedProposal.Candidates;
        var aptitude = Assert.Single(offers, x =>
            x.Identity.MechanicalDefinitionId ==
            "trait.ancestral-aptitude");
        var link = new NativeOriginConstraint("linked-goliath-aptitude",
            NativeOriginRelationshipKind.LinkedParameters,
            [new(MechanicalDefinitionId: "trait.stone-strike")],
            [new(MechanicalDefinitionId: "trait.stone-throw")],
            LinkedParameterPaths: new Dictionary<string, string>
            {
                ["giantAncestryAbility"] = "giantAncestryAbility"
            },
            TableDecisionId: "enforce-goliath-link");

        var result = Evaluate(draft with
        {
            Manifestations =
            [
                new("selected-aptitude", aptitude.Id,
                    Choices: [new("abilityChoice", ["constitution"])])
            ],
            Constraints = [link],
            TableDecisions = [Enforce(link)]
        });

        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var grants = result.Profile!.Underlying.InherentGrants;
        var strike = Assert.Single(grants, x => x.Identity.MechanicalDefinitionId ==
            "trait.stone-strike");
        var thrown = Assert.Single(grants, x => x.Identity.MechanicalDefinitionId ==
            "trait.stone-throw");
        Assert.Equal("constitution",
            strike.Parameters!["giantAncestryAbility"].GetString());
        Assert.Equal("constitution",
            thrown.Parameters!["giantAncestryAbility"].GetString());
        Assert.DoesNotContain(result.Issues,
            x => x.Code.StartsWith("effective-linked-parameter",
                StringComparison.Ordinal) || x.Code == "linked-parameter-unbound");
    }

    [Fact]
    public void ExplicitGeneLinkRejectsConflictingBoundParameters()
    {
        var draft = Direct("ancestry.dragonborn.red");
        var link = new NativeOriginConstraint("incompatible-linked-fields",
            NativeOriginRelationshipKind.LinkedParameters,
            [new(MechanicalDefinitionId: "trait.breath-weapon")],
            [new(MechanicalDefinitionId: "trait.damage-type-resistance")],
            LinkedParameterPaths: new Dictionary<string, string>
            {
                ["area"] = "damageType"
            },
            TableDecisionId: "enforce-incompatible-link");
        var result = Evaluate(draft with
        {
            Constraints = [link],
            TableDecisions = [Enforce(link)]
        });

        Assert.Equal(NativeOriginConstructionStatus.Conflicting, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "effective-linked-parameter-conflict" &&
            x.Path == "constraints." + link.Id);
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId is
                "trait.breath-weapon" or
                "trait.damage-type-resistance");
    }

    [Fact]
    public void CallerOnlyGeneLinkDoesNotBecomeAnExecutableRule()
    {
        var draft = Direct("ancestry.goliath.stone");
        var link = new NativeOriginConstraint("unverified-gene-link",
            NativeOriginRelationshipKind.LinkedParameters,
            [new(MechanicalDefinitionId: "trait.stone-strike")],
            [new(MechanicalDefinitionId: "trait.stone-throw")],
            LinkedParameterPaths: new Dictionary<string, string>
            {
                ["giantAncestryAbility"] = "giantAncestryAbility"
            },
            NativeRuleReference: "rule.creature-origination");
        var result = Evaluate(draft with { Constraints = [link] });

        Assert.Contains(result.Issues, x =>
            x.Code == "constraint-policy-unresolved" &&
            x.Path == "constraints." + link.Id);
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
            "trait.stone-strike");
    }
}
