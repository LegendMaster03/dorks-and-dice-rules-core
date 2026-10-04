using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

internal static class CanonicalPublicationMetadataBatchReader
{
    internal static async Task<IReadOnlyDictionary<Guid, CanonicalPublicationMetadata>> ReadAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyCollection<Guid> sourceEntityIds,
        CancellationToken cancellationToken)
    {
        var ids = sourceEntityIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, CanonicalPublicationMetadata>();
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
                SELECT DISTINCT
                    binding.source_entity_id,
                    publication.game_edition,
                    publication.release_kind,
                    publication.publication_date
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                WHERE binding.source_entity_id = ANY(@source_entity_ids)
                    AND publication.game_edition IS NOT NULL;
                """;
            AddParameter(command, "@source_entity_ids", ids);

            var candidates = new Dictionary<Guid, List<CanonicalPublicationMetadata>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var sourceEntityId = reader.GetGuid(0);
                if (!candidates.TryGetValue(sourceEntityId, out var values))
                {
                    values = [];
                    candidates[sourceEntityId] = values;
                }
                values.Add(new CanonicalPublicationMetadata(
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3)));
            }

            var result = new Dictionary<Guid, CanonicalPublicationMetadata>();
            foreach (var (sourceEntityId, values) in candidates)
            {
                var editions = values
                    .Select(value => value.GameEdition)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (editions.Length != 1)
                {
                    continue;
                }
                result[sourceEntityId] = values
                    .OrderByDescending(value => value.PublicationDate ?? DateOnly.MinValue)
                    .First();
            }
            return result;
        }
        catch (DbException)
        {
            return new Dictionary<Guid, CanonicalPublicationMetadata>();
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
