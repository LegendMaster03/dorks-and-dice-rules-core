using System.Text.Json;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Synthetic inheritance cases, not approved lycanthropy rules or entitlement
/// issuers. The verifier imitates the trusted host's separately checked receipts.
/// </summary>
public sealed class NativeCharacterSheetResolverTests
{
    private sealed class SyntheticAuthority : INativeCharacterSheetAuthority
    {
        public bool DenyPrimary { get; set; }
        public HashSet<string> DeniedApplicability { get; } = new(StringComparer.Ordinal);
        public HashSet<string> DeniedSheets { get; } = new(StringComparer.Ordinal);
        public HashSet<string> DeniedProgression { get; } = new(StringComparer.Ordinal);
        public HashSet<string> DeniedGrants { get; } = new(StringComparer.Ordinal);

        public bool AuthorizesPrimary(string characterId, NativeCharacterRequest primary) =>
            characterId == "character.one" && !DenyPrimary;
        public bool AuthorizesApplicability(string characterId, string sheetId,
            NativeSheetGrantApplicability decision) =>
            characterId == "character.one" && !DeniedApplicability.Contains(decision.GrantId);
        public bool AuthorizesSheet(string characterId, NativePersistentCharacterSheet sheet) =>
            characterId == "character.one" && !DeniedSheets.Contains(sheet.Id);
        public bool AuthorizesProgression(string characterId, string ownerSheetId,
            NativeSheetEarnedProgression progression) =>
            characterId == "character.one" && !DeniedProgression.Contains(progression.Id);
        public bool AuthorizesGrant(string characterId, string ownerSheetId,
            NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant) =>
            characterId == "character.one" && !DeniedGrants.Contains(grant.Id);
    }

    private static NativeOriginSourceReference Source(string id = "source.lycanthropy") =>
        new(NativeOriginSourceKind.Native, id, "synthetic-v1");

    private static NativeOriginSourceOccurrence Occurrence(string path = "progression[0]") =>
        new(Source(), path);

    private static NativeOriginDecisionBasis Allowed() =>
        new(NativeOriginAdjudication.Supported, NativeRuleReference: "synthetic-rule");

    private static NativeSheetEarnedProgression Earn(
        string id, string grantId, string definitionId = "trait.synthetic.sense",
        string occurrence = "progression[0]") =>
        new(id, "advancement." + id, Occurrence(occurrence), "event." + id,
        [
            new(grantId, definitionId, Occurrence(occurrence + ".grant"),
                "event." + grantId)
        ],
        new Dictionary<string, string> { ["selected"] = "synthetic-choice" });

    private static NativePersistentCharacterSheet Werewolf() => new(
        "werewolf", NativeCharacterSheetResolver.PrimarySheetId,
        "state.synthetic.werewolf", Source(), "event.werewolf",
        Progression: [Earn("shared", "grant.shared")],
        GrantApplicability: [new("grant.shared", NativeOriginGrantApplicabilityState.Applicable, Allowed())]);

    private static NativePersistentCharacterSheet Wolf() => new(
        "wolf", "werewolf", "state.synthetic.wolf", Source(), "event.wolf",
        Overrides: new(
            AbilityScores: new(false, new Dictionary<string, int> { ["strength"] = 17 }),
            Skills: new(false, new Dictionary<string, NativeSheetTrainingPatch>
            {
                ["skill.listen"] = new(Proficient: new(true))
            }),
            CurrentResources: new(false, new Dictionary<string, int>
            {
                ["resource.wolf"] = 3
            })),
        Progression: [Earn("wolf", "grant.wolf", "trait.synthetic.pounce", "progression[1]")],
        GrantApplicability: [new("grant.wolf", NativeOriginGrantApplicabilityState.Applicable, Allowed())]);

    private static NativePersistentCharacterSheet Hybrid() => new(
        "hybrid", "werewolf", "state.synthetic.hybrid", Source(), "event.hybrid",
        Overrides: new(
            AbilityScores: new(false, new Dictionary<string, int> { ["dexterity"] = 15 })));

