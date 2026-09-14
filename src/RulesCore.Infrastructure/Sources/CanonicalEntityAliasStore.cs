using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Stores developer-confirmed, source-lineage identity aliases for canonical entities.
/// Alias rows contain identity metadata only; they never store or grant access to source content.
/// The semantic fingerprint is part of the alias identity so a later mechanical revision of the
/// same upstream key can intentionally resolve to a different canonical entity.
/// </summary>
public sealed class CanonicalEntityAliasStore(RulesCoreDbContext dbContext)
{
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    public async Task RegisterAsync(
        Guid canonicalEntityId,
        string aliasScheme,
        string aliasValue,
        string semanticFingerprint,
        string evidenceKind,
        double confidence,
        CancellationToken cancellationToken = default)
    {
        if (canonicalEntityId == Guid.Empty)
        {
            throw new ArgumentException("Canonical entity ID can not be empty.", nameof(canonicalEntityId));
        }
        var scheme = NormalizeScheme(aliasScheme);
        var value = Require(aliasValue, nameof(aliasValue), 1000);
        var fingerprint = NormalizeFingerprint(semanticFingerprint);
        var evidence = Require(evidenceKind, nameof(evidenceKind), 100);
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
            await using (var entity = connection.CreateCommand())
            {
                entity.CommandText = """
                    SELECT EXISTS (
                        SELECT 1
                        FROM canonical_entity
                        WHERE canonical_entity_id = @canonical_entity_id);
                    """;
                AddParameter(entity, "@canonical_entity_id", canonicalEntityId);
                if (!Convert.ToBoolean(await entity.ExecuteScalarAsync(cancellationToken)))
                {
                    throw new KeyNotFoundException($"Canonical entity '{canonicalEntityId}' does not exist.");
                }
            }

            await using (var insert = connection.CreateCommand())
            {
                insert.CommandText = """
                    INSERT INTO canonical_entity_alias (
                        canonical_entity_alias_id,
                        canonical_entity_id,
                        alias_scheme,
                        alias_value,
                        semantic_fingerprint,
                        evidence_kind,
                        confidence,
                        created_at)
                    VALUES (
                        @id,
                        @canonical_entity_id,
                        @alias_scheme,
                        @alias_value,
                        @semantic_fingerprint,
                        @evidence_kind,
                        @confidence,
                        @created_at)
                    ON CONFLICT (alias_scheme, alias_value, semantic_fingerprint) DO NOTHING;
                    """;
                AddParameter(insert, "@id", Guid.NewGuid());
                AddParameter(insert, "@canonical_entity_id", canonicalEntityId);
                AddParameter(insert, "@alias_scheme", scheme);
                AddParameter(insert, "@alias_value", value);
                AddParameter(insert, "@semantic_fingerprint", fingerprint);
                AddParameter(insert, "@evidence_kind", evidence);
                AddParameter(insert, "@confidence", confidence);
                AddParameter(insert, "@created_at", DateTimeOffset.UtcNow);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            Guid storedCanonicalEntityId;
            double storedConfidence;
            await using (var read = connection.CreateCommand())
            {
                read.CommandText = """
                    SELECT canonical_entity_id, confidence
                    FROM canonical_entity_alias
                    WHERE alias_scheme = @alias_scheme
                        AND alias_value = @alias_value
                        AND semantic_fingerprint = @semantic_fingerprint;
                    """;
                AddParameter(read, "@alias_scheme", scheme);
                AddParameter(read, "@alias_value", value);
                AddParameter(read, "@semantic_fingerprint", fingerprint);
                await using var reader = await read.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException("Canonical entity alias was not readable after registration.");
                }
                storedCanonicalEntityId = reader.GetGuid(0);
                storedConfidence = reader.GetDouble(1);
            }

            if (storedCanonicalEntityId != canonicalEntityId)
            {
                throw new InvalidOperationException(
                    $"Strong canonical alias '{scheme}:{value}' is already assigned to a different canonical entity for semantic fingerprint '{fingerprint}'.");
            }

            if (confidence > storedConfidence)
            {
                await using var update = connection.CreateCommand();
                update.CommandText = """
                    UPDATE canonical_entity_alias
                    SET confidence = @confidence
                    WHERE alias_scheme = @alias_scheme
                        AND alias_value = @alias_value
                        AND semantic_fingerprint = @semantic_fingerprint
                        AND canonical_entity_id = @canonical_entity_id;
                    """;
                AddParameter(update, "@confidence", confidence);
                AddParameter(update, "@alias_scheme", scheme);
                AddParameter(update, "@alias_value", value);
                AddParameter(update, "@semantic_fingerprint", fingerprint);
                AddParameter(update, "@canonical_entity_id", canonicalEntityId);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public async Task<Guid?> ResolveAsync(
        string aliasScheme,
        string aliasValue,
        string semanticFingerprint,
        CancellationToken cancellationToken = default)
    {
        var scheme = NormalizeScheme(aliasScheme);
        var value = Require(aliasValue, nameof(aliasValue), 1000);
        var fingerprint = NormalizeFingerprint(semanticFingerprint);

        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            return await ResolveCoreAsync(connection, scheme, value, fingerprint, cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    public async Task<Guid?> ResolveAnyAsync(
        IReadOnlyDictionary<string, string>? aliases,
        string semanticFingerprint,
        CancellationToken cancellationToken = default)
    {
        if (aliases is null || aliases.Count == 0) return null;

        var fingerprint = NormalizeFingerprint(semanticFingerprint);
        var normalizedAliases = aliases
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new KeyValuePair<string, string>(
                NormalizeScheme(value.Key),
                Require(value.Value, nameof(aliases), 1000)))
            .ToArray();

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            Guid? resolved = null;
            foreach (var alias in normalizedAliases)
            {
                var candidate = await ResolveCoreAsync(
                    connection,
                    alias.Key,
                    alias.Value,
                    fingerprint,
                    cancellationToken);
                if (!candidate.HasValue) continue;
                if (resolved.HasValue && resolved.Value != candidate.Value)
                {
                    throw new CanonicalReconciliationConflictException(
                        "Trusted source-lineage aliases for one source record resolve to conflicting canonical entities.");
                }
                resolved = candidate;
            }
            return resolved;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<Guid?> ResolveCoreAsync(
        DbConnection connection,
        string scheme,
        string value,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT canonical_entity_id
            FROM canonical_entity_alias
            WHERE alias_scheme = @alias_scheme
                AND alias_value = @alias_value
                AND semantic_fingerprint = @semantic_fingerprint;
            """;
        AddParameter(command, "@alias_scheme", scheme);
        AddParameter(command, "@alias_value", value);
        AddParameter(command, "@semantic_fingerprint", fingerprint);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid id ? id : null;
    }

    private static string NormalizeScheme(string value)
    {
        var normalized = CanonicalSourceIdentity.NormalizeIdentityPart(
            Require(value, nameof(value), 100));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Alias scheme must contain an alphanumeric character.", nameof(value));
        }
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }

    private static string NormalizeFingerprint(string value)
    {
        var normalized = Require(value, nameof(value), 64).ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Semantic fingerprint must be a SHA-256 hex string.", nameof(value));
        }
        return normalized;
    }

    private static string Require(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS canonical_entity_alias (
            canonical_entity_alias_id uuid NOT NULL,
            canonical_entity_id uuid NOT NULL,
            alias_scheme varchar(100) NOT NULL,
            alias_value varchar(1000) NOT NULL,
            semantic_fingerprint varchar(64) NOT NULL,
            evidence_kind varchar(100) NOT NULL,
            confidence double precision NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_entity_alias PRIMARY KEY (canonical_entity_alias_id),
            CONSTRAINT fk_canonical_entity_alias_entity FOREIGN KEY (canonical_entity_id)
                REFERENCES canonical_entity(canonical_entity_id) ON DELETE CASCADE,
            CONSTRAINT ck_canonical_entity_alias_confidence CHECK (confidence >= 0 AND confidence <= 1));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_entity_alias_identity
            ON canonical_entity_alias(alias_scheme, alias_value, semantic_fingerprint);
        CREATE INDEX IF NOT EXISTS ix_canonical_entity_alias_entity
            ON canonical_entity_alias(canonical_entity_id);
        """;
}
