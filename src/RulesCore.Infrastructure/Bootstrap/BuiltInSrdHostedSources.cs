using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Bootstrap;

internal static class BuiltInSrdHostedSources
{
    private const string RawRoot = "https://raw.githubusercontent.com/CoolFireGiant/hewnhero-srd/main/data/";

    public static readonly IReadOnlyList<BuiltInHostedSourceSeed> Definitions =
    [
        new(
            "builtin-wotc-srd-5-1",
            Build(
                displayName: "SRD 5.1 public corpus",
                workKey: "srd-5-1",
                workDisplayName: "System Reference Document 5.1",
                editionKey: "5.1",
                editionDisplayName: "SRD 5.1",
                gameEdition: "5e",
                sourceCode: "SRD51",
                publicationDate: null,
                editionSpecificResources:
                [
                    Direct("bestiary/bestiary-srd51.json"),
                    Direct("spells/spells-srd51.json"),
                    Direct("deities.json")
                ],
                note: "Corpus membership was manually reviewed against the official Wizards SRD 5.1 PDF. CoolFireGiant/hewnhero-srd is used only as the structured representation. Aggregate backgrounds, races, and feats are additionally constrained by the checked-in official SRD membership catalog.")),
        new(
            "builtin-wotc-srd-5-2-1",
            Build(
                displayName: "SRD 5.2.1 public corpus",
                workKey: "srd-5-2-1",
                workDisplayName: "System Reference Document 5.2.1",
                editionKey: "5.2.1",
                editionDisplayName: "SRD 5.2.1",
                gameEdition: "5.5e",
                sourceCode: "SRD52",
                publicationDate: new DateOnly(2025, 5, 1),
                editionSpecificResources:
                [
                    Direct("bestiary/bestiary-srd52.json"),
                    Direct("spells/spells-srd52.json")
                ],
                note: "Corpus membership was manually reviewed against the official SRD 5.2.1 PDF. CoolFireGiant/hewnhero-srd is used only as the structured representation. Aggregate backgrounds, species, and feats are constrained by the checked-in official SRD membership catalog; this also selects the PDF-confirmed 2024 Magic Initiate record instead of the malformed duplicate."))
    ];

    public static async Task<int> EnsureAsync(
        RulesCoreDbContext dbContext,
        ISourceImportService importer,
        CancellationToken cancellationToken)
    {
        var service = new HostedSourceService(dbContext, importer);
        var existing = await service.ListAsync(includeDisabled: true, cancellationToken);
        var keys = existing.Select(value => value.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var definition in Definitions)
        {
            if (keys.Contains(definition.Key))
            {
                // Built-in registration is install/bootstrap behavior, not policy enforcement.
                // A Rules Lawyer's later revisions remain authoritative and are never overwritten.
                continue;
            }

            await service.SetAsync(
                definition.Key,
                definition.Request,
                RulesCoreBaselineCatalog.BootstrapActor,
                cancellationToken);
            keys.Add(definition.Key);
        }

        return Definitions.Count;
    }

    private static SetHostedSourceDefinitionRequest Build(
        string displayName,
        string workKey,
        string workDisplayName,
        string editionKey,
        string editionDisplayName,
        string gameEdition,
        string sourceCode,
        DateOnly? publicationDate,
        IReadOnlyList<HostedSourceResourceRequest> editionSpecificResources,
        string note)
    {
        var resources = GetCommonResources()
            .Concat(editionSpecificResources)
            .ToArray();

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
            ReleaseKind: "srd",
            PublicationDate: publicationDate,
            IncludedSourceCodes: [sourceCode],
            Resources: resources,
            IsEnabled: true,
            Note: note);
    }

    private static IReadOnlyList<HostedSourceResourceRequest> GetCommonResources() =>
    [
        Direct("actions.json"),
        Direct("backgrounds.json"),
        Index("class/index.json"),
        Direct("conditionsdiseases.json"),
        Direct("feats.json"),
        Direct("items-base.json"),
        Direct("items.json"),
        Direct("languages.json"),
        Direct("magicvariants.json"),
        Direct("objects.json"),
        Direct("optionalfeatures.json"),
        Direct("races.json"),
        Direct("senses.json"),
        Direct("skills.json"),
        Direct("tables.json"),
        Direct("trapshazards.json"),
        Direct("variantrules.json"),
        Direct("vehicles.json")
    ];

    private static HostedSourceResourceRequest Direct(string path) =>
        new(HostedSourceResourceKinds.DirectJson, RawRoot + path);

    private static HostedSourceResourceRequest Index(string path) =>
        new(HostedSourceResourceKinds.JsonIndex, RawRoot + path);
}

internal sealed record BuiltInHostedSourceSeed(
    string Key,
    SetHostedSourceDefinitionRequest Request);
