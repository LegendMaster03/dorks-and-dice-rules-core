namespace RulesCore.Domain.Rules;

public static class HarvestingComponentOrigins
{
    public const string Base = "base";
    public const string Creature = "creature";
    public const string Manual = "manual";
}

public sealed record HarvestingComponentDefinition(
    string Key,
    string DisplayName,
    int ComponentDc,
    int? Quantity = null);

public sealed record HarvestingCreatureTypeDefinition(
    string Key,
    string DisplayName,
    string CompetencyKey,
    string CompetencyDisplayName,
    IReadOnlyList<HarvestingComponentDefinition> BaseComponents);

public sealed record HarvestingComponentEdit(
    string Key,
    string? DisplayName = null,
    int? ComponentDc = null,
    int? Quantity = null);

public sealed record HarvestingTableEdits(
    IReadOnlyList<string> RemoveComponentKeys,
    IReadOnlyList<HarvestingComponentEdit> UpsertComponents)
{
    public static HarvestingTableEdits Empty { get; } = new([], []);
}

public sealed record HarvestingResolvedComponent(
    string Key,
    string DisplayName,
    int ComponentDc,
    int? Quantity,
    string Origin);

/// <summary>
/// Public Harvesting & Crafting Lite creature-type defaults normalized into stable Rules Core
/// identities. A creature may remove or upsert entries, and a runtime workflow may apply a final
/// manual edit layer without mutating the published monster rule.
/// </summary>
public static class KnownHarvestingRules
{
    public const string WorkKey = "loot-tavern.harvesting-crafting-lite";
    public const string WorkDisplayName = "Harvesting & Crafting Lite";
    public const string Provider = "Loot Tavern";
    public const string GameEdition = "5e";
    public const string ReleaseKind = "public-release";
    public static readonly DateOnly PublicationDate = new(2024, 7, 3);
    public const string ReferenceUri =
        "https://www.patreon.com/LootTavern/posts/helianas-and-to-107406117";

    public const string AssessmentMechanicKey = "check.harvesting.assessment";
    public const string CarvingMechanicKey = "check.harvesting.carving";
    public const string TotalMechanicKey = "check.harvesting.total";
    public const string AssessmentAbilityKey = "intelligence";
    public const string AssessmentAbilityDisplayName = "Intelligence";
    public const string CarvingAbilityKey = "dexterity";
    public const string CarvingAbilityDisplayName = "Dexterity";
    public const string ComponentDcAggregation = "cumulative-in-order";
    public const string AwardMode = "ordered-prefix";

    public static IReadOnlyDictionary<string, int> HelperLimitsByCreatureSize { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tiny"] = 0,
            ["Small"] = 1,
            ["Medium"] = 2,
            ["Large"] = 4,
            ["Huge"] = 6,
            ["Gargantuan"] = 10
        };

