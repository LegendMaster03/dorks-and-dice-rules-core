using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;

namespace RulesCore.Infrastructure.Bootstrap;

internal static class RulesCoreBaselineCatalog
{
    public const string BootstrapActor = "rules-core-bootstrap";
    public const string HouseRulesPackageKey = "dorks-and-dice-baseline";
    public const string HouseRulesSourceCode = "DDBASE";

    public static readonly IReadOnlyList<SourcePackageSeed> SourcePackages =
    [
        new(
            "wotc-srd-ogl",
            "Wizards of the Coast SRD (OGL)",
            "Wizards of the Coast",
            "OGL-1.0a",
            true,
            [
                new("srd-3e", "System Reference Document 3e", "original", "3e SRD", "3e", SourceReleaseKinds.Srd, null),
                new("srd-3-5e", "System Reference Document 3.5e", "original", "3.5e SRD", "3.5e", SourceReleaseKinds.Srd, null)
            ]),
        new(
            "wotc-srd-cc",
            "Wizards of the Coast SRD (Creative Commons)",
            "Wizards of the Coast",
            "CC-BY-4.0",
            true,
            [
                new("srd-5-1", "System Reference Document 5.1", "5.1", "SRD 5.1", "5e", SourceReleaseKinds.Srd, null),
                new("srd-5-2-1", "System Reference Document 5.2.1", "5.2.1", "SRD 5.2.1", "5.5e", SourceReleaseKinds.Srd, new DateOnly(2025, 5, 1))
            ]),
        new(
            "loot-tavern-free",
            "Loot Tavern Free Releases",
            "Loot Tavern",
            null,
            true,
            []),
        new(
            "loot-tavern-licensed",
            "Loot Tavern Licensed Releases",
            "Loot Tavern",
            null,
            false,
            [])
    ];

    public static readonly IReadOnlyList<HostedSourceSeed> HostedSources =
    [
        new(
            "builtin-wotc-srd-5-1",
            BuildSrdHostedRequest(
                displayName: "SRD 5.1 hosted corpus",
                workKey: "srd-5-1",
                workDisplayName: "System Reference Document 5.1",
                editionKey: "5.1",
                editionDisplayName: "SRD 5.1",
                gameEdition: "5e",
                publicationDate: null,
                sourceCode: "SRD51",
                includeFeats: true,
                note: "Built-in acquisition definition for the SRD 5.1 public corpus. The live representation is the SRD-only, CC BY 4.0 hewnhero-srd data pack; canonical source identity remains the Wizards of the Coast SRD.")),
        new(
            "builtin-wotc-srd-5-2-1",
            BuildSrdHostedRequest(
                displayName: "SRD 5.2.1 hosted corpus",
                workKey: "srd-5-2-1",
                workDisplayName: "System Reference Document 5.2.1",
                editionKey: "5.2.1",
                editionDisplayName: "SRD 5.2.1",
                gameEdition: "5.5e",
                publicationDate: new DateOnly(2025, 5, 1),
                sourceCode: "SRD52",
                includeFeats: false,
                note: "Built-in acquisition definition for the SRD 5.2.1 public corpus. The live representation is the SRD-only, CC BY 4.0 hewnhero-srd data pack. feats.json is intentionally quarantined because the current upstream representation contains a duplicate SRD52 Magic Initiate natural identity; Rules Core does not guess an identity split."))
    ];

    public static readonly IReadOnlyList<BaselineRuleSeed> Rules =
    [
        new("house.healing-potion-use", "Healing Potion Use", "Healing Potion Use"),
        new("house.spell-preparation", "Spell Preparation", "Spell Preparation"),
        new("house.spellcasting-resource-choice", "Spellcasting Resource Choice", "Spellcasting Resource Choice"),
        new("house.controlled-creature-initiative", "Controlled Creature Initiative", "Controlled Creature Initiative"),
        new("house.free-flavor-feats", "Free Flavor Feats", "Free Flavor Feats"),
        new("house.cross-edition-additive-compatibility", "Cross-Edition Additive Compatibility", "Cross-Edition Additive Compatibility")
    ];

    public static Import5eToolsDocumentRequest CreateHouseRuleImportRequest() =>
        new(
            PackageKey: HouseRulesPackageKey,
            PackageDisplayName: "Dorks & Dice Baseline",
            Provider: "Dorks & Dice",
            License: null,
            IsPublic: true,
            WorkKey: "house-rules",
            WorkDisplayName: "Dorks & Dice House Rules",
            EditionKey: "baseline-v1",
            EditionDisplayName: "Baseline v1",
            Json: HouseRulesJson,
            GameEdition: null,
            ReleaseKind: SourceReleaseKinds.Other,
            PublicationDate: new DateOnly(2026, 9, 10));

