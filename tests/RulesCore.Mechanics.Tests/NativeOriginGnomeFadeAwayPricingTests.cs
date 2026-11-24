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

    private static NativeOriginConstructionResult Direct(
        NativeCatalogSnapshot catalog, string selected)
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
        var spellAbility = candidates.SingleOrDefault(x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var choices = spellAbility is null
            ? Array.Empty<NativeOriginManifestation>()
            : new NativeOriginManifestation[]
            {
                new("chosen-spellcasting-ability", spellAbility.Id,
                    Choices: [
                        new(Assert.Single(spellAbility.RequiredChoices!).Id,
                            ["intelligence"])
                    ])
            };
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
    [InlineData("rock", 12)]
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

    [Fact]
    public void ResourceRecoveryHonorsShortAndLongRests()
    {
        var resource = new NativeResource("origin-trait:fade-away",
            1, 0, "short-or-long-rest");
        var used = NativeResourceResolution.Spend(resource, 1, 1);
        Assert.Equal("resolved", used.Status);
        Assert.Equal(0, used.Current);
        var afterShortRest = NativeResourceResolution.Recover(resource, 0,
            NativeRecoveryEvent.ShortRest);
        Assert.Equal("resolved", afterShortRest.Status);
        Assert.Equal(1, afterShortRest.Current);
        var afterLongRest = NativeResourceResolution.Recover(resource, 0,
            NativeRecoveryEvent.LongRest);
        Assert.Equal("resolved", afterLongRest.Status);
        Assert.Equal(1, afterLongRest.Current);
    }

    [Fact]
    public void GnomeFoundationRetainsItsPreviouslyApprovedCharacteristics()
    {
        var catalog = Catalog();
        Assert.True(catalog.TryGetById("species.gnome", out var entity));
        var ids = entity!.Definition.GetProperty("traits")
            .EnumerateArray().Select(x =>
                x.GetProperty("traitId").GetString()).ToArray();
        Assert.Equal(new[]
        {
            "trait.low-light-vision", "trait.magical-cunning",
            "trait.fade-away"
        }, ids);
    }
}
