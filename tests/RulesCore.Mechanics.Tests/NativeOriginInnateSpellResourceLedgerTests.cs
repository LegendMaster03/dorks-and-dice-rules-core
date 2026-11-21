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
        var hellish = Grant(origin, "trait.hellish-rebuke");
        var darkness = Grant(origin, "trait.darkness");
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
        var hellish = Grant(origin, "trait.hellish-rebuke");
        var darkness = Grant(origin, "trait.darkness");
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
        var hellish = Grant(origin, "trait.hellish-rebuke");
        var atWill = Grant(origin, "trait.otherworldly-presence");

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
        var hellish = Grant(origin, "trait.hellish-rebuke");
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
        var hellish = Grant(origin, "trait.hellish-rebuke");
        var spent = Step(catalog, origin, levelThree, ledger,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, hellish).State!;
        var atFive = Step(catalog, origin, Character(catalog, 5), spent,
            NativeOriginInnateSpellLedgerActionKind.LongRest);
        Assert.Equal("unresolved", atFive.Status);
        Assert.Equal("spell-ledger-snapshot-mismatch", atFive.Reason);
        Assert.Null(atFive.State);
    }

    [Fact]
    public void AdvancementUnlocksNewPoolWithoutRefillingAlreadySpentSpell()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var previous = Character(catalog, 3);
        var advanced = Character(catalog, 5);
        var hellish = Grant(origin, "trait.hellish-rebuke");
        var darkness = Grant(origin, "trait.darkness");
        var initialized = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, previous).State!;
        var spent = Step(catalog, origin, previous, initialized,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, hellish).State!;
        Assert.Equal(0, spent.RemainingFreeCasts[hellish]);
        Assert.DoesNotContain(darkness, spent.RemainingFreeCasts.Keys);

        var migration = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, spent, spent.Revision);
        Assert.Equal("resolved", migration.Status);
        var next = migration.State!;
        Assert.Equal(5, next.CharacterLevel);
        Assert.Equal(spent.Revision + 1, next.Revision);
        Assert.Equal(0, next.RemainingFreeCasts[hellish]);
        Assert.Equal(1, next.RemainingFreeCasts[darkness]);
        Assert.Equal(0, spent.RemainingFreeCasts[hellish]);
        Assert.DoesNotContain(darkness, spent.RemainingFreeCasts.Keys);
        Assert.NotEqual(spent.ProjectionFingerprint, next.ProjectionFingerprint);

        // The old level cannot be used with the migrated state; the
        // newly unlocked spell may be spent independently of the old pool.
        Assert.Equal("spell-ledger-snapshot-mismatch",
            Step(catalog, origin, previous, next,
                NativeOriginInnateSpellLedgerActionKind.LongRest).Reason);
        var darknessSpend = Step(catalog, origin, advanced, next,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, darkness);
        Assert.Equal("resolved", darknessSpend.Status);
        Assert.Equal(0, darknessSpend.State!.RemainingFreeCasts[darkness]);
        Assert.Equal(0, darknessSpend.State.RemainingFreeCasts[hellish]);
        Assert.Equal("insufficient-free-casts",
            Step(catalog, origin, advanced, darknessSpend.State,
                NativeOriginInnateSpellLedgerActionKind.SpendFreeCast,
                hellish).Status);
        var afterRest = Step(catalog, origin, advanced, darknessSpend.State,
            NativeOriginInnateSpellLedgerActionKind.LongRest);
        Assert.Equal("resolved", afterRest.Status);
        Assert.Equal(1, afterRest.State!.RemainingFreeCasts[hellish]);
        Assert.Equal(1, afterRest.State.RemainingFreeCasts[darkness]);
    }

    [Fact]
    public void AdvancementFromLevelOneCreatesFirstUnlockedPoolExactlyOnce()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var first = Character(catalog, 1);
        var third = Character(catalog, 3);
        var initial = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, first).State!;
        Assert.Empty(initial.RemainingFreeCasts);
        var migration = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, first, third, initial, expectedRevision: 0);
        Assert.Equal("resolved", migration.Status);
        Assert.Equal(1, migration.State!.Revision);
        Assert.Single(migration.State.RemainingFreeCasts);
        var hellish = Grant(origin, "trait.hellish-rebuke");
        Assert.Equal(1, migration.State.RemainingFreeCasts[hellish]);
        var replay = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, first, third, migration.State, expectedRevision: 0);
        Assert.Equal("unresolved", replay.Status);
        Assert.Equal("spell-ledger-snapshot-mismatch", replay.Reason);
        Assert.Null(replay.State);
    }

    [Fact]
    public void CharacterAdvancementRejectsStaleRevisionRollbackAndUnrelatedOrigin()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var previous = Character(catalog, 3);
        var advanced = Character(catalog, 5);
        var ledger = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, previous).State!;
        var stale = NativeOriginInnateSpellResourceLedger.Advance(catalog, origin,
            previous, advanced, ledger, expectedRevision: 1);
        Assert.Equal("spell-ledger-revision-mismatch", stale.Reason);
        Assert.Null(stale.State);

        var unchanged = NativeOriginInnateSpellResourceLedger.Advance(catalog,
            origin, previous, previous, ledger, expectedRevision: 0);
        Assert.Equal("spell-ledger-level-not-advanced", unchanged.Reason);
        Assert.Null(unchanged.State);
        var rollback = NativeOriginInnateSpellResourceLedger.Advance(catalog,
            origin, previous, Character(catalog, 2), ledger, 0);
        Assert.Equal("spell-ledger-level-not-advanced", rollback.Reason);
        Assert.Null(rollback.State);

        var unrelated = Direct(catalog, "ancestry.tiefling.chthonic");
        var wrongSource = NativeOriginInnateSpellResourceLedger.Advance(catalog,
            unrelated, previous, advanced, ledger, 0);
        Assert.Equal("spell-ledger-snapshot-mismatch", wrongSource.Reason);
        Assert.Null(wrongSource.State);

        var wrongRuleSet = previous with { Fingerprint = "unverified" };
        var unverified = NativeOriginInnateSpellResourceLedger.Advance(catalog,
            origin, wrongRuleSet, advanced, ledger, 0);
        Assert.Equal("unresolved", unverified.Status);
        Assert.Null(unverified.State);
    }

    [Fact]
    public void ProficiencyBasedMaximumChangeRequiresAnExplicitGameplayPolicy()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.gnome.forest");
        var fourth = Character(catalog, 4);
        var fifth = Character(catalog, 5);
        var oldMaximum = fourth.Values["proficiency.bonus"].Value;
        var newMaximum = fifth.Values["proficiency.bonus"].Value;
        Assert.NotNull(oldMaximum);
        Assert.NotNull(newMaximum);
        Assert.True(newMaximum > oldMaximum);

        var ledger = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, fourth).State!;
        var animal = Grant(origin, "trait.animal-speech");
        var spent = Step(catalog, origin, fourth, ledger,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, animal).State!;
        var migrated = NativeOriginInnateSpellResourceLedger.Advance(catalog,
            origin, fourth, fifth, spent, spent.Revision);
        Assert.Equal("unresolved", migrated.Status);
        Assert.Equal("spell-ledger-capacity-change-requires-policy",
            migrated.Reason);
        Assert.Null(migrated.State);
        Assert.Equal(oldMaximum - 1, spent.RemainingFreeCasts[animal]);
    }

    /// <summary>
    /// In-process test authority verifies exact campaign scope and identity.
    /// Production authority must authenticate the actor and evidence.
    /// </summary>
    private sealed class CapacityTestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "innate-ledger-tests" &&
            decision.Authority?.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "verified-capacity-test";

        public bool AuthorizesCatalogChoice(string catalogId,
            string optionId, string evidenceId) => false;
    }

    private static NativeTableDecisionScope CapacityScope() =>
        new("campaign", "innate-ledger-tests");

    private static NativeTableRuleDecision CapacityDecision(
        NativeOriginInnateSpellLedgerState previous,
        NativeOriginInnateSpellLedgerState advanced,
        string grantId, int oldMaximum, int newMaximum,
        int newRemaining, string? decisionGrantId = null,
        NativeTableDecisionScope? scope = null,
        long? claimedRevision = null) =>
        new("approved-capacity:" + grantId,
            new("dm", "verified-capacity-test"),
            scope ?? CapacityScope(),
            new("origin-innate-spell-capacity", decisionGrantId ?? grantId),
            JsonSerializer.SerializeToElement(new
            {
                grantId,
                previousProjectionFingerprint = previous.ProjectionFingerprint,
                advancedProjectionFingerprint = advanced.ProjectionFingerprint,
                previousCharacterLevel = previous.CharacterLevel,
                advancedCharacterLevel = advanced.CharacterLevel,
                previousMaximum = oldMaximum,
                advancedMaximum = newMaximum,
                previousRemaining = previous.RemainingFreeCasts[grantId],
                previousRevision = claimedRevision ?? previous.Revision,
                remainingAfter = newRemaining
            }));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void VerifiedTableDecisionMayExplicitlyResolveExistingCapacityChange(
        int remainingAfter)
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.gnome.forest");
        var previousCharacter = Character(catalog, 4);
        var advancedCharacter = Character(catalog, 5);
        var before = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, previousCharacter).State!;
        var grant = Grant(origin, "trait.animal-speech");
        var oldMaximum = previousCharacter.Values["proficiency.bonus"].Value!.Value;
        var newMaximum = advancedCharacter.Values["proficiency.bonus"].Value!.Value;
        Assert.True(newMaximum > oldMaximum);
        var spent = Step(catalog, origin, previousCharacter, before,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, grant).State!;
        var newPreview = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, advancedCharacter).State!;
        var decision = CapacityDecision(spent, newPreview, grant,
            oldMaximum, newMaximum, remainingAfter);
        var updated = NativeOriginInnateSpellResourceLedger.Advance(catalog,
            origin, previousCharacter, advancedCharacter,
            spent, spent.Revision, [decision], CapacityScope(),
            new CapacityTestAuthority());

        Assert.Equal("resolved", updated.Status);
        var migrated = updated.State!;
        Assert.Equal(newMaximum, migrated.CharacterLevel == 5
            ? advancedCharacter.Values["proficiency.bonus"].Value!.Value : -1);
        Assert.Equal(remainingAfter, migrated.RemainingFreeCasts[grant]);
        Assert.Equal(spent.Revision + 1, migrated.Revision);
        Assert.Equal(new[] { decision.Id }, updated.AppliedCapacityDecisionIds);
        Assert.Equal(oldMaximum - 1, spent.RemainingFreeCasts[grant]);
        // The selected table outcome must actually be the new resource state,
        // not just documentation or a newly bootstrapped unspent ledger.
        var used = Step(catalog, origin, advancedCharacter, migrated,
            NativeOriginInnateSpellLedgerActionKind.LongRest);
        Assert.Equal("resolved", used.Status);
        Assert.Equal(newMaximum, used.State!.RemainingFreeCasts[grant]);
    }

    [Fact]
    public void CapacityDecisionRequiresExactAuthorityScopeAndPriorRevision()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.gnome.forest");
        var previous = Character(catalog, 4);
        var advanced = Character(catalog, 5);
        var state = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, previous).State!;
        var next = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, advanced).State!;
        var grantId = Grant(origin, "trait.animal-speech");
        var oldMax = previous.Values["proficiency.bonus"].Value!.Value;
        var newMax = advanced.Values["proficiency.bonus"].Value!.Value;
        var decision = CapacityDecision(state, next, grantId,
            oldMax, newMax, oldMax);
        var authority = new CapacityTestAuthority();
        var trusted = CapacityScope();

        var noAuthority = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, state,
            state.Revision, [decision], trusted);
        Assert.Equal("spell-ledger-capacity-change-requires-policy",
            noAuthority.Reason);
        var noScope = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, state,
            state.Revision, [decision], authority: authority);
        Assert.Equal("spell-ledger-capacity-change-requires-policy",
            noScope.Reason);
        var wrongTrusted = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, state,
            state.Revision, [decision],
            new("campaign", "another-campaign"), authority);
        Assert.Equal("spell-ledger-capacity-decision-unverified",
            wrongTrusted.Reason);
        var wrongDeclared = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, state,
            state.Revision, [CapacityDecision(state, next, grantId,
                oldMax, newMax, oldMax,
                scope: new("campaign", "another-campaign"))], trusted, authority);
        Assert.Equal("spell-ledger-capacity-decision-unverified",
            wrongDeclared.Reason);
        var wrongRevision = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, state,
            state.Revision, [CapacityDecision(state, next, grantId,
                oldMax, newMax, oldMax, claimedRevision: 77)], trusted, authority);
        Assert.Equal("spell-ledger-capacity-decision-unverified",
            wrongRevision.Reason);
        var outOfRange = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, state,
            state.Revision, [CapacityDecision(state, next, grantId,
                oldMax, newMax, newMax + 1)], trusted, authority);
        Assert.Equal("spell-ledger-capacity-decision-unverified",
            outOfRange.Reason);
        var wrongGrant = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, state,
            state.Revision, [CapacityDecision(state, next, grantId,
                oldMax, newMax, oldMax, decisionGrantId: "unselected")],
            trusted, authority);
        Assert.Equal("spell-ledger-capacity-decision-target-invalid",
            wrongGrant.Reason);
        var duplicated = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, state,
            state.Revision, [decision, decision], trusted, authority);
        Assert.Equal("spell-ledger-capacity-decision-target-invalid",
            duplicated.Reason);

        // Changed values without a verified table decision stay unresolved.
        var unchanged = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, state, state.Revision);
        Assert.Equal("spell-ledger-capacity-change-requires-policy",
            unchanged.Reason);
        Assert.Null(unchanged.State);
        Assert.Equal(oldMax, state.RemainingFreeCasts[grantId]);
    }

    [Fact]
    public void CapacityDecisionCannotBeAppliedWhenNoCapChangeExists()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var previous = Character(catalog, 5);
        var advanced = Character(catalog, 6);
        var state = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, previous).State!;
        var grant = Grant(origin, "trait.darkness");
        var newPreview = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, advanced).State!;
        var decision = CapacityDecision(state, newPreview, grant, 1, 1, 1);
        var attempt = NativeOriginInnateSpellResourceLedger.Advance(
            catalog, origin, previous, advanced, state,
            state.Revision, [decision], CapacityScope(),
            new CapacityTestAuthority());
        Assert.Equal("spell-ledger-unexpected-capacity-decision", attempt.Reason);
        Assert.Null(attempt.State);
        Assert.Equal(0, state.Revision);
    }

    [Fact]
    public void AdvancementWithoutNewUnlockPreservesSpentCountAndFrozenIdentity()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var fifth = Character(catalog, 5);
        var sixth = Character(catalog, 6);
        var hellish = Grant(origin, "trait.hellish-rebuke");
        var darkness = Grant(origin, "trait.darkness");
        var initial = NativeOriginInnateSpellResourceLedger.Initialize(
            catalog, origin, fifth).State!;
        var spent = Step(catalog, origin, fifth, initial,
            NativeOriginInnateSpellLedgerActionKind.SpendFreeCast, darkness).State!;
        var migration = NativeOriginInnateSpellResourceLedger.Advance(catalog,
            origin, fifth, sixth, spent, spent.Revision);
        Assert.Equal("resolved", migration.Status);
        Assert.Equal(1, migration.State!.RemainingFreeCasts[hellish]);
        Assert.Equal(0, migration.State.RemainingFreeCasts[darkness]);
        Assert.Equal(6, migration.State.CharacterLevel);
        var roundTrip = JsonSerializer.Deserialize<NativeOriginInnateSpellLedgerState>(
            JsonSerializer.Serialize(migration.State))!;
        Assert.Equal("resolved", Step(catalog, origin, sixth, roundTrip,
            NativeOriginInnateSpellLedgerActionKind.ShortRest).Status);
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
        var animal = Grant(origin, "trait.animal-speech");
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
        var hellish = Grant(origin, "trait.hellish-rebuke");
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
