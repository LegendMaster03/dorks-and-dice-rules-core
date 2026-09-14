using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

internal static class CanonicalEntityRelationshipKinds
{
    public const string Revision = "revision";
    public const string Reprint = "reprint";
    public const string Rename = "rename";
    public const string Variant = "variant";
}

internal sealed class CanonicalEntityRelationshipStore(RulesCoreDbContext dbContext)
{
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    public async Task RelateRevisionAsync(
        Guid fromCanonicalEntityId,
        Guid toCanonicalEntityId,
        string evidenceKind,
        double confidence,
        CancellationToken cancellationToken = default)
    {
        if (fromCanonicalEntityId == Guid.Empty || toCanonicalEntityId == Guid.Empty)
        {
            throw new ArgumentException("Canonical entity relationship IDs can not be empty.");
        }
        if (fromCanonicalEntityId == toCanonicalEntityId)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(evidenceKind))
        {
            throw new ArgumentException("Canonical entity relationship evidence kind can not be blank.", nameof(evidenceKind));
        }
        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO canonical_entity_relationship (
                    canonical_entity_relationship_id,
                    from_canonical_entity_id,
                    to_canonical_entity_id,
                    relationship_kind,
                    evidence_kind,
                    confidence,
                    created_at)
                VALUES (@id, @from_id, @to_id, @relationship_kind, @evidence_kind, @confidence, @created_at)
                ON CONFLICT (from_canonical_entity_id, to_canonical_entity_id, relationship_kind)
                DO UPDATE SET
                    confidence = GREATEST(canonical_entity_relationship.confidence, EXCLUDED.confidence);
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@from_id", fromCanonicalEntityId);
            AddParameter(command, "@to_id", toCanonicalEntityId);
            AddParameter(command, "@relationship_kind", CanonicalEntityRelationshipKinds.Revision);
            AddParameter(command, "@evidence_kind", evidenceKind.Trim());
            AddParameter(command, "@confidence", confidence);
            AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS canonical_entity_relationship (
            canonical_entity_relationship_id uuid NOT NULL,
            from_canonical_entity_id uuid NOT NULL,
            to_canonical_entity_id uuid NOT NULL,
            relationship_kind varchar(40) NOT NULL,
            evidence_kind varchar(100) NOT NULL,
            confidence double precision NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_entity_relationship PRIMARY KEY (canonical_entity_relationship_id),
            CONSTRAINT fk_canonical_entity_relationship_from FOREIGN KEY (from_canonical_entity_id)
                REFERENCES canonical_entity(canonical_entity_id) ON DELETE CASCADE,
            CONSTRAINT fk_canonical_entity_relationship_to FOREIGN KEY (to_canonical_entity_id)
                REFERENCES canonical_entity(canonical_entity_id) ON DELETE CASCADE,
            CONSTRAINT ck_canonical_entity_relationship_distinct CHECK (from_canonical_entity_id <> to_canonical_entity_id),
            CONSTRAINT ck_canonical_entity_relationship_kind CHECK (
                relationship_kind IN ('revision', 'reprint', 'rename', 'variant')),
            CONSTRAINT ck_canonical_entity_relationship_confidence CHECK (confidence >= 0 AND confidence <= 1));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_entity_relationship_identity
            ON canonical_entity_relationship(from_canonical_entity_id, to_canonical_entity_id, relationship_kind);
        CREATE INDEX IF NOT EXISTS ix_canonical_entity_relationship_to
            ON canonical_entity_relationship(to_canonical_entity_id, relationship_kind);
        """;
}
