using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

internal static class CanonicalRuleBindingStore
{
    public static async Task EnsureSchemaAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await new CanonicalEntityStore(dbContext).EnsureSchemaAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);
    }

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
                JOIN rule_concept_source_binding rule_binding
                    ON rule_binding.canonical_entity_id = occurrence.canonical_entity_id
                JOIN source_package package
                    ON package.source_package_id = source.source_package_id
                WHERE rule_binding.rule_concept_id = @concept_id
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
                SELECT DISTINCT source_binding.source_entity_id
                FROM rule_concept_source_binding rule_binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_entity_id = rule_binding.canonical_entity_id
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

        var canonicalIds = canonicalBySource.Values.Distinct().ToArray();
        var bindings = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .Where(value => canonicalIds.Contains(value.CanonicalEntityId))
            .Select(value => new { value.CanonicalEntityId, value.RuleConceptId })
            .ToArrayAsync(cancellationToken);
        var conceptIdsByCanonical = bindings
            .GroupBy(value => value.CanonicalEntityId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group
                    .Select(value => value.RuleConceptId)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArray());

        return canonicalBySource.ToDictionary(
            pair => pair.Key,
            pair => conceptIdsByCanonical.GetValueOrDefault(pair.Value, Array.Empty<Guid>()));
    }

    private static async Task<bool> IsCanonicalEntityBoundAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        Guid canonicalEntityId,
        CancellationToken cancellationToken) =>
        await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .AnyAsync(
                value => value.RuleConceptId == ruleConceptId
                    && value.CanonicalEntityId == canonicalEntityId,
                cancellationToken);

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
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private const string SchemaSql = """
        ALTER TABLE rule_concept_source_binding
            ADD COLUMN IF NOT EXISTS canonical_entity_id uuid NULL;

        UPDATE rule_concept_source_binding target
        SET canonical_entity_id = occurrence.canonical_entity_id
        FROM source_entity_revision revision
        JOIN source_entity_occurrence_binding source_binding
            ON source_binding.source_entity_revision_id = revision.source_entity_revision_id
        JOIN canonical_source_occurrence occurrence
            ON occurrence.canonical_source_occurrence_id = source_binding.canonical_source_occurrence_id
        WHERE target.canonical_entity_id IS NULL
            AND target.source_entity_id = revision.source_entity_id
            AND occurrence.canonical_entity_id IS NOT NULL
            AND revision.revision_number = (
                SELECT MAX(candidate.revision_number)
                FROM source_entity_revision candidate
                WHERE candidate.source_entity_id = revision.source_entity_id);

        DELETE FROM rule_concept_source_binding target
        WHERE target.canonical_entity_id IS NULL;

        DELETE FROM rule_concept_source_binding duplicate
        USING rule_concept_source_binding keeper
        WHERE duplicate.rule_concept_id = keeper.rule_concept_id
            AND duplicate.canonical_entity_id = keeper.canonical_entity_id
            AND (
                duplicate.created_at > keeper.created_at
                OR (duplicate.created_at = keeper.created_at
                    AND duplicate.rule_concept_source_binding_id > keeper.rule_concept_source_binding_id));

        ALTER TABLE rule_concept_source_binding
            ALTER COLUMN canonical_entity_id SET NOT NULL;
        ALTER TABLE rule_concept_source_binding
            ALTER COLUMN source_entity_id DROP NOT NULL;
        ALTER TABLE rule_concept_source_binding
            DROP CONSTRAINT IF EXISTS fk_rule_concept_source_binding_entity;
        ALTER TABLE rule_concept_source_binding
            ADD CONSTRAINT fk_rule_concept_source_binding_entity
            FOREIGN KEY (source_entity_id)
            REFERENCES source_entity(source_entity_id) ON DELETE SET NULL;
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'fk_rule_concept_source_binding_canonical_entity'
                  AND conrelid = 'rule_concept_source_binding'::regclass)
            THEN
                ALTER TABLE rule_concept_source_binding
                    ADD CONSTRAINT fk_rule_concept_source_binding_canonical_entity
                    FOREIGN KEY (canonical_entity_id)
                    REFERENCES canonical_entity(canonical_entity_id) ON DELETE RESTRICT;
            END IF;
        END $$;
        DROP INDEX IF EXISTS ux_rule_concept_source_binding_concept_entity;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_rule_concept_source_binding_concept_canonical_entity
            ON rule_concept_source_binding(rule_concept_id, canonical_entity_id);
        """;
}
