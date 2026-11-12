using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Catalog and scoped-decision contract tests. Native definitions here are
/// synthetic fixtures, not approved first-party supernatural content. The
/// fake host trust accepts only the exact test Origin instance and grants.
/// </summary>
public sealed class NativeCatalogEffectiveEffectAuthorityTests
{
    private sealed class CatalogFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "rc-effective-catalog-" + Guid.NewGuid().ToString("N"));
        public NativeCatalogSnapshot Catalog { get; }

        public CatalogFixture()
        {
            var package = Path.Combine(_root, "first-party-001");
            var native = Path.Combine(package, "native");
            Directory.CreateDirectory(native);
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            File.WriteAllText(Path.Combine(native, "gift.json"),
                """{"id":"feature.test-gift","kind":"feature","name":"Fixture supernatural gift"}""");
            File.WriteAllText(Path.Combine(native, "effective-effects.json"),
                """
                {
                  "id": "rule.synthetic-effects",
                  "kind": "rule",
                  "name": "Synthetic effect-rule fixture",
                  "effectiveEffectDefinitions": [
                    {
                      "id": "strength-gift",
                      "sourceId": "feature.test-gift",
                      "sourceOccurrencePath": "/traits/gift",
                      "sourceMechanicalDefinitionId": "mechanic.gift",
                      "target": "ability-score",
                      "targetId": "strength",
                      "operation": "add",
                      "numericValue": 2,
                      "requiredFormId": null
                    },
                    {
                      "id": "suspend-class-in-form",
                      "sourceId": "feature.test-gift",
                      "sourceOccurrencePath": "/traits/gift",
                      "sourceMechanicalDefinitionId": "mechanic.gift",
                      "target": "independent-grant",
                      "targetId": "earned-class",
                      "operation": "suspend",
                      "numericValue": null,
                      "requiredFormId": "form.test-wolf"
                    }
                  ]
                }
                """);
            Catalog = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }

    private static NativeOriginEffectiveGrant Grant(string id = "earned-gift",
        string source = "feature.test-gift", string path = "/traits/gift",
        string mechanic = "mechanic.gift",
        NativeOriginSourceKind kind = NativeOriginSourceKind.Native) =>
        new(id, "manifest:" + id,
            new(NativeOriginCharacteristicKind.Trait,
                new(new(kind, source), path),
                mechanic),
            ["candidate:" + id]);

    private static NativeOriginIndependentGrantApplicability Applicable(string id) =>
        new(id, NativeOriginGrantApplicabilityState.Applicable,
            new(NativeOriginAdjudication.Supported,
                NativeRuleReference: "rule.synthetic-applicability"));

    private static NativeOriginConstructionResult Origin(
        NativeOriginEffectiveGrant? gift = null,
        NativeOriginConstructionStatus status = NativeOriginConstructionStatus.Authorized)
    {
        var baseline = new NativeOriginCurrentConfiguration("species.test-human", null, []);
        var source = gift ?? Grant();
        var profile = new NativeOriginProfile("profile.fixture", baseline, baseline,
            [source], [], new(NativeOriginAdjudication.Supported, "rule.synthetic-origin"),
            IndependentGrantApplicability: [Applicable(source.Id)]);
        var proposal = new NativeOriginProposal("proposal.fixture", NativeOriginIntent.Direct,
            new("rule.synthetic-origin", "biological"), [], [], []);
        return new(status, proposal, profile, []);
    }

    private static NativeEffectiveEffectDirective NativeDirective(
        string source = "earned-gift", string definitionId = "strength-gift") =>
        new("effect-instance", source, "rule.synthetic-effects",
            NativeEffectiveEffectTarget.AbilityScore, "strength",
            NativeEffectiveEffectOperation.Add, NumericValue: 2,
            DefinitionId: definitionId);

    private static NativeEffectiveCharacterRequest Request(
        NativeOriginConstructionResult origin,
        params NativeEffectiveEffectDirective[] directives) =>
        new(origin, new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["strength"] = 12, ["dexterity"] = 10
        }, directives);

    private sealed class TrustedFixture(NativeOriginConstructionResult origin) :
        INativeEffectiveCatalogTrust
    {
        public bool AllowGrants { get; set; } = true;
        public bool AllowApplicability { get; set; } = true;
        public bool AllowForm { get; set; } = true;
        public string? AuthorizedDecisionId { get; set; }

        public bool AuthorizesOrigin(NativeOriginConstructionResult result) =>
            ReferenceEquals(result, origin) && result.IsMechanicallyAuthorized;

        public bool AuthorizesEarnedGrant(NativeOriginEffectiveGrant grant,
            NativeOriginProfile profile) =>
            AllowGrants && ReferenceEquals(profile, origin.Profile) &&
            profile.IndependentGrants.Any(x => ReferenceEquals(x, grant));

        public bool AuthorizesApplicability(
            NativeOriginIndependentGrantApplicability applicability,
            NativeOriginProfile profile) =>
            AllowApplicability && ReferenceEquals(profile, origin.Profile) &&
            applicability.Basis?.NativeRuleReference == "rule.synthetic-applicability";

        public bool AuthorizesForm(NativeOriginActiveTemporaryTransformation form,
            NativeOriginProfile profile) => AllowForm &&
            ReferenceEquals(profile, origin.Profile);

        public bool AuthorizesTableDecision(NativeTableRuleDecision decision,
            NativeTableDecisionScope trustedScope) =>
            decision.Id == AuthorizedDecisionId &&
            decision.Scope == trustedScope &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "signed-proof";
    }

    private static NativeCatalogEffectiveEffectAuthority Authority(
        CatalogFixture fixture, NativeOriginConstructionResult origin,
        TrustedFixture trust, NativeTableDecisionScope? scope = null,
        IReadOnlyList<NativeTableRuleDecision>? decisions = null) =>
        new(fixture.Catalog, trust, scope, decisions);

    private static NativeTableRuleDecision Decision(
        NativeEffectiveEffectDirective directive,
        NativeTableDecisionScope? scope = null,
        int? overrideValue = null) =>
        new("trusted-effect-decision", new("dm", "signed-proof"),
            scope ?? new("campaign", "test-campaign"),
            new("effective-effect", directive.Id),
            JsonSerializer.SerializeToElement(new
            {
                sourceGrantId = directive.SourceGrantId,
                ruleId = directive.RuleId,
                target = "ability-score",
                targetId = directive.TargetId,
                operation = "add",
                numericValue = overrideValue ?? directive.NumericValue,
                requiredFormId = directive.RequiredFormId,
                definitionId = directive.DefinitionId
            }));

    [Fact]
    public void ExactNativeEffectDefinitionAllowsVerifiedEarnedSource()
    {
        using var fixture = new CatalogFixture();
        var origin = Origin();
        var trust = new TrustedFixture(origin);
        var effect = NativeDirective();
        var result = NativeEffectiveCharacterEffects.Evaluate(Request(origin, effect),
            Authority(fixture, origin, trust));

        Assert.True(result.IsResolved);
        Assert.Equal(14, result.EffectiveAbilityScores["strength"]);
        Assert.Equal("species.test-human", result.EffectiveOrigin!.SpeciesId);
        Assert.Equal(12, Request(origin).AbilityScores["strength"]);
    }

    [Theory]
    [InlineData("wrong-definition")]
    [InlineData("")]
    public void UnknownOrMissingDefinitionIdentityIsNotPermission(string definitionId)
    {
        using var fixture = new CatalogFixture();
        var origin = Origin();
        var effect = NativeDirective(definitionId: definitionId);
        var result = NativeEffectiveCharacterEffects.Evaluate(Request(origin, effect),
            Authority(fixture, origin, new TrustedFixture(origin)));

        Assert.Equal("unresolved", result.Status);
        Assert.Contains(result.Issues, x => x.Code == "effective-directive-unauthorized");
    }

    [Fact]
    public void NativeDefinitionDoesNotAuthorizeWrongMagnitudeTargetOrOperation()
    {
        using var fixture = new CatalogFixture();
        var origin = Origin();
        var approved = NativeDirective();
        var attempts = new[]
        {
            approved with { NumericValue = 20 },
            approved with { TargetId = "dexterity" },
            approved with { Operation = NativeEffectiveEffectOperation.Replace },
            approved with { RuleId = "rule.does-not-exist" }
        };

        foreach (var attempt in attempts)
        {
            var result = NativeEffectiveCharacterEffects.Evaluate(Request(origin, attempt),
                Authority(fixture, origin, new TrustedFixture(origin)));
            Assert.False(result.IsResolved);
            Assert.Contains(result.Issues, x => x.Code == "effective-directive-unauthorized");
        }
    }

    [Fact]
    public void AFormScopedEffectWithTheWrongScopeIsNotNativeAuthorized()
    {
        using var fixture = new CatalogFixture();
        var origin = Origin();
        var authority = Authority(fixture, origin, new TrustedFixture(origin));
        var incorrectlyScoped = NativeDirective() with
        {
            RequiredFormId = "form.test-wolf"
        };

        // Authorizer rejects the incorrect scope. Evaluation itself correctly
        // skips a form-only effect when the form is not active; skipping must
        // not be confused with permission to execute the effect.
        Assert.False(authority.AuthorizesDirective(incorrectlyScoped, origin.Profile!));
        var noActiveForm = NativeEffectiveCharacterEffects.Evaluate(
            Request(origin, incorrectlyScoped), authority);
        Assert.True(noActiveForm.IsResolved);
        Assert.Equal(12, noActiveForm.EffectiveAbilityScores["strength"]);
    }

    [Fact]
    public void NativeRuleMustBindExactGrantSourcePathAndMechanicalDefinition()
    {
        using var fixture = new CatalogFixture();
        var variants = new[]
        {
            Grant(source: "feature.unknown"),
            Grant(path: "/traits/wrong"),
            Grant(mechanic: "mechanic.other"),
            Grant(kind: NativeOriginSourceKind.Custom)
        };
        foreach (var grant in variants)
        {
            var origin = Origin(grant);
            var result = NativeEffectiveCharacterEffects.Evaluate(
                Request(origin, NativeDirective()),
                Authority(fixture, origin, new TrustedFixture(origin)));
            Assert.False(result.IsResolved);
            Assert.Empty(result.EffectiveAbilityScores);
        }
    }

    [Fact]
    public void SyntacticallyNamedButUnearnedGrantDoesNotAuthorize()
    {
        using var fixture = new CatalogFixture();
        var origin = Origin(Grant("other-grant"));
        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(origin, NativeDirective()),
            Authority(fixture, origin, new TrustedFixture(origin)));

        Assert.Equal("unresolved", result.Status);
        Assert.Contains(result.Issues, x => x.Code == "effective-directive-unauthorized");
    }

    [Fact]
    public void HostTrustMayRejectEarnedGrantOrItsApplicability()
    {
        using var fixture = new CatalogFixture();
        var origin = Origin();
        foreach (var trust in new[]
        {
            new TrustedFixture(origin) { AllowGrants = false },
            new TrustedFixture(origin) { AllowApplicability = false }
        })
        {
            var result = NativeEffectiveCharacterEffects.Evaluate(
                Request(origin, NativeDirective()), Authority(fixture, origin, trust));
            Assert.False(result.IsResolved);
            Assert.Empty(result.EffectiveAbilityScores);
        }
    }

    [Fact]
    public void ForgedAuthorizedStatusIsNotEquivalentToTrustedConstruction()
    {
        using var fixture = new CatalogFixture();
        var original = Origin();
        var trust = new TrustedFixture(original);
        var forged = original with { Profile = original.Profile! with { Id = "forged" } };
        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(forged, NativeDirective()),
            Authority(fixture, original, trust));

        Assert.False(result.IsResolved);
        Assert.Contains(result.Issues, x => x.Code == "effective-origin-not-authorized");
    }

    [Fact]
    public void UnresolvedConstructionCannotAuthorizeEvenWhenOriginIsKnown()
    {
        using var fixture = new CatalogFixture();
        var origin = Origin(status: NativeOriginConstructionStatus.Unresolved);
        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(origin, NativeDirective()),
            Authority(fixture, origin, new TrustedFixture(origin)));

        Assert.False(result.IsResolved);
    }

    [Fact]
    public void MissingNativeEffectRuleRequiresAuthenticatedScopedDmOverride()
    {
        using var fixture = new CatalogFixture();
        var origin = Origin();
        var requested = NativeDirective() with
        {
            RuleId = "rule.dm-custom",
            NumericValue = 7,
            DefinitionId = null
        };
        var scope = new NativeTableDecisionScope("campaign", "test-campaign");
        var decision = Decision(requested, scope);
        var trust = new TrustedFixture(origin)
        {
            AuthorizedDecisionId = decision.Id
        };
        var authority = Authority(fixture, origin, trust, scope, [decision]);

        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(origin, requested), authority);

        Assert.True(result.IsResolved);
        Assert.Equal(19, result.EffectiveAbilityScores["strength"]);
    }

    [Fact]
    public void DmOverridesRequireExactTargetScopeContentAndTrustedAuthority()
    {
        using var fixture = new CatalogFixture();
        var origin = Origin();
        var requested = NativeDirective() with
        {
            RuleId = "rule.dm-custom", DefinitionId = null,
            NumericValue = 7
        };
        var scope = new NativeTableDecisionScope("campaign", "test-campaign");
        var decision = Decision(requested, scope);
        var mismatchedScope = new NativeTableDecisionScope("campaign", "other-campaign");
        var attempts = new[]
        {
            Authority(fixture, origin, new TrustedFixture(origin), scope, [decision]),
            Authority(fixture, origin,
                new TrustedFixture(origin) { AuthorizedDecisionId = decision.Id },
                mismatchedScope, [decision]),
            Authority(fixture, origin,
                new TrustedFixture(origin) { AuthorizedDecisionId = decision.Id },
                scope, [Decision(requested, scope, overrideValue: 99)]),
            Authority(fixture, origin,
                new TrustedFixture(origin) { AuthorizedDecisionId = decision.Id },
                null, [decision]),
            Authority(fixture, origin,
                new TrustedFixture(origin) { AuthorizedDecisionId = decision.Id },
                scope, [decision with
                {
                    Target = new("independent-grant", requested.Id)
                }])
        };

        foreach (var authority in attempts)
        {
            var result = NativeEffectiveCharacterEffects.Evaluate(
                Request(origin, requested), authority);
            Assert.False(result.IsResolved);
            Assert.Empty(result.EffectiveAbilityScores);
        }
    }

    [Fact]
    public void NativeFormScopedRuleCannotRunInNormalForm()
    {
        using var fixture = new CatalogFixture();
        var origin = Origin();
        var effect = new NativeEffectiveEffectDirective("form-restriction",
            "earned-gift", "rule.synthetic-effects",
            NativeEffectiveEffectTarget.IndependentGrant, "earned-class",
            NativeEffectiveEffectOperation.Suspend,
            RequiredFormId: "form.test-wolf",
            DefinitionId: "suspend-class-in-form");
        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(origin, effect), Authority(fixture, origin, new TrustedFixture(origin)));
        // Inactive form-scoped directives are skipped without granting an effect.
        Assert.True(result.IsResolved);
        Assert.Empty(result.SuppressedIndependentGrantIds);
        Assert.Equal(12, result.EffectiveAbilityScores["strength"]);
    }

    [Fact]
    public void FormNeedsExplicitTrustedOverrideNotJustNativeRuleName()
    {
        using var fixture = new CatalogFixture();
        var origin = Origin();
        var form = new NativeOriginActiveTemporaryTransformation(
            "active-occurrence", "form.test-wolf",
            new(NativeOriginSourceKind.Native, "feature.test-gift"),
            "event.shift");
        var trust = new TrustedFixture(origin);
        var noDecision = Authority(fixture, origin, trust);
        Assert.False(noDecision.AuthorizesForm(form, origin.Profile!));

        var scope = new NativeTableDecisionScope("campaign", "test-campaign");
        var decision = new NativeTableRuleDecision("form-decision",
            new("dm", "signed-proof"), scope,
            new("effective-form", form.Id), JsonSerializer.SerializeToElement(new
            {
                formId = form.FormId,
                sourceId = form.EffectSource.Id,
                activationEvidenceId = form.ActivationEvidenceId,
                persistentInfluenceId = form.PersistentInfluenceId
            }));
        trust.AuthorizedDecisionId = decision.Id;
        var explicitAuthority = Authority(fixture, origin, trust, scope, [decision]);
        Assert.True(explicitAuthority.AuthorizesForm(form, origin.Profile!));
    }
}
