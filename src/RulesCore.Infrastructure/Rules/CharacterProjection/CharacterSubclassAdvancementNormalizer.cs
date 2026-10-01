using RulesCore.Application.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Subclasses do not own an independent progression level. Normalize structurally supplied
/// Subclass advancement facts to their effective parent Class occurrence before any mechanics are
/// projected, and surface malformed or ambiguous parent state as explicit conflicts.
/// </summary>
internal static class CharacterSubclassAdvancementNormalizer
{
    internal static CharacterSubclassAdvancementNormalization Normalize(
        CharacterRulesProjectionRequest request,
        IReadOnlyList<ResolvedRuleCatalogItemView> rules)
    {
        var advancements = request.Advancements;
        if (advancements is null || advancements.Count == 0)
        {
            return new CharacterSubclassAdvancementNormalization(request, []);
        }

        var rulesByConcept = rules.ToDictionary(
            value => value.ConceptKey,
            StringComparer.OrdinalIgnoreCase);
        var normalized = new List<CharacterAdvancementFactInput>(advancements.Count);
        var conflicts = new List<CharacterProjectionConflictView>();

        foreach (var advancement in advancements)
        {
            if (!rulesByConcept.TryGetValue(advancement.ConceptKey, out var rule)
                || !string.Equals(rule.EntityType, "subclass", StringComparison.OrdinalIgnoreCase))
            {
                normalized.Add(advancement);
                continue;
            }

            var parents = rule.Relationships
                .Where(value =>
                    string.Equals(value.Kind, "parent-class", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(value.RelatedEntityType, "class", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (parents.Length != 1)
            {
                normalized.Add(advancement with { Level = 0 });
                conflicts.Add(Conflict(
                    rule.ConceptKey,
                    advancement.ParentConceptKey,
                    "subclass-parent-unresolved",
                    $"Rules Core can not determine one effective parent Class for Subclass '{rule.DisplayName}'."));
                continue;
            }

            var parent = parents[0];
            if (!string.IsNullOrWhiteSpace(advancement.ParentConceptKey)
                && !string.Equals(
                    advancement.ParentConceptKey.Trim(),
                    parent.RelatedConceptKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                normalized.Add(advancement with { Level = 0 });
                conflicts.Add(Conflict(
                    rule.ConceptKey,
                    parent.RelatedConceptKey,
                    "subclass-parent-mismatch",
                    $"Subclass '{rule.DisplayName}' belongs to '{parent.RelatedDisplayName}', not '{advancement.ParentConceptKey}'."));
                continue;
            }

            var parentAdvancements = advancements
                .Where(value => value.Level > 0)
                .Where(value => string.Equals(
                    value.ConceptKey,
                    parent.RelatedConceptKey,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (parentAdvancements.Length == 0)
            {
                normalized.Add(advancement with { Level = 0 });
                conflicts.Add(Conflict(
                    rule.ConceptKey,
                    parent.RelatedConceptKey,
                    "subclass-parent-missing",
                    $"Subclass '{rule.DisplayName}' can not progress without its parent Class '{parent.RelatedDisplayName}'."));
                continue;
            }
            if (parentAdvancements.Length > 1)
            {
                normalized.Add(advancement with { Level = 0 });
                conflicts.Add(Conflict(
                    rule.ConceptKey,
                    parent.RelatedConceptKey,
                    "subclass-parent-occurrence-ambiguous",
                    $"More than one '{parent.RelatedDisplayName}' occurrence exists, but the Subclass advancement fact does not identify which parent occurrence it belongs to."));
                continue;
            }

            var parentLevel = parentAdvancements[0].Level;
            if (advancement.Level != parentLevel)
            {
                conflicts.Add(Conflict(
                    rule.ConceptKey,
                    parent.RelatedConceptKey,
                    "subclass-parent-level-mismatch",
                    $"Subclass '{rule.DisplayName}' supplied level {advancement.Level}, but its parent Class '{parent.RelatedDisplayName}' is level {parentLevel}. Subclass progression follows the parent Class."));
            }
            normalized.Add(advancement with
            {
                Level = parentLevel,
                ParentConceptKey = parent.RelatedConceptKey
            });
        }

        return new CharacterSubclassAdvancementNormalization(
            request with { Advancements = normalized },
            conflicts);
    }

    private static CharacterProjectionConflictView Conflict(
        string subclassConceptKey,
        string? parentConceptKey,
        string kind,
        string message) =>
        new(
            $"conflict.advancement.{subclassConceptKey}.{kind}",
            kind,
            message,
            [],
            string.IsNullOrWhiteSpace(parentConceptKey)
                ? [subclassConceptKey]
                : [subclassConceptKey, parentConceptKey]);
}

internal sealed record CharacterSubclassAdvancementNormalization(
    CharacterRulesProjectionRequest Request,
    IReadOnlyList<CharacterProjectionConflictView> Conflicts);
