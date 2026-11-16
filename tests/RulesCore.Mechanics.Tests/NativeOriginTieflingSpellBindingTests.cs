using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Real native Tiefling catalog: a shared parent-Species cantrip uses the
/// explicitly chosen spellcasting ability on its selected Fiendish Ancestry.
/// A source's innate spell course remains independent of unrelated donors.
/// Mixed selections use only scoped, in-process DM test authorization.
/// </summary>
public sealed class NativeOriginTieflingSpellBindingTests
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
        new("tiefling-direct-" + ancestry, NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: ancestry);

    private static NativeOriginProposal Mixed(string secondAncestry) =>
        new("tiefling-mixed", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("a", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.tiefling.infernal")),
                new("b", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, secondAncestry))
            ], [], []);

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, bool auto = true,
        INativeOriginAuthority? authority = null) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null :
                new("campaign", "tiefling-ability-test"),
                AutoManifestDirectDefaults: auto), authority);

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "tiefling-ability-test" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "tiefling-spell-test-only";
        public bool AuthorizesCatalogChoice(string catalogId,
            string optionId, string evidenceId) => false;
    }

    private static NativeTableRuleDecision Permit(NativeOriginCandidate candidate) =>
        new("permit:" + candidate.Id,
            new("dm", "tiefling-spell-test-only"),
            new("campaign", "tiefling-ability-test"),
            new("candidate", candidate.Id),
            JsonSerializer.SerializeToElement(new { eligibility = "eligible" }));

    private static readonly (string Ancestry, string Cantrip, string Three,
        string Five)[] Courses =
    [
        ("ancestry.tiefling.infernal", "trait.tiefling.infernal.fire-bolt",
            "trait.tiefling.infernal.hellish-rebuke",
            "trait.tiefling.infernal.darkness"),
        ("ancestry.tiefling.chthonic", "trait.tiefling.chthonic.chill-touch",
            "trait.tiefling.chthonic.false-life",
            "trait.tiefling.chthonic.ray-of-enfeeblement"),
        ("ancestry.tiefling.abyssal", "trait.tiefling.abyssal.poison-spray",
            "trait.tiefling.abyssal.ray-of-sickness",
            "trait.tiefling.abyssal.hold-person")
    ];

    private static NativeOriginConstructionResult SelectedDirect(
        string ancestry, string ability)
    {
        var proposal = Direct(ancestry);
        var candidates = Evaluate(proposal).EvaluatedProposal.Candidates;
        var size = Assert.Single(candidates, x =>
            x.Identity.IntrinsicSlotId == "size");
        var casting = Assert.Single(candidates, x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        Assert.Equal("species.tiefling", size.Identity.Occurrence.Source.Id);
        Assert.Equal(ancestry, casting.Identity.Occurrence.Source.Id);

        return Evaluate(proposal with
        {
            Manifestations =
            [
                new("choose-size", size.Id,
                    Choices: [new(size.RequiredChoices!.Single().Id, ["medium"])]),
                new("choose-ability", casting.Id,
                    Choices: [new(casting.RequiredChoices!.Single().Id, [ability])])
            ]
        });
    }

    [Theory]
    [InlineData("ancestry.tiefling.infernal", "intelligence")]
    [InlineData("ancestry.tiefling.infernal", "wisdom")]
    [InlineData("ancestry.tiefling.infernal", "charisma")]
    [InlineData("ancestry.tiefling.chthonic", "intelligence")]
    [InlineData("ancestry.tiefling.chthonic", "wisdom")]
    [InlineData("ancestry.tiefling.chthonic", "charisma")]
    [InlineData("ancestry.tiefling.abyssal", "intelligence")]
    [InlineData("ancestry.tiefling.abyssal", "wisdom")]
    [InlineData("ancestry.tiefling.abyssal", "charisma")]
    public void EachNativeFiendishAncestrySharesItsChosenAbilityAcrossFullSpellCourse(
        string ancestry, string ability)
    {
        var course = Assert.Single(Courses, c => c.Ancestry == ancestry);
        var result = SelectedDirect(ancestry, ability);
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Detail)));

        var grants = result.Profile!.Underlying.InherentGrants;
        var choice = Assert.Single(grants, x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        Assert.Equal(ability, choice.EffectiveValue!.Value.GetString());

        var shared = Assert.Single(grants, x =>
            x.Identity.MechanicalDefinitionId == "trait.tiefling.otherworldly-presence");
        Assert.Equal("species.tiefling", shared.Identity.Occurrence.Source.Id);
        Assert.Equal("descendant", shared.EffectiveValue!.Value
            .GetProperty("originBindings").GetProperty("spellcastingAbility")
            .GetProperty("scope").GetString());
        Assert.Equal("selected-tiefling-ancestry",
            shared.EffectiveValue.Value.GetProperty("spellcastingAbilityFrom")
                .GetString());
        Assert.Equal("thaumaturgy", shared.EffectiveValue.Value
            .GetProperty("spell").GetString());
        Assert.True(shared.EffectiveValue.Value.GetProperty("atWill").GetBoolean());
        Assert.Equal(ability, shared.Parameters!["spellcastingAbility"].GetString());

        foreach (var (id, unlock) in new[]
        {
            (course.Cantrip, 1), (course.Three, 3), (course.Five, 5)
        })
        {
            var spell = Assert.Single(grants, x =>
                x.Identity.MechanicalDefinitionId == id);
            Assert.Equal(ancestry, spell.Identity.Occurrence.Source.Id);
            Assert.Equal(ability,
                spell.Parameters!["spellcastingAbility"].GetString());
            Assert.Equal("source", spell.EffectiveValue!.Value
                .GetProperty("originBindings")
                .GetProperty("spellcastingAbility").GetProperty("scope").GetString());
            Assert.Equal(unlock, spell.EffectiveValue.Value
                .GetProperty("level").GetInt32());
            if (unlock == 1)
                Assert.True(spell.EffectiveValue.Value
                    .GetProperty("atWill").GetBoolean());
            else
            {
                Assert.True(spell.EffectiveValue.Value
                    .GetProperty("alwaysPrepared").GetBoolean());
                Assert.Equal(1, spell.EffectiveValue.Value
                    .GetProperty("freeUses").GetInt32());
                Assert.Equal("long-rest", spell.EffectiveValue.Value
                    .GetProperty("recharge").GetString());
                Assert.True(spell.EffectiveValue.Value
                    .GetProperty("slotCasting").GetBoolean());
            }
            if (id == "trait.tiefling.infernal.hellish-rebuke")
                Assert.Equal(2, spell.EffectiveValue!.Value
                    .GetProperty("freeCastSpellLevel").GetInt32());
        }

        // A serialized Origin must retain source-bound choices and original
        // free-cast data; serialization does not authorize an external profile.
        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile))!;
        var rebound = Assert.Single(saved.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.tiefling.otherworldly-presence");
        Assert.Equal(ability,
            rebound.Parameters!["spellcastingAbility"].GetString());
    }

    [Theory]
    [InlineData("ancestry.tiefling.infernal")]
    [InlineData("ancestry.tiefling.chthonic")]
    [InlineData("ancestry.tiefling.abyssal")]
    public void MissingAncestrySpellcastingChoiceDoesNotIssueSharedOrLeafSpells(
        string ancestry)
    {
        var result = Evaluate(Direct(ancestry), auto: true);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Contains(result.Issues, x => x.Code == "required-origin-selection-missing");
        Assert.Contains(result.Issues, x => x.Code == "origin-binding-choice-missing");
        Assert.DoesNotContain(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.tiefling.otherworldly-presence" ||
            x.Identity.MechanicalDefinitionId?.StartsWith(
                "trait.tiefling." + ancestry.Split('.')[2] + ".",
                StringComparison.Ordinal) == true &&
            x.EffectiveValue?.ValueKind == JsonValueKind.Object &&
            x.EffectiveValue.Value.TryGetProperty("spell", out _));
    }

    private static NativeOriginConstructionResult SelectedMixed(
        string secondAncestry, string abilityOwner,
        bool selectSameContributorLeaf = true)
    {
        var proposal = Mixed(secondAncestry);
        var offered = Evaluate(proposal, auto: false).EvaluatedProposal.Candidates;
        var foundation = offered.Where(x => x.ContributorId == "a" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);

        var shared = Assert.Single(offered, x => x.ContributorId == "a" &&
            x.Identity.MechanicalDefinitionId ==
                "trait.tiefling.otherworldly-presence");
        var ability = Assert.Single(offered, x => x.ContributorId == abilityOwner &&
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var leaf = Assert.Single(offered, x =>
            x.ContributorId == (selectSameContributorLeaf ? "a" : "b") &&
            x.Identity.MechanicalDefinitionId ==
                (selectSameContributorLeaf
                    ? "trait.tiefling.infernal.fire-bolt"
                    : "trait.tiefling." + secondAncestry.Split('.')[2] +
                        (secondAncestry.EndsWith(".chthonic", StringComparison.Ordinal)
                            ? ".chill-touch" : ".fire-bolt")));

        var manifest = foundation.Select((x, i) =>
            x.Identity.IntrinsicSlotId == "size"
                ? new NativeOriginManifestation("foundation:" + i, x.Id,
                    Choices: [new(x.RequiredChoices!.Single().Id, ["medium"])])
                : new NativeOriginManifestation("foundation:" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat([
                new NativeOriginManifestation("chosen-shared", shared.Id),
                new NativeOriginManifestation("chosen-ability", ability.Id,
                    Choices: [new(ability.RequiredChoices!.Single().Id, ["wisdom"])]),
                new NativeOriginManifestation("chosen-leaf", leaf.Id)
            ]).ToArray();
        var selectedOffers = foundation.Concat([shared, ability, leaf]).ToArray();
        return Evaluate(proposal with
        {
            Manifestations = manifest,
            TableDecisions = selectedOffers.Select(Permit).ToArray()
        }, auto: false, authority: new TestAuthority());
    }

    [Theory]
    [InlineData("ancestry.tiefling.chthonic")]
    [InlineData("ancestry.tiefling.infernal")]
    public void ParentSpeciesSpellCanNotBorrowAnotherContributorsAncestryChoice(
        string second)
    {
        var result = SelectedMixed(second, "b");
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Contains(result.Issues, x =>
            x.Code == "origin-binding-choice-missing");
        Assert.DoesNotContain(result.Profile?.Underlying.InherentGrants ?? [], x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.tiefling.otherworldly-presence");
        Assert.DoesNotContain(result.Profile?.Underlying.InherentGrants ?? [], x =>
            x.Identity.MechanicalDefinitionId == "trait.tiefling.infernal.fire-bolt");
    }

    [Fact]
    public void VerifiedAncestryChoiceBindsItsOwnParentAndLeafSpellsInMixedConstruction()
    {
        var result = SelectedMixed("ancestry.tiefling.chthonic", "a");
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code + ":" + x.Detail)));
        var shared = Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.tiefling.otherworldly-presence");
        var leaf = Assert.Single(result.Profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.tiefling.infernal.fire-bolt");
        Assert.Equal("wisdom",
            shared.Parameters!["spellcastingAbility"].GetString());
        Assert.Equal("wisdom",
            leaf.Parameters!["spellcastingAbility"].GetString());
        Assert.Equal("species.tiefling", shared.Identity.Occurrence.Source.Id);
        Assert.Equal("ancestry.tiefling.infernal",
            leaf.Identity.Occurrence.Source.Id);
    }
}
