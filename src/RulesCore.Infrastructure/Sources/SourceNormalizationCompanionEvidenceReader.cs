using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Rehydrates persisted companion evidence needed while replaying derived source normalization.
/// Evidence is deliberately constrained to the target entity's own package so translation replay
/// can not bake separately licensed package content into another package's ContentJson.
/// </summary>
internal sealed class SourceNormalizationCompanionEvidenceReader(RulesCoreDbContext dbContext)
{
    public async Task<IReadOnlyList<NormalizedSourceCompanionContent>> ReadForSourceEntityAsync(
        Guid sourceEntityId,
        string companionKind,
        CancellationToken cancellationToken = default)
    {
        if (sourceEntityId == Guid.Empty)
        {
            throw new ArgumentException("Source entity ID can not be empty.", nameof(sourceEntityId));
        }
        if (string.IsNullOrWhiteSpace(companionKind))
        {
            throw new ArgumentException("Companion kind can not be blank.", nameof(companionKind));
        }

        await new SourceCompanionContentStore(dbContext).EnsureSchemaAsync(cancellationToken);
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
                    content.companion_kind,
                    content.companion_name,
                    content.source_code,
                    content.native_key,
                    content.raw_json::text
                FROM source_companion_attachment attachment
                JOIN source_companion_content content
                    ON content.source_companion_content_id = attachment.source_companion_content_id
                JOIN source_entity target
                    ON target.source_entity_id = attachment.source_entity_id
                WHERE attachment.source_entity_id = @source_entity_id
                  AND content.companion_kind = @companion_kind
                  AND content.source_package_id = target.source_package_id
                ORDER BY
                    content.companion_kind,
                    content.source_code,
                    content.companion_name,
                    content.native_key;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            AddParameter(command, "@companion_kind", companionKind.Trim());

            var evidence = new List<NormalizedSourceCompanionContent>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                evidence.Add(new NormalizedSourceCompanionContent(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    []));
            }
            return evidence;
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
