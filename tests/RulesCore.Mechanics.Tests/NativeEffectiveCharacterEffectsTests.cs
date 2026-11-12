using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Synthetic architecture tests, not approval of a werewolf, vampire or
/// Bloodline's specific rules. Authority is deliberately substituted here:
/// the authenticated production adapter remains a separate requirement.
/// </summary>
public sealed class NativeEffectiveCharacterEffectsTests
{
    private static NativeOriginSourceReference Source(string id) =>
        new(NativeOriginSourceKind.Native, id);

    private static NativeOriginEffectiveGrant Grant(string id, string sourceId) =>
        new(id, "manifest:" + id,
            new(NativeOriginCharacteristicKind.Trait,
                new(Source(sourceId), "/traits/" + id),
                "mechanic." + id),
            ["candidate:" + id]);

    private static NativeOriginCurrentConfiguration Base(
        string species = "species.human") =>
        new(species, null, [Grant("human-origin", species)], "baseline:" + species);

    private static NativeOriginIndependentGrantApplicability Applicable(string id) =>
        new(id, NativeOriginGrantApplicabilityState.Applicable,
            new(NativeOriginAdjudication.Supported, "test.rule.applicability"));

    private static NativeOriginProfile Profile(
        NativeOriginCurrentConfiguration? underlying = null,
        IReadOnlyList<NativeOriginEffectiveGrant>? independent = null,
        IReadOnlyList<NativeOriginIndependentGrantApplicability>? applicability = null)
    {
        var baseline = underlying ?? Base();
        return new("test.profile", baseline, baseline, independent ?? [], [],
            new(NativeOriginAdjudication.Supported, "test.rule.character"),
            IndependentGrantApplicability: applicability);
    }

    private static NativeOriginConstructionResult Authorized(NativeOriginProfile profile) =>
        new(NativeOriginConstructionStatus.Authorized,
            new NativeOriginProposal("test.proposal", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"), [], [], []),
            profile, []);

    private static NativeEffectiveCharacterRequest Request(
        NativeOriginProfile profile, params NativeEffectiveEffectDirective[] directives) =>
        new(Authorized(profile), new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["strength"] = 12, ["dexterity"] = 10
        }, directives);

    private sealed class TrustedFixtureAuthority : INativeEffectiveEffectAuthority
    {
        public bool OriginAllowed { get; init; } = true;
        public bool FormAllowed { get; init; } = true;
        public HashSet<string> AllowedRules { get; } =
            new(["test.rule.draconic", "test.rule.wolf", "test.rule.override"],
                StringComparer.Ordinal);

        public bool AuthorizesOrigin(NativeOriginConstructionResult result) =>
            OriginAllowed && result.IsMechanicallyAuthorized;

        public bool AuthorizesDirective(NativeEffectiveEffectDirective directive,
            NativeOriginProfile profile) =>
            AllowedRules.Contains(directive.RuleId);

        public bool AuthorizesForm(NativeOriginActiveTemporaryTransformation form,
            NativeOriginProfile profile) => FormAllowed;

        public bool AuthorizesApplicability(
            NativeOriginIndependentGrantApplicability applicability,
            NativeOriginProfile profile) =>
            applicability.Basis?.NativeRuleReference == "test.rule.applicability";
    }

    [Fact]
    public void AcquiredDraconicModifierChangesEffectiveScoreButNotBaselineOrClass()
    {
        var source = Grant("dragon-influence-grant", "influence.draconic");
        var classGrant = Grant("earned-class-grant", "class.wizard");
        var profile = Profile(independent: [source, classGrant],
            applicability: [Applicable(source.Id), Applicable(classGrant.Id)])
            with
            {
                PersistentInfluences = [new("draconic-influence",
                    Source("influence.draconic"), "event.acquire-draconic")]
            };
        var effect = new NativeEffectiveEffectDirective("dragon-power",
            source.Id, "test.rule.draconic",
            NativeEffectiveEffectTarget.AbilityScore, "strength",
            NativeEffectiveEffectOperation.Add, NumericValue: 2);

        var result = NativeEffectiveCharacterEffects.Evaluate(Request(profile, effect),
            new TrustedFixtureAuthority());

        Assert.True(result.IsResolved);
        Assert.Equal(14, result.EffectiveAbilityScores["strength"]);
        Assert.Equal(12, Request(profile).AbilityScores["strength"]);
        Assert.Equal("species.human", result.EffectiveOrigin!.SpeciesId);
        Assert.Contains(classGrant.Id, result.ApplicableIndependentGrantIds);
        Assert.Contains(source.Id, result.ApplicableIndependentGrantIds);
        Assert.Empty(result.Issues);
    }

