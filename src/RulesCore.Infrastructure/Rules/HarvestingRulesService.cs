using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

public sealed class HarvestingRulesService(
    IGlobalRulesService globalRules,
    ICampaignRulesService campaignRules)
    : IHarvestingRulesService
{
    public HarvestingRulesCatalogView GetCatalog() =>
        new(
            Source(),
            KnownHarvestingRules.CreatureTypes
                .Select(type => new HarvestingCreatureTypeView(
                    type.Key,
                    type.DisplayName,
                    type.SkillConceptKey,
                    type.SkillDisplayName,
                    type.BaseComponents
                        .Select(component => ToView(
                            new HarvestingResolvedComponent(
                                component.Key,
                                component.DisplayName,
                                component.ComponentDc,
                                component.Quantity,
                                HarvestingComponentOrigins.Base)))
                        .ToArray()))
                .ToArray(),
            Procedure());

    public async Task<HarvestingResolvedTableView?> ResolveGlobalAsync(
        HarvestingTableResolutionRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateResolutionRequest(request);

        if (!string.IsNullOrWhiteSpace(request.CreatureConceptKey))
        {
            var rule = await globalRules.ResolveLatestAsync(
                request.CreatureConceptKey.Trim(),
                userId,
                cancellationToken);
            return rule is null
                ? null
                : ResolveFromCreature(
                    rule.ConceptKey,
                    rule.DisplayName,
                    rule.EntityType,
                    rule.Document,
                    request.ManualEdits);
        }

        return ResolveFromType(request.CreatureType!, request.ManualEdits);
    }

    public async Task<HarvestingResolvedTableView?> ResolveCampaignAsync(
        Guid campaignId,
        HarvestingTableResolutionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }

        ValidateResolutionRequest(request);

        if (!string.IsNullOrWhiteSpace(request.CreatureConceptKey))
        {
            var rule = await campaignRules.ResolveLatestAsync(
                campaignId,
                request.CreatureConceptKey.Trim(),
                userId.Trim(),
                cancellationToken);
            return rule is null
                ? null
                : ResolveFromCreature(
                    rule.ConceptKey,
                    rule.DisplayName,
                    rule.EntityType,
                    rule.Document,
                    request.ManualEdits);
        }

        return ResolveFromType(request.CreatureType!, request.ManualEdits);
    }

    private static HarvestingResolvedTableView ResolveFromCreature(
        string conceptKey,
        string displayName,
        string entityType,
        JsonElement document,
        HarvestingTableEditRequest? manualEdits)
    {
        if (!string.Equals(entityType, "monster", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Rule concept '{conceptKey}' is '{entityType}', not a monster.");
        }

        var creatureEdits = ReadCreatureEdits(document);
        var creatureType = ReadCreatureType(document)
            ?? throw new InvalidOperationException(
                $"Monster '{conceptKey}' does not expose a recognized creature type.");
        var creatureSize = ReadCreatureSize(document);

        return Resolve(
            creatureType,
            creatureEdits,
            ConvertEdits(manualEdits),
            conceptKey,
            displayName,
            creatureSize);
    }

    private static HarvestingResolvedTableView ResolveFromType(
        string creatureType,
        HarvestingTableEditRequest? manualEdits) =>
        Resolve(
            creatureType,
            null,
            ConvertEdits(manualEdits),
            null,
            null,
            null);

    private static HarvestingResolvedTableView Resolve(
        string creatureTypeKey,
        HarvestingTableEdits? creatureEdits,
        HarvestingTableEdits? manualEdits,
        string? creatureConceptKey,
        string? creatureDisplayName,
        string? creatureSize)
    {
        var type = KnownHarvestingRules.FindCreatureType(creatureTypeKey)
            ?? throw new ArgumentException(
                $"Creature type '{creatureTypeKey}' is not present in the Harvesting rules catalog.");

        var components = KnownHarvestingRules.ResolveComponents(
            type,
            creatureEdits,
            manualEdits);

        return new HarvestingResolvedTableView(
            Source(),
            type.Key,
            type.DisplayName,
            type.SkillConceptKey,
            type.SkillDisplayName,
            components.Select(ToView).ToArray(),
            creatureConceptKey,
            creatureDisplayName,
            creatureSize,
            HasEdits(creatureEdits),
            HasEdits(manualEdits));
    }

    private static string? ReadCreatureType(JsonElement document)
    {
        if (TryGet(document, "type", out var type))
        {
            if (type.ValueKind == JsonValueKind.String)
            {
                return type.GetString();
            }

            if (type.ValueKind == JsonValueKind.Object
                && TryGet(type, "type", out var nested)
                && nested.ValueKind == JsonValueKind.String)
            {
                return nested.GetString();
            }

            if (type.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in type.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        return item.GetString();
                    }
                    if (item.ValueKind == JsonValueKind.Object
                        && TryGet(item, "type", out var itemType)
                        && itemType.ValueKind == JsonValueKind.String)
                    {
                        return itemType.GetString();
                    }
                }
            }
        }

        return TryGetHarvestingExtension(document, out var harvesting)
            && TryGet(harvesting, "creatureType", out var explicitType)
            && explicitType.ValueKind == JsonValueKind.String
                ? explicitType.GetString()
                : null;
    }

    private static string? ReadCreatureSize(JsonElement document)
    {
        if (!TryGet(document, "size", out var size))
        {
            return null;
        }

        var values = new List<string>();
        if (size.ValueKind == JsonValueKind.String)
        {
            if (!string.IsNullOrWhiteSpace(size.GetString()))
            {
                values.Add(size.GetString()!);
            }
        }
        else if (size.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in size.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    values.Add(item.GetString()!);
                }
            }
        }

        var normalized = values
            .Select(UniversalSizeCategories.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length switch
        {
            0 => null,
            1 => normalized[0],
            _ => throw new InvalidOperationException(
                "Monster Harvesting resolution requires one effective creature size.")
        };
    }

    private static HarvestingTableEdits? ReadCreatureEdits(JsonElement document)
    {
        if (!TryGetHarvestingExtension(document, out var harvesting))
        {
            return null;
        }

        var removals = new List<string>();
        if (TryGet(harvesting, "removeComponents", out var remove)
            && remove.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in remove.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    removals.Add(item.GetString()!.Trim());
                }
            }
        }

        var upserts = new List<HarvestingComponentEdit>();
        if (TryGet(harvesting, "upsertComponents", out var upsert)
            && upsert.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in upsert.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException(
                        "Monster harvesting upsertComponents entries must be objects.");
                }

                var key = ReadString(item, "key")
                    ?? throw new InvalidOperationException(
                        "Monster harvesting component overrides require a stable key.");
                var displayName = ReadString(item, "displayName")
                    ?? ReadString(item, "name");
                var componentDc = ReadInt(item, "componentDc");
                var quantity = ReadInt(item, "quantity");
                upserts.Add(new HarvestingComponentEdit(
                    key,
                    displayName,
                    componentDc,
                    quantity));
            }
        }

        return removals.Count == 0 && upserts.Count == 0
            ? null
            : new HarvestingTableEdits(removals, upserts);
    }

    private static HarvestingTableEdits? ConvertEdits(HarvestingTableEditRequest? request)
    {
        if (request is null)
        {
            return null;
        }

        var removals = (request.RemoveComponentKeys ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        var upserts = (request.UpsertComponents ?? [])
            .Select(value => new HarvestingComponentEdit(
                value.Key,
                value.DisplayName,
                value.ComponentDc,
                value.Quantity))
            .ToArray();

        return removals.Length == 0 && upserts.Length == 0
            ? null
            : new HarvestingTableEdits(removals, upserts);
    }

    private static bool TryGetHarvestingExtension(
        JsonElement document,
        out JsonElement harvesting)
    {
        harvesting = default;
        return TryGet(document, "_rulesCore", out var extension)
            && extension.ValueKind == JsonValueKind.Object
            && TryGet(extension, "harvesting", out harvesting)
            && harvesting.ValueKind == JsonValueKind.Object;
    }

    private static bool TryGet(JsonElement element, string key, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(key, out value);
    }

    private static string? ReadString(JsonElement element, string key) =>
        TryGet(element, key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string key) =>
        TryGet(element, key, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static HarvestingComponentView ToView(HarvestingResolvedComponent component) =>
        new(
            component.Key,
            component.DisplayName,
            component.ComponentDc,
            component.Quantity,
            component.Origin);

    private static HarvestingProcedureView Procedure() =>
        new(
            new HarvestingProcedureCheckView(
                KnownHarvestingRules.AssessmentMechanicKey,
                KnownHarvestingRules.AssessmentAbilityKey,
                KnownHarvestingRules.AssessmentAbilityDisplayName,
                CharacterMechanicRollModes.Normal),
            new HarvestingProcedureCheckView(
                KnownHarvestingRules.CarvingMechanicKey,
                KnownHarvestingRules.CarvingAbilityKey,
                KnownHarvestingRules.CarvingAbilityDisplayName,
                CharacterMechanicRollModes.Normal),
            KnownHarvestingRules.TotalMechanicKey,
            CharacterMechanicRollModes.Disadvantage,
            KnownHarvestingRules.ComponentDcAggregation,
            KnownHarvestingRules.AwardMode,
            new HarvestingHelperRulesView(
                new Dictionary<string, int>(
                    KnownHarvestingRules.HelperLimitsByCreatureSize,
                    StringComparer.OrdinalIgnoreCase),
                StandardHelpActionApplies: false));

    private static HarvestingSourceView Source() =>
        new(
            KnownHarvestingRules.WorkKey,
            KnownHarvestingRules.WorkDisplayName,
            KnownHarvestingRules.Provider,
            KnownHarvestingRules.GameEdition,
            KnownHarvestingRules.ReleaseKind,
            KnownHarvestingRules.PublicationDate,
            KnownHarvestingRules.ReferenceUri);

    private static bool HasEdits(HarvestingTableEdits? edits) =>
        edits is not null
        && (edits.RemoveComponentKeys.Count > 0 || edits.UpsertComponents.Count > 0);

    private static void ValidateResolutionRequest(HarvestingTableResolutionRequest request)
    {
        var hasConcept = !string.IsNullOrWhiteSpace(request.CreatureConceptKey);
        var hasType = !string.IsNullOrWhiteSpace(request.CreatureType);
        if (hasConcept == hasType)
        {
            throw new ArgumentException(
                "Supply exactly one of creatureConceptKey or creatureType.");
        }
    }
}
