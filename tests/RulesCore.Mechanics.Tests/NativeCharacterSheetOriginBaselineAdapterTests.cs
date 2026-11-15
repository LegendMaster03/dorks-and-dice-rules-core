using System.Text.Json;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Exercises a synthetic trusted Origin record and newest-baseline consumption;
/// it does not simulate or authorize a Rite, source migration or native form.
/// </summary>
public sealed class NativeCharacterSheetOriginBaselineAdapterTests
{
    private sealed class Trust : INativeCharacterOriginBaselineAuthority
    {
        public bool DenyBaseline { get; init; }
        public bool DenyGrant { get; init; }

        public bool AuthorizesBaseline(string characterId, NativeOriginProfile profile) =>
            characterId == "character.one" &&
            profile.Id == "profile.authoritative" && !DenyBaseline;
        public bool AuthorizesGrant(string characterId, NativeOriginProfile profile,
            NativeOriginEffectiveGrant grant) =>
            characterId == "character.one" &&
            grant.Id.StartsWith("grant.walk.", StringComparison.Ordinal) &&
            grant.Identity.Occurrence.Source.Id == "source.verified" && !DenyGrant;
    }

    private sealed class SheetTrust : INativeCharacterSheetAuthority
    {
        public bool AuthorizesPrimary(string characterId, NativeCharacterRequest primary) =>
            characterId == "character.one";
        public bool AuthorizesSheet(string characterId, NativePersistentCharacterSheet sheet) =>
            characterId == "character.one";
        public bool AuthorizesProgression(string characterId, string ownerSheetId,
            NativeSheetEarnedProgression progression) => characterId == "character.one";
        public bool AuthorizesGrant(string characterId, string ownerSheetId,
            NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant) =>
            characterId == "character.one";
        public bool AuthorizesApplicability(string characterId, string sheetId,
            NativeSheetGrantApplicability decision) => characterId == "character.one";
    }

    private static NativeOriginSourceReference Source() =>
        new(NativeOriginSourceKind.Native, "source.verified", "v1");

    private static NativeOriginEffectiveGrant Walk(int speed) => new(
        "grant.walk." + speed,
        "manifest.walk." + speed,
        new NativeOriginCharacteristicIdentity(NativeOriginCharacteristicKind.Intrinsic,
            new(Source(), "/intrinsics/walkingSpeedFeet"),
            MechanicalDefinitionId: "trait.synthetic.walk", IntrinsicSlotId: "walkingSpeedFeet"),
        ["candidate.walk"],
        EffectiveValue: JsonSerializer.SerializeToElement(speed),
        Authorization: new(NativeOriginAdjudication.Supported));

    private static NativeOriginCurrentConfiguration Config(params NativeOriginEffectiveGrant[] grants) =>
        new("species.synthetic", null, grants, "configuration.authoritative");

    private static NativeOriginProfile Profile(int baseline, int? oldCurrent = null) =>
        new("profile.authoritative",
            Config(Walk(baseline)),
            Config(Walk(oldCurrent ?? baseline)),
            [], [],
            new(NativeOriginAdjudication.Supported),
            ActiveTemporaryTransformation: oldCurrent is null ? null :
                new("activation.wolf", "wolf", Source(), "receipt.activation"));

    private static NativeCharacterState State() => new(
        "character.one",
        new NativeCharacterRequest("first-party-001", "synthetic",
            new Dictionary<string, int>
            {
                ["strength"] = 10, ["dexterity"] = 12, ["constitution"] = 14,
                ["intelligence"] = 8, ["wisdom"] = 15, ["charisma"] = 11
            }, [new("class.synthetic", 1, "class.level.1")], BaseWalkSpeed: 20),
        [
            new("wolf", NativeCharacterSheetResolver.PrimarySheetId, "state.synthetic.wolf",
                Source(), "receipt.wolf")
        ]);

