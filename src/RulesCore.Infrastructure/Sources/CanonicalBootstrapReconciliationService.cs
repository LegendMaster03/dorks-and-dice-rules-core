using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Persists developer/bootstrap reconciliation outcomes for trusted public source lineages.
/// The records contain identity evidence only: no package IDs, representation bytes, or private
/// source bodies are copied into the shared reconciliation layer.
/// </summary>
public sealed class CanonicalBootstrapReconciliationService(RulesCoreDbContext dbContext)
{
    public async Task<CanonicalBootstrapReconciliationView> RecordAsync(
        RecordCanonicalBootstrapReconciliationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = Normalize(request);

        await EnsureSchemaAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var current = await ReadCoreAsync(
            normalized.AliasScheme,
            normalized.AliasValue,
            normalized.SemanticFingerprint,
            cancellationToken);
        if (current is not null
            && !string.Equals(
                current.Classification,
                CanonicalBootstrapReconciliationClassifications.Unresolved,
                StringComparison.Ordinal)
            && !SameOutcome(current, normalized))
        {
            throw new InvalidOperationException(
                "A finalized bootstrap reconciliation can not be silently replaced. " +
                "Use a new semantic fingerprint or an explicit corrective migration.");
        }

        await ValidateCanonicalEntityAsync(normalized.CanonicalEntityId, cancellationToken);
        await ValidateCanonicalEntityAsync(normalized.RelatedCanonicalEntityId, cancellationToken);

        if (CanonicalBootstrapReconciliationClassifications.RegistersTrustedAlias(normalized.Classification))
        {
            if (!normalized.CanonicalEntityId.HasValue)
            {
                throw new ArgumentException(
                    "An exact bootstrap identity requires a canonical entity ID.",
                    nameof(request));
            }
            if (normalized.RelatedCanonicalEntityId.HasValue)
            {
                throw new ArgumentException(
                    "An exact bootstrap identity can not also specify a related canonical entity.",
                    nameof(request));
            }
            if (normalized.Confidence != 1.0)
            {
                throw new ArgumentException(
                    "A trusted canonical alias requires a fully confirmed bootstrap decision.",
                    nameof(request));
            }

            await new CanonicalEntityAliasStore(dbContext).RegisterAsync(
                normalized.CanonicalEntityId.Value,
                normalized.AliasScheme,
                normalized.AliasValue,
                normalized.SemanticFingerprint,
                normalized.Classification == CanonicalBootstrapReconciliationClassifications.ExactIdentity
                    ? "bootstrap-confirmed-exact-identity"
                    : "bootstrap-confirmed-corroborated-identity",
                1.0,
                cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var id = current?.Id ?? Guid.NewGuid();
        var createdAt = current?.CreatedAt ?? now;
        var connection = dbContext.Database.GetDbConnection();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO canonical_bootstrap_reconciliation (
                    canonical_bootstrap_reconciliation_id,
                    alias_scheme,
                    alias_value,
                    semantic_fingerprint,
                    classification,
                    canonical_entity_id,
                    related_canonical_entity_id,
                    evidence_kind,
                    confidence,
                    notes,
                    decided_by,
                    created_at,
                    updated_at)
                VALUES (
                    @id,
                    @alias_scheme,
                    @alias_value,
                    @semantic_fingerprint,
                    @classification,
                    @canonical_entity_id,
                    @related_canonical_entity_id,
                    @evidence_kind,
                    @confidence,
                    @notes,
                    @decided_by,
                    @created_at,
                    @updated_at)
                ON CONFLICT (alias_scheme, alias_value, semantic_fingerprint)
                DO UPDATE SET
                    classification = EXCLUDED.classification,
                    canonical_entity_id = EXCLUDED.canonical_entity_id,
                    related_canonical_entity_id = EXCLUDED.related_canonical_entity_id,
                    evidence_kind = EXCLUDED.evidence_kind,
                    confidence = EXCLUDED.confidence,
                    notes = EXCLUDED.notes,
                    decided_by = EXCLUDED.decided_by,
                    updated_at = EXCLUDED.updated_at;
                """;
            AddParameter(command, "@id", id);
            AddParameter(command, "@alias_scheme", normalized.AliasScheme);
            AddParameter(command, "@alias_value", normalized.AliasValue);
            AddParameter(command, "@semantic_fingerprint", normalized.SemanticFingerprint);
            AddParameter(command, "@classification", normalized.Classification);
            AddParameter(command, "@canonical_entity_id", normalized.CanonicalEntityId);
            AddParameter(command, "@related_canonical_entity_id", normalized.RelatedCanonicalEntityId);
            AddParameter(command, "@evidence_kind", normalized.EvidenceKind);
            AddParameter(command, "@confidence", normalized.Confidence);
            AddParameter(command, "@notes", normalized.Notes);
            AddParameter(command, "@decided_by", normalized.DecidedBy);
            AddParameter(command, "@created_at", createdAt);
            AddParameter(command, "@updated_at", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var stored = await ReadCoreAsync(
            normalized.AliasScheme,
            normalized.AliasValue,
            normalized.SemanticFingerprint,
            cancellationToken)
            ?? throw new InvalidOperationException("Bootstrap reconciliation was not readable after persistence.");
        await transaction.CommitAsync(cancellationToken);
        return stored;
    }

    public async Task<CanonicalBootstrapReconciliationView?> GetAsync(
        string aliasScheme,
        string aliasValue,
        string semanticFingerprint,
        CancellationToken cancellationToken = default)
    {
        var scheme = NormalizeRegisteredScheme(aliasScheme);
        var value = Require(aliasValue, nameof(aliasValue), 1000);
        var fingerprint = NormalizeFingerprint(semanticFingerprint);
        await EnsureSchemaAsync(cancellationToken);

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            return await ReadCoreAsync(scheme, value, fingerprint, cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task ValidateCanonicalEntityAsync(
        Guid? canonicalEntityId,
        CancellationToken cancellationToken)
    {
        if (!canonicalEntityId.HasValue) return;
        if (canonicalEntityId.Value == Guid.Empty)
        {
            throw new ArgumentException("Canonical entity IDs can not be empty.");
        }

        if (await new CanonicalEntityStore(dbContext).ReadAsync(canonicalEntityId.Value, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"Canonical entity '{canonicalEntityId.Value}' does not exist.");
        }
    }

    private async Task<CanonicalBootstrapReconciliationView?> ReadCoreAsync(
        string aliasScheme,
        string aliasValue,
        string semanticFingerprint,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    canonical_bootstrap_reconciliation_id,
                    alias_scheme,
                    alias_value,
                    semantic_fingerprint,
                    classification,
                    canonical_entity_id,
                    related_canonical_entity_id,
                    evidence_kind,
                    confidence,
                    notes,
                    decided_by,
                    created_at,
                    updated_at
                FROM canonical_bootstrap_reconciliation
                WHERE alias_scheme = @alias_scheme
                    AND alias_value = @alias_value
                    AND semantic_fingerprint = @semantic_fingerprint;
                """;
            AddParameter(command, "@alias_scheme", aliasScheme);
            AddParameter(command, "@alias_value", aliasValue);
            AddParameter(command, "@semantic_fingerprint", semanticFingerprint);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return new CanonicalBootstrapReconciliationView(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.GetString(7),
                reader.GetDouble(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetString(10),
                reader.GetFieldValue<DateTimeOffset>(11),
                reader.GetFieldValue<DateTimeOffset>(12));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static bool SameOutcome(
        CanonicalBootstrapReconciliationView current,
        NormalizedRequest requested) =>
        string.Equals(current.Classification, requested.Classification, StringComparison.Ordinal)
        && current.CanonicalEntityId == requested.CanonicalEntityId
        && current.RelatedCanonicalEntityId == requested.RelatedCanonicalEntityId;

    private static NormalizedRequest Normalize(RecordCanonicalBootstrapReconciliationRequest request)
    {
        var scheme = NormalizeRegisteredScheme(request.AliasScheme);
        var value = Require(request.AliasValue, nameof(request.AliasValue), 1000);
        var fingerprint = NormalizeFingerprint(request.SemanticFingerprint);
        var classification = CanonicalSourceIdentity.NormalizeIdentityPart(
            Require(request.Classification, nameof(request.Classification), 80));
        if (!CanonicalBootstrapReconciliationClassifications.IsKnown(classification))
        {
            throw new ArgumentException(
                $"Unknown bootstrap reconciliation classification '{request.Classification}'.",
                nameof(request.Classification));
        }
        if (request.Confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Confidence));
        }
        if (request.CanonicalEntityId.HasValue
            && request.RelatedCanonicalEntityId.HasValue
            && request.CanonicalEntityId.Value == request.RelatedCanonicalEntityId.Value)
        {
            throw new ArgumentException("Canonical and related entity IDs must be distinct.", nameof(request));
        }

        return new NormalizedRequest(
            scheme,
            value,
            fingerprint,
            classification,
            request.CanonicalEntityId,
            request.RelatedCanonicalEntityId,
            Require(request.EvidenceKind, nameof(request.EvidenceKind), 100),
            request.Confidence,
            Require(request.DecidedBy, nameof(request.DecidedBy), 200),
            NormalizeOptional(request.Notes, 2000));
    }

    private static string NormalizeRegisteredScheme(string value)
    {
        var normalized = CanonicalSourceIdentity.NormalizeIdentityPart(
            Require(value, nameof(value), 100));
        if (string.IsNullOrWhiteSpace(normalized)
            || !TrustedSourceLineageRegistry.IsRegisteredScheme(normalized))
        {
            throw new ArgumentException(
                $"Alias scheme '{value}' is not a registered trusted source lineage.",
                nameof(value));
        }
        return normalized;
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

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", nameof(value));
        }
        return normalized;
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private Task EnsureSchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    private sealed record NormalizedRequest(
        string AliasScheme,
        string AliasValue,
        string SemanticFingerprint,
        string Classification,
        Guid? CanonicalEntityId,
        Guid? RelatedCanonicalEntityId,
        string EvidenceKind,
        double Confidence,
        string DecidedBy,
        string? Notes);

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS canonical_bootstrap_reconciliation (
            canonical_bootstrap_reconciliation_id uuid NOT NULL,
            alias_scheme varchar(100) NOT NULL,
            alias_value varchar(1000) NOT NULL,
            semantic_fingerprint varchar(64) NOT NULL,
            classification varchar(80) NOT NULL,
            canonical_entity_id uuid NULL,
            related_canonical_entity_id uuid NULL,
            evidence_kind varchar(100) NOT NULL,
            confidence double precision NOT NULL,
            notes varchar(2000) NULL,
            decided_by varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            updated_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_bootstrap_reconciliation PRIMARY KEY (canonical_bootstrap_reconciliation_id),
            CONSTRAINT fk_canonical_bootstrap_reconciliation_entity FOREIGN KEY (canonical_entity_id)
                REFERENCES canonical_entity(canonical_entity_id) ON DELETE CASCADE,
            CONSTRAINT fk_canonical_bootstrap_reconciliation_related_entity FOREIGN KEY (related_canonical_entity_id)
                REFERENCES canonical_entity(canonical_entity_id) ON DELETE CASCADE,
            CONSTRAINT ck_canonical_bootstrap_reconciliation_classification CHECK (
                classification IN (
                    'exact-identity',
                    'corroborated-exact-identity',
                    'reprint',
                    'revision',
                    'rename',
                    'variant',
                    'same-name-different-entity',
                    'bad-source-data',
                    'parser-error',
                    'unresolved')),
            CONSTRAINT ck_canonical_bootstrap_reconciliation_confidence CHECK (confidence >= 0 AND confidence <= 1));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_bootstrap_reconciliation_identity
            ON canonical_bootstrap_reconciliation(alias_scheme, alias_value, semantic_fingerprint);
        CREATE INDEX IF NOT EXISTS ix_canonical_bootstrap_reconciliation_entity
            ON canonical_bootstrap_reconciliation(canonical_entity_id);
        CREATE INDEX IF NOT EXISTS ix_canonical_bootstrap_reconciliation_related_entity
            ON canonical_bootstrap_reconciliation(related_canonical_entity_id);
        """;
}
