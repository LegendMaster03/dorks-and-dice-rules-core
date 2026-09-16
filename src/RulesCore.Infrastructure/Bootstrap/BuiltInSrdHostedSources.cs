using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Bootstrap;

internal static class BuiltInSrdHostedSources
{
    private const string HewnHeroRevision = "d06d1dadee357857767b1e4da985df6609509bcf";
    private const string ThreeFiveRevision = "c7f30a0ce11a579f75456746f278a4c75f67b4c1";
    private const string RawRoot = $"https://raw.githubusercontent.com/CoolFireGiant/hewnhero-srd/{HewnHeroRevision}/data/";
    private const string ThreeFiveGitHubRoot = $"https://github.com/olimot/srd-v3.5-md/tree/{ThreeFiveRevision}/";

    public static readonly IReadOnlyList<BuiltInHostedSourceSeed> Definitions =
    [
        new(
            "builtin-wotc-srd-3e",
            BuildLegacy(
                displayName: "3e SRD public corpus",
                workKey: "srd-3e",
                workDisplayName: "System Reference Document 3e",
                editionDisplayName: "3e SRD",
                gameEdition: "3e",
                sourceCode: "SRD3",
                resources:
                [
                    new HostedSourceResourceRequest(
                        HostedSourceResourceKinds.HtmlIndex,
                        "https://www.dragon.ee/30srd/")
                ],
                note: "Corpus membership follows the archived 3.0 SRD distribution. Dragon.ee is used only as a surviving HTML representation of that public OGL corpus. SRD3 is a Rules Core normalization code, not a historical Wizards source code.")),
        new(
            "builtin-wotc-srd-3-5e",
            BuildLegacy(
                displayName: "3.5e SRD public corpus",
                workKey: "srd-3-5e",
                workDisplayName: "System Reference Document 3.5e",
                editionDisplayName: "3.5e SRD",
                gameEdition: "3.5e",
                sourceCode: "SRD35",
                resources:
                [
                    MarkdownTree("basic-rules-and-legal"),
                    MarkdownTree("divine"),
                    MarkdownTree("epic"),
                    MarkdownTree("magic-items"),
                    MarkdownTree("monsters"),
                    MarkdownTree("psionics"),
                    MarkdownTree("spells")
                ],
                note: $"Corpus membership follows the archived official Wizards Revised 3.5 SRD distribution. olimot/srd-v3.5-md at reviewed commit {ThreeFiveRevision} is used only as a Markdown representation. SRD35 is a Rules Core normalization code, not a historical Wizards source code.")),
        new(
            "builtin-wotc-srd-5-1",
            BuildCreativeCommons(
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
                note: $"Corpus membership was manually reviewed against the official Wizards SRD 5.1 PDF. CoolFireGiant/hewnhero-srd at reviewed commit {HewnHeroRevision} is used only as the structured representation. Aggregate backgrounds, races, and feats are additionally constrained by the checked-in official SRD membership catalog.")),
        new(
            "builtin-wotc-srd-5-2-1",
            BuildCreativeCommons(
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
                note: $"Corpus membership was manually reviewed against the official Wizards SRD 5.2.1 PDF. CoolFireGiant/hewnhero-srd at reviewed commit {HewnHeroRevision} is used only as the structured representation. Aggregate backgrounds, species, and feats are constrained by the checked-in official SRD membership catalog; this also selects the PDF-confirmed 2024 Magic Initiate record instead of the malformed duplicate."))
    ];

