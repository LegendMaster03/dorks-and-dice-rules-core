using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Coordinates construction of Character support from an already-resolved rules scope.
/// Effective document materialization, parsing, and provenance loading are delegated to
/// focused collaborators.
/// </summary>
internal static class CharacterSupportCatalogBuilder
{
    public static async Task<CharacterSupportCatalogDefinition> BuildAsync(
        RulesCoreDbContext dbContext,
        ResolvedRulesCatalogView rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Rules.Count == 0 || rules.RevisionNumber is null)
        {
            return Empty(rules);
        }

        var documents = await CharacterSupportEffectiveDocumentReader.ReadAsync(
            dbContext,
            rules,
            cancellationToken);
        var attributions = await CharacterSupportAttributionReader.ReadAsync(
            dbContext,
            rules.Rules,
            cancellationToken);
        var ruleByConceptKey = rules.Rules.ToDictionary(
            value => value.ConceptKey,
            value => value,
            StringComparer.OrdinalIgnoreCase);

        var recovery = new List<CharacterRecoveryProcedureDefinition>();
        var passive = new List<CharacterPassiveValueDefinition>();
        var qualifications = new List<CharacterQualificationDefinition>();

        foreach (var rule in rules.Rules)
        {
            if (!documents.TryGetValue(rule.RuleConceptId, out var document))
            {
                continue;
            }
            if (!CharacterSupportDefinitionParser.TryGetCharacterSupport(
                    document,
                    out var support))
            {
                continue;
            }

            var sourceAttributions = attributions.GetValueOrDefault(
                rule.RuleConceptId,
                []);
            CharacterSupportDefinitionParser.ParseRecoveryProcedures(
                support,
                rule,
                sourceAttributions,
                recovery);
            CharacterSupportDefinitionParser.ParsePassiveValues(
                support,
                rule,
                sourceAttributions,
                ruleByConceptKey,
                passive);
            CharacterSupportDefinitionParser.ParseQualifications(
                support,
                rule,
                sourceAttributions,
                ruleByConceptKey,
                qualifications);
        }

        CharacterSupportDefinitionParser.EnsureUniqueKeys(
            recovery.Select(value => value.Key),
            "recovery procedure");
        CharacterSupportDefinitionParser.EnsureUniqueKeys(
            passive.Select(value => value.Mechanic.Key),
            "passive value");
        CharacterSupportDefinitionParser.EnsureUniqueKeys(
            qualifications.Select(value => value.Key),
            "qualification");

        return new CharacterSupportCatalogDefinition(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            recovery,
            passive,
            qualifications);
    }

    private static CharacterSupportCatalogDefinition Empty(
        ResolvedRulesCatalogView rules) =>
        new(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            [],
            [],
            []);
}
