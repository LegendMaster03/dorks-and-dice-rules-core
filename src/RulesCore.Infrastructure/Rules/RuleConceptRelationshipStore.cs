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
    public static Task EnsureSchemaAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken = default) =>
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
            """,
            cancellationToken);

    public static async Task SynchronizeSubclassParentsAsync(
        RulesCoreDbContext dbContext,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(dbContext, cancellationToken);

        var bindings = await ReadBoundConceptSourcesAsync(dbContext, cancellationToken);
        var classBindings = bindings
            .Where(value => string.Equals(
                RuleConceptEntityTypes.Normalize(value.EntityType),
                RuleConceptEntityTypes.Class,
                StringComparison.Ordinal))
            .ToArray();

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
                .Where(candidate => candidate.SourcePackageId == subclass.SourcePackageId)
                .Where(candidate => string.Equals(candidate.SourceEntityName, className, StringComparison.OrdinalIgnoreCase))
                .Where(candidate => classSource is null
                    || string.Equals(candidate.SourceCode, classSource, StringComparison.OrdinalIgnoreCase))
                .Select(candidate => candidate.RuleConceptId)
                .Distinct()
                .ToArray();

            foreach (var parentConceptId in parentConceptIds)
            {
                if (parentConceptId == subclass.RuleConceptId)
                {
                    continue;
                }

                await InsertRelationshipAsync(
                    dbContext,
                    subclass.RuleConceptId,
                    parentConceptId,
                    RuleConceptRelationshipKinds.ParentClass,
                    actorUserId,
                    cancellationToken);
            }
        }
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
                SELECT binding.rule_concept_id,
                       concept.entity_type,
                       source.source_package_id,
                       source.entity_name,
                       source.source_code,
                       source.native_identity_json::text
                FROM rule_concept_source_binding binding
                JOIN rule_concept concept
                  ON concept.rule_concept_id = binding.rule_concept_id
                JOIN source_entity source
                  ON source.source_entity_id = binding.source_entity_id
                WHERE binding.source_entity_id IS NOT NULL
                  AND lower(concept.entity_type) IN ('class', 'subclass');
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add(new BoundConceptSource(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetGuid(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetString(5)));
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
        Guid SourcePackageId,
        string SourceEntityName,
        string? SourceCode,
        string NativeIdentityJson);
}
