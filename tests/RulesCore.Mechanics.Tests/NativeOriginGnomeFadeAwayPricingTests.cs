using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Approved Gnome Fade Away biological grant and first-party trial valuation.
/// Verifies canonical origin construction and payload, not live Reaction
/// timing, damage resolution, invisibility execution, or rest persistence.
/// </summary>
public sealed class NativeOriginGnomeFadeAwayPricingTests
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

    internal static NativeOriginConstructionResult Direct(
        NativeCatalogSnapshot catalog, string selected, string spell = "silent-image",
        string ability = "intelligence")
    {
        var proposal = new NativeOriginProposal("fade-away-" + selected,
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedSpeciesId: selected == "gnome" ? "species.gnome" : null,
            SelectedAncestryId: selected == "gnome"
                ? null : "ancestry.gnome." + selected);
        var engine = new NativeOriginConstructionEngine(catalog);
        var candidates = engine.Evaluate(new(proposal,
            AutoManifestDirectDefaults: true)).EvaluatedProposal.Candidates;
        var choices = new List<NativeOriginManifestation>();
        var spellAbility = candidates.SingleOrDefault(x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        if (spellAbility is not null)
            choices.Add(new("chosen-spellcasting-ability", spellAbility.Id,
                Choices: [new(Assert.Single(spellAbility.RequiredChoices!).Id, [ability])]));
        var trickster = candidates.SingleOrDefault(x =>
            x.Identity.MechanicalDefinitionId == "trait.natural-trickster");
        if (trickster is not null)
            choices.Add(new("chosen-natural-trickster-spell", trickster.Id,
                Choices: [new(Assert.Single(trickster.RequiredChoices!).Id, [spell])]));
        return engine.Evaluate(new(proposal with
        {
            Manifestations = choices
        }, AutoManifestDirectDefaults: true,
            RequireCreationAllocation: true,
            CreationAllocationPolicy:
                NativeOriginFirstPartyBiologicalPricing.Create()));
    }

    [Theory]
    [InlineData("gnome", 9)]
    [InlineData("forest", 18)]
    [InlineData("rock", 18)]
    public void SharedGnomeFadeAwayIsInheritedOnceWithCorrectTrialCost(
        string selected, int expectedPoints)
    {
        var result = Direct(Catalog(), selected);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal("within-allowance", result.CreationAllocation!.Status);
        Assert.Equal(expectedPoints, result.CreationAllocation.CommittedLoad);

        var fade = Assert.Single(result.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == "trait.fade-away");
        Assert.Equal("species.gnome", fade.Identity.Occurrence.Source.Id);
        Assert.Single(fade.SourceCandidateIds);
        Assert.Equal("fade-away",
            fade.EffectiveValue!.Value.GetProperty("id").GetString());

        var charge = Assert.Single(result.CreationAllocation.Charges, x =>
            x.CharacteristicKeys.Contains("trait:trait.fade-away"));
        Assert.Equal(4, charge.Load);
    }

    [Theory]
    [InlineData("disguise-self", "intelligence")]
    [InlineData("silent-image", "wisdom")]
    [InlineData("ventriloquism", "charisma")]
    public void RockGnomeFinalPackagePricesAndProjectsExactlyOneChosenSpell(
        string spell, string ability)
    {
        var catalog = Catalog();
        var result = Direct(catalog, "rock", spell, ability);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        Assert.Equal(18, result.CreationAllocation!.CommittedLoad);
        var traits = result.Profile!.Underlying.InherentGrants;
        var climb = Assert.Single(traits, x =>
            x.Identity.MechanicalDefinitionId == "trait.natural-climber");
        Assert.Equal(15, climb.EffectiveValue!.Value.GetProperty("speedFeet").GetInt32());
        var trickster = Assert.Single(traits, x =>
            x.Identity.MechanicalDefinitionId == "trait.natural-trickster");
        Assert.Equal(spell, Assert.Single(Assert.Single(trickster.ResolvedChoices!).SelectedOptionIds));
        var projection = NativeOriginInnateSpellProgressionProjector.Project(catalog, result, 1);
        Assert.Equal("resolved", projection.Status);
        var innate = Assert.Single(projection.Spells);
        Assert.Equal(spell, innate.SpellId);
        Assert.Equal(ability, innate.SpellcastingAbility);
        Assert.Equal("available", innate.Availability);
        Assert.Equal(1, innate.FreeUsesPerRecharge);
        Assert.Equal("long-rest", innate.Recharge);
        Assert.False(innate.AtWill);
        Assert.False(innate.SlotCasting);
        foreach (var (key, load) in new[] {
            ("trait:trait.stonebound-tremorsense", 4),
            ("trait:trait.natural-climber", 2),
            ("trait:trait.natural-trickster", 2),
            ("intrinsic:spellcastingAbility", 1) })
            Assert.Equal(load, Assert.Single(result.CreationAllocation.Charges,
                x => x.CharacteristicKeys.Contains(key)).Load);
    }

    [Fact]
    public void NaturalTricksterRequiresAnOfferedPersistentSpellChoice()
    {
        var catalog = Catalog();
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized,
            Direct(catalog, "rock", "fireball").Status);
        var result = Direct(catalog, "rock", "silent-image");
        var original = result.Profile!.Underlying;
        var altered = original with
        {
            InherentGrants = original.InherentGrants.Select(g =>
                g.Identity.MechanicalDefinitionId == "trait.natural-trickster"
                    ? g with { ResolvedChoices = [new("spell", ["disguise-self"])] }
                    : g).ToArray()
        };
        var forged = result with { Profile = result.Profile with
            { Underlying = altered, Current = altered } };
        Assert.Equal("unresolved", NativeOriginInnateSpellProgressionProjector
            .Project(catalog, forged, 1).Status);
    }

    [Fact]
    public void FadeAwayReactionIsAfterDamageAndEndsOnSpecifiedEvents()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("trait.fade-away", out var entity));
        var trait = entity!.Definition.GetProperty("trait");
        var activation = trait.GetProperty("activation");
        Assert.Equal("reaction", activation.GetProperty("action").GetString());
        Assert.Equal("immediately-after-you-take-damage",
            activation.GetProperty("trigger").GetString());

        var invisibility = trait.GetProperty("invisibility");
        Assert.Equal("end-of-your-next-turn",
            invisibility.GetProperty("endsAt").GetString());
        Assert.Equal(new[]
        {
            "you-attack", "you-deal-damage",
            "you-force-a-creature-to-make-a-saving-throw"
        }, invisibility.GetProperty("alsoEndsImmediatelyAfter")
            .EnumerateArray().Select(x => x.GetString()).ToArray());

        var uses = trait.GetProperty("uses");
        Assert.Equal(1, uses.GetProperty("value").GetInt32());
        Assert.Equal("short-or-long-rest",
            uses.GetProperty("recharge").GetString());
    }

}
