using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

// Type defaults and authored source metadata live in the active native ruleset.
// These records preserve the established Loot Tavern workflow response shape.
public sealed record NativeHarvestingSource(
    string WorkKey, string WorkDisplayName, string Provider, string GameEdition,
    string ReleaseKind, string PublicationDate, string ReferenceUri);
public sealed record NativeHarvestingComponent(
    string Key, string DisplayName, int ComponentDc, int? Quantity = null,
    string? Origin = null);
public sealed record NativeHarvestingCreatureType(
    string Key, string DisplayName, string CompetencyKey,
    string CompetencyDisplayName, IReadOnlyList<NativeHarvestingComponent> DefaultComponents);
public sealed record NativeHarvestingCheck(
    string MechanicKey, string AbilityKey, string AbilityDisplayName, string DefaultRollMode);
public sealed record NativeHarvestingHelperRules(
    IReadOnlyDictionary<string, int> MaximumByCreatureSize, bool StandardHelpActionApplies);
public sealed record NativeHarvestingProcedure(
    NativeHarvestingCheck Assessment, NativeHarvestingCheck Carving,
    string TotalMechanicKey, string SameActorRollMode,
    string ComponentDcAggregation, string AwardMode, NativeHarvestingHelperRules Helpers);
public sealed record NativeHarvestingCatalog(
    int ContractVersion, string RulesetId, string Fingerprint,
    NativeHarvestingSource Source, IReadOnlyList<NativeHarvestingCreatureType> CreatureTypes,
    NativeHarvestingProcedure Procedure);
public sealed record NativeHarvestingComponentEdit(
    string Key, string? DisplayName = null, int? ComponentDc = null, int? Quantity = null);
public sealed record NativeHarvestingEdits(
    IReadOnlyList<string>? RemoveComponentKeys = null,
    IReadOnlyList<NativeHarvestingComponentEdit>? UpsertComponents = null);
public sealed record NativeHarvestingTableRequest(
    string RulesetId, string Fingerprint, string? CreatureConceptKey = null,
    string? CreatureType = null, NativeHarvestingEdits? ManualEdits = null);
public sealed record NativeHarvestingTable(
    NativeHarvestingSource Source, string CreatureType, string CreatureTypeDisplayName,
    string CompetencyKey, string CompetencyDisplayName,
    IReadOnlyList<NativeHarvestingComponent> Components,
    string? CreatureConceptKey = null, string? CreatureDisplayName = null,
    string? CreatureSize = null, bool CreatureOverridesApplied = false,
    bool ManualEditsApplied = false);
public sealed record NativeHarvestingHelper(
    int ProficiencyBonus, bool IsProficient,
    bool ParticipatedForEntireDuration = true,
    bool IsAssessmentParticipant = false, bool IsCarvingParticipant = false);
public sealed record NativeHarvestingOutcomeRequest(
    NativeHarvestingTableRequest Table,
    int AssessmentResult, int CarvingResult, bool SameActor,
    IReadOnlyList<string> HarvestOrderComponentKeys,
    string? CreatureSize = null, IReadOnlyList<NativeHarvestingHelper>? Helpers = null);
public sealed record NativeHarvestingComponentOutcome(
    string Key, string DisplayName, int ComponentDc, int HarvestDc,
    int? Quantity, string? Origin, bool Awarded);
public sealed record NativeHarvestingOutcome(
    NativeHarvestingTable Table, string AssessmentRollMode,
    string CarvingRollMode, string? CreatureSize, int AssessmentResult,
    int CarvingResult, int HelperCount, int HelperContribution, int HarvestingResult,
    IReadOnlyList<NativeHarvestingComponentOutcome> Components);

/// <summary>
/// Resolves the previously approved Harvesting & Crafting Lite procedure from
/// one immutable native content snapshot. No runtime legacy rules or independent
/// Character inventory are used. Actual awards remain Character Sheet actions.
/// </summary>
public sealed class NativeHarvestingResolver(NativeCatalogSnapshot catalog)
{
    private const string ProcedureId = "procedure.harvesting.loot-tavern-lite";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public NativeHarvestingCatalog GetCatalog(string fingerprint)
    {
        ValidateSnapshot(catalog.RulesetId, fingerprint);
        if (!catalog.TryGetById(ProcedureId, out var rule) ||
            rule?.Category != "harvesting-procedure")
            throw new InvalidOperationException("No approved native harvesting procedure is loaded.");
        var source = Read<NativeHarvestingSource>(rule.Definition, "source");
        var procedure = Read<NativeHarvestingProcedure>(rule.Definition, "procedure");
        var types = Read<NativeHarvestingCreatureType[]>(rule.Definition, "creatureTypes");
        if (types.Length == 0 || types.Length > 100 ||
            types.Any(type => type.DefaultComponents is null ||
                type.DefaultComponents.Count > 200 ||
                type.DefaultComponents.Any(c => c.ComponentDc is < 1 or > 100000)))
            throw new InvalidOperationException("Native harvesting definitions are incomplete.");
        return new(1, catalog.RulesetId, catalog.Fingerprint, source, types, procedure);
    }

