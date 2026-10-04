using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

internal static class ResolvedRuleCatalogEditionMetadata
{
    public static async Task<ResolvedRuleCatalogItemView[]> AttachAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyCollection<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        if (rules.Count == 0)
        {
            return [];
        }

        var sourceEntityIds = rules
            .Select(value => value.SourceEntityId)
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();
        var editions = await ReadAsync(dbContext, sourceEntityIds, cancellationToken);

        return rules
            .Select(rule =>
            {
                var edition = editions.GetValueOrDefault(rule.SourceEntityId) ?? string.Empty;
                return rule with
                {
                    EditionKey = edition,
                    EditionDisplayName = edition
                };
            })
            .ToArray();
    }

    public static string Normalize(string? value)
    {
        if (!DndEditionCatalog.TryParse(value, out var edition))
        {
            return string.Empty;
        }
        return DndEditionCatalog.GetCanonicalLabel(edition);
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> ReadAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyList<Guid> sourceEntityIds,
        CancellationToken cancellationToken)
    {
        if (sourceEntityIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT binding.source_entity_id, publication.game_edition
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                WHERE binding.source_entity_id = ANY(@source_entity_ids)
                    AND publication.game_edition IS NOT NULL;
                """;
            AddParameter(command, "@source_entity_ids", sourceEntityIds.ToArray());

            var candidates = new Dictionary<Guid, List<string?>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var sourceEntityId = reader.GetGuid(0);
                if (!candidates.TryGetValue(sourceEntityId, out var values))
                {
                    values = [];
                    candidates[sourceEntityId] = values;
                }
                var normalized = Normalize(reader.GetString(1));
                values.Add(string.IsNullOrEmpty(normalized) ? null : normalized);
            }

            var resolved = new Dictionary<Guid, string>();
            foreach (var (sourceEntityId, values) in candidates)
            {
                if (values.Count == 0 || values.Any(value => value is null))
                {
                    continue;
                }

                var distinct = values
                    .Select(value => value!)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (distinct.Length == 1)
                {
                    resolved[sourceEntityId] = distinct[0];
                }
            }
            return resolved;
        }
        catch (DbException)
        {
            return new Dictionary<Guid, string>();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
