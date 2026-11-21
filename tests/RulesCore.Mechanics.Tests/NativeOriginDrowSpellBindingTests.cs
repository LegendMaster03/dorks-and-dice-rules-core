using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Source-qualified, native Drow innate-spell ability binding. This does not
/// create new spell slots, waive unlock levels, or grant mixed biological
/// eligibility for any Drow spell: the mixed examples have scoped DM permission.
/// </summary>
public sealed class NativeOriginDrowSpellBindingTests
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
        throw new InvalidOperationException("Native ruleset missing.");
    }

    private static NativeOriginProposal Direct() =>
        new("direct-drow-spells", NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: "ancestry.elf.drow");

    private static NativeOriginProposal Mixed() =>
        new("mixed-drow-spells", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("a", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.elf.drow")),
                new("b", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.elf.drow"))
            ], [], []);

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, bool auto = true,
        INativeOriginAuthority? authority = null) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal,
                authority is null ? null : new("campaign", "drow-spell-test"),
                AutoManifestDirectDefaults: auto), authority);

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "drow-spell-test" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "verified-drow-spell";
        public bool AuthorizesCatalogChoice(string catalogId,
            string optionId, string evidenceId) => false;
    }

    private static NativeTableRuleDecision Permit(
        string id, string candidateId) =>
        new(id, new("dm", "verified-drow-spell"),
            new("campaign", "drow-spell-test"),
            new("candidate", candidateId),
            JsonSerializer.SerializeToElement(new { eligibility = "eligible" }));

    private static readonly (string Id, string Spell, int UnlockLevel)[] Spells =
    [
        ("trait.elf.drow.dancing-lights", "dancing-lights", 1),
        ("trait.elf.drow.faerie-fire", "faerie-fire", 3),
        ("trait.elf.drow.darkness", "darkness", 5)
    ];

    [Theory]
    [InlineData("intelligence")]
    [InlineData("wisdom")]
    [InlineData("charisma")]
    public void DirectDrowPreservesWholeSpellCourseAndOneAncestryChoice(
        string chosenAbility)
    {
        var offers = Evaluate(Direct(), auto: true).EvaluatedProposal.Candidates;
        var ability = Assert.Single(offers, x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var requirement = Assert.Single(ability.RequiredChoices!);
        Assert.Contains(chosenAbility, requirement.FiniteOptions!);

        var completed = Evaluate(Direct() with
        {
            Manifestations =
            [
                new("drow-ability", ability.Id,
                    Choices: [new(requirement.Id, [chosenAbility])])
            ]
        });
        Assert.True(completed.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", completed.Issues.Select(x =>
                x.Code + ":" + x.Detail)));
        var grants = completed.Profile!.Underlying.InherentGrants;
        var chosen = Assert.Single(grants, x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        Assert.Equal(chosenAbility, chosen.EffectiveValue!.Value.GetString());

        foreach (var (id, spell, level) in Spells)
        {
            var grant = Assert.Single(grants, x =>
                x.Identity.MechanicalDefinitionId == id);
            Assert.Equal(chosenAbility,
                grant.Parameters!["spellcastingAbility"].GetString());
            Assert.Equal("manifested-intrinsic", grant.EffectiveValue!.Value
                .GetProperty("originBindings")
                .GetProperty("spellcastingAbility")
                .GetProperty("lookup").GetString());
            Assert.Equal(spell, grant.EffectiveValue.Value
                .GetProperty("spell").GetString());
            Assert.Equal(level, grant.EffectiveValue.Value
                .GetProperty("level").GetInt32());
            if (level == 1)
                Assert.True(grant.EffectiveValue.Value
                    .GetProperty("atWill").GetBoolean());
            else
            {
                Assert.Equal(1, grant.EffectiveValue.Value
                    .GetProperty("freeUses").GetInt32());
                Assert.Equal("long-rest", grant.EffectiveValue.Value
                    .GetProperty("recharge").GetString());
                Assert.True(grant.EffectiveValue.Value
                    .GetProperty("slotCasting").GetBoolean());
            }
        }

        var restored = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(completed.Profile))!;
        Assert.All(restored.Underlying.InherentGrants.Where(g =>
            Spells.Any(s => s.Id == g.Identity.MechanicalDefinitionId)), grant =>
            Assert.Equal(chosenAbility, grant.Parameters!["spellcastingAbility"]
                .GetString()));
    }

    [Fact]
    public void DirectDrowSpellCourseWithoutItsChosenAbilityIsIncomplete()
    {
        var incomplete = Evaluate(Direct(), auto: true);
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized,
            incomplete.Status);
        Assert.Contains(incomplete.Issues, issue =>
            issue.Code == "required-origin-selection-missing");
        Assert.Contains(incomplete.Issues, issue =>
            issue.Code == "origin-binding-choice-missing");
        Assert.DoesNotContain(incomplete.Profile!.Underlying.InherentGrants, grant =>
            Spells.Any(s => s.Id == grant.Identity.MechanicalDefinitionId));
    }

    private static NativeOriginConstructionResult MixedSpell(
        string spellContributor)
    {
        var baseProposal = Mixed();
        var offers = Evaluate(baseProposal, auto: false).EvaluatedProposal.Candidates;
        var foundation = offers.Where(x => x.ContributorId == "a" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var ability = Assert.Single(offers, x => x.ContributorId == "a" &&
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var spell = Assert.Single(offers, x =>
            x.ContributorId == spellContributor &&
            x.Identity.MechanicalDefinitionId == "trait.elf.drow.faerie-fire");
        Assert.Equal(3, foundation.Length);
        Assert.Equal(NativeOriginEligibility.Eligible,
            spell.SelectionBasis.Eligibility);

        var selected = foundation.Select((x, i) =>
            new NativeOriginManifestation("foundation:" + i, x.Id,
                SelectedValue: x.OfferedValue)).Concat([
            new NativeOriginManifestation("ability", ability.Id,
                Choices: [new(ability.RequiredChoices!.Single().Id, ["wisdom"])]),
            new NativeOriginManifestation("faerie-fire", spell.Id)
        ]).ToArray();

        return Evaluate(baseProposal with
        {
            Manifestations = selected,
            TableDecisions =
            [
                Permit("permit-ability", ability.Id),
                Permit("permit-spell", spell.Id)
            ]
        }, auto: false, authority: new TestAuthority());
    }

    [Fact]
    public void MixedDrowSpellUsesItsOwnContributorsSelectedAbility()
    {
        var completed = MixedSpell("a");
        Assert.True(completed.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", completed.Issues.Select(x =>
                x.Code + ":" + x.Detail)));
        var spell = Assert.Single(completed.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == "trait.elf.drow.faerie-fire");
        Assert.Equal("wisdom", spell.Parameters!["spellcastingAbility"].GetString());
        Assert.Equal(3, spell.EffectiveValue!.Value.GetProperty("level").GetInt32());
        Assert.Equal(1, spell.EffectiveValue.Value.GetProperty("freeUses").GetInt32());
    }

    [Fact]
    public void MixedDrowSpellCanNotBorrowOtherContributorsAbility()
    {
        var blocked = MixedSpell("b");
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, blocked.Status);
        Assert.Contains(blocked.Issues, issue =>
            issue.Code == "origin-binding-choice-missing");
        Assert.DoesNotContain(blocked.Profile?.Underlying.InherentGrants ?? [],
            x => x.Identity.MechanicalDefinitionId == "trait.elf.drow.faerie-fire");
    }
}