    private static SetHostedSourceDefinitionRequest BuildSrdHostedRequest(
        string displayName,
        string workKey,
        string workDisplayName,
        string editionKey,
        string editionDisplayName,
        string gameEdition,
        DateOnly? publicationDate,
        string sourceCode,
        bool includeFeats,
        string note)
    {
        var resources = CommonSrdResources.ToList();
        if (includeFeats)
        {
            resources.Add(Direct("feats.json"));
        }

        return new SetHostedSourceDefinitionRequest(
            DisplayName: displayName,
            FormatKind: HostedSourceFormatKinds.FiveEToolsJson,
            PackageKey: "wotc-srd-cc",
            PackageDisplayName: "Wizards of the Coast SRD (Creative Commons)",
            Provider: "Wizards of the Coast",
            License: "CC-BY-4.0",
            IsPublic: true,
            WorkKey: workKey,
            WorkDisplayName: workDisplayName,
            EditionKey: editionKey,
            EditionDisplayName: editionDisplayName,
            GameEdition: gameEdition,
            ReleaseKind: SourceReleaseKinds.Srd,
            PublicationDate: publicationDate,
            IncludedSourceCodes: [sourceCode],
            Resources: resources,
            IsEnabled: true,
            Note: note);
    }

    private static readonly IReadOnlyList<HostedSourceResourceRequest> CommonSrdResources =
    [
        Direct("actions.json"),
        Direct("backgrounds.json"),
        Index("bestiary/index.json"),
        Index("class/index.json"),
        Direct("conditionsdiseases.json"),
        Direct("deities.json"),
        Direct("items-base.json"),
        Direct("items.json"),
        Direct("languages.json"),
        Direct("magicvariants.json"),
        Direct("objects.json"),
        Direct("optionalfeatures.json"),
        Direct("races.json"),
        Direct("senses.json"),
        Direct("skills.json"),
        Index("spells/index.json"),
        Direct("tables.json"),
        Direct("trapshazards.json"),
        Direct("variantrules.json"),
        Direct("vehicles.json")
    ];

    private static HostedSourceResourceRequest Direct(string path) =>
        new(HostedSourceResourceKinds.DirectJson, RawData(path));

    private static HostedSourceResourceRequest Index(string path) =>
        new(HostedSourceResourceKinds.JsonIndex, RawData(path));

    private static string RawData(string path) =>
        $"https://raw.githubusercontent.com/CoolFireGiant/hewnhero-srd/main/data/{path}";

    private const string HouseRulesJson = """
        {
          "houseRule": [
            {
              "name": "Healing Potion Use",
              "source": "DDBASE",
              "category": "action-economy",
              "appliesTo": "healing potions",
              "bonusAction": {
                "healing": "roll normally"
              },
              "action": {
                "healing": "maximum possible healing"
              }
            },
            {
              "name": "Spell Preparation",
              "source": "DDBASE",
              "category": "spellcasting",
              "preparedSpellRestriction": "removed",
              "description": "A caster is not restricted by the normal prepared-spell limit."
            },
            {
              "name": "Spellcasting Resource Choice",
              "source": "DDBASE",
              "category": "spellcasting",
              "casterChoosesResourceSystem": true,
              "availableResourceSystems": [
                "spell slots",
                "spell points"
              ]
            },
            {
              "name": "Controlled Creature Initiative",
              "source": "DDBASE",
              "category": "initiative",
              "controlledCreatureActsOn": "controller initiative"
            },
            {
              "name": "Free Flavor Feats",
              "source": "DDBASE",
              "category": "character-options",
              "eligibility": "purely flavor feats",
              "mechanicalFeatCost": "none"
            },
            {
              "name": "Cross-Edition Additive Compatibility",
              "source": "DDBASE",
              "category": "source-resolution",
              "editions": [
                "3e",
                "3.5e",
                "5e",
                "5.5e"
              ],
              "mergeStrategy": "additive",
              "omissionDoesNotRemoveCompatibleOlderOptions": true,
              "explicitConflictsRequireAdjudication": true,
              "preserveSourceProvenance": true
            }
          ]
        }
        """;
}

internal sealed record SourcePackageSeed(
    string Key,
    string DisplayName,
    string Provider,
    string? License,
    bool IsPublic,
    IReadOnlyList<SourceWorkSeed> Works);

internal sealed record SourceWorkSeed(
    string Key,
    string DisplayName,
    string EditionKey,
    string EditionDisplayName,
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate);

internal sealed record HostedSourceSeed(
    string Key,
    SetHostedSourceDefinitionRequest Request);

internal sealed record BaselineRuleSeed(
    string ConceptKey,
    string DisplayName,
    string SourceEntityName);
