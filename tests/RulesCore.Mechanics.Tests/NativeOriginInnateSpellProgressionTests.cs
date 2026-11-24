using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// A selected native Origin spells package is a full level trajectory, not
/// an immediate free-casting resource grant or an executable spell action.
/// </summary>
public sealed class NativeOriginInnateSpellProgressionTests
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

    private static NativeOriginConstructionResult Direct(
        NativeCatalogSnapshot catalog, string ancestry, string ability = "wisdom")
    {
        var proposal = new NativeOriginProposal("spells-" + ancestry,
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"), [], [], [],
            SelectedAncestryId: ancestry);
        var engine = new NativeOriginConstructionEngine(catalog);
        var discovered = engine.Evaluate(new(proposal,
            AutoManifestDirectDefaults: true)).EvaluatedProposal.Candidates;
        var choices = new List<NativeOriginManifestation>();
        var size = discovered.SingleOrDefault(x =>
            x.Identity.IntrinsicSlotId == "size" && x.RequiredChoices is { Count: > 0 });
        if (size is not null)
            choices.Add(new("selected-size", size.Id,
                Choices: [new(size.RequiredChoices!.Single().Id, ["medium"])]));
        var spellAbility = discovered.SingleOrDefault(x =>
            x.Identity.IntrinsicSlotId == "spellcastingAbility");
        if (spellAbility is not null)
            choices.Add(new("selected-ability", spellAbility.Id,
                Choices: [new(spellAbility.RequiredChoices!.Single().Id, [ability])]));
        return engine.Evaluate(new(proposal with
        {
            Manifestations = choices
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

    [Theory]
    [InlineData(1, 2, 0)]
    [InlineData(2, 2, 0)]
    [InlineData(3, 3, 1)]
    [InlineData(4, 3, 1)]
    [InlineData(5, 4, 2)]
    [InlineData(10, 4, 2)]
    public void InfernalWholeSpellCourseUnlocksWithoutAwardingFutureFreeUses(
        int level, int available, int activeFreeSpells)
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, origin.Status);
        var projection = NativeOriginInnateSpellProgressionProjector.Project(
            catalog, origin, level);
        Assert.Equal("resolved", projection.Status);
        Assert.Equal(4, projection.Spells.Count);
        Assert.Equal(available, projection.Spells.Count(s => s.Availability == "available"));
        Assert.Equal(activeFreeSpells, projection.Spells.Count(s =>
            s.Availability == "available" && s.FreeUsesPerRecharge is > 0));
        Assert.All(projection.Spells, x => Assert.Equal("wisdom", x.SpellcastingAbility));
        Assert.True(projection.PreviewOnly);

        var hellish = Assert.Single(projection.Spells, x =>
            x.SpellId == "hellish-rebuke");
        Assert.Equal(3, hellish.UnlockCharacterLevel);
        Assert.Equal(level >= 3 ? "available" : "locked", hellish.Availability);
        Assert.Equal(2, hellish.FreeCastSpellLevel);
        Assert.Equal(1, hellish.FreeUsesPerRecharge);
        Assert.True(hellish.SlotCasting);
        Assert.Equal("long-rest", hellish.Recharge);
        var baseSpell = Assert.Single(projection.Spells, x =>
            x.SpellId == "thaumaturgy");
        Assert.Equal("species.tiefling", baseSpell.SourceId);
        Assert.True(baseSpell.AtWill);
        Assert.Null(baseSpell.FreeUsesPerRecharge);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(origin.Profile))!;
        var roundTripped = NativeOriginInnateSpellProgressionProjector.Project(
            catalog, origin with { Profile = saved }, level);
        Assert.Equal(projection.Spells.Count, roundTripped.Spells.Count);
        Assert.Equal(projection.Status, roundTripped.Status);
    }

    [Theory]
    [InlineData("ancestry.elf.drow", "dancing-lights", "faerie-fire", "darkness")]
    [InlineData("ancestry.tiefling.chthonic", "chill-touch", "false-life", "ray-of-enfeeblement")]
    [InlineData("ancestry.tiefling.abyssal", "poison-spray", "ray-of-sickness", "hold-person")]
    public void OtherNativeCoursesKeepTheirLevelGatesAndIndependentSourceGrantIds(
        string ancestry, string first, string third, string fifth)
    {
        var catalog = Catalog();
        var origin = Direct(catalog, ancestry, "intelligence");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, origin.Status);
        var atOne = NativeOriginInnateSpellProgressionProjector.Project(catalog, origin, 1);
        var atFive = NativeOriginInnateSpellProgressionProjector.ForCharacter(
            catalog, origin, Character(catalog, 5));
        Assert.Equal("resolved", atOne.Status);
        Assert.Equal("resolved", atFive.Status);
        var bySpell = atFive.Spells.ToDictionary(x => x.SpellId, StringComparer.Ordinal);
        foreach (var (spellId, level) in new[] { (first, 1), (third, 3), (fifth, 5) })
        {
            Assert.Equal(level, bySpell[spellId].UnlockCharacterLevel);
            Assert.Equal(level == 1 ? "available" : "locked",
                Assert.Single(atOne.Spells, x => x.SpellId == spellId).Availability);
            Assert.Equal("available", bySpell[spellId].Availability);
            Assert.Equal("intelligence", bySpell[spellId].SpellcastingAbility);
        }
        Assert.Equal(3, bySpell.Values.Count(x => x.SpellId is
            not "thaumaturgy"));
        Assert.Equal(bySpell.Values.Count,
            bySpell.Values.Select(x => x.GrantId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void WoodElfAuthoredSpeedOverrideAllowsCompleteSpellProjection()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.elf.wood");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, origin.Status);
        var speed = Assert.Single(origin.Profile!.Underlying.InherentGrants,
            x => x.Identity.IntrinsicSlotId == "walkingSpeedFeet");
        Assert.Equal("ancestry.elf.wood", speed.Identity.Occurrence.Source.Id);
        Assert.Equal(35, speed.EffectiveValue!.Value.GetInt32());
        var projected = NativeOriginInnateSpellProgressionProjector.Project(
            catalog, origin, 5);
        Assert.Equal("resolved", projected.Status);
        Assert.Equal(new[] { "druidcraft", "longstrider", "pass-without-trace" },
            projected.Spells.Select(x => x.SpellId).OrderBy(x => x).ToArray());
        var druidcraft = Assert.Single(projected.Spells, x => x.SpellId == "druidcraft");
        Assert.True(druidcraft.AtWill);
        Assert.Null(druidcraft.FreeUsesPerRecharge);
    }

    [Fact]
    public void ForestGnomeSymbolicFreeUsesAreResolvedOnlyFromCharacterProficiency()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.gnome.forest");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, origin.Status);
        var raw = NativeOriginInnateSpellProgressionProjector.Project(catalog, origin, 5);
        Assert.Equal("resolved", raw.Status);
        var animal = Assert.Single(raw.Spells, x =>
            x.SpellId == "speak-with-animals");
        Assert.Equal("woodland", animal.AnimalHabitat);
        Assert.Equal("proficiency-bonus", animal.FreeUsesFormula);
        Assert.Null(animal.FreeUsesPerRecharge);
        Assert.Equal("long-rest", animal.Recharge);
        Assert.True(animal.SlotCasting);
        Assert.Equal(1, animal.UnlockCharacterLevel);
        var illusion = Assert.Single(raw.Spells, x =>
            x.SpellId == "minor-illusion");
        Assert.True(illusion.AtWill);
        Assert.Null(illusion.FreeUsesPerRecharge);

        var actual = Character(catalog, 5);
        Assert.Equal("resolved", actual.Values["proficiency.bonus"].Status);
        var projected = NativeOriginInnateSpellProgressionProjector.ForCharacter(
            catalog, origin, actual);
        Assert.Equal("resolved", projected.Status);
        Assert.Equal(actual.Values["proficiency.bonus"].Value,
            Assert.Single(projected.Spells, x =>
                x.SpellId == "speak-with-animals").FreeUsesPerRecharge);
        // A missing proficiency bonus must not be interpreted as zero or one.
        var noBonus = actual with
        {
            Values = actual.Values.Where(x => x.Key != "proficiency.bonus")
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
        };
        var unresolved = NativeOriginInnateSpellProgressionProjector.ForCharacter(
            catalog, origin, noBonus);
        Assert.Equal("unresolved", unresolved.Status);
        Assert.Contains("character-proficiency-unresolved",
            unresolved.UnresolvedReasons);
    }

    [Fact]
    public void InvalidOriginLevelAndForgedGrantMechanicsFailClosed()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        Assert.Equal("unresolved",
            NativeOriginInnateSpellProgressionProjector.Project(
                catalog, origin, null).Status);
        Assert.Equal("unresolved",
            NativeOriginInnateSpellProgressionProjector.Project(
                catalog, origin, 0).Status);
        Assert.Equal("unresolved",
            NativeOriginInnateSpellProgressionProjector.Project(
                catalog, origin, 1001).Status);
        var original = Assert.Single(origin.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.hellish-rebuke");
        foreach (var altered in new[]
        {
            JsonSerializer.SerializeToElement(new
            {
                spell = "hellish-rebuke", level = 1, freeUses = 99
            }),
            JsonSerializer.SerializeToElement(new { id = "hellish-rebuke" })
        })
        {
            var config = origin.Profile.Underlying with
            {
                InherentGrants = origin.Profile.Underlying.InherentGrants
                    .Select(x => x.Id == original.Id
                        ? x with { EffectiveValue = altered } : x).ToArray()
            };
            var forged = origin with
            {
                Profile = origin.Profile with
                {
                    Underlying = config, Current = config
                }
            };
            var result = NativeOriginInnateSpellProgressionProjector.Project(
                catalog, forged, 5);
            Assert.Equal("unresolved", result.Status);
            Assert.Contains(result.UnresolvedReasons, x =>
                x == "spell-grant-not-verified:" + original.Id);
        }
    }

    [Fact]
    public void RewrittenSpellcastingAbilityDoesNotSurviveIntegrityReview()
    {
        var catalog = Catalog();
        var origin = Direct(catalog, "ancestry.tiefling.infernal");
        var original = Assert.Single(origin.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId ==
                "trait.otherworldly-presence");
        var changed = original.Parameters!.ToDictionary(x => x.Key, x => x.Value,
            StringComparer.Ordinal);
        changed["spellcastingAbility"] =
            JsonSerializer.SerializeToElement("charisma");
        var current = origin.Profile.Underlying with
        {
            InherentGrants = origin.Profile.Underlying.InherentGrants.Select(x =>
                x.Id == original.Id
                    ? x with { Parameters = changed }
                    : x).ToArray()
        };
        var forged = origin with { Profile = origin.Profile with
        {
            Underlying = current, Current = current
        }};
        var output = NativeOriginInnateSpellProgressionProjector.Project(catalog,
            forged, 5);
        Assert.Equal("unresolved", output.Status);
        Assert.Contains(output.UnresolvedReasons, x =>
            x.StartsWith("profile-intrinsic-binding-invalid:", StringComparison.Ordinal));
    }

    [Fact]
    public void UnselectedNativeSourcesDoNotGrantSpells()
    {
        var catalog = Catalog();
        var elf = new NativeOriginConstructionEngine(catalog).Evaluate(new(
            new("elf", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"), [], [], [],
                SelectedSpeciesId: "species.elf"),
            AutoManifestDirectDefaults: true));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, elf.Status);
        var projected = NativeOriginInnateSpellProgressionProjector.Project(catalog,
            elf, 5);
        Assert.Equal("none", projected.Status);
        Assert.Empty(projected.Spells);
    }
}
