using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

internal sealed record RuleConceptRelationshipReference(
    Guid FromRuleConceptId,
    string Kind,
    Guid RelatedRuleConceptId,
    string RelatedConceptKey,
    string RelatedEntityType,
    string RelatedDisplayName);

internal static class RuleConceptRelationshipStore
{
    private const string SubclassParentBackfillKey = "subclass-parent-v2";
    private const string SystemActorUserId = "rules-core-system";

    internal static async Task InitializeSchemaAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaOnlyAsync(dbContext, cancellationToken);
        if (await HasCompletedBackfillAsync(dbContext, cancellationToken))
        {
            return;
        }

        await SynchronizeSubclassParentsCoreAsync(
            dbContext,
            SystemActorUserId,
            cancellationToken);
        await MarkBackfillCompletedAsync(dbContext, cancellationToken);
    }

    public static async Task EnsureSchemaAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        // Schema DDL is owned by RulesCoreSchemaInitializer so concurrent request
        // paths can not race on CREATE/ALTER statements. Keep the legacy runtime
        // backfill contract, however: a database missing the backfill marker must
        // reconstruct subclass-parent relationships on the first catalog access.
        if (await HasCompletedBackfillAsync(dbContext, cancellationToken))
        {
            return;
        }

        await SynchronizeSubclassParentsCoreAsync(
            dbContext,
            SystemActorUserId,
            cancellationToken);
        await MarkBackfillCompletedAsync(dbContext, cancellationToken);
    }

    public static async Task SynchronizeSubclassParentsAsync(
        RulesCoreDbContext dbContext,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await SynchronizeSubclassParentsCoreAsync(dbContext, actorUserId, cancellationToken);
        await MarkBackfillCompletedAsync(dbContext, cancellationToken);
    }

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<RuleConceptRelationshipReference>>> GetOutgoingAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyCollection<Guid> fromRuleConceptIds,
        CancellationToken cancellationToken = default)
    {
        if (fromRuleConceptIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<RuleConceptRelationshipReference>>();
        }

        await EnsureSchemaAsync(dbContext, cancellationToken);
        var requested = fromRuleConceptIds.ToHashSet();
        var result = new Dictionary<Guid, List<RuleConceptRelationshipReference>>();
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT relationship.from_rule_concept_id,
                       relationship.relationship_kind,
                       related.rule_concept_id,
                       related.concept_key,
                       related.entity_type,
                       related.display_name
                FROM rule_concept_relationship relationship
                JOIN rule_concept related
                  ON related.rule_concept_id = relationship.to_rule_concept_id
                ORDER BY relationship.relationship_kind, related.concept_key;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var fromId = reader.GetGuid(0);
                if (!requested.Contains(fromId))
                {
                    continue;
                }

                if (!result.TryGetValue(fromId, out var relationships))
                {
                    relationships = [];
                    result.Add(fromId, relationships);
                }

                relationships.Add(new RuleConceptRelationshipReference(
                    fromId,
                    reader.GetString(1),
                    reader.GetGuid(2),
                    reader.GetString(3),
                    RuleConceptEntityTypes.Normalize(reader.GetString(4)),
                    reader.GetString(5)));
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<RuleConceptRelationshipReference>)pair.Value.ToArray());
    }

    internal static bool TryReadParentClassIdentity(
        string nativeIdentityJson,
        out string className,
        out string? classSource)
    {
        className = string.Empty;
        classSource = null;
        if (string.IsNullOrWhiteSpace(nativeIdentityJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(nativeIdentityJson);
            if (!document.RootElement.TryGetProperty("className", out var classNameElement)
                || classNameElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(classNameElement.GetString()))
            {
                return false;
            }

            className = classNameElement.GetString()!.Trim();
            if (document.RootElement.TryGetProperty("classSource", out var classSourceElement)
                && classSourceElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(classSourceElement.GetString()))
            {
                classSource = classSourceElement.GetString()!.Trim();
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Task EnsureSchemaOnlyAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS rule_concept_relationship (
                rule_concept_relationship_id uuid NOT NULL,
                from_rule_concept_id uuid NOT NULL,
                to_rule_concept_id uuid NOT NULL,
                relationship_kind varchar(80) NOT NULL,
                created_by_user_id varchar(200) NOT NULL,
                created_at timestamp with time zone NOT NULL,
                CONSTRAINT pk_rule_concept_relationship PRIMARY KEY (rule_concept_relationship_id),
                CONSTRAINT fk_rule_concept_relationship_from FOREIGN KEY (from_rule_concept_id)
                    REFERENCES rule_concept(rule_concept_id) ON DELETE CASCADE,
                CONSTRAINT fk_rule_concept_relationship_to FOREIGN KEY (to_rule_concept_id)
                    REFERENCES rule_concept(rule_concept_id) ON DELETE CASCADE,
                CONSTRAINT ck_rule_concept_relationship_distinct CHECK (from_rule_concept_id <> to_rule_concept_id));
            CREATE UNIQUE INDEX IF NOT EXISTS ux_rule_concept_relationship_identity
                ON rule_concept_relationship(from_rule_concept_id, to_rule_concept_id, relationship_kind);
            CREATE INDEX IF NOT EXISTS ix_rule_concept_relationship_to
                ON rule_concept_relationship(to_rule_concept_id, relationship_kind);

            CREATE TABLE IF NOT EXISTS rule_concept_relationship_backfill (
                backfill_key varchar(120) NOT NULL,
                completed_at timestamp with time zone NOT NULL,
                CONSTRAINT pk_rule_concept_relationship_backfill PRIMARY KEY (backfill_key));
            """,
            cancellationToken);

    private static async Task SynchronizeSubclassParentsCoreAsync(
        RulesCoreDbContext dbContext,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? ownedTransaction = null;
        if (dbContext.Database.CurrentTransaction is null)
        {
            ownedTransaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
        }

        try
        {
            var bindings = await ReadBoundConceptSourcesAsync(dbContext, cancellationToken);
            var classBindings = bindings
                .Where(value => string.Equals(
                    RuleConceptEntityTypes.Normalize(value.EntityType),
                    RuleConceptEntityTypes.Class,
                    StringComparison.Ordinal))
                .ToArray();
            var desiredRelationships = new HashSet<(Guid FromRuleConceptId, Guid ToRuleConceptId)>();

            foreach (var subclass in bindings.Where(value => string.Equals(
                         RuleConceptEntityTypes.Normalize(value.EntityType),
                         RuleConceptEntityTypes.Subclass,
                         StringComparison.Ordinal)))
            {
                if (!TryReadParentClassIdentity(subclass.NativeIdentityJson, out var className, out var classSource))
                {
                    continue;
                }

                var parentConceptIds = classBindings
                    .Where(candidate => string.Equals(candidate.SourceEntityName, className, StringComparison.OrdinalIgnoreCase))
                    .Where(candidate => classSource is null
                        || string.Equals(candidate.SourceCode, classSource, StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => candidate.RuleConceptId)
                    .Distinct()
                    .ToArray();

                foreach (var parentConceptId in parentConceptIds)
                {
                    if (parentConceptId != subclass.RuleConceptId)
                    {
                        desiredRelationships.Add((subclass.RuleConceptId, parentConceptId));
                    }
                }
            }

            var existingRelationships = await ReadRelationshipIdentitiesAsync(
                dbContext,
                RuleConceptRelationshipKinds.ParentClass,
                cancellationToken);
            foreach (var relationship in existingRelationships.Where(value => !desiredRelationships.Contains(value)))
            {
                await DeleteRelationshipAsync(
                    dbContext,
                    relationship.FromRuleConceptId,
                    relationship.ToRuleConceptId,
                    RuleConceptRelationshipKinds.ParentClass,
                    cancellationToken);
            }

            foreach (var relationship in desiredRelationships)
            {
                await InsertRelationshipAsync(
                    dbContext,
                    relationship.FromRuleConceptId,
                    relationship.ToRuleConceptId,
                    RuleConceptRelationshipKinds.ParentClass,
                    actorUserId,
                    cancellationToken);
            }

            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }

    private static async Task<HashSet<(Guid FromRuleConceptId, Guid ToRuleConceptId)>> ReadRelationshipIdentitiesAsync(
        RulesCoreDbContext dbContext,
        string kind,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var relationships = new HashSet<(Guid FromRuleConceptId, Guid ToRuleConceptId)>();
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT from_rule_concept_id,
                       to_rule_concept_id
                FROM rule_concept_relationship
                WHERE relationship_kind = @kind;
                """;
            AddParameter(command, "kind", kind);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                relationships.Add((reader.GetGuid(0), reader.GetGuid(1)));
            }
            return relationships;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task DeleteRelationshipAsync(
        RulesCoreDbContext dbContext,
        Guid fromRuleConceptId,
        Guid toRuleConceptId,
        string kind,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                DELETE FROM rule_concept_relationship
                WHERE from_rule_concept_id = @from
                  AND to_rule_concept_id = @to
                  AND relationship_kind = @kind;
                """;

            AddParameter(command, "from", fromRuleConceptId);
            AddParameter(command, "to", toRuleConceptId);
            AddParameter(command, "kind", kind);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> HasCompletedBackfillAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM rule_concept_relationship_backfill
                    WHERE backfill_key = @key);
                """;
            AddParameter(command, "key", SubclassParentBackfillKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is true;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task MarkBackfillCompletedAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                INSERT INTO rule_concept_relationship_backfill (backfill_key, completed_at)
                VALUES (@key, @completed)
                ON CONFLICT (backfill_key) DO NOTHING;
                """;
            AddParameter(command, "key", SubclassParentBackfillKey);
            AddParameter(command, "completed", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<BoundConceptSource[]> ReadBoundConceptSourcesAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var values = new List<BoundConceptSource>();
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                WITH RECURSIVE concept_entities(rule_concept_id, canonical_entity_id) AS (
                    SELECT binding.rule_concept_id,
                           binding.canonical_entity_id
                    FROM rule_concept_source_binding binding
                    UNION
                    SELECT parent.rule_concept_id,
                           relationship.to_canonical_entity_id
                    FROM canonical_entity_relationship relationship
                    JOIN concept_entities parent
                      ON parent.canonical_entity_id = relationship.from_canonical_entity_id
                    WHERE relationship.relationship_kind = 'revision'
                ),
                bound_sources AS (
                    SELECT DISTINCT concept_entity.rule_concept_id,
                           source.source_entity_id
                    FROM concept_entities concept_entity
                    JOIN canonical_source_occurrence occurrence
                      ON occurrence.canonical_entity_id = concept_entity.canonical_entity_id
                    JOIN source_entity_occurrence_binding occurrence_binding
                      ON occurrence_binding.canonical_source_occurrence_id = occurrence.canonical_source_occurrence_id
                    JOIN source_entity_revision revision
                      ON revision.source_entity_revision_id = occurrence_binding.source_entity_revision_id
                    JOIN source_entity source
                      ON source.source_entity_id = revision.source_entity_id
                )
                SELECT bound_source.rule_concept_id,
                       concept.entity_type,
                       source.entity_name,
                       source.source_code,
                       source.native_identity_json::text
                FROM bound_sources bound_source
                JOIN rule_concept concept
                  ON concept.rule_concept_id = bound_source.rule_concept_id
                JOIN source_entity source
                  ON source.source_entity_id = bound_source.source_entity_id
                WHERE lower(concept.entity_type) IN ('class', 'subclass');
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add(new BoundConceptSource(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4)));
            }
            return values.ToArray();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task InsertRelationshipAsync(
        RulesCoreDbContext dbContext,
        Guid fromRuleConceptId,
        Guid toRuleConceptId,
        string kind,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                INSERT INTO rule_concept_relationship (
                    rule_concept_relationship_id,
                    from_rule_concept_id,
                    to_rule_concept_id,
                    relationship_kind,
                    created_by_user_id,
                    created_at)
                VALUES (@id, @from, @to, @kind, @actor, @created)
                ON CONFLICT (from_rule_concept_id, to_rule_concept_id, relationship_kind) DO NOTHING;
                """;

            AddParameter(command, "id", Guid.NewGuid());
            AddParameter(command, "from", fromRuleConceptId);
            AddParameter(command, "to", toRuleConceptId);
            AddParameter(command, "kind", kind);
            AddParameter(command, "actor", actorUserId);
            AddParameter(command, "created", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record BoundConceptSource(
        Guid RuleConceptId,
        string EntityType,
        string SourceEntityName,
        string? SourceCode,
        string NativeIdentityJson);
}