    private static readonly IReadOnlyList<HarvestingCreatureTypeDefinition> Definitions =
    [
        Type("aberration", "Aberration", "competency.arcana", "Arcana",
        [
            C("antenna", "Antenna", 5),
            C("eye", "Eye", 5),
            C("flesh", "Flesh", 5),
            C("phial-of-blood", "Phial of blood", 5),
            C("bone", "Bone", 10),
            C("egg", "Egg", 10),
            C("fat", "Fat", 10),
            C("pouch-of-claws", "Pouch of claws", 10),
            C("pouch-of-teeth", "Pouch of teeth", 10),
            C("tentacle", "Tentacle", 10),
            C("heart", "Heart", 15),
            C("phial-of-mucus", "Phial of mucus", 15),
            C("liver", "Liver", 15),
            C("stinger", "Stinger", 15),
            C("brain", "Brain", 20),
            C("chitin", "Chitin", 20),
            C("hide", "Hide", 20),
            C("main-eye", "Main eye", 20)
        ]),
        Type("beast", "Beast", "competency.survival", "Survival",
        [
            C("antenna", "Antenna", 5),
            C("eye", "Eye", 5),
            C("flesh", "Flesh", 5),
            C("hair", "Hair", 5),
            C("phial-of-blood", "Phial of blood", 5),
            C("antler", "Antler", 10),
            C("beak", "Beak", 10),
            C("bone", "Bone", 10),
            C("egg", "Egg", 10),
            C("fat", "Fat", 10),
            C("fin", "Fin", 10),
            C("horn", "Horn", 10),
            C("pincer", "Pincer", 10),
            C("pouch-of-claws", "Pouch of claws", 10),
            C("pouch-of-teeth", "Pouch of teeth", 10),
            C("talon", "Talon", 10),
            C("tusk", "Tusk", 10),
            C("heart", "Heart", 15),
            C("liver", "Liver", 15),
            C("poison-gland", "Poison gland", 15),
            C("pouch-of-feathers", "Pouch of feathers", 15),
            C("pouch-of-scales", "Pouch of scales", 15),
            C("stinger", "Stinger", 15),
            C("tentacle", "Tentacle", 15),
            C("chitin", "Chitin", 20),
            C("pelt", "Pelt", 20)
        ]),
        Type("celestial", "Celestial", "competency.religion", "Religion",
        [
            C("eye", "Eye", 5),
            C("flesh", "Flesh", 5),
            C("hair", "Hair", 5),
            C("phial-of-blood", "Phial of blood", 5),
            C("pouch-of-dust", "Pouch of dust", 5),
            C("bone", "Bone", 10),
            C("fat", "Fat", 10),
            C("horn", "Horn", 10),
            C("pouch-of-teeth", "Pouch of teeth", 10),
            C("heart", "Heart", 15),
            C("liver", "Liver", 15),
            C("pouch-of-feathers", "Pouch of feathers", 15),
            C("pouch-of-scales", "Pouch of scales", 15),
            C("brain", "Brain", 20),
            C("skin", "Skin", 20),
            C("soul", "Soul", 25)
        ]),
        Type("construct", "Construct", "competency.investigation", "Investigation",
        [
            C("phial-of-blood", "Phial of blood", 5),
            C("phial-of-oil", "Phial of oil", 5),
            C("flesh", "Flesh", 10),
            C("plating", "Plating", 10),
            C("stone", "Stone", 10),
            C("bone", "Bone", 15),
            C("heart", "Heart", 15),
            C("liver", "Liver", 15),
            C("gears", "Gears", 15),
            C("brain", "Brain", 20),
            C("instructions", "Instructions", 20),
            C("lifespark", "Lifespark", 25)
        ]),
        Type("dragon", "Dragon", "competency.survival", "Survival",
        [
            C("eye", "Eye", 5),
            C("flesh", "Flesh", 5),
            C("phial-of-blood", "Phial of blood", 5),
            C("bone", "Bone", 10),
            C("egg", "Egg", 10),
            C("fat", "Fat", 10),
            C("pouch-of-claws", "Pouch of claws", 10),
            C("pouch-of-teeth", "Pouch of teeth", 10),
            C("horn", "Horn", 15),
            C("liver", "Liver", 15),
            C("pouch-of-scales", "Pouch of scales", 15),
            C("heart", "Heart", 20),
            C("breath-sac", "Breath sac", 25)
        ]),
        Type("elemental", "Elemental", "competency.arcana", "Arcana",
        [
            C("eye", "Eye", 5),
            C("primordial-dust", "Primordial dust", 5),
            C("bone", "Bone", 10),
            C("volatile-mote", "Volatile mote of air/earth/fire/water", 15),
            C("core", "Core of air/earth/fire/water", 25)
        ]),
        Type("fey", "Fey", "competency.arcana", "Arcana",
        [
            C("antenna", "Antenna", 5),
            C("eye", "Eye", 5),
            C("flesh", "Flesh", 5),
            C("hair", "Hair", 5),
            C("phial-of-blood", "Phial of blood", 5),
            C("antler", "Antler", 10),
            C("beak", "Beak", 10),
            C("bone", "Bone", 10),
            C("egg", "Egg", 10),
            C("horn", "Horn", 10),
            C("pouch-of-claws", "Pouch of claws", 10),
            C("pouch-of-teeth", "Pouch of teeth", 10),
            C("talon", "Talon", 10),
            C("tusk", "Tusk", 10),
            C("heart", "Heart", 15),
            C("fat", "Fat", 15),
            C("liver", "Liver", 15),
            C("poison-gland", "Poison gland", 15),
            C("pouch-of-feathers", "Pouch of feathers", 15),
            C("pouch-of-scales", "Pouch of scales", 15),
            C("tentacle", "Tentacle", 15),
            C("tongue", "Tongue", 15),
            C("brain", "Brain", 20),
            C("skin", "Skin", 20),
            C("pelt", "Pelt", 20),
            C("psyche", "Psyche", 25)
        ]),
        Type("fiend", "Fiend", "competency.religion", "Religion",
        [
            C("eye", "Eye", 5),
            C("flesh", "Flesh", 5),
            C("hair", "Hair", 5),
            C("phial-of-blood", "Phial of blood", 5),
            C("pouch-of-dust", "Pouch of dust", 5),
            C("bone", "Bone", 10),
            C("horn", "Horn", 10),
            C("pouch-of-claws", "Pouch of claws", 10),
            C("pouch-of-teeth", "Pouch of teeth", 10),
            C("heart", "Heart", 15),
            C("fat", "Fat", 15),
            C("liver", "Liver", 15),
            C("poison-gland", "Poison gland", 15),
            C("pouch-of-feathers", "Pouch of feathers", 15),
            C("pouch-of-scales", "Pouch of scales", 15),
            C("brain", "Brain", 20),
            C("skin", "Skin", 20),
            C("soul", "Soul", 25)
        ]),
        Type("giant", "Giant", "competency.medicine", "Medicine",
        [
            C("flesh", "Flesh", 5),
            C("nail", "Nail", 5),
            C("phial-of-blood", "Phial of blood", 5),
            C("bone", "Bone", 10),
            C("fat", "Fat", 10),
            C("tooth", "Tooth", 10),
            C("heart", "Heart", 15),
            C("liver", "Liver", 15),
            C("skin", "Skin", 20)
        ]),
        Type("humanoid", "Humanoid", "competency.medicine", "Medicine",
        [
            C("eye", "Eye", 5),
            C("phial-of-blood", "Phial of blood", 5),
            C("bone", "Bone", 10),
            C("egg", "Egg", 10),
            C("pouch-of-teeth", "Pouch of teeth", 10),
            C("heart", "Heart", 15),
            C("liver", "Liver", 15),
            C("pouch-of-feathers", "Pouch of feathers", 15),
            C("pouch-of-scales", "Pouch of scales", 15),
            C("brain", "Brain", 20),
            C("skin", "Skin", 20)
        ]),
        Type("monstrosity", "Monstrosity", "competency.survival", "Survival",
        [
            C("antenna", "Antenna", 5),
            C("eye", "Eye", 5),
            C("flesh", "Flesh", 5),
            C("hair", "Hair", 5),
            C("phial-of-blood", "Phial of blood", 5),
            C("antler", "Antler", 10),
            C("beak", "Beak", 10),
            C("bone", "Bone", 10),
            C("egg", "Egg", 10),
            C("fat", "Fat", 10),
            C("fin", "Fin", 10),
            C("horn", "Horn", 10),
            C("pincer", "Pincer", 10),
            C("pouch-of-claws", "Pouch of claws", 10),
            C("pouch-of-teeth", "Pouch of teeth", 10),
            C("talon", "Talon", 10),
            C("tusk", "Tusk", 10),
            C("heart", "Heart", 15),
            C("liver", "Liver", 15),
            C("poison-gland", "Poison gland", 15),
            C("pouch-of-feathers", "Pouch of feathers", 15),
            C("pouch-of-scales", "Pouch of scales", 15),
            C("stinger", "Stinger", 15),
            C("tentacle", "Tentacle", 15),
            C("chitin", "Chitin", 20),
            C("pelt", "Pelt", 20)
        ]),
        Type("ooze", "Ooze", "competency.nature", "Nature",
        [
            C("phial-of-acid", "Phial of acid", 5),
            C("phial-of-mucus", "Phial of mucus", 10),
            C("vesicle", "Vesicle", 15),
            C("membrane", "Membrane", 20)
        ]),
        Type("plant", "Plant", "competency.nature", "Nature",
        [
            C("phial-of-sap", "Phial of sap", 5),
            C("tuber", "Tuber", 5),
            C("bundle-of-roots", "Bundle of roots", 10),
            C("phial-of-wax", "Phial of wax", 10),
            C("pouch-of-hyphae", "Pouch of hyphae", 10),
            C("pouch-of-leaves", "Pouch of leaves", 10),
            C("poison-gland", "Poison gland", 15),
            C("pouch-of-pollen", "Pouch of pollen", 15),
            C("pouch-of-spores", "Pouch of spores", 15),
            C("bark", "Bark", 20),
            C("membrane", "Membrane", 20)
        ]),
        Type("undead", "Undead", "competency.medicine", "Medicine",
        [
            C("eye", "Eye", 5),
            C("bone", "Bone", 5),
            C("phial-of-congealed-blood", "Phial of congealed blood", 5),
            C("marrow", "Marrow", 10),
            C("pouch-of-teeth", "Pouch of teeth", 10),
            C("rancid-fat", "Rancid fat", 10),
            C("ethereal-ichor", "Ethereal ichor", 15),
            C("undying-flesh", "Undying flesh", 15),
            C("undying-heart", "Undying heart", 20)
        ])
    ];