    public NativeHarvestingTable Resolve(NativeHarvestingTableRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshot(request.RulesetId, request.Fingerprint);
        if (string.IsNullOrWhiteSpace(request.CreatureConceptKey) ==
            string.IsNullOrWhiteSpace(request.CreatureType))
            throw new ArgumentException("Choose exactly one native monster or creature type.");
        var rules = GetCatalog(request.Fingerprint);
        string? monsterId = null, monsterName = null, size = null;
        NativeHarvestingEdits? creatureEdits = null;
        var typeKey = request.CreatureType;
        if (!string.IsNullOrWhiteSpace(request.CreatureConceptKey))
        {
            monsterId = request.CreatureConceptKey.Trim();
            if (!catalog.TryGetById(monsterId, out var monster) ||
                monster?.Category != "monster" ||
                !TryObject(monster.Definition, "definition", out var definition))
                throw new KeyNotFoundException("Selected monster has no native bestiary record.");
            monsterName = monster.Name;
            typeKey = CreatureType(definition);
            size = Size(definition);
            // Preserve authored per-monster overrides; an absence of overrides
            // does not require an artificial empty placeholder.
            if (TryObject(definition, "_rulesCore", out var extra) &&
                TryObject(extra, "harvesting", out var extension))
                creatureEdits = new(
                    ReadOptional<string[]>(extension, "removeComponents"),
                    ReadOptional<NativeHarvestingComponentEdit[]>(extension, "upsertComponents"));
            if (string.IsNullOrWhiteSpace(typeKey))
                throw new InvalidOperationException("Monster has no resolved creature type.");
        }
        var type = rules.CreatureTypes.SingleOrDefault(t =>
            string.Equals(t.Key, typeKey?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("Creature type is not in the native harvesting catalog.");
        var components = type.DefaultComponents.Select(c => c with { Origin = "base" }).ToList();
        ApplyEdits(components, creatureEdits, "creature");
        ApplyEdits(components, request.ManualEdits, "manual");
        return new(rules.Source, type.Key, type.DisplayName, type.CompetencyKey,
            type.CompetencyDisplayName, components, monsterId, monsterName, size,
            HasEdits(creatureEdits), HasEdits(request.ManualEdits));
    }

    public NativeHarvestingOutcome Evaluate(NativeHarvestingOutcomeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Table is null || request.HarvestOrderComponentKeys is null ||
            request.HarvestOrderComponentKeys.Count is < 1 or > 200 ||
            request.AssessmentResult is < -100000 or > 100000 ||
            request.CarvingResult is < -100000 or > 100000)
            throw new ArgumentException("Harvesting results and an ordered component list are required.");
        var table = Resolve(request.Table);
        var available = table.Components.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<NativeHarvestingComponent>();
        foreach (var key in request.HarvestOrderComponentKeys)
        {
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key.Trim()))
                throw new ArgumentException("Harvesting component keys must be unique and nonblank.");
            if (!available.TryGetValue(key.Trim(), out var component))
                throw new KeyNotFoundException("Harvesting order contains an unknown component.");
            ordered.Add(component);
        }
        var helpers = request.Helpers ?? [];
        if (helpers.Count > 10)
            throw new ArgumentException("Too many harvesting helpers.");
        var size = table.CreatureSize ?? NormalizeSize(request.CreatureSize);
        if (table.CreatureSize is not null && request.CreatureSize is not null &&
            NormalizeSize(request.CreatureSize) != table.CreatureSize)
            throw new ArgumentException("The supplied size differs from the native monster size.");
        if (helpers.Count > 0 && size is null)
            throw new ArgumentException("Creature size is required when helpers participate.");
        var limits = GetCatalog(request.Table.Fingerprint).Procedure.Helpers.MaximumByCreatureSize;
        if (helpers.Count > 0 &&
            (size is null || !limits.TryGetValue(size, out var maximum) ||
                helpers.Count > maximum))
            throw new ArgumentException("Harvesting helpers exceed the creature-size limit.");
        if (helpers.Any(h => h.ProficiencyBonus is < 0 or > 1000 ||
            !h.ParticipatedForEntireDuration ||
            h.IsAssessmentParticipant || h.IsCarvingParticipant))
            throw new ArgumentException("Harvesting helpers must be eligible and present throughout.");
        var contribution = helpers.Sum(h => h.IsProficient
            ? h.ProficiencyBonus : h.ProficiencyBonus / 2);
        var result = checked(request.AssessmentResult + request.CarvingResult + contribution);
        var cumulative = 0;
        var outcomes = new List<NativeHarvestingComponentOutcome>();
        foreach (var component in ordered)
        {
            cumulative = checked(cumulative + component.ComponentDc);
            outcomes.Add(new(component.Key, component.DisplayName, component.ComponentDc,
                cumulative, component.Quantity, component.Origin, result >= cumulative));
        }
        var mode = request.SameActor ? "disadvantage" : "normal";
        return new(table, mode, mode, size, request.AssessmentResult,
            request.CarvingResult, helpers.Count, contribution, result, outcomes);
    }

    private void ValidateSnapshot(string? id, string? fingerprint)
    {
        if (id != catalog.RulesetId || fingerprint != catalog.Fingerprint)
            throw new InvalidOperationException("ruleset_version_mismatch");
    }

    private static void ApplyEdits(List<NativeHarvestingComponent> components,
        NativeHarvestingEdits? edits, string origin)
    {
        if (!HasEdits(edits)) return;
        if (edits!.RemoveComponentKeys?.Count > 200 ||
            edits.UpsertComponents?.Count > 200)
            throw new ArgumentException("Harvesting component edits exceed the allowed limit.");
        var removed = (edits.RemoveComponentKeys ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        components.RemoveAll(c => removed.Contains(c.Key));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in edits.UpsertComponents ?? [])
        {
            if (change is null || string.IsNullOrWhiteSpace(change.Key) ||
                change.Key.Length > 160)
                throw new ArgumentException("Harvesting component key is invalid.");
            var key = Key(change.Key);
            if (!seen.Add(key) || change.ComponentDc is <= 0 or > 100000 ||
                change.Quantity is <= 0 or > 1000000 ||
                change.DisplayName?.Length > 200)
                throw new ArgumentException("Harvesting edit is duplicated or invalid.");
            var i = components.FindIndex(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (i >= 0)
            {
                var original = components[i];
                components[i] = original with {
                    DisplayName = string.IsNullOrWhiteSpace(change.DisplayName)
                        ? original.DisplayName : change.DisplayName.Trim(),
                    ComponentDc = change.ComponentDc ?? original.ComponentDc,
                    Quantity = change.Quantity ?? original.Quantity, Origin = origin
                };
            }
            else
            {
                if (string.IsNullOrWhiteSpace(change.DisplayName) || change.ComponentDc is null)
                    throw new ArgumentException("New Harvesting component requires a name and DC.");
                components.Add(new(key, change.DisplayName.Trim(),
                    change.ComponentDc.Value, change.Quantity, origin));
            }
        }
    }

    private static string Key(string text) =>
        text.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
    private static bool HasEdits(NativeHarvestingEdits? edits) =>
        (edits?.RemoveComponentKeys?.Count ?? 0) > 0 ||
        (edits?.UpsertComponents?.Count ?? 0) > 0;

    private static string? CreatureType(JsonElement definition)
    {
        if (!definition.TryGetProperty("type", out var type)) return null;
        if (type.ValueKind == JsonValueKind.String) return type.GetString();
        if (type.ValueKind == JsonValueKind.Object)
            return Text(type, "type");
        if (type.ValueKind == JsonValueKind.Array)
            return type.EnumerateArray().Select(t => t.ValueKind switch {
                JsonValueKind.String => t.GetString(),
                JsonValueKind.Object => Text(t, "type"),
                _ => null
            }).FirstOrDefault(t => t is not null);
        return null;
    }

    private static string? Size(JsonElement definition)
    {
        if (!definition.TryGetProperty("size", out var size)) return null;
        var sizes = size.ValueKind switch {
            JsonValueKind.String => [size.GetString()!],
            JsonValueKind.Array => size.EnumerateArray().Where(v =>
                v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).ToArray(),
            _ => Array.Empty<string>()
        };
        var normalized = sizes.Select(NormalizeSize).Distinct(StringComparer.Ordinal).ToArray();
        if (normalized.Length > 1)
            throw new InvalidOperationException("Monster has multiple unresolved sizes.");
        return normalized.FirstOrDefault();
    }

    private static string? NormalizeSize(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch {
            "t" or "tiny" => "Tiny", "s" or "small" => "Small",
            "m" or "medium" => "Medium", "l" or "large" => "Large",
            "h" or "huge" => "Huge", "g" or "gargantuan" => "Gargantuan",
            null or "" => null,
            _ => throw new ArgumentException("Unknown harvesting creature size.")
        };
    private static string? Text(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object &&
        node.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() : null;
    private static bool TryObject(JsonElement node, string name, out JsonElement value) =>
        node.ValueKind == JsonValueKind.Object &&
        node.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;
    private static T Read<T>(JsonElement node, string key)
    {
        if (node.TryGetProperty(key, out var prop))
            return prop.Deserialize<T>(JsonOptions) ??
                throw new InvalidOperationException("Native Harvesting definition is incomplete.");
        throw new InvalidOperationException("Native Harvesting definition is missing " + key + ".");
    }
    private static T? ReadOptional<T>(JsonElement node, string key)
    {
        if (node.TryGetProperty(key, out var prop) && prop.ValueKind != JsonValueKind.Null)
            return prop.Deserialize<T>(JsonOptions);
        return default;
    }
}
