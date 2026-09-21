using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

internal sealed record CanonicalEntityIdentity(
    Guid Id,
    string Key,
    string EntityType,
    string DisplayName,
    string SemanticFingerprint);

/// <summary>
/// Stores global identity metadata for a rule-bearing entity without storing or exposing
/// package-owned source content. Exact source bodies remain behind SourcePackage access grants.
/// </summary>
internal sealed class CanonicalEntityStore(RulesCoreDbContext dbContext)
{
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task<CanonicalEntityIdentity> ResolveAsync(
        CanonicalSourceOccurrenceEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var entityType = CanonicalSourceIdentity.NormalizeIdentityPart(evidence.EntityType);
        var normalizedName = CanonicalSourceIdentity.NormalizeIdentityPart(evidence.Name);
        var semanticFingerprint = NormalizeFingerprint(evidence.SemanticFingerprint);
        if (string.IsNullOrEmpty(entityType) || string.IsNullOrEmpty(normalizedName))
        {
            throw new ArgumentException(
                "Canonical entity identity requires a non-blank entity type and name.",
                nameof(evidence));
        }

        await EnsureSchemaAsync(cancellationToken);
        var canonicalKey = $"entity-{CanonicalSourceIdentity.Fingerprint(
            $"{entityType}\n{normalizedName}\n{semanticFingerprint}")[..32]}";

        var existing = await ReadByKeyAsync(canonicalKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using (var insert = connection.CreateCommand())
            {
                insert.CommandText = """
                    INSERT INTO canonical_entity (
                        canonical_entity_id,
                        canonical_key,
                        entity_type,
                        normalized_name,
                        display_name,
                        semantic_fingerprint,
                        created_at)
                    VALUES (
                        @id,
                        @canonical_key,
                        @entity_type,
                        @normalized_name,
                        @display_name,
                        @semantic_fingerprint,
                        @created_at)
                    ON CONFLICT (canonical_key) DO NOTHING;
                    """;
                AddParameter(insert, "@id", Guid.NewGuid());
                AddParameter(insert, "@canonical_key", canonicalKey);
                AddParameter(insert, "@entity_type", entityType);
                AddParameter(insert, "@normalized_name", normalizedName);
                AddParameter(insert, "@display_name", evidence.Name.Trim());
                AddParameter(insert, "@semantic_fingerprint", semanticFingerprint);
                AddParameter(insert, "@created_at", DateTimeOffset.UtcNow);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var read = connection.CreateCommand();
            read.CommandText = """
                SELECT canonical_entity_id, canonical_key, entity_type, display_name, semantic_fingerprint
                FROM canonical_entity
                WHERE canonical_key = @canonical_key;
                """;
            AddParameter(read, "@canonical_key", canonicalKey);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Canonical entity was not readable after creation.");
            }
            return ReadIdentity(reader);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<CanonicalEntityIdentity?> ReadAsync(
        Guid canonicalEntityId,
        CancellationToken cancellationToken = default)
    {
        if (canonicalEntityId == Guid.Empty)
        {
            return null;
        }

        await EnsureSchemaAsync(cancellationToken);
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
                SELECT canonical_entity_id, canonical_key, entity_type, display_name, semantic_fingerprint
                FROM canonical_entity
                WHERE canonical_entity_id = @id;
                """;
            AddParameter(command, "@id", canonicalEntityId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadIdentity(reader) : null;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<CanonicalEntityIdentity?> ReadByKeyAsync(
        string canonicalKey,
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
            command.CommandText = """
                SELECT canonical_entity_id, canonical_key, entity_type, display_name, semantic_fingerprint
                FROM canonical_entity
                WHERE canonical_key = @canonical_key;
                """;
            AddParameter(command, "@canonical_key", canonicalKey);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadIdentity(reader) : null;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static CanonicalEntityIdentity ReadIdentity(DbDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4));

    private static string NormalizeFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Semantic fingerprint can not be blank.", nameof(value));
        }
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Semantic fingerprint must be a SHA-256 hex string.", nameof(value));
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

}