    private static NativeCharacterState State(
        IReadOnlyList<NativePersistentCharacterSheet>? sheets = null) => new(
        "character.one",
        new NativeCharacterRequest(
            "first-party-001", "synthetic-catalog",
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["strength"] = 10, ["dexterity"] = 12, ["constitution"] = 13,
                ["intelligence"] = 14, ["wisdom"] = 15, ["charisma"] = 16
            },
            [new NativeAdvancement("class.synthetic", 1, "class-level.1")],
            Skills: new Dictionary<string, NativeSkillTraining>
            {
                ["skill.listen"] = new(Ranks: 3, Proficient: false, MiscModifier: 2,
                    Ability: "wisdom"),
                ["skill.spot"] = new(Ranks: 2, Ability: "wisdom")
            },
            DefenseContributions: new Dictionary<string, int> { ["natural-armor"] = 1 },
            SelectedOptions: ["option.original"],
            CurrentResources: new Dictionary<string, int> { ["resource.primary"] = 8 },
            BaseWalkSpeed: 30),
        sheets ?? [Werewolf(), Wolf(), Hybrid()]);

    private static NativeCharacterSheetResolution Resolve(NativeCharacterState state,
        string target = "wolf", SyntheticAuthority? authority = null) =>
        NativeCharacterSheetResolver.Resolve(state, target, authority ?? new SyntheticAuthority());

    [Fact]
    public void PersistentSheetsResolveWithoutAnyManifestationOrClonedClassProgression()
    {
        var state = State(); // No activation or temporary transformation record exists.
        var primary = Resolve(state, NativeCharacterSheetResolver.PrimarySheetId);
        var wolf = Resolve(state);
        var hybrid = Resolve(state, "hybrid");

        Assert.Equal("resolved", primary.Status);
        Assert.Empty(primary.Progression);
        Assert.Equal("resolved", wolf.Status);
        Assert.Equal("resolved", hybrid.Status);
        Assert.Equal(2, wolf.Progression.Count);
        Assert.Single(hybrid.Progression);
        Assert.Equal("werewolf", hybrid.Progression[0].OwnerSheetId);
        Assert.Equal("werewolf", wolf.Progression[0].OwnerSheetId);
        Assert.Equal("wolf", wolf.Progression[1].OwnerSheetId);
        Assert.Equal(state.PrimaryMechanics.Advancements, wolf.EffectiveMechanics!.Advancements);
        Assert.Equal(state.PrimaryMechanics.Advancements, hybrid.EffectiveMechanics!.Advancements);
    }

    [Fact]
    public void SharedAndLocalProgressionPreserveOneOwnerPerGrant()
    {
        var wolf = Resolve(State());
        var hybrid = Resolve(State(), "hybrid");
        Assert.Equal(new[] { "grant.shared", "grant.wolf" },
            wolf.Grants.Select(grant => grant.Entitlement.Id));
        Assert.Equal(new[] { "grant.shared" },
            hybrid.Grants.Select(grant => grant.Entitlement.Id));
        Assert.All(wolf.Grants, grant =>
            Assert.Equal(NativeOriginGrantApplicabilityState.Applicable, grant.Applicability));
        Assert.Equal("werewolf", wolf.Grants[0].OwnerSheetId);
        Assert.Equal("wolf", wolf.Grants[1].OwnerSheetId);
        Assert.Equal("trait.synthetic.sense", wolf.Grants[0].Entitlement.DefinitionId);
    }

    [Fact]
    public void LateAuthoritativeClassAndBaselineChangesFlowWithoutRebuildingSheets()
    {
        var state = State();
        var before = Resolve(state);
        var changedScores = state.PrimaryMechanics.AbilityScores
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        changedScores["wisdom"] = 18;
        var changed = state with
        {
            PrimaryMechanics = state.PrimaryMechanics with
            {
                AbilityScores = changedScores,
                Advancements =
                [
                    new NativeAdvancement("class.synthetic", 1, "class-level.1"),
                    new NativeAdvancement("class.synthetic", 2, "class-level.2")
                ],
                BaseWalkSpeed = 40
            }
        };
        var after = Resolve(changed);
        Assert.Same(state.AlternateSheets, changed.AlternateSheets);
        Assert.Single(before.EffectiveMechanics!.Advancements);
        Assert.Equal(2, after.EffectiveMechanics!.Advancements.Count);
        Assert.Equal(18, after.EffectiveMechanics.AbilityScores["wisdom"]);
        Assert.Equal(17, after.EffectiveMechanics.AbilityScores["strength"]);
        Assert.Equal(40, after.EffectiveMechanics.BaseWalkSpeed);
        Assert.Equal(30, before.EffectiveMechanics.BaseWalkSpeed);
    }

    [Fact]
    public void NestedTrainingOverrideInheritsItsSiblingFields()
    {
        var training = Resolve(State()).EffectiveMechanics!.Skills!["skill.listen"];
        Assert.Equal(3, training.Ranks);
        Assert.True(training.Proficient);
        Assert.Equal(2, training.MiscModifier);
        Assert.Equal("wisdom", training.Ability);
        Assert.Equal(2, Resolve(State()).EffectiveMechanics!.Skills!["skill.spot"].Ranks);
    }

    [Fact]
    public void WholeTrainingObjectAndMapReplacementHaveExplicitBoundaries()
    {
        var state = State();
        var replaced = Wolf() with
        {
            Overrides = new(
                Skills: new(true, new Dictionary<string, NativeSheetTrainingPatch>
                {
                    ["skill.listen"] = new(
                        Replacement: new(new NativeSkillTraining(Ranks: 7, Ability: "strength")))
                }),
                AbilityScores: new(false, new Dictionary<string, int> { ["strength"] = 17 }))
        };
        var changed = state with { AlternateSheets = [Werewolf(), replaced, Hybrid()] };
        var result = Resolve(changed);
        Assert.Equal("resolved", result.Status);
        Assert.Single(result.EffectiveMechanics!.Skills!);
        Assert.False(result.EffectiveMechanics.Skills!.ContainsKey("skill.spot"));
        Assert.Equal(7, result.EffectiveMechanics.Skills["skill.listen"].Ranks);
        Assert.False(result.EffectiveMechanics.Skills["skill.listen"].Proficient);
        Assert.Equal(0, result.EffectiveMechanics.Skills["skill.listen"].MiscModifier);
        Assert.Equal("strength", result.EffectiveMechanics.Skills["skill.listen"].Ability);
        Assert.Equal(15, result.EffectiveMechanics.AbilityScores["wisdom"]);
    }

    [Fact]
    public void WholeAbilityCollectionReplacementDoesNotRetainExcludedMembers()
    {
        var wolf = Wolf() with
        {
            Overrides = new(AbilityScores: new(true, new Dictionary<string, int>
            {
                ["strength"] = 19
            }))
        };
        var result = Resolve(State([Werewolf(), wolf, Hybrid()]));
        Assert.Single(result.EffectiveMechanics!.AbilityScores);
        Assert.False(result.EffectiveMechanics.AbilityScores.ContainsKey("wisdom"));
    }

    [Fact]
    public void SiblingSheetsDoNotShareOverrideOrResourceState()
    {
        var state = State();
        var wolf = Resolve(state);
        var hybrid = Resolve(state, "hybrid");
        Assert.Equal(17, wolf.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(10, hybrid.EffectiveMechanics!.AbilityScores["strength"]);
        Assert.Equal(15, hybrid.EffectiveMechanics.AbilityScores["dexterity"]);
        Assert.Equal(12, wolf.EffectiveMechanics.AbilityScores["dexterity"]);
        Assert.Equal(3, wolf.EffectiveMechanics.CurrentResources!["resource.wolf"]);
        Assert.False(hybrid.EffectiveMechanics.CurrentResources!.ContainsKey("resource.wolf"));
        Assert.Equal(8, hybrid.EffectiveMechanics.CurrentResources["resource.primary"]);
        Assert.False(state.PrimaryMechanics.CurrentResources!.ContainsKey("resource.wolf"));
    }

    [Fact]
    public void SuppressionChangesApplicabilityButDoesNotDeleteEarnedEntitlement()
    {
        var wolf = Wolf() with
        {
            GrantApplicability =
            [
                new("grant.shared", NativeOriginGrantApplicabilityState.Suppressed, Allowed()),
                new("grant.wolf", NativeOriginGrantApplicabilityState.Applicable, Allowed())
            ]
        };
        var state = State([Werewolf(), wolf, Hybrid()]);
        var suppressed = Resolve(state);
        var available = Resolve(state, "hybrid");
        Assert.Equal(NativeOriginGrantApplicabilityState.Suppressed, suppressed.Grants[0].Applicability);
        Assert.Equal(NativeOriginGrantApplicabilityState.Applicable, available.Grants[0].Applicability);
        Assert.Equal("werewolf", suppressed.Grants[0].OwnerSheetId);
        Assert.Single(available.Grants);
        Assert.Equal(2, suppressed.Progression.Count);
    }

    [Theory]
    [InlineData("missing-parent", "invalid-sheet-parent")]
    [InlineData("self-cycle", "sheet-inheritance-cycle")]
    [InlineData("two-cycle", "sheet-inheritance-cycle")]
    [InlineData("duplicate-progress", "duplicate-progression-id")]
    [InlineData("duplicate-grant", "duplicate-grant-id")]
    [InlineData("invalid-applicability", "unknown-applicability-grant")]
    public void CorruptOrAmbiguousGraphFailsClosed(string caseName, string expectedCode)
    {
        var werewolf = Werewolf();
        var wolf = Wolf();
        var hybrid = Hybrid();
        switch (caseName)
        {
            case "missing-parent": wolf = wolf with { ParentId = "missing" }; break;
            case "self-cycle": wolf = wolf with { ParentId = "wolf" }; break;
            case "two-cycle":
                wolf = wolf with { ParentId = "hybrid" };
                hybrid = hybrid with { ParentId = "wolf" };
                break;
            case "duplicate-progress":
                hybrid = hybrid with { Progression = [Earn("wolf", "grant.other")] };
                break;
            case "duplicate-grant":
                hybrid = hybrid with { Progression = [Earn("distinct", "grant.wolf")] };
                break;
            case "invalid-applicability":
                hybrid = hybrid with
                {
                    GrantApplicability =
                    [
                        new("grant.wolf", NativeOriginGrantApplicabilityState.Applicable, Allowed())
                    ]
                };
                break;
        }
        var result = Resolve(State([werewolf, wolf, hybrid]));
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Empty(result.Progression);
        Assert.Empty(result.Grants);
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void MissingTargetAndDuplicateSheetIdentityAreReported()
    {
        var missing = Resolve(State(), "other");
        Assert.Equal("invalid", missing.Status);
        Assert.Contains(missing.Issues, issue => issue.Code == "missing-target-sheet");
        var duplicate = Resolve(State([Werewolf(), Wolf(), Wolf()]));
        Assert.Equal("invalid", duplicate.Status);
        Assert.Contains(duplicate.Issues, issue => issue.Code == "duplicate-sheet-id");
    }

    [Fact]
    public void UnverifiedSheetProgressionAndGrantAreNeverProjected()
    {
        var authority = new SyntheticAuthority();
        authority.DeniedSheets.Add("wolf");
        authority.DeniedProgression.Add("shared");
        authority.DeniedGrants.Add("grant.wolf");
        var result = Resolve(State(), "wolf", authority);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Contains(result.Issues, issue => issue.Code == "unauthorized-sheet");
        Assert.Contains(result.Issues, issue => issue.Code == "unauthorized-progression");
        Assert.Contains(result.Issues, issue => issue.Code == "unauthorized-grant");
    }

    [Fact]
    public void CanonicalDefinitionAndSourceOccurrenceDoNotReplaceDurableGrantIdentity()
    {
        var hybrid = Hybrid() with
        {
            Progression =
            [
                Earn("different-source", "grant.other", "trait.synthetic.sense",
                    "another-source-occurrence")
            ],
            GrantApplicability =
            [
                new("grant.other", NativeOriginGrantApplicabilityState.Applicable, Allowed())
            ]
        };
        var result = Resolve(State([Werewolf(), Wolf(), hybrid]), "hybrid");
        Assert.Equal("resolved", result.Status);
        Assert.Equal(2, result.Grants.Count);
        Assert.Equal(result.Grants[0].Entitlement.DefinitionId,
            result.Grants[1].Entitlement.DefinitionId);
        Assert.NotEqual(result.Grants[0].Entitlement.Id, result.Grants[1].Entitlement.Id);
        Assert.NotEqual(result.Grants[0].Entitlement.SourceOccurrence,
            result.Grants[1].Entitlement.SourceOccurrence);
    }

    [Fact]
    public void RepeatedResolutionDoesNotMutateAuthoritativeCharacterOrSheetRecords()
    {
        var state = State();
        var before = JsonSerializer.Serialize(state);
        var first = Resolve(state);
        var second = Resolve(state);
        Assert.Equal("resolved", first.Status);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(before, JsonSerializer.Serialize(state));
        Assert.False(state.PrimaryMechanics.AbilityScores["strength"] == 17);
        Assert.Single(state.PrimaryMechanics.Advancements);
    }

    [Fact]
    public void UnspecifiedApplicabilityIsUnresolvedRatherThanAssumedUsable()
    {
        var werewolf = Werewolf() with { GrantApplicability = null };
        var result = Resolve(State([werewolf, Wolf(), Hybrid()]), "hybrid");
        Assert.Equal("resolved", result.Status);
        Assert.Equal(NativeOriginGrantApplicabilityState.Unresolved,
            Assert.Single(result.Grants).Applicability);
    }
    [Fact]
    public void UnauthorizedPrimaryMechanicsAreRejectedBeforeAnyEffectiveProjection()
    {
        var authority = new SyntheticAuthority { DenyPrimary = true };
        var result = Resolve(State(), "wolf", authority);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Contains(result.Issues, i => i.Code == "unauthorized-primary");
    }

    [Fact]
    public void ForgedApplicabilityEvidenceIsRejectedEvenWhenItNamesAnExistingRule()
    {
        var authority = new SyntheticAuthority();
        authority.DeniedApplicability.Add("grant.shared");
        var result = Resolve(State(), "wolf", authority);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.EffectiveMechanics);
        Assert.Contains(result.Issues, i => i.Code == "unauthorized-applicability");
    }

    [Fact]
    public void DetachedProjectionsNeverShareMutableNestedInputCollections()
    {
        var input = State();
        var result = Resolve(input);
        Assert.Equal("resolved", result.Status);
        var before = JsonSerializer.Serialize(result);
        var originalChoices = (Dictionary<string, string>)input.AlternateSheets[1].Progression![0].Choices!;
        originalChoices["selected"] = "changed-after-resolution";
        var originalScores = (Dictionary<string, int>)input.PrimaryMechanics.AbilityScores;
        originalScores["wisdom"] = 100;
        var grants = (NativeSheetEarnedGrant[])input.AlternateSheets[0].Progression![0].Grants;
        grants[0] = grants[0] with { DefinitionId = "trait.changed" };

        Assert.Equal(before, JsonSerializer.Serialize(result));
        Assert.Equal("synthetic-choice", result.Progression[1].Record.Choices!["selected"]);
        Assert.Equal("trait.synthetic.sense", result.Grants[0].Entitlement.DefinitionId);
        Assert.Equal(15, result.EffectiveMechanics!.AbilityScores["wisdom"]);
        Assert.False(result.Progression is List<NativeSheetResolvedProgression>);
        Assert.False(result.Progression is NativeSheetResolvedProgression[]);
    }

    [Theory]
    [InlineData(NativeSheetLevelSource.TotalClassLevels, "class.synthetic")]
    [InlineData(NativeSheetLevelSource.SpecificClassLevel, null)]
    public void InvalidLevelReferenceScopesAreRejected(
        NativeSheetLevelSource source, string? classId)
    {
        var wolf = Wolf();
        var initial = wolf.Progression![0];
        wolf = wolf with
        {
            Progression =
            [
                initial with
                {
                    Grants = [initial.Grants[0] with
                    {
                        LevelReference = new(source, classId)
                    }]
                }
            ]
        };
        var result = Resolve(State([Werewolf(), wolf, Hybrid()]));
        Assert.Equal("invalid", result.Status);
        Assert.Contains(result.Issues, i => i.Code == "invalid-sheet-grant");
    }

}