    private static readonly IReadOnlyDictionary<string, HarvestingCreatureTypeDefinition> ByKey =
        Definitions.ToDictionary(value => value.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<HarvestingCreatureTypeDefinition> CreatureTypes => Definitions;

    public static HarvestingCreatureTypeDefinition? FindCreatureType(string? creatureType)
    {
        if (string.IsNullOrWhiteSpace(creatureType))
        {
            return null;
        }

        return ByKey.TryGetValue(NormalizeKey(creatureType), out var definition)
            ? definition
            : null;
    }

    public static IReadOnlyList<HarvestingResolvedComponent> ResolveComponents(
        HarvestingCreatureTypeDefinition creatureType,
        HarvestingTableEdits? creatureEdits = null,
        HarvestingTableEdits? manualEdits = null)
    {
        ArgumentNullException.ThrowIfNull(creatureType);

        var ordered = creatureType.BaseComponents
            .Select(value => new HarvestingResolvedComponent(
                value.Key,
                value.DisplayName,
                value.ComponentDc,
                value.Quantity,
                HarvestingComponentOrigins.Base))
            .ToList();

        ApplyLayer(ordered, creatureEdits, HarvestingComponentOrigins.Creature);
        ApplyLayer(ordered, manualEdits, HarvestingComponentOrigins.Manual);
        return ordered;
    }

    private static void ApplyLayer(
        List<HarvestingResolvedComponent> components,
        HarvestingTableEdits? edits,
        string origin)
    {
        if (edits is null)
        {
            return;
        }

        var removals = edits.RemoveComponentKeys
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        components.RemoveAll(value => removals.Contains(value.Key));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edit in edits.UpsertComponents)
        {
            if (string.IsNullOrWhiteSpace(edit.Key))
            {
                throw new ArgumentException("Harvesting component edit keys can not be blank.");
            }

            var key = NormalizeKey(edit.Key);
            if (!seen.Add(key))
            {
                throw new ArgumentException($"Harvesting component '{key}' is edited more than once in the same layer.");
            }
            if (edit.ComponentDc is <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(edit.ComponentDc),
                    "Harvesting component DC must be greater than zero.");
            }
            if (edit.Quantity is <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(edit.Quantity),
                    "Harvesting component quantity must be greater than zero.");
            }

