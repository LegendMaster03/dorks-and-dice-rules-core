using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

internal static class CanonicalRuleBindingStore
{
    public static Task EnsureSchemaAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public static async Task<Guid> GetCanonicalEntityIdAsync(
        RulesCoreDbContext dbContext,
        Guid sourceEntityId,
        CancellationToken cancellationToken = default)
    {
        if (sourceEntityId == Guid.Empty)
        {
            throw new ArgumentException("Source entity ID can not be empty.", nameof(sourceEntityId));
        }

        await EnsureSchemaAsync(dbContext, cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_entity_id
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE revision.source_entity_id = @source_entity_id
                    AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY revision.revision_number DESC
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is Guid canonicalEntityId) return canonicalEntityId;
            throw new InvalidOperationException(
                $"Source entity '{sourceEntityId}' has not been resolved to a canonical entity.");
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public static async Task<Guid> GetCanonicalEntityIdForRevisionAsync(
        RulesCoreDbContext dbContext,
        Guid sourceEntityRevisionId,
        CancellationToken cancellationToken = default)
    {
        if (sourceEntityRevisionId == Guid.Empty)
        {
            throw new ArgumentException("Source entity revision ID can not be empty.", nameof(sourceEntityRevisionId));
        }

        await EnsureSchemaAsync(dbContext, cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_entity_id
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE binding.source_entity_revision_id = @revision_id
                    AND occurrence.canonical_entity_id IS NOT NULL;
                """;
            AddParameter(command, "@revision_id", sourceEntityRevisionId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is Guid canonicalEntityId) return canonicalEntityId;
            throw new InvalidOperationException(
                $"Source entity revision '{sourceEntityRevisionId}' has not been resolved to a canonical entity.");
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public static async Task<IReadOnlyDictionary<Guid, Guid>> GetCanonicalEntityIdsAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyCollection<Guid> sourceEntityIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceEntityIds);
        var ids = sourceEntityIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, Guid>();

        await EnsureSchemaAsync(dbContext, cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            var placeholders = AddGuidList(command, "source", ids);
            command.CommandText = $"""
                SELECT DISTINCT ON (revision.source_entity_id)
                    revision.source_entity_id,
                    occurrence.canonical_entity_id
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE revision.source_entity_id IN ({string.Join(", ", placeholders)})
                    AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY revision.source_entity_id, revision.revision_number DESC;
                """;
            var result = new Dictionary<Guid, Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) result[reader.GetGuid(0)] = reader.GetGuid(1);
            return result;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public static async Task<IReadOnlyList<Guid>> GetSourceEntityIdsForConceptAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty)
        {
            throw new ArgumentException("Rule concept ID can not be empty.", nameof(ruleConceptId));
        }

        await EnsureSchemaAsync(dbContext, cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH RECURSIVE concept_entities(canonical_entity_id) AS (
                    SELECT canonical_entity_id
                    FROM rule_concept_source_binding
                    WHERE rule_concept_id = @concept_id
                    UNION
                    SELECT relationship.to_canonical_entity_id
                    FROM canonical_entity_relationship relationship
                    JOIN concept_entities parent
                        ON parent.canonical_entity_id = relationship.from_canonical_entity_id
                    WHERE relationship.relationship_kind = 'revision'
                )
                SELECT DISTINCT source.source_entity_id
                FROM source_entity source
                JOIN (
                    SELECT DISTINCT ON (source_entity_id)
                        source_entity_id,
                        source_entity_revision_id
                    FROM source_entity_revision
                    ORDER BY source_entity_id, revision_number DESC
                ) latest
                    ON latest.source_entity_id = source.source_entity_id
                JOIN source_entity_occurrence_binding source_binding
                    ON source_binding.source_entity_revision_id = latest.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = source_binding.canonical_source_occurrence_id
                JOIN concept_entities concept_entity
                    ON concept_entity.canonical_entity_id = occurrence.canonical_entity_id
                ORDER BY source.source_entity_id;
                """;
            AddParameter(command, "@concept_id", ruleConceptId);
            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetGuid(0));
            return ids;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public static async Task<IReadOnlyList<Guid>> GetAccessibleSourceEntityIdsForConceptAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty)
        {
            throw new ArgumentException("Rule concept ID can not be empty.", nameof(ruleConceptId));
        }

        await EnsureSchemaAsync(dbContext, cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH RECURSIVE concept_entities(canonical_entity_id) AS (
                    SELECT canonical_entity_id
                    FROM rule_concept_source_binding
                    WHERE rule_concept_id = @concept_id
                    UNION
                    SELECT relationship.to_canonical_entity_id
                    FROM canonical_entity_relationship relationship
                    JOIN concept_entities parent
                        ON parent.canonical_entity_id = relationship.from_canonical_entity_id
                    WHERE relationship.relationship_kind = 'revision'
                )
                SELECT DISTINCT source.source_entity_id
                FROM source_entity source
                JOIN (
                    SELECT DISTINCT ON (source_entity_id)
                        source_entity_id,
                        source_entity_revision_id
                    FROM source_entity_revision
                    ORDER BY source_entity_id, revision_number DESC
                ) latest
                    ON latest.source_entity_id = source.source_entity_id
                JOIN source_entity_occurrence_binding source_binding
                    ON source_binding.source_entity_revision_id = latest.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = source_binding.canonical_source_occurrence_id
                JOIN concept_entities concept_entity
                    ON concept_entity.canonical_entity_id = occurrence.canonical_entity_id
                JOIN source_package package
                    ON package.source_package_id = source.source_package_id
                WHERE package.is_public
                    OR (@user_id IS NOT NULL AND EXISTS (
                        SELECT 1
                        FROM user_source_grant grant_row
                        WHERE grant_row.source_package_id = package.source_package_id
                            AND grant_row.user_id = @user_id))
                ORDER BY source.source_entity_id;
                """;
            AddParameter(command, "@concept_id", ruleConceptId);
            AddNullableParameter(command, "@user_id", NormalizeUserId(userId));
            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetGuid(0));
            return ids;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public static async Task<IReadOnlyList<Guid>> GetAccessibleRevisionIdsForCanonicalEntityAsync(
        RulesCoreDbContext dbContext,
        Guid canonicalEntityId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (canonicalEntityId == Guid.Empty) return [];
        await EnsureSchemaAsync(dbContext, cancellationToken);

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT revision.source_entity_revision_id
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN source_entity_revision revision
                    ON revision.source_entity_revision_id = binding.source_entity_revision_id
                JOIN source_entity source
                    ON source.source_entity_id = revision.source_entity_id
                JOIN source_package package
                    ON package.source_package_id = source.source_package_id
                WHERE occurrence.canonical_entity_id = @canonical_entity_id
                    AND (
                        package.is_public
                        OR (@user_id IS NOT NULL AND EXISTS (
                            SELECT 1
                            FROM user_source_grant grant_row
                            WHERE grant_row.source_package_id = package.source_package_id
                                AND grant_row.user_id = @user_id)))
                ORDER BY revision.source_entity_revision_id;
                """;
            AddParameter(command, "@canonical_entity_id", canonicalEntityId);
            AddNullableParameter(command, "@user_id", NormalizeUserId(userId));
            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetGuid(0));
            return ids;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public static async Task<IReadOnlySet<Guid>> GetSourceEntityIdsWithAnyRuleBindingAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(dbContext, cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH RECURSIVE bound_entities(canonical_entity_id) AS (
                    SELECT DISTINCT canonical_entity_id
                    FROM rule_concept_source_binding
                    UNION
                    SELECT relationship.to_canonical_entity_id
                    FROM canonical_entity_relationship relationship
                    JOIN bound_entities parent
                        ON parent.canonical_entity_id = relationship.from_canonical_entity_id
                    WHERE relationship.relationship_kind = 'revision'
                )
                SELECT DISTINCT source_binding.source_entity_id
                FROM bound_entities bound
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_entity_id = bound.canonical_entity_id
                JOIN source_entity_occurrence_binding source_binding
                    ON source_binding.canonical_source_occurrence_id = occurrence.canonical_source_occurrence_id;
                """;
            var ids = new HashSet<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetGuid(0));
            return ids;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public static async Task<bool> IsSourceEntityBoundAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        Guid sourceEntityId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty || sourceEntityId == Guid.Empty) return false;
        var canonicalEntityId = await GetCanonicalEntityIdAsync(dbContext, sourceEntityId, cancellationToken);
        return await IsCanonicalEntityBoundAsync(dbContext, ruleConceptId, canonicalEntityId, cancellationToken);
    }

    public static async Task<bool> IsSourceEntityRevisionBoundAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        Guid sourceEntityRevisionId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty || sourceEntityRevisionId == Guid.Empty) return false;
        var canonicalEntityId = await GetCanonicalEntityIdForRevisionAsync(
            dbContext,
            sourceEntityRevisionId,
            cancellationToken);
        return await IsCanonicalEntityBoundAsync(dbContext, ruleConceptId, canonicalEntityId, cancellationToken);
    }

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetConceptIdsBySourceEntityAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyCollection<Guid> sourceEntityIds,
        CancellationToken cancellationToken = default)
    {
        var canonicalBySource = await GetCanonicalEntityIdsAsync(dbContext, sourceEntityIds, cancellationToken);
        if (canonicalBySource.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        await EnsureSchemaAsync(dbContext, cancellationToken);
        var canonicalIds = canonicalBySource.Values.Distinct().ToArray();
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            var placeholders = AddGuidList(command, "canonical", canonicalIds);
            command.CommandText = $"""
                WITH RECURSIVE concept_entities(rule_concept_id, canonical_entity_id) AS (
                    SELECT rule_concept_id, canonical_entity_id
                    FROM rule_concept_source_binding
                    UNION
                    SELECT parent.rule_concept_id, relationship.to_canonical_entity_id
                    FROM canonical_entity_relationship relationship
                    JOIN concept_entities parent
                        ON parent.canonical_entity_id = relationship.from_canonical_entity_id
                    WHERE relationship.relationship_kind = 'revision'
                )
                SELECT canonical_entity_id, rule_concept_id
                FROM concept_entities
                WHERE canonical_entity_id IN ({string.Join(", ", placeholders)})
                ORDER BY canonical_entity_id, rule_concept_id;
                """;
            var conceptIdsByCanonical = new Dictionary<Guid, List<Guid>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var canonicalId = reader.GetGuid(0);
                var conceptId = reader.GetGuid(1);
                if (!conceptIdsByCanonical.TryGetValue(canonicalId, out var list))
                {
                    list = [];
                    conceptIdsByCanonical[canonicalId] = list;
                }
                if (!list.Contains(conceptId)) list.Add(conceptId);
            }

            return canonicalBySource.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<Guid>)(conceptIdsByCanonical.TryGetValue(pair.Value, out var list)
                    ? list.OrderBy(value => value).ToArray()
                    : Array.Empty<Guid>()));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<bool> IsCanonicalEntityBoundAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        Guid canonicalEntityId,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(dbContext, cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH RECURSIVE concept_entities(canonical_entity_id) AS (
                    SELECT canonical_entity_id
                    FROM rule_concept_source_binding
                    WHERE rule_concept_id = @concept_id
                    UNION
                    SELECT relationship.to_canonical_entity_id
                    FROM canonical_entity_relationship relationship
                    JOIN concept_entities parent
                        ON parent.canonical_entity_id = relationship.from_canonical_entity_id
                    WHERE relationship.relationship_kind = 'revision'
                )
                SELECT EXISTS (
                    SELECT 1
                    FROM concept_entities
                    WHERE canonical_entity_id = @canonical_entity_id);
                """;
            AddParameter(command, "@concept_id", ruleConceptId);
            AddParameter(command, "@canonical_entity_id", canonicalEntityId);
            return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static string[] AddGuidList(DbCommand command, string prefix, IReadOnlyList<Guid> values)
    {
        var names = new string[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            names[index] = $"@{prefix}_{index}";
            AddParameter(command, names[index], values[index]);
        }
        return names;
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

    private static void AddNullableParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

}