    [Fact]
    public void UpdatedUnderlyingOriginIsReadWithoutRestoringHistoricalOrCurrentForm()
    {
        var state = State();
        var initial = NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed(
            state, Profile(30), new Trust());
        var newProfile = Profile(45, oldCurrent: 10);
        var reconstructed = NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed(
            state, newProfile, new Trust());

        Assert.Equal("resolved", initial.Status);
        Assert.Equal("resolved", reconstructed.Status);
        Assert.Equal(30, initial.State!.PrimaryMechanics.BaseWalkSpeed);
        Assert.Equal(45, reconstructed.State!.PrimaryMechanics.BaseWalkSpeed);
        Assert.Equal(20, state.PrimaryMechanics.BaseWalkSpeed);
        Assert.NotSame(state.AlternateSheets, reconstructed.State.AlternateSheets);
        Assert.Single(reconstructed.State.PrimaryMechanics.Advancements);
        var projected = NativeCharacterSheetResolver.Resolve(
            reconstructed.State, "wolf", new SheetTrust());
        Assert.Equal("resolved", projected.Status);
        Assert.Equal(45, projected.EffectiveMechanics!.BaseWalkSpeed);
    }

    [Fact]
    public void UnverifiedOriginOrGrantNeverChangesTheInheritedPrimaryRequest()
    {
        var state = State();
        var forbiddenProfile = NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed(
            state, Profile(45), new Trust { DenyBaseline = true });
        var forbiddenGrant = NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed(
            state, Profile(45), new Trust { DenyGrant = true });
        Assert.Equal("unresolved", forbiddenProfile.Status);
        Assert.Equal("unresolved", forbiddenGrant.Status);
        Assert.Null(forbiddenProfile.State);
        Assert.Null(forbiddenGrant.State);
        Assert.Equal(20, state.PrimaryMechanics.BaseWalkSpeed);
    }

    [Fact]
    public void DuplicateAndMissingOriginWalkingSpeedGrantsFailClosed()
    {
        var state = State();
        var original = Profile(30);
        var duplicate = original with
        {
            Underlying = Config(Walk(30), Walk(45))
        };
        var none = original with
        {
            Underlying = Config()
        };
        Assert.Null(NativeCharacterSheetOriginBaselineAdapter
            .ApplyWalkSpeed(state, duplicate, new Trust()).State);
        Assert.Null(NativeCharacterSheetOriginBaselineAdapter
            .ApplyWalkSpeed(state, none, new Trust()).State);
    }
    [Fact]
    public void DerivedStateDoesNotAliasHostOwnedProgressionChoicesOrOverrides()
    {
        var original = State();
        var scores = original.PrimaryMechanics.AbilityScores.ToDictionary(
            pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var choices = new Dictionary<string, string> { ["path"] = "original" };
        var grantList = new List<NativeSheetEarnedGrant>
        {
            new("grant.wolf", "trait.wolf", new(Source(), "/trait"),
                "receipt.trait")
        };
        var progression = new NativeSheetEarnedProgression(
            "progression.wolf", "advancement.wolf", new(Source(), "/progression"),
            "receipt.progression", grantList, choices);
        var overrideScores = new Dictionary<string, int> { ["strength"] = 18 };
        var sheet = original.AlternateSheets[0] with
        {
            Progression = new List<NativeSheetEarnedProgression> { progression },
            Overrides = new(AbilityScores: new(false, overrideScores))
        };
        var sheets = new List<NativePersistentCharacterSheet> { sheet };
        var input = original with
        {
            PrimaryMechanics = original.PrimaryMechanics with { AbilityScores = scores },
            AlternateSheets = sheets
        };
        var derived = NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed(
            input, Profile(40), new Trust());
        Assert.Equal("resolved", derived.Status);
        var before = JsonSerializer.Serialize(derived.State);

        // These mutations simulate a host update after the read projection.
        scores["wisdom"] = 100;
        overrideScores["strength"] = 2;
        choices["path"] = "later";
        grantList[0] = grantList[0] with { DefinitionId = "trait.modified" };
        sheets.Clear();

        Assert.Equal(before, JsonSerializer.Serialize(derived.State));
        Assert.Equal(40, derived.State!.PrimaryMechanics.BaseWalkSpeed);
        Assert.Single(derived.State.AlternateSheets);
        Assert.Equal("original", derived.State.AlternateSheets[0].Progression![0].Choices!["path"]);
    }

    [Fact]
    public void MalformedSheetDoesNotCauseOriginAdapterToThrow()
    {
        var original = State();
        var broken = original.AlternateSheets[0] with
        {
            Progression = [new("bad", "advancement.bad", new(Source(), "/bad"),
                "receipt.bad", null!)]
        };
        var state = original with { AlternateSheets = [broken] };
        var result = NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed(
            state, Profile(40), new Trust());
        Assert.Equal("unresolved", result.Status);
        Assert.Null(result.State);
        Assert.Contains(result.Issues, issue => issue.Code == "invalid-character-state");
    }

}
