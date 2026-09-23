using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class UniversalCharacterConceptTests
{
    [Fact]
    public void ReviewedCompetencyFamiliesExposeExactIndependentMemberUnion()
    {
        Assert.Equal(
            [
                "Alchemy",
                "Armorsmithing",
                "Basketweaving",
                "Blacksmithing",
                "Bookbinding",
                "Bowmaking",
                "Calligraphy",
                "Carpentry",
                "Cobbling",
                "Gemcutting",
                "Leatherworking",
                "Locksmithing",
                "Painting",
                "Pottery",
                "Sculpting",
                "Shipmaking",
                "Stonemasonry",
                "Trapmaking",
                "Weaponsmithing",
                "Weaving"
            ],
            Members("Craft"));

        Assert.Equal(
            [
                "Act",
                "Comedy",
                "Dance",
                "Keyboard Instruments",
                "Oratory",
                "Percussion Instruments",
                "Sing",
                "String Instruments",
                "Wind Instruments"
            ],
            Members("Perform"));

        Assert.Equal(
            [
                "Apothecary",
                "Boater",
                "Bookkeeper",
                "Brewer",
                "Cook",
                "Driver",
                "Farmer",
                "Fisher",
                "Guide",
                "Herbalist",
                "Herder",
                "Hunter",
                "Innkeeper",
                "Lumberjack",
                "Miller",
                "Miner",
                "Porter",
                "Rancher",
                "Sailor",
                "Scribe",
                "Siege Engineer",
                "Stablehand",
                "Tanner",
                "Teamster",
                "Woodcutter"
            ],
            Members("Profession"));
    }

    [Fact]
    public void ReviewedAliasesResolveSemanticIdentityWithoutCollapsingDistinctSkills()
    {
        Assert.Equal(
            new UniversalCompetencyAlias("deception", "Deception"),
            KnownUniversalCompetencies.ResolveLegacyConceptKey("skill.bluff"));
        Assert.Equal(
            new UniversalCompetencyAlias("arcana", "Arcana"),
            KnownUniversalCompetencies.ResolveLegacyConceptKey("skill.knowledge-arcana"));
        Assert.Equal(
            new UniversalCompetencyAlias("alchemy", "Alchemy"),
            KnownUniversalCompetencies.ResolveLegacyConceptKey("skill.craft-alchemy"));
        Assert.Equal(
            new UniversalCompetencyAlias("alchemy", "Alchemy"),
            KnownUniversalCompetencies.ResolveLegacyConceptKey("tool.alchemists-supplies"));

        Assert.Null(KnownUniversalCompetencies.ResolveLegacyConceptKey("skill.hide"));
        Assert.Null(KnownUniversalCompetencies.ResolveLegacyConceptKey("skill.move-silently"));
        Assert.Null(KnownUniversalCompetencies.ResolveLegacyConceptKey("skill.open-lock"));
        Assert.Null(KnownUniversalCompetencies.ResolveLegacyConceptKey("skill.disable-device"));

        var alchemy = KnownUniversalCompetencies.FindByIdentityKey("alchemy");
        Assert.NotNull(alchemy);
        Assert.Equal("Craft", alchemy!.FamilyName);
        Assert.Contains("Alchemy", alchemy.SourceAliases!);
        Assert.Contains("Craft (alchemy)", alchemy.SourceAliases!);
        Assert.Contains("Alchemist's Supplies", alchemy.SourceAliases!);
        Assert.Equal("competency.alchemy.training", alchemy.TrainingStateKey);

        Assert.Equal(
            ["Bluff", "Deception"],
            KnownUniversalCompetencies.SourceAliases("deception"));
        Assert.Equal(
            ["Forgery", "Forgery Kit"],
            KnownUniversalCompetencies.SourceAliases("forgery"));
        Assert.Equal(
            new[] { "Knowledge (Arcana)", "Arcana" }
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase),
            KnownUniversalCompetencies.SourceAliases("arcana"));
    }

    [Fact]
    public void ReviewedFamilyMembersInheritAuthoritativeRulesLayerMechanics()
    {
        var families = new[]
        {
            (Name: "Craft", Count: 20, Ability: "intelligence", TrainedOnly: false),
            (Name: "Perform", Count: 9, Ability: "charisma", TrainedOnly: false),
            (Name: "Profession", Count: 25, Ability: "wisdom", TrainedOnly: true)
        };

        foreach (var family in families)
        {
            var members = KnownUniversalCompetencies.FamilyMembers
                .Where(value => string.Equals(
                    value.FamilyName,
                    family.Name,
                    StringComparison.Ordinal))
                .ToArray();
            Assert.Equal(family.Count, members.Length);

            foreach (var member in members)
            {
                var mechanics = KnownUniversalCompetencies.ResolveMechanics(member.IdentityKey);
                Assert.NotNull(mechanics);
                Assert.Equal(family.Ability, mechanics!.GoverningAbilityKey);
                Assert.True(mechanics.SupportsRanks);
                Assert.True(mechanics.SupportsClassSkillState);
                Assert.True(mechanics.SupportsTrainingState);
                Assert.Equal(family.TrainedOnly, mechanics.TrainedOnly);
                Assert.False(mechanics.ArmorCheckPenaltyApplies);
                Assert.Equal("ranked-skill", mechanics.EvaluationProfileKey);
                Assert.Equal(CharacterMechanicEvaluationKinds.Sum, mechanics.EvaluationKind);
                Assert.True(mechanics.CanEvaluate);
            }
        }
    }

    [Fact]
    public void UniversalAbilityAliasesNormalizeAndKnowledgeNeverBecomesAFamily()
    {
        Assert.Equal("strength", KnownUniversalCompetencies.NormalizeAbilityKey("str"));
        Assert.Equal("strength", KnownUniversalCompetencies.NormalizeAbilityKey("strength"));
        Assert.Equal("dexterity", KnownUniversalCompetencies.NormalizeAbilityKey("dex"));
        Assert.Equal("dexterity", KnownUniversalCompetencies.NormalizeAbilityKey("dexterity"));
        Assert.Equal("constitution", KnownUniversalCompetencies.NormalizeAbilityKey("con"));
        Assert.Equal("constitution", KnownUniversalCompetencies.NormalizeAbilityKey("constitution"));
        Assert.Equal("intelligence", KnownUniversalCompetencies.NormalizeAbilityKey("int"));
        Assert.Equal("intelligence", KnownUniversalCompetencies.NormalizeAbilityKey("intelligence"));
        Assert.Equal("wisdom", KnownUniversalCompetencies.NormalizeAbilityKey("wis"));
        Assert.Equal("wisdom", KnownUniversalCompetencies.NormalizeAbilityKey("wisdom"));
        Assert.Equal("charisma", KnownUniversalCompetencies.NormalizeAbilityKey("cha"));
        Assert.Equal("charisma", KnownUniversalCompetencies.NormalizeAbilityKey("charisma"));

        Assert.DoesNotContain(
            KnownUniversalCompetencies.Families,
            value => string.Equals(
                value.DisplayName,
                "Knowledge",
                StringComparison.OrdinalIgnoreCase));
        Assert.All(
            KnownUniversalCompetencies.Catalog,
            value => Assert.False(string.Equals(
                value.FamilyName,
                "Knowledge",
                StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void SpeakLanguageIsNotAnOrdinaryUniversalCompetency()
    {
        Assert.False(
            KnownUniversalCompetencies.IsOrdinaryCharacterCompetencyIdentity(
                "speak-language"));
        Assert.False(
            KnownUniversalCompetencies.IsOrdinaryCharacterCompetencyIdentity(
                "competency.speak-language"));
        Assert.True(
            KnownUniversalCompetencies.IsOrdinaryCharacterCompetencyIdentity(
                "arcana"));
    }

    [Fact]
    public void UniversalSizeCatalogContainsAllNineThreeXCategoriesAndConsequences()
    {
        var expected = new[]
        {
            ("Fine", "F", 8, -16),
            ("Diminutive", "D", 4, -12),
            ("Tiny", "T", 2, -8),
            ("Small", "S", 1, -4),
            ("Medium", "M", 0, 0),
            ("Large", "L", -1, 4),
            ("Huge", "H", -2, 8),
            ("Gargantuan", "G", -4, 12),
            ("Colossal", "C", -8, 16)
        };

        Assert.Equal(expected.Length, UniversalSizeCategories.All.Count);
        foreach (var (name, code, armorClass, grapple) in expected)
        {
            Assert.True(UniversalSizeCategories.TryResolve(name, out var byName));
            Assert.True(UniversalSizeCategories.TryResolve(code, out var byCode));
            Assert.Same(byName, byCode);
            Assert.Equal(name, byName.DisplayName);
            Assert.Equal(code, byName.SourceCode);
            Assert.Equal(armorClass, byName.ThreeXArmorClassModifier);
            Assert.Equal(grapple, byName.ThreeXGrappleModifier);
            Assert.Equal(name, UniversalSizeCategories.Normalize(code));
        }

        Assert.False(UniversalSizeCategories.TryResolve("SourceDefinedHugePlus", out _));
        Assert.Equal(
            "SourceDefinedHugePlus",
            UniversalSizeCategories.Normalize("SourceDefinedHugePlus"));
    }

    private static string[] Members(string familyName) =>
        KnownUniversalCompetencies.FamilyMembers
            .Where(value => string.Equals(
                value.FamilyName,
                familyName,
                StringComparison.Ordinal))
            .Select(value => value.DisplayName)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
}
