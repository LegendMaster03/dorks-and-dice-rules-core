using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class SourceRevisionRejectionService(RulesCoreDbContext dbContext)
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS source_revision_rejection (
            source_revision_rejection_id uuid NOT NULL,
            global_rule_decision_id uuid NOT NULL,
            source_entity_revision_id uuid NOT NULL,
            source_fingerprint varchar(64) NOT NULL,
            reason varchar(2000) NOT NULL,
            created_by_user_id varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_revision_rejection PRIMARY KEY (source_revision_rejection_id),
            CONSTRAINT fk_source_revision_rejection_decision FOREIGN KEY (global_rule_decision_id)
                REFERENCES global_rule_decision(global_rule_decision_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_revision_rejection_revision FOREIGN KEY (source_entity_revision_id)
                REFERENCES source_entity_revision(source_entity_revision_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_revision_rejection_review_target
            ON source_revision_rejection(global_rule_decision_id, source_entity_revision_id);
        CREATE INDEX IF NOT EXISTS ix_source_revision_rejection_revision
            ON source_revision_rejection(source_entity_revision_id);
        """;

    public async Task<IReadOnlyList<SourceRevisionReviewItemView>> FilterRejectedAsync(
        IReadOnlyList<SourceRevisionReviewItemView> pending,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pending);
        if (pending.Count == 0)
        {
            return pending;
        }

        await EnsureSchemaAsync(cancellationToken);
        var rejected = await GetRejectedTargetsAsync(cancellationToken);
        return pending
            .Where(value => !rejected.Contains((value.GlobalRuleDecisionId, value.LatestSourceEntityRevisionId)))
            .ToArray();
    }

    public async Task<SourceRevisionRejectionView?> RejectLatestAsync(
        Guid ruleConceptId,
        RejectLatestSourceRevisionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireGuid(ruleConceptId, nameof(ruleConceptId));
        RequireGuid(request.ExpectedGlobalRuleDecisionId, nameof(request.ExpectedGlobalRuleDecisionId));
        RequireGuid(request.ExpectedLatestSourceEntityRevisionId, nameof(request.ExpectedLatestSourceEntityRevisionId));
        var actor = RequireUserId(actorUserId);
        var fingerprint = RequireFingerprint(request.ExpectedLatestFingerprint);
        var reason = RequireReason(request.Reason);
        await EnsureSchemaAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var current = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.SelectedSourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
                .ThenInclude(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => value.RuleConceptId == ruleConceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            throw new KeyNotFoundException($"Rule concept '{ruleConceptId}' has no global rule decision.");
        }

        if (current.Id != request.ExpectedGlobalRuleDecisionId)
        {
            throw new InvalidOperationException(
                "The global rule decision changed after this source update was reviewed. Reload the review before rejecting a source revision.");
        }

        var package = current.SelectedSourceEntityRevision
            .SourceEntity.SourceEdition.SourceWork.SourcePackage;
        if (!package.IsPublic && !package.UserGrants.Any(grant => grant.UserId == actor))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var latestRevision = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => value.SourceEntityId == current.SelectedSourceEntityRevision.SourceEntityId)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstAsync(cancellationToken);
        if (latestRevision.RevisionNumber <= current.SelectedSourceEntityRevision.RevisionNumber)
        {
            throw new InvalidOperationException(
                "This global rule decision no longer has a newer source revision to reject.");
        }

        if (latestRevision.Id != request.ExpectedLatestSourceEntityRevisionId
            || !string.Equals(latestRevision.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The source entity changed after this update was reviewed. Reload the review before rejecting a source revision.");
        }

        var existing = await GetAsync(current.Id, latestRevision.Id, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.Reason, reason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "This source revision was already rejected for the current global decision with a different reason.");
            }

            await transaction.CommitAsync(cancellationToken);
            return ToView(existing, current.RuleConceptId, current.RuleConcept.Key, latestRevision.RevisionNumber, created: false);
        }

        var stored = new StoredSourceRevisionRejection(
            Guid.NewGuid(),
            current.Id,
            latestRevision.Id,
            latestRevision.Fingerprint,
            reason,
            actor,
            DateTimeOffset.UtcNow);
        var created = await InsertAsync(stored, cancellationToken);
        if (!created)
        {
            existing = await GetAsync(current.Id, latestRevision.Id, cancellationToken)
                ?? throw new InvalidOperationException("The source revision rejection could not be read after a concurrent insert.");
            if (!string.Equals(existing.Reason, reason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "This source revision was already rejected for the current global decision with a different reason.");
            }
            stored = existing;
        }

        await transaction.CommitAsync(cancellationToken);
        return ToView(stored, current.RuleConceptId, current.RuleConcept.Key, latestRevision.RevisionNumber, created);
    }

    private async Task<HashSet<(Guid DecisionId, Guid RevisionId)>> GetRejectedTargetsAsync(
        CancellationToken cancellationToken)
    {
        var results = new HashSet<(Guid, Guid)>();
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = "SELECT global_rule_decision_id, source_entity_revision_id FROM source_revision_rejection;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add((reader.GetGuid(0), reader.GetGuid(1)));
            }
            return results;
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    private async Task<StoredSourceRevisionRejection?> GetAsync(
        Guid decisionId,
        Guid revisionId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = """
                SELECT source_revision_rejection_id, global_rule_decision_id,
                    source_entity_revision_id, source_fingerprint, reason,
                    created_by_user_id, created_at
                FROM source_revision_rejection
                WHERE global_rule_decision_id = @decision_id
                    AND source_entity_revision_id = @revision_id;
                """;
            AddParameter(command, "@decision_id", decisionId);
            AddParameter(command, "@revision_id", revisionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            return Read(reader);
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    private async Task<bool> InsertAsync(StoredSourceRevisionRejection value, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = """
                INSERT INTO source_revision_rejection (
                    source_revision_rejection_id, global_rule_decision_id,
                    source_entity_revision_id, source_fingerprint, reason,
                    created_by_user_id, created_at)
                VALUES (@id, @decision_id, @revision_id, @fingerprint, @reason, @actor, @created_at)
                ON CONFLICT (global_rule_decision_id, source_entity_revision_id) DO NOTHING;
                """;
            AddParameter(command, "@id", value.Id);
            AddParameter(command, "@decision_id", value.GlobalRuleDecisionId);
            AddParameter(command, "@revision_id", value.SourceEntityRevisionId);
            AddParameter(command, "@fingerprint", value.SourceFingerprint);
            AddParameter(command, "@reason", value.Reason);
            AddParameter(command, "@actor", value.CreatedByUserId);
            AddParameter(command, "@created_at", value.CreatedAt);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    private Task EnsureSchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        if (dbContext.Database.CurrentTransaction is { } transaction)
        {
            command.Transaction = transaction.GetDbTransaction();
        }
        return command;
    }

    private static StoredSourceRevisionRejection Read(DbDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetFieldValue<DateTimeOffset>(6));

    private static SourceRevisionRejectionView ToView(
        StoredSourceRevisionRejection value,
        Guid ruleConceptId,
        string conceptKey,
        int revisionNumber,
        bool created) => new(
            value.Id,
            ruleConceptId,
            conceptKey,
            value.GlobalRuleDecisionId,
            value.SourceEntityRevisionId,
            revisionNumber,
            value.SourceFingerprint,
            value.Reason,
            value.CreatedByUserId,
            value.CreatedAt,
            created);

    private static async Task<bool> EnsureOpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State == ConnectionState.Open)
        {
            return false;
        }
        await connection.OpenAsync(cancellationToken);
        return true;
    }

    private async Task CloseIfNeededAsync(DbConnection connection, bool openedHere)
    {
        if (openedHere && dbContext.Database.CurrentTransaction is null)
        {
            await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be an empty GUID.", parameterName);
        }
    }

    private static string RequireUserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", nameof(value));
        }
        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("Value can not exceed 200 characters.", nameof(value));
        }
        return normalized;
    }

    private static string RequireFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Expected source fingerprint can not be blank.", nameof(value));
        }
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "Expected source fingerprint must be a 64-character SHA-256 hexadecimal value.",
                nameof(value));
        }
        return normalized;
    }

    private static string RequireReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A source revision rejection reason is required.", nameof(value));
        }
        var normalized = value.Trim();
        if (normalized.Length > 2000)
        {
            throw new ArgumentException("A source revision rejection reason can not exceed 2000 characters.", nameof(value));
        }
        return normalized;
    }

    private sealed record StoredSourceRevisionRejection(
        Guid Id,
        Guid GlobalRuleDecisionId,
        Guid SourceEntityRevisionId,
        string SourceFingerprint,
        string Reason,
        string CreatedByUserId,
        DateTimeOffset CreatedAt);
}
