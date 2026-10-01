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

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        Revision,
        Reprint,
        Rename,
        Variant
    };

    public static bool IsKnown(string relationshipKind) => Known.Contains(relationshipKind);
}

internal sealed class CanonicalEntityRelationshipStore(RulesCoreDbContext dbContext)
{
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE OR REPLACE FUNCTION rules_core_enforce_history_separation_precedence()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NEW.relationship_kind IN ('variant', 'reprint') THEN
                        DELETE FROM canonical_entity_relationship automatic
                        WHERE automatic.canonical_entity_relationship_id <> NEW.canonical_entity_relationship_id
                          AND automatic.relationship_kind IN ('revision', 'rename')
                          AND automatic.evidence_kind = 'normalized-name-compatible-category'
                          AND (
                              (automatic.from_canonical_entity_id = NEW.from_canonical_entity_id
                               AND automatic.to_canonical_entity_id = NEW.to_canonical_entity_id)
                              OR
                              (automatic.from_canonical_entity_id = NEW.to_canonical_entity_id
                               AND automatic.to_canonical_entity_id = NEW.from_canonical_entity_id));
                    ELSIF NEW.relationship_kind IN ('revision', 'rename')
                          AND NEW.evidence_kind = 'normalized-name-compatible-category'
                          AND EXISTS (
                              SELECT 1
                              FROM canonical_entity_relationship separation
                              WHERE separation.relationship_kind IN ('variant', 'reprint')
                                AND (
                                    (separation.from_canonical_entity_id = NEW.from_canonical_entity_id
                                     AND separation.to_canonical_entity_id = NEW.to_canonical_entity_id)
                                    OR
                                    (separation.from_canonical_entity_id = NEW.to_canonical_entity_id
                                     AND separation.to_canonical_entity_id = NEW.from_canonical_entity_id)))
                    THEN
                        DELETE FROM canonical_entity_relationship automatic
                        WHERE automatic.canonical_entity_relationship_id = NEW.canonical_entity_relationship_id;
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                DO $block$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_trigger
                        WHERE tgname = 'trg_canonical_entity_relationship_separation_precedence'
                          AND tgrelid = 'canonical_entity_relationship'::regclass
                          AND NOT tgisinternal)
                    THEN
                        BEGIN
                            CREATE TRIGGER trg_canonical_entity_relationship_separation_precedence
                            AFTER INSERT OR UPDATE OF relationship_kind, evidence_kind
                            ON canonical_entity_relationship
                            FOR EACH ROW
                            EXECUTE FUNCTION rules_core_enforce_history_separation_precedence();
                        EXCEPTION
                            WHEN duplicate_object THEN NULL;
                        END;
                    END IF;
                END
                $block$;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public Task RelateRevisionAsync(
        Guid fromCanonicalEntityId,
        Guid toCanonicalEntityId,
        string evidenceKind,
        double confidence,
        CancellationToken cancellationToken = default) =>
        RelateAsync(
            fromCanonicalEntityId,
            toCanonicalEntityId,
            CanonicalEntityRelationshipKinds.Revision,
            evidenceKind,
            confidence,
            cancellationToken);

    public async Task RelateAsync(
        Guid fromCanonicalEntityId,
        Guid toCanonicalEntityId,
        string relationshipKind,
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
            throw new ArgumentException("Canonical entity relationship endpoints must be distinct.");
        }
        if (string.IsNullOrWhiteSpace(relationshipKind)
            || !CanonicalEntityRelationshipKinds.IsKnown(relationshipKind.Trim()))
        {
            throw new ArgumentException(
                $"Unknown canonical entity relationship kind '{relationshipKind}'.",
                nameof(relationshipKind));
        }
        if (string.IsNullOrWhiteSpace(evidenceKind))
        {
            throw new ArgumentException("Canonical entity relationship evidence kind can not be blank.", nameof(evidenceKind));
        }
        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        var normalizedRelationshipKind = relationshipKind.Trim();
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
            AddParameter(command, "@relationship_kind", normalizedRelationshipKind);
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

}