    private static (NativeOriginProfile Profile,
        NativeEffectiveEffectDirective Restriction) WolfProfile()
    {
        var wizardGrant = Grant("wizard-feature", "class.wizard");
        var wolfGrant = Grant("wolf-form-grant", "form.wolf");
        var baseline = Base();
        var wolf = new NativeOriginCurrentConfiguration("species.wolf-form", null,
            [wolfGrant], "effective:wolf");
        var influence = new NativeOriginPersistentInfluence("lycanthropy",
            Source("condition.lycanthropy"), "event.acquire");
        var activation = new NativeOriginActiveTemporaryTransformation(
            "shift-one", "form.wolf", Source("condition.lycanthropy"),
            "event.shift", influence.Id, [wolfGrant.Id]);
        var profile = Profile(baseline, [wizardGrant], [Applicable(wizardGrant.Id)]) with
        {
            Current = wolf,
            PersistentInfluences = [influence],
            ActiveTemporaryTransformation = activation
        };
        var restriction = new NativeEffectiveEffectDirective("wolf-no-wizard",
            wolfGrant.Id, "test.rule.wolf",
            NativeEffectiveEffectTarget.IndependentGrant, wizardGrant.Id,
            NativeEffectiveEffectOperation.Suspend, RequiredFormId: "form.wolf");
        return (profile, restriction);
    }

    [Fact]
    public void ActiveWolfFormReplacesEffectiveSpeciesAndSuspendsClassUseNotEntitlement()
    {
        var (profile, restriction) = WolfProfile();

        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(profile, restriction), new TrustedFixtureAuthority());