    /// <summary>
    /// Removes only the exact hosted-source definitions that were created by the old bootstrap
    /// path and were never revised. The bundled normalized snapshots are now the built-in SRD
    /// ingestion path. Any first-revision definition whose content differs from the historical
    /// seed is deliberate configuration and is preserved just like a later Rules Lawyer revision.
    /// </summary>
    public static async Task<int> RetireBootstrapDefaultsAsync(
        RulesCoreDbContext dbContext,
        ISourceImportService importer,
        CancellationToken cancellationToken)
    {
        var service = new LegacyAwareHostedSourceService(dbContext, importer);
        var existing = await service.ListAsync(includeDisabled: true, cancellationToken);
        var seedsByKey = Definitions.ToDictionary(value => value.Key, StringComparer.Ordinal);
        var preserved = 0;

        foreach (var definition in existing.Where(value => seedsByKey.ContainsKey(value.Key)))
        {
            var seed = seedsByKey[definition.Key];
            var untouchedBootstrapDefault = definition.RevisionNumber == 1
                && string.Equals(
                    definition.CreatedByUserId,
                    RulesCoreBaselineCatalog.BootstrapActor,
                    StringComparison.Ordinal)
                && MatchesHistoricalSeed(definition, seed.Request);
            if (!untouchedBootstrapDefault)
            {
                preserved++;
                continue;
            }

            await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                DELETE FROM hosted_source_definition
                WHERE hosted_source_definition_id = {{definition.Id}};
                """, cancellationToken);
        }

        return preserved;
    }

    private static bool MatchesHistoricalSeed(
        HostedSourceDefinitionView definition,
        SetHostedSourceDefinitionRequest seed)
    {
        if (!string.Equals(definition.DisplayName, seed.DisplayName, StringComparison.Ordinal)
            || !string.Equals(definition.FormatKind, seed.FormatKind, StringComparison.Ordinal)
            || !string.Equals(definition.PackageKey, seed.PackageKey, StringComparison.Ordinal)
            || !string.Equals(definition.PackageDisplayName, seed.PackageDisplayName, StringComparison.Ordinal)
            || !string.Equals(definition.Provider, seed.Provider, StringComparison.Ordinal)
            || !string.Equals(definition.License, seed.License, StringComparison.Ordinal)
            || definition.IsPublic != seed.IsPublic
            || !string.Equals(definition.WorkKey, seed.WorkKey, StringComparison.Ordinal)
            || !string.Equals(definition.WorkDisplayName, seed.WorkDisplayName, StringComparison.Ordinal)
            || !string.Equals(definition.EditionKey, seed.EditionKey, StringComparison.Ordinal)
            || !string.Equals(definition.EditionDisplayName, seed.EditionDisplayName, StringComparison.Ordinal)
            || !string.Equals(definition.GameEdition, seed.GameEdition, StringComparison.Ordinal)
            || !string.Equals(definition.ReleaseKind, seed.ReleaseKind, StringComparison.Ordinal)
            || definition.PublicationDate != seed.PublicationDate
            || definition.IsEnabled != seed.IsEnabled
            || !string.Equals(definition.Note, seed.Note, StringComparison.Ordinal))
        {
            return false;
        }

        var seedCodes = seed.IncludedSourceCodes ?? [];
        if (!definition.IncludedSourceCodes.SequenceEqual(seedCodes, StringComparer.Ordinal))
        {
            return false;
        }

        var actualResources = definition.Resources
            .Select(value => $"{value.Kind}\n{value.Uri}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var expectedResources = seed.Resources
            .Select(value =>
            {
                var kind = value.Kind.Trim().ToLowerInvariant();
                var uri = new Uri(value.Uri.Trim(), UriKind.Absolute).AbsoluteUri;
                return $"{kind}\n{uri}";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return actualResources.SequenceEqual(expectedResources, StringComparer.Ordinal);
    }

    private static SetHostedSourceDefinitionRequest BuildLegacy(
        string displayName,
        string workKey,
        string workDisplayName,
        string editionDisplayName,
        string gameEdition,
        string sourceCode,
        IReadOnlyList<HostedSourceResourceRequest> resources,
        string note) =>
        new(
            DisplayName: displayName,
            FormatKind: HostedSourceFormatKinds.LegacySrdText,
            PackageKey: "wotc-srd-ogl",
            PackageDisplayName: "Wizards of the Coast SRD (OGL)",
            Provider: "Wizards of the Coast",
            License: "OGL-1.0a",
            IsPublic: true,
            WorkKey: workKey,
            WorkDisplayName: workDisplayName,
            EditionKey: "original",
            EditionDisplayName: editionDisplayName,
            GameEdition: gameEdition,
            ReleaseKind: "srd",
            PublicationDate: null,
            IncludedSourceCodes: [sourceCode],
            Resources: resources,
            IsEnabled: true,
            Note: note);

    private static SetHostedSourceDefinitionRequest BuildCreativeCommons(
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

    private static HostedSourceResourceRequest MarkdownTree(string path) =>
        new(HostedSourceResourceKinds.GitHubTree, ThreeFiveGitHubRoot + path);
}

internal sealed record BuiltInHostedSourceSeed(
    string Key,
    SetHostedSourceDefinitionRequest Request);
