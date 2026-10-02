using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Projects finite effective progression limits for independently leveled Class-family concepts.
/// A limit is rules data, not a Character Sheet constant. Rules Core prefers an explicit normalized
/// maximum and otherwise derives a maximum only from a finite per-level effective progression table.
/// </summary>
internal static class CharacterAdvancementLimitProjector
{
    internal static void Project(
        IReadOnlyList<CharacterProjectionRule> rules,
        CharacterProjectionContext context)
    {
        foreach (var rule in rules.Where(rule =>
                     context.IsSelected(rule.Catalog.ConceptKey)
                     && (string.Equals(rule.Catalog.EntityType, "class", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(rule.Catalog.EntityType, "prestigeClass", StringComparison.OrdinalIgnoreCase))))
        {
            var maximumLevel = ResolveMaximumLevel(rule.Document);
            if (maximumLevel is not > 0)
            {
                continue;
            }

            var mechanicKey = $"advancement.{rule.Catalog.ConceptKey}.maximum-level";
            context.Mechanics[mechanicKey] = new CharacterResolvedMechanicView(
                mechanicKey,
                "advancement",
                $"{rule.Catalog.DisplayName} Maximum Level",
                CharacterResolutionStates.Resolved,
                maximumLevel,
                null,
                "level",
                [],
                [],
                [],
                [],
                [new CharacterMechanicContributionView(
                    mechanicKey,
                    $"{rule.Catalog.DisplayName} effective progression",
                    CharacterEffectOperations.Set,
                    maximumLevel,
                    null,
                    rule.Catalog.ConceptKey,
                    rule.Provenance)],
                rule.Provenance);

            var highestOccurrenceLevel = (context.Request.Advancements ?? [])
                .Where(value => string.Equals(
                    value.ConceptKey,
                    rule.Catalog.ConceptKey,
                    StringComparison.OrdinalIgnoreCase))
                .Select(value => value.Level)
                .DefaultIfEmpty(0)
                .Max();
            var conflictKey = $"conflict.{rule.Catalog.ConceptKey}.maximum-level";

            // Older normalized Class projection already emitted this conflict for explicit
            // maximumLevel data. Replace that legacy-shaped result so every effective maximum,
            // regardless of its internal representation, exposes one stable public conflict kind.
            context.Conflicts.RemoveAll(value =>
                string.Equals(value.ConflictKey, conflictKey, StringComparison.Ordinal));

            if (highestOccurrenceLevel <= maximumLevel)
            {
                continue;
            }

            context.Conflicts.Add(new CharacterProjectionConflictView(
                conflictKey,
                "maximum-level",
                $"{rule.Catalog.DisplayName} is limited to {maximumLevel} levels by the effective rule, but an occurrence supplies level {highestOccurrenceLevel}.",
                [],
                [rule.Catalog.ConceptKey]));
        }
    }

    internal static int? ResolveMaximumLevel(JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (CharacterProjectionJson.TryGetProperty(document, "_rulesCore", out var rulesCore)
            && CharacterProjectionJson.TryGetProperty(rulesCore, "character", out var character))
        {
            var explicitMaximum = CharacterProjectionJson.Integer(character, "maximumLevel");
            if (explicitMaximum is > 0)
            {
                return explicitMaximum;
            }
        }

        if (!CharacterProjectionJson.TryGetProperty(document, "classTableGroups", out var groups)
            || groups.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var rowCounts = groups.EnumerateArray()
            .Where(group => group.ValueKind == JsonValueKind.Object)
            .SelectMany(PerLevelRowCounts)
            .Where(count => count > 0)
            .ToArray();

        return rowCounts.Length == 0 ? null : rowCounts.Max();
    }

    private static IEnumerable<int> PerLevelRowCounts(JsonElement group)
    {
        foreach (var propertyName in new[] { "rows", "rowsSpellProgression" })
        {
            if (CharacterProjectionJson.TryGetProperty(group, propertyName, out var rows)
                && rows.ValueKind == JsonValueKind.Array
                && rows.GetArrayLength() > 0)
            {
                yield return rows.GetArrayLength();
            }
        }
    }
}
