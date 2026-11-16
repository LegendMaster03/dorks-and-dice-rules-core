using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// The Origin-only free-cast ledger is an immutable calculation. Tests do not
/// represent authenticated storage or actual casting. The native character
/// projection supplies known level and proficiency; grant IDs own separate
/// free-use pools and one event is checked against the expected revision.
/// </summary>
public sealed class NativeOriginInnateSpellResourceLedgerTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var root = Path.Combine(dir.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001"));
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Native ruleset unavailable.");
    }

    private static NativeOriginConstructionResult Direct(
        NativeCatalogSnapshot catalog, string ancestry,
        string spellcastingAbility = "wisdom")
    {
        var proposal = new NativeOriginProposal("ledger-" + ancestry,
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: ancestry);
        var engine = new NativeOriginConstructionEngine(catalog);
        var discovered = engine.Evaluate(new(proposal,
            AutoManifestDirectDefaults: true)).EvaluatedProposal.Candidates;
        var manifests = new List<NativeOriginManifestation>();
        var size = discovered.SingleOrDefault(x =>
            x.Identity.IntrinsicSlotId == "size" && x.RequiredChoices is { Count: > 0 });
        if (size is not null)
            manifests.Add(new("size", size.Id,
                Choices: [new(size.RequiredChoices!.Single().Id, ["medium"])]));
        var ability = discovered.SingleOrDefault(x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        if (ability is not null)
            manifests.Add(new("ability", ability.Id,
                Choices: [new(ability.RequiredChoices!.Single().Id,
                    [spellcastingAbility])]));
        return engine.Evaluate(new(proposal with
        {
            Manifestations = manifests
        }, AutoManifestDirectDefaults: true));
    }

    private static NativeCharacterProjection Character(
        NativeCatalogSnapshot catalog, int level) =>
        new NativeCharacterProjector(catalog).Project(new NativeCharacterRequest(
            catalog.RulesetId, catalog.Fingerprint,
            new Dictionary<string, int>
            {
                ["strength"] = 10, ["dexterity"] = 12, ["constitution"] = 14,
                ["intelligence"] = 8, ["wisdom"] = 15, ["charisma"] = 11
            }, [new("class.warlock", level, "warlock")]));

    private static NativeOriginInnateSpellLedgerResult Step(
        NativeCatalogSnapshot catalog, NativeOriginConstructionResult origin,
        NativeCharacterProjection character, NativeOriginInnateSpellLedgerState state,
        NativeOriginInnateSpellLedgerActionKind kind, string? grantId = null,
        long? expectedRevision = null) =>
        NativeOriginInnateSpellResourceLedger.Apply(catalog, origin, character,
            state, new(kind, expectedRevision ?? state.Revision, grantId));

    private static string Grant(NativeOriginConstructionResult origin, string id) =>
        Assert.Single(origin.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == id).Id;

    [Fact]
    public void LevelThreeCanSpendHellishRebukeOnlyOnceUntilLongRest()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, origin.Status);
        var character = Character(catalog, 3);
        var fresh = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, character);
        Assert.Equal("resolved", fresh.Status);
        var ledger = fresh.State!;
        Assert.Equal(0, ledger.Revision);
        var hellish = Grant(origin, "trait.tiefling.infernal.hellish-rebuke");
        var darkness = Grant(origin, "trait.tiefling.infernal.darkness");
        Assert.Single(ledger.RemainingFreeCasts);
        Assert.Equal(1, ledger.RemainingFreeCasts[hellish]);
        Assert.False(ledger.RemainingFreeCasts.ContainsKey(darkness));

        var spent = Step(catalog, origin, character, ledger,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, hellish);
        Assert.Equal("resolved", spent.Status);
        Assert.Equal(0, spent.Remaining);
        Assert.Equal(1, spent.Maximum);
        Assert.Equal(1, spent.State!.Revision);
        Assert.Equal(0, spent.State.RemainingFreeCasts[hellish]);
        Assert.Equal(1, ledger.RemainingFreeCasts[hellish]); // immutable

        var exhausted = Step(catalog, origin, character, spent.State,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, hellish);
        Assert.Equal("insufficient-free-casts", exhausted.Status);
        Assert.Equal(0, exhausted.State!.RemainingFreeCasts[hellish]);
        Assert.Equal(spent.State.Revision, exhausted.State.Revision);

        var shortRest = Step(catalog, origin, character, spent.State,
            NativeOriginInnateSpellLedgerActionKind.ShortRest);
        Assert.Equal("resolved", shortRest.Status);
        Assert.Equal(0, shortRest.State!.RemainingFreeCasts[hellish]);

        var longRest = Step(catalog, origin, character, shortRest.State,
            NativeOriginInnateSpellLedgerActionKind.LongRest);
        Assert.Equal("resolved", longRest.Status);
        Assert.Equal(1, longRest.State!.RemainingFreeCasts[hellish]);
        Assert.Equal(3, longRest.State.Revision);
        Assert.Equal("resolved", Step(catalog, origin, character, longRest.State,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, hellish).Status);
    }

    [Fact]
    public void DistinctInnateSpellsHaveSeparatePoolsAndRestResetsBoth()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var character = Character(catalog, 5);
        var ledger = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, character).State!;
        var hellish = Grant(origin, "trait.tiefling.infernal.hellish-rebuke");
        var darkness = Grant(origin, "trait.tiefling.infernal.darkness");
        Assert.Equal(2, ledger.RemainingFreeCasts.Count);
        Assert.Equal(1, ledger.RemainingFreeCasts[hellish]);
        Assert.Equal(1, ledger.RemainingFreeCasts[darkness]);
        var first = Step(catalog, origin, character, ledger,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, hellish).State!;
        Assert.Equal(0, first.RemainingFreeCasts[hellish]);
        Assert.Equal(1, first.RemainingFreeCasts[darkness]);
        var second = Step(catalog, origin, character, first,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, darkness).State!;
        Assert.All(second.RemainingFreeCasts.Values, x => Assert.Equal(0, x));
        var restored = Step(catalog, origin, character, second,
            NativeOriginInnateSpellLedgerActionKind.LongRest).State!;
        Assert.All(restored.RemainingFreeCasts.Values, x => Assert.Equal(1, x));
    }

    [Fact]
    public void SlotRequestsAndAtWillChecksNeverSpendOriginFreeUses()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var character = Character(catalog, 5);
        var ledger = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, character).State!;
        var hellish = Grant(origin, "trait.tiefling.infernal.hellish-rebuke");
        var atWill = Grant(origin, "trait.tiefling.otherworldly-presence");

        var slot = Step(catalog, origin, character, ledger,
            NativeOriginInnateSpellLedgerActionKind.RequestSlotCast, hellish);
        Assert.Equal("external-slot-required", slot.Status);
        Assert.Equal(ledger, slot.State);
        Assert.Equal(0, slot.State!.Revision);
        Assert.Equal(1, slot.State.RemainingFreeCasts[hellish]);
        Assert.Equal("unresolved", Step(catalog, origin, character, ledger,
            NativeOriginInnateSpellLedgerActionKind.RequestSlotCast, atWill).Status);

        var cantrip = Step(catalog, origin, character, ledger,
            NativeOriginInnateSpellLedgerActionKind.CheckAtWill, atWill);
        Assert.Equal("permitted", cantrip.Status);
        Assert.Equal(ledger, cantrip.State);
        Assert.Equal("unresolved", Step(catalog, origin, character, ledger,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, atWill).Status);
        Assert.Equal("unresolved", Step(catalog, origin, character, ledger,
            NativeOriginInnateSpellLedgerActionKind.CheckAtWill, hellish).Status);
    }

    [Fact]
    public void RevisionAndEntitlementCannotBeReplayedOrForged()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var character = Character(catalog, 3);
        var ledger = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, character).State!;
        var hellish = Grant(origin, "trait.tiefling.infernal.hellish-rebuke");
        var used = Step(catalog, origin, character, ledger,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, hellish).State!;
        Assert.Equal("spell-ledger-revision-mismatch",
            Step(catalog, origin, character, used,
                NativeOriginInnateSpellLedgerActionKind.SpendFreeCast,
                hellish, expectedRevision: 0).Reason);

        foreach (var altered in new[]
        {
            ledger with { RemainingFreeCasts =
                new Dictionary<string, int> { [hellish] = 3 } },
            ledger with { RemainingFreeCasts =
                new Dictionary<string, int> { [hellish] = -1 } },
            ledger with { RemainingFreeCasts =
                new Dictionary<string, int> { ["other"] = 1 } },
            ledger with { RemainingFreeCasts =
                new Dictionary<string, int>() },
            ledger with { ProjectionFingerprint = "forged" },
            ledger with { CatalogFingerprint = "forged" },
            ledger with { OriginProfileId = "different-profile" },
            ledger with { ProposalId = "different-proposal" },
            ledger with { CharacterLevel = 5 }
        })
        {
            var result = Step(catalog, origin, character, altered,
                NativeOriginInnateSpellLedgerActionKind.LongRest);
            Assert.Equal("unresolved", result.Status);
            Assert.Null(result.State);
        }

        var saved = JsonSerializer.Deserialize<NativeOriginInnateSpellLedgerState>(
            JsonSerializer.Serialize(used))!;
        var again = Step(catalog, origin, character, saved,
            NativeOriginInnateSpellLedgerActionKind.LongRest);
        Assert.Equal("resolved", again.Status);
        Assert.Equal(1, again.State!.RemainingFreeCasts[hellish]);
        Assert.Equal(2, again.State.Revision);
    }

    [Fact]
    public void AdvancementNeedsExplicitLedgerMigrationRatherThanImplicitRefill()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var levelThree = Character(catalog, 3);
        var ledger = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, levelThree).State!;
        var hellish = Grant(origin, "trait.tiefling.infernal.hellish-rebuke");
        var spent = Step(catalog, origin, levelThree, ledger,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, hellish).State!;
        var atFive = Step(catalog, origin, Character(catalog, 5), spent,
            NativeOriginInnateSpellLedgerActionKind.LongRest);
        Assert.Equal("unresolved", atFive.Status);
        Assert.Equal("spell-ledger-snapshot-mismatch", atFive.Reason);
        Assert.Null(atFive.State);
    }

    [Fact]
    public void ForestGnomeFreeUseAmountIsBasedOnResolvedProficiencyBonus()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.gnome.forest");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, origin.Status);
        var character = Character(catalog, 5);
        var proficiency = character.Values["proficiency.bonus"].Value;
        Assert.True(proficiency is > 1);
        var initialized = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, character);
        Assert.Equal("resolved", initialized.Status);
        var animal = Grant(origin, "trait.gnome.forest.animal-speech");
        var ledger = initialized.State!;
        Assert.Equal(proficiency, ledger.RemainingFreeCasts[animal]);
        var result = Step(catalog, origin, character, ledger,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, animal);
        Assert.Equal(proficiency - 1, result.State!.RemainingFreeCasts[animal]);

        var noProficiency = character with
        {
            Values = character.Values.Where(x => x.Key != "proficiency.bonus")
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
        };
        var unresolved = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, noProficiency);
        Assert.Equal("unresolved", unresolved.Status);
        Assert.Null(unresolved.State);
    }

    [Fact]
    public void LockedSpellsAndUnresolvedSpeciesAreNeverSpendable()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var character = Character(catalog, 1);
        var state = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, character).State!;
        Assert.Empty(state.RemainingFreeCasts);
        var hellish = Grant(origin, "trait.tiefling.infernal.hellish-rebuke");
        var attempt = Step(catalog, origin, character, state,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, hellish);
        Assert.Equal("spell-ledger-spell-locked", attempt.Reason);
        Assert.Null(attempt.State);

        var wood = Direct(catalog, "ancestry.elf.wood");
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, wood.Status);
        var blocked = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, wood, Character(catalog, 5));
        Assert.Equal("unresolved", blocked.Status);
        Assert.Null(blocked.State);
    }
}
