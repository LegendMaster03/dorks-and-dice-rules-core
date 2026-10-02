using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Read-optimized canonical-history traversal for unresolved Rules Layer fallback.
///
/// The general binding store historically found the latest revision for every Source Entity in
/// the corpus before joining that global result back to one concept history. That is correct but
/// becomes prohibitively expensive once reconciliation creates RuleConcept coverage for the full
/// corpus. This reader starts from the requested concept history instead, walks only that history,
/// and checks latest-revision status with the indexed (source_entity_id, revision_number) key.
/// </summary>
internal static class CanonicalRuleFallbackSourceReader
{
    public static async Task<IReadOnlyList<Guid>> GetAccessibleSourceEntityIdsAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty)
        {
            throw new ArgumentException("Rule concept ID can not be empty.", nameof(ruleConceptId));
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
                WITH RECURSIVE concept_entities(canonical_entity_id) AS (
                    SELECT canonical_entity_id
                    FROM rule_concept_source_binding
                    WHERE rule_concept_id = @concept_id

                    UNION

                    SELECT neighbour.canonical_entity_id
                    FROM concept_entities parent
                    CROSS JOIN LATERAL (
                        SELECT relationship.to_canonical_entity_id AS canonical_entity_id
                        FROM canonical_entity_relationship relationship
                        WHERE relationship.from_canonical_entity_id = parent.canonical_entity_id
                          AND relationship.relationship_kind IN ('revision', 'rename')

                        UNION

                        SELECT relationship.from_canonical_entity_id AS canonical_entity_id
                        FROM canonical_entity_relationship relationship
                        WHERE relationship.to_canonical_entity_id = parent.canonical_entity_id
                          AND relationship.relationship_kind IN ('revision', 'rename')
                    ) neighbour
                )
                SELECT DISTINCT source.source_entity_id
                FROM concept_entities concept_entity
                JOIN canonical_source_occurrence occurrence
                  ON occurrence.canonical_entity_id = concept_entity.canonical_entity_id
                JOIN source_entity_occurrence_binding source_binding
                  ON source_binding.canonical_source_occurrence_id = occurrence.canonical_source_occurrence_id
                JOIN source_entity_revision revision
                  ON revision.source_entity_revision_id = source_binding.source_entity_revision_id
                JOIN source_entity source
                  ON source.source_entity_id = revision.source_entity_id
                JOIN source_package package
                  ON package.source_package_id = source.source_package_id
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM source_entity_revision newer
                    WHERE newer.source_entity_id = revision.source_entity_id
                      AND newer.revision_number > revision.revision_number)
                  AND (
                    package.is_public
                    OR (@user_id IS NOT NULL AND EXISTS (
                        SELECT 1
                        FROM user_source_grant grant_row
                        WHERE grant_row.source_package_id = package.source_package_id
                          AND grant_row.user_id = @user_id)))
                ORDER BY source.source_entity_id;
                """;
            AddParameter(command, "@concept_id", ruleConceptId);
            AddNullableStringParameter(command, "@user_id", NormalizeUserId(userId));

            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string? NormalizeUserId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddNullableStringParameter(DbCommand command, string name, string? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