            var index = components.FindIndex(value =>
                string.Equals(value.Key, key, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                var existing = components[index];
                components[index] = existing with
                {
                    DisplayName = string.IsNullOrWhiteSpace(edit.DisplayName)
                        ? existing.DisplayName
                        : edit.DisplayName.Trim(),
                    ComponentDc = edit.ComponentDc ?? existing.ComponentDc,
                    Quantity = edit.Quantity ?? existing.Quantity,
                    Origin = origin
                };
                continue;
            }

            if (string.IsNullOrWhiteSpace(edit.DisplayName) || edit.ComponentDc is null)
            {
                throw new ArgumentException(
                    $"New harvesting component '{key}' requires both displayName and componentDc.");
            }

            components.Add(new HarvestingResolvedComponent(
                key,
                edit.DisplayName.Trim(),
                edit.ComponentDc.Value,
                edit.Quantity,
                origin));
        }
    }

    private static HarvestingCreatureTypeDefinition Type(
        string key,
        string displayName,
        string competencyKey,
        string competencyDisplayName,
        IReadOnlyList<HarvestingComponentDefinition> components) =>
        new(key, displayName, competencyKey, competencyDisplayName, components);

    private static HarvestingComponentDefinition C(
        string key,
        string displayName,
        int componentDc) =>
        new(key, displayName, componentDc);

    private static string NormalizeKey(string value) =>
        value.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
}
