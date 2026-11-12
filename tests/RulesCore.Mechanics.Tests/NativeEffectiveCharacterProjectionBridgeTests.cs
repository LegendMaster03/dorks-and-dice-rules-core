using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// The source and character authorities are deliberately synthetic. These
/// checks prove derived-value composition and fail-closed boundaries, not
/// authenticated production entitlements or approved supernatural content.
/// </summary>
public sealed class NativeEffectiveCharacterProjectionBridgeTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var rulesets = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(rulesets, "first-party-001", "manifest.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(rulesets, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Selected first-party ruleset not found.");
    }

    private static NativeOriginEffectiveGrant Grant(string id, string sourceId) =>
        new(id, "manifest:" + id,
            new(NativeOriginCharacteristicKind.Trait,
                new(new(NativeOriginSourceKind.Native, sourceId),
                    "/traits/" + id), "mechanic." + id),
            ["candidate:" + id]);

    private static NativeOriginConstructionResult Origin(bool withGrant = true,
        bool suppressGrant = false)
    {
        var baseline = new NativeOriginCurrentConfiguration("species.human", null, []);
        var independent = withGrant
            ? new[] { Grant("earned-modifier", "source.synthetic-bloodline") }
            : Array.Empty<NativeOriginEffectiveGrant>();
        var applicable = withGrant
            ? new[]
            {
                new NativeOriginIndependentGrantApplicability(
                    independent[0].Id,
                    suppressGrant ? NativeOriginGrantApplicabilityState.Suppressed :
                        NativeOriginGrantApplicabilityState.Applicable,
                    new(NativeOriginAdjudication.Supported, "fixture.verified-applicability"))
            }
            : [];
        var profile = new NativeOriginProfile(
            "origin:test", baseline, baseline, independent, [],
            new(NativeOriginAdjudication.Supported, "fixture.constructed-origin"),
            IndependentGrantApplicability: applicable);
        return new(NativeOriginConstructionStatus.Authorized,
            new("test", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"), [], [], []),
            profile, []);
    }

    private static NativeCharacterRequest Character(NativeCatalogSnapshot catalog) =>
        new(catalog.RulesetId, catalog.Fingerprint,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["strength"] = 12, ["dexterity"] = 10,
                ["constitution"] = 10, ["intelligence"] = 10,
                ["wisdom"] = 10, ["charisma"] = 10
            },
            [], BaseWalkSpeed: 30);

    private static NativeEffectiveCharacterRequest EffectInput(
        NativeCharacterRequest character, NativeOriginConstructionResult origin,
        params NativeEffectiveEffectDirective[] directives) =>
        new(origin, character.AbilityScores, directives);

    private static NativeEffectiveEffectDirective StrengthModifier() =>
        new("draconic-strength", "earned-modifier", "fixture.draconic",
            NativeEffectiveEffectTarget.AbilityScore, "strength",
            NativeEffectiveEffectOperation.Add, NumericValue: 2);

    private sealed class TrustedEffects : INativeEffectiveEffectAuthority
    {
        public bool AuthorizesOrigin(NativeOriginConstructionResult result) =>
            result.IsMechanicallyAuthorized && result.Profile is not null;

        public bool AuthorizesDirective(NativeEffectiveEffectDirective directive,
            NativeOriginProfile profile) =>
            directive.RuleId == "fixture.draconic";

        public bool AuthorizesForm(NativeOriginActiveTemporaryTransformation form,
            NativeOriginProfile profile) => true;

        public bool AuthorizesApplicability(
            NativeOriginIndependentGrantApplicability applicability,
            NativeOriginProfile profile) =>
            applicability.Basis?.NativeRuleReference == "fixture.verified-applicability";
    }

    private sealed class TrustedCharacter(bool authorized = true)
        : INativeEffectiveCharacterInputAuthority
    {
        public bool AuthorizesCharacterInput(NativeCharacterRequest request,
            NativeOriginConstructionResult origin) => authorized;
    }

    [Fact]
    public void VerifiedNumericModifierFeedsTheExistingCharacterProjection()
    {
        var catalog = Catalog();
        var character = Character(catalog);
        var input = EffectInput(character, Origin(), StrengthModifier());

        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, input, new TrustedEffects(), new TrustedCharacter());

        Assert.True(result.HasCalculatedValues);
        Assert.Equal(14, result.Values["ability.strength.score"].Value);
        Assert.Equal(2, result.Values["ability.strength.modifier"].Value);
        Assert.Equal(12, character.AbilityScores["strength"]);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void RejectedCharacterAuthorityWithholdsAllCalculatedValues()
    {
        var catalog = Catalog();
        var character = Character(catalog);
        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, EffectInput(character, Origin(), StrengthModifier()),
            new TrustedEffects(), new TrustedCharacter(false));

        Assert.False(result.HasCalculatedValues);
        Assert.Empty(result.Values);
        Assert.Contains(result.Issues,
            issue => issue.Code == "effective-bridge-input-unverified");
    }

    [Fact]
    public void MissingEffectiveAuthorityWithholdsCalculatedValues()
    {
        var catalog = Catalog();
        var character = Character(catalog);
        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, EffectInput(character, Origin(), StrengthModifier()),
            null, new TrustedCharacter());

        Assert.False(result.HasCalculatedValues);
        Assert.Empty(result.Values);
    }

    [Fact]
    public void MismatchedUnmodifiedScoresCannotPassThroughTheBridge()
    {
        var catalog = Catalog();
        var character = Character(catalog);
        var fakeInput = EffectInput(character, Origin(), StrengthModifier()) with
        {
            AbilityScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["strength"] = 25
            }
        };

        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, fakeInput, new TrustedEffects(), new TrustedCharacter());

        Assert.False(result.HasCalculatedValues);
        Assert.Contains(result.Issues,
            issue => issue.Code == "effective-bridge-baseline-mismatch");
    }

    [Fact]
    public void SuspendedIndependentFeatureIsNeverLeakedFromTheOldProjector()
    {
        var catalog = Catalog();
        var character = Character(catalog);
        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, EffectInput(character, Origin(suppressGrant: true)),
            new TrustedEffects(), new TrustedCharacter());

        Assert.False(result.HasCalculatedValues);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-bridge-feature-suppression-unimplemented");
        Assert.Empty(result.Values);
    }

    [Fact]
    public void ActiveFormIsBlockedUntilItsWiderMechanicsAreExecutable()
    {
        var catalog = Catalog();
        var character = Character(catalog);
        var original = Origin();
        var profile = original.Profile!;
        var transformed = profile with
        {
            ActiveTemporaryTransformation = new("form-occurrence", "form.wolf",
                new(NativeOriginSourceKind.Native, "form.wolf"), "event.shift"),
            Current = new("species.wolf-form", null, [])
        };
        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, EffectInput(character,
                original with { Profile = transformed }),
            new TrustedEffects(), new TrustedCharacter());

        Assert.False(result.HasCalculatedValues);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-bridge-form-policy-unimplemented");
    }

    [Fact]
    public void NonFormSpeciesReplacementIsNotAssumedImplemented()
    {
        var catalog = Catalog();
        var character = Character(catalog);
        var original = Origin();
        var profile = original.Profile!;
        var replaced = profile with { Current = new("species.elf", null, []) };

        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, EffectInput(character, original with { Profile = replaced }),
            new TrustedEffects(), new TrustedCharacter());

        Assert.False(result.HasCalculatedValues);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-bridge-form-policy-unimplemented");
    }

    [Fact]
    public void CatalogMismatchCannotProjectUnverifiedNumbers()
    {
        var catalog = Catalog();
        var character = Character(catalog) with { Fingerprint = "outdated" };
        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, EffectInput(character, Origin(), StrengthModifier()),
            new TrustedEffects(), new TrustedCharacter());

        Assert.False(result.HasCalculatedValues);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-bridge-catalog-mismatch");
    }

    [Fact]
    public void FailedNativeCharacterProjectionWithholdsPartialValues()
    {
        var catalog = Catalog();
        var character = Character(catalog) with
        {
            AbilityScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["strength"] = 12, ["dexterity"] = 10
            }
        };
        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, EffectInput(character, Origin(), StrengthModifier()),
            new TrustedEffects(), new TrustedCharacter());

        Assert.False(result.HasCalculatedValues);
        Assert.Empty(result.Values);
        Assert.Contains(result.Issues, issue => issue.Code == "ability-required");
    }

    [Fact]
    public void ConstitutionModificationRequiresAnExplicitHitPointPolicy()
    {
        var catalog = Catalog();
        var character = Character(catalog);
        var effect = StrengthModifier() with
        {
            Id = "constitution-modification",
            TargetId = "constitution"
        };
        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, EffectInput(character, Origin(), effect),
            new TrustedEffects(), new TrustedCharacter());

        Assert.False(result.HasCalculatedValues);
        Assert.Empty(result.Values);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-bridge-hitpoints-policy-unimplemented");
    }

    [Fact]
    public void MissingIndependentGrantEntitlementWithholdsResult()
    {
        var catalog = Catalog();
        var character = Character(catalog);
        var result = new NativeEffectiveCharacterProjectionBridge(catalog).Project(
            character, EffectInput(character, Origin(withGrant: false), StrengthModifier()),
            new TrustedEffects(), new TrustedCharacter());

        Assert.False(result.HasCalculatedValues);
        Assert.Empty(result.Values);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effective-directive-unauthorized");
    }
}
