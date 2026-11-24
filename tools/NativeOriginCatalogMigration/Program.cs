using System.Text.Json;
using System.Text.Json.Serialization;
using RulesCore.Content;
using RulesCore.Mechanics;

if (args.Length != 4)
{
    Console.Error.WriteLine(
        "Usage: <previous-rulesets-root> <current-rulesets-root> <stored-export.json> <new-export.json>");
    return 2;
}

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true
};

try
{
    var previousCatalog = Load(args[0]);
    var currentCatalog = Load(args[1]);
    var saved = JsonSerializer.Deserialize<OriginExport>(File.ReadAllText(args[2]), options)
        ?? throw new InvalidDataException("Stored Origin export is missing.");
    var proposal = saved.Proposal;
    if (saved.Fingerprint != previousCatalog.Fingerprint ||
        previousCatalog.Fingerprint == currentCatalog.Fingerprint ||
        proposal is null || proposal.Intent != NativeOriginIntent.Direct ||
        proposal.Method != new NativeOriginMethodReference("rule.creature-origination", "biological") ||
        proposal.Contributors is not { Count: 0 } ||
        proposal.Candidates is not { Count: 0 } ||
        proposal.ExistingConfiguration is not null ||
        proposal.ExistingEffectiveConfiguration is not null ||
        proposal.BiologicalTransmission is not null ||
        (proposal.TableDecisions?.Count ?? 0) != 0)
        throw new InvalidDataException("Expected an authenticated export of a prior native direct biological Origin.");

    var oldOrigin = Replay(previousCatalog, proposal);
    if (!Equal(saved.Profile, oldOrigin.Profile))
        throw new InvalidDataException("Saved Origin differs from its prior catalog reconstruction.");

    var currentRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1]));
    var migrationPath = Path.Combine(Path.GetDirectoryName(currentRoot)!,
        "migrations", "biological-gene-ids-20261121.json");
    using var document = JsonDocument.Parse(File.ReadAllText(migrationPath));
    var manifest = document.RootElement;
    var identityMap = manifest.GetProperty("identityMap").Deserialize<Dictionary<string, string>>();
    if (manifest.GetProperty("kind").GetString() != "biological-gene-id-migration" ||
        manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
        identityMap is null || identityMap.Count != manifest.GetProperty("legacyGeneCount").GetInt32() ||
        identityMap.Values.Any(string.IsNullOrWhiteSpace) ||
        identityMap.Values.Distinct(StringComparer.Ordinal).Count() !=
            manifest.GetProperty("canonicalGeneCount").GetInt32())
        throw new InvalidDataException("Biological gene migration manifest is invalid.");
    var currentOrigin = Replay(currentCatalog, proposal);
    if (!Equal(Comparable(oldOrigin.Profile!, identityMap),
               Comparable(currentOrigin.Profile!, new Dictionary<string, string>())))
        throw new InvalidDataException(
            "Current replay changed a mechanical grant, selection, or provenance. Review the migration manually.");

    var character = saved.Character;
    var ledger = saved.Ledger;
    if (character is not null && (character.RulesetId != previousCatalog.RulesetId ||
        character.Fingerprint != previousCatalog.Fingerprint))
        throw new InvalidDataException("Stored character projection input has a different catalog fingerprint.");

    var migratedCharacter = character is null ? null :
        character with { Fingerprint = currentCatalog.Fingerprint };
    if (ledger is not null)
    {
        if (character is null)
            throw new InvalidDataException("The saved ledger requires its trusted character projection input.");
        var migrated = NativeOriginInnateSpellResourceLedger.MigrateCatalog(
            previousCatalog, oldOrigin, new NativeCharacterProjector(previousCatalog).Project(character),
            currentCatalog, currentOrigin, new NativeCharacterProjector(currentCatalog).Project(migratedCharacter!),
            ledger, ledger.Revision, identityMap);
        ledger = migrated.State ?? throw new InvalidDataException(
            "The saved spell ledger needs manual reconciliation: " + migrated.Reason);
    }

    var export = new OriginExport(currentCatalog.Fingerprint, proposal, currentOrigin.Profile!,
        migratedCharacter, ledger);
    var outputPath = Path.GetFullPath(args[3]);
    var temporary = Path.Combine(Path.GetDirectoryName(outputPath)!, Path.GetRandomFileName());
    var content = JsonSerializer.SerializeToUtf8Bytes(export, options);
    try
    {
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            output.Write(content);
        File.Move(temporary, outputPath, overwrite: false);
    }
    finally
    {
        File.Delete(temporary);
    }
    return 0;
}
catch (Exception error) when (error is IOException or InvalidDataException or JsonException or ArgumentException or
    InvalidOperationException or KeyNotFoundException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static NativeCatalogSnapshot Load(string rulesets) =>
    NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(rulesets, "first-party-001"));

static NativeOriginConstructionResult Replay(NativeCatalogSnapshot catalog, NativeOriginProposal proposal)
{
    var result = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(proposal);
    if (!result.IsMechanicallyAuthorized || result.Profile is null || result.Issues.Count != 0)
        throw new InvalidDataException("Origin replay failed: " +
            string.Join(", ", result.Issues.Select(x => x.Code)));
    return result;
}

static bool Equal(NativeOriginProfile? left, NativeOriginProfile? right) =>
    JsonElement.DeepEquals(JsonSerializer.SerializeToElement(left),
        JsonSerializer.SerializeToElement(right));

static NativeOriginProfile Comparable(NativeOriginProfile profile,
    IReadOnlyDictionary<string, string> identityMap)
{
    return profile with
    {
        Underlying = Configuration(profile.Underlying),
        Current = Configuration(profile.Current)
    };

    NativeOriginCurrentConfiguration Configuration(NativeOriginCurrentConfiguration configuration) =>
        configuration with
        {
            InherentGrants = configuration.InherentGrants.Select(ComparableGrant).ToArray()
        };

    NativeOriginEffectiveGrant ComparableGrant(NativeOriginEffectiveGrant grant)
    {
        if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
            grant.Identity.MechanicalDefinitionId is not { } id ||
            grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } value)
            return grant;

        return grant with
        {
            Identity = grant.Identity with
            {
                MechanicalDefinitionId = identityMap.GetValueOrDefault(id, id)
            },
            EffectiveValue = JsonSerializer.SerializeToElement(value.EnumerateObject()
                .Where(property => property.Name is not ("name" or "description" or "reviewNotes"))
                .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal))
        };
    }
}

sealed record OriginExport(string Fingerprint, NativeOriginProposal Proposal, NativeOriginProfile Profile,
    NativeCharacterRequest? Character = null, NativeOriginInnateSpellLedgerState? Ledger = null);