        Assert.True(result.IsResolved);
        Assert.Equal("species.wolf-form", result.EffectiveOrigin!.SpeciesId);
        Assert.Equal("species.human", profile.Underlying.SpeciesId);
        Assert.Single(profile.IndependentGrants);
        Assert.Contains("wizard-feature", result.SuppressedIndependentGrantIds);
        Assert.DoesNotContain("wizard-feature", result.ApplicableIndependentGrantIds);
    }

    [Fact]
    public void ReversionUsesChangedBaselineAndDoesNotKeepFormRestrictions()
    {
        var (during, restriction) = WolfProfile();
        var updated = Base("species.elf");
        var reverted = during with
        {
            Underlying = updated, Current = updated,
            ActiveTemporaryTransformation = null
        };

        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(reverted, restriction), new TrustedFixtureAuthority());

        Assert.True(result.IsResolved);
        Assert.Equal("species.elf", result.EffectiveOrigin!.SpeciesId);
        Assert.Contains("wizard-feature", result.ApplicableIndependentGrantIds);
        Assert.Empty(result.SuppressedIndependentGrantIds);
        Assert.Equal("species.human", during.Underlying.SpeciesId);
    }

    [Fact]
    public void NullOrRejectedAuthorityCannotCreatePlayableEffects()
    {
        var profile = Profile();
        var input = Request(profile);

        var missing = NativeEffectiveCharacterEffects.Evaluate(input, null);
        var rejected = NativeEffectiveCharacterEffects.Evaluate(input,
            new TrustedFixtureAuthority { OriginAllowed = false });

        Assert.False(missing.IsResolved);
        Assert.False(rejected.IsResolved);
        Assert.Null(missing.EffectiveOrigin);
        Assert.Empty(rejected.EffectiveAbilityScores);
    }

    [Fact]
    public void UnknownEarnedSourceCannotApplyModifierEvenWithApprovedRuleName()
    {
        var effect = new NativeEffectiveEffectDirective("forged",
            "nonexistent-grant", "test.rule.draconic",
            NativeEffectiveEffectTarget.AbilityScore, "strength",
            NativeEffectiveEffectOperation.Replace, NumericValue: 100);

        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(Profile(), effect), new TrustedFixtureAuthority());

        Assert.Equal("unresolved", result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-directive-unauthorized");
        Assert.Empty(result.EffectiveAbilityScores);
    }

    [Fact]
    public void AuthoredRuleStillRequiresExactAuthorization()
    {
        var source = Grant("source", "influence.draconic");
        var profile = Profile(independent: [source],
            applicability: [Applicable(source.Id)]);
        var effect = new NativeEffectiveEffectDirective("unknown-rule", source.Id,
            "test.rule.not-approved", NativeEffectiveEffectTarget.AbilityScore,
            "strength", NativeEffectiveEffectOperation.Add, NumericValue: 4);

        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(profile, effect), new TrustedFixtureAuthority());

        Assert.Equal("unresolved", result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-directive-unauthorized");
    }

    [Fact]
    public void TwoNumericDirectivesOnOneScoreAreNotStackedByDefault()
    {
        var source = Grant("source", "influence.draconic");
        var profile = Profile(independent: [source],
            applicability: [Applicable(source.Id)]);
        var a = new NativeEffectiveEffectDirective("a", source.Id, "test.rule.draconic",
            NativeEffectiveEffectTarget.AbilityScore, "strength",
            NativeEffectiveEffectOperation.Add, NumericValue: 2);
        var b = a with { Id = "b", NumericValue = 3 };

        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(profile, a, b), new TrustedFixtureAuthority());

        Assert.False(result.IsResolved);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-target-overlap-unresolved");
        Assert.Empty(result.EffectiveAbilityScores);
    }

    [Fact]
    public void ConcurrentUnresolvedFormsDoNotSelectOneSilently()
    {
        var (profile, restriction) = WolfProfile();
        var competing = new NativeOriginActiveTemporaryTransformation("shift-other",
            "form.bear", Source("condition.bear"), "event.other-shift");
        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(profile with
            {
                OtherActiveTemporaryTransformations = [competing]
            }, restriction), new TrustedFixtureAuthority());

        Assert.Equal("unresolved", result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-form-precedence-unresolved");
    }

    [Fact]
    public void UnverifiedFormOrMissingFeatureApplicabilityBlocksResult()
    {
        var (wolf, restriction) = WolfProfile();
        var untrustedForm = NativeEffectiveCharacterEffects.Evaluate(
            Request(wolf, restriction),
            new TrustedFixtureAuthority { FormAllowed = false });
        Assert.Equal("unresolved", untrustedForm.Status);
        Assert.Contains(untrustedForm.Issues, issue =>
            issue.Code == "effective-form-not-authorized");

        var missingApplicability = wolf with { IndependentGrantApplicability = [] };
        var unqualified = NativeEffectiveCharacterEffects.Evaluate(
            Request(missingApplicability, restriction), new TrustedFixtureAuthority());
        Assert.Equal("unresolved", unqualified.Status);
        Assert.Contains(unqualified.Issues, issue =>
            issue.Code == "effective-applicability-missing");
    }

    [Fact]
    public void OutOfRangeNumericResultDoesNotReturnPartialPlayableScores()
    {
        var source = Grant("source", "influence.draconic");
        var profile = Profile(independent: [source],
            applicability: [Applicable(source.Id)]);
        var effect = new NativeEffectiveEffectDirective("overflow", source.Id,
            "test.rule.draconic", NativeEffectiveEffectTarget.AbilityScore,
            "strength", NativeEffectiveEffectOperation.Add, NumericValue: 1000);

        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(profile, effect), new TrustedFixtureAuthority());

        Assert.Equal("unresolved", result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-numeric-out-of-range");
        Assert.Empty(result.EffectiveAbilityScores);
    }

    [Fact]
    public void FormCannotClaimAnUnrelatedPersistentInfluence()
    {
        var (profile, restriction) = WolfProfile();
        var forged = profile with
        {
            ActiveTemporaryTransformation = profile.ActiveTemporaryTransformation! with
            {
                PersistentInfluenceId = "not-the-lycanthropy-source"
            }
        };
        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(forged, restriction), new TrustedFixtureAuthority());

        Assert.False(result.IsResolved);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-form-influence-mismatch");
    }

    [Fact]
    public void IndependentAndInherentSourcesCannotShareSameGrantOccurrenceId()
    {
        var duplicate = Grant("duplicate-source", "influence.draconic");
        var original = Base();
        var profile = Profile(independent: [duplicate],
            applicability: [Applicable(duplicate.Id)]) with
        {
            Underlying = original with { InherentGrants = [duplicate] },
            Current = original with { InherentGrants = [duplicate] }
        };
        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(profile), new TrustedFixtureAuthority());

        Assert.False(result.IsResolved);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-grant-identity-conflict");
    }

    [Fact]
    public void AuthorizedReplacementOnlyChangesTheEffectiveScore()
    {
        var source = Grant("source", "influence.draconic");
        var profile = Profile(independent: [source],
            applicability: [Applicable(source.Id)]);
        var directive = new NativeEffectiveEffectDirective("replace",
            source.Id, "test.rule.override",
            NativeEffectiveEffectTarget.AbilityScore, "strength",
            NativeEffectiveEffectOperation.Replace, NumericValue: 18);

        var result = NativeEffectiveCharacterEffects.Evaluate(
            Request(profile, directive), new TrustedFixtureAuthority());

        Assert.True(result.IsResolved);
        Assert.Equal(18, result.EffectiveAbilityScores["strength"]);
        Assert.Equal(12, Request(profile).AbilityScores["strength"]);
    }

    [Fact]
    public void AnUnresolvedOriginIsNotTreatedAsPlayableEvenWithTrustedAdapter()
    {
        var request = Request(Profile());
        var unresolved = request with
        {
            Origin = request.Origin with { Status = NativeOriginConstructionStatus.Unresolved }
        };

        var result = NativeEffectiveCharacterEffects.Evaluate(
            unresolved, new TrustedFixtureAuthority());

        Assert.Equal("unresolved", result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-origin-not-authorized");
    }
}
