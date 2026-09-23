using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

internal sealed record StoredRuleAdjudicationWorkItem(
    Guid Id,
    string WorkKey,
    string WorkKind,
    string State,
    int Version,
    Guid? RuleConceptId,
    Guid? SourceEntityId,
    Guid? SourceRevisionId,
    Guid? ExpectedGlobalRuleDecisionId,
    string? Question,
    string? QuestionRequestedBy,
    DateTimeOffset? QuestionRequestedAt,
    int? QuestionRequestedVersion,
    string? Answer,
    string? AnsweredBy,
    DateTimeOffset? AnsweredAt,
    string? ManualReason,
    string? DeferredReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed class RuleAdjudicationWorkStore(RulesCoreDbContext dbContext)
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS rule_adjudication_work_item (
            rule_adjudication_work_item_id uuid NOT NULL,
            work_key varchar(500) NOT NULL,
            work_kind varchar(80) NOT NULL,
            state varchar(80) NOT NULL,
            version integer NOT NULL,
            rule_concept_id uuid NULL,
            source_entity_id uuid NULL,
            source_entity_revision_id uuid NULL,
            expected_global_rule_decision_id uuid NULL,
            clarification_question varchar(4000) NULL,
            clarification_requested_by_user_id varchar(200) NULL,
            clarification_requested_at timestamp with time zone NULL,
            clarification_requested_version integer NULL,
            clarification_answer varchar(8000) NULL,
            clarification_answered_by_user_id varchar(200) NULL,
            clarification_answered_at timestamp with time zone NULL,
            manual_reason varchar(2000) NULL,
            deferred_reason varchar(2000) NULL,
            created_at timestamp with time zone NOT NULL,
            updated_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_rule_adjudication_work_item PRIMARY KEY (rule_adjudication_work_item_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_rule_adjudication_work_item_key
            ON rule_adjudication_work_item(work_key);
        CREATE INDEX IF NOT EXISTS ix_rule_adjudication_work_item_state
            ON rule_adjudication_work_item(state, work_kind);
        CREATE INDEX IF NOT EXISTS ix_rule_adjudication_work_item_concept
            ON rule_adjudication_work_item(rule_concept_id);
        CREATE INDEX IF NOT EXISTS ix_rule_adjudication_work_item_source
            ON rule_adjudication_work_item(source_entity_id);

        CREATE TABLE IF NOT EXISTS rule_adjudication_work_event (
            rule_adjudication_work_event_id uuid NOT NULL,
            rule_adjudication_work_item_id uuid NOT NULL,
            event_kind varchar(100) NOT NULL,
            actor_user_id varchar(200) NULL,
            work_version integer NOT NULL,
            message text NULL,
            rule_concept_id uuid NULL,
            source_entity_id uuid NULL,
            global_rule_decision_id uuid NULL,
            ruleset_revision_id uuid NULL,
            dedupe_key varchar(700) NULL,
            occurred_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_rule_adjudication_work_event PRIMARY KEY (rule_adjudication_work_event_id),
            CONSTRAINT fk_rule_adjudication_work_event_item FOREIGN KEY (rule_adjudication_work_item_id)
                REFERENCES rule_adjudication_work_item(rule_adjudication_work_item_id) ON DELETE CASCADE);
        CREATE INDEX IF NOT EXISTS ix_rule_adjudication_work_event_item
            ON rule_adjudication_work_event(rule_adjudication_work_item_id, occurred_at, rule_adjudication_work_event_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_rule_adjudication_work_event_dedupe
            ON rule_adjudication_work_event(rule_adjudication_work_item_id, dedupe_key)
            WHERE dedupe_key IS NOT NULL;
        ALTER TABLE rule_adjudication_work_event
            ALTER COLUMN message TYPE text;
        """;

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    public async Task<StoredRuleAdjudicationWorkItem> UpsertAsync(
        string workKey,
        string workKind,
        string initialState,
        Guid? ruleConceptId,
        Guid? sourceEntityId,
        Guid? sourceRevisionId,
        Guid? expectedGlobalRuleDecisionId,
        string? manualReason,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = """
                INSERT INTO rule_adjudication_work_item (
                    rule_adjudication_work_item_id, work_key, work_kind, state, version,
                    rule_concept_id, source_entity_id, source_entity_revision_id,
                    expected_global_rule_decision_id, manual_reason,
                    created_at, updated_at)
                VALUES (@id, @work_key, @work_kind, @state, 1,
                    @rule_concept_id, @source_entity_id, @source_revision_id,
                    @expected_decision_id, @manual_reason, @now, @now)
                ON CONFLICT (work_key) DO UPDATE SET
                    rule_concept_id = COALESCE(rule_adjudication_work_item.rule_concept_id, EXCLUDED.rule_concept_id),
                    source_entity_id = COALESCE(rule_adjudication_work_item.source_entity_id, EXCLUDED.source_entity_id),
                    source_entity_revision_id = COALESCE(rule_adjudication_work_item.source_entity_revision_id, EXCLUDED.source_entity_revision_id),
                    expected_global_rule_decision_id = COALESCE(rule_adjudication_work_item.expected_global_rule_decision_id, EXCLUDED.expected_global_rule_decision_id)
                RETURNING rule_adjudication_work_item_id, work_key, work_kind, state, version,
                    rule_concept_id, source_entity_id, source_entity_revision_id, expected_global_rule_decision_id,
                    clarification_question, clarification_requested_by_user_id, clarification_requested_at,
                    clarification_requested_version, clarification_answer, clarification_answered_by_user_id,
                    clarification_answered_at, manual_reason, deferred_reason, created_at, updated_at;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@work_key", workKey);
            AddParameter(command, "@work_kind", workKind);
            AddParameter(command, "@state", initialState);
            AddNullableParameter(command, "@rule_concept_id", ruleConceptId, DbType.Guid);
            AddNullableParameter(command, "@source_entity_id", sourceEntityId, DbType.Guid);
            AddNullableParameter(command, "@source_revision_id", sourceRevisionId, DbType.Guid);
            AddNullableParameter(command, "@expected_decision_id", expectedGlobalRuleDecisionId, DbType.Guid);
            AddNullableParameter(command, "@manual_reason", manualReason, DbType.String);
            AddParameter(command, "@now", DateTimeOffset.UtcNow);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return ReadWorkItem(reader);
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    public async Task<IReadOnlyList<StoredRuleAdjudicationWorkItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = SelectColumns + " ORDER BY created_at, rule_adjudication_work_item_id;";
            var results = new List<StoredRuleAdjudicationWorkItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) results.Add(ReadWorkItem(reader));
            return results;
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    public Task<StoredRuleAdjudicationWorkItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        GetOneAsync(id, forUpdate: false, cancellationToken);

    public Task<StoredRuleAdjudicationWorkItem?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetOneAsync(id, forUpdate: true, cancellationToken);

    private async Task<StoredRuleAdjudicationWorkItem?> GetOneAsync(Guid id, bool forUpdate, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = SelectColumns + " WHERE rule_adjudication_work_item_id = @id" + (forUpdate ? " FOR UPDATE;" : ";");
            AddParameter(command, "@id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadWorkItem(reader) : null;
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    public async Task<StoredRuleAdjudicationWorkItem> CompleteAsync(
        StoredRuleAdjudicationWorkItem row,
        Guid? ruleConceptId,
        CancellationToken cancellationToken)
    {
        if (row.State == RuleAdjudicationWorkStates.Completed
            && (ruleConceptId is null || row.RuleConceptId == ruleConceptId)) return row;
        return await UpdateAsync(
            row.Id,
            """
            state = @state,
            version = version + 1,
            rule_concept_id = COALESCE(@rule_concept_id, rule_concept_id),
            updated_at = @now
            """,
            command =>
            {
                AddParameter(command, "@state", RuleAdjudicationWorkStates.Completed);
                AddNullableParameter(command, "@rule_concept_id", ruleConceptId, DbType.Guid);
                AddParameter(command, "@now", DateTimeOffset.UtcNow);
            },
            cancellationToken);
    }

    public Task<StoredRuleAdjudicationWorkItem> TransitionAsync(
        StoredRuleAdjudicationWorkItem row,
        string state,
        string? manualReason,
        string? deferredReason,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            row.Id,
            """
            state = @state,
            version = version + 1,
            manual_reason = @manual_reason,
            deferred_reason = @deferred_reason,
            updated_at = @now
            """,
            command =>
            {
                AddParameter(command, "@state", state);
                AddNullableParameter(command, "@manual_reason", manualReason, DbType.String);
                AddNullableParameter(command, "@deferred_reason", deferredReason, DbType.String);
                AddParameter(command, "@now", DateTimeOffset.UtcNow);
            },
            cancellationToken);

    public Task<StoredRuleAdjudicationWorkItem> SetClarificationAsync(
        StoredRuleAdjudicationWorkItem row,
        string question,
        string actor,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            row.Id,
            """
            state = @state,
            version = version + 1,
            clarification_question = @question,
            clarification_requested_by_user_id = @actor,
            clarification_requested_at = @now,
            clarification_requested_version = version + 1,
            clarification_answer = NULL,
            clarification_answered_by_user_id = NULL,
            clarification_answered_at = NULL,
            updated_at = @now
            """,
            command =>
            {
                AddParameter(command, "@state", RuleAdjudicationWorkStates.WaitingForHuman);
                AddParameter(command, "@question", question);
                AddParameter(command, "@actor", actor);
                AddParameter(command, "@now", now);
            },
            cancellationToken);

    public Task<StoredRuleAdjudicationWorkItem> AnswerClarificationAsync(
        StoredRuleAdjudicationWorkItem row,
        string answer,
        string actor,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            row.Id,
            """
            state = @state,
            version = version + 1,
            clarification_answer = @answer,
            clarification_answered_by_user_id = @actor,
            clarification_answered_at = @now,
            updated_at = @now
            """,
            command =>
            {
                AddParameter(command, "@state", RuleAdjudicationWorkStates.AgentReview);
                AddParameter(command, "@answer", answer);
                AddParameter(command, "@actor", actor);
                AddParameter(command, "@now", now);
            },
            cancellationToken);

    public async Task AppendEventAsync(
        Guid workItemId,
        string eventKind,
        string? actorUserId,
        int workVersion,
        string? message,
        Guid? ruleConceptId,
        Guid? sourceEntityId,
        Guid? globalRuleDecisionId,
        Guid? rulesetRevisionId,
        string? dedupeKey,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = """
                INSERT INTO rule_adjudication_work_event (
                    rule_adjudication_work_event_id, rule_adjudication_work_item_id,
                    event_kind, actor_user_id, work_version, message,
                    rule_concept_id, source_entity_id, global_rule_decision_id,
                    ruleset_revision_id, dedupe_key, occurred_at)
                VALUES (@id, @work_item_id, @event_kind, @actor, @work_version, @message,
                    @rule_concept_id, @source_entity_id, @decision_id,
                    @ruleset_revision_id, @dedupe_key, @occurred_at)
                ON CONFLICT (rule_adjudication_work_item_id, dedupe_key)
                    WHERE dedupe_key IS NOT NULL DO NOTHING;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@work_item_id", workItemId);
            AddParameter(command, "@event_kind", eventKind);
            AddNullableParameter(command, "@actor", actorUserId, DbType.String);
            AddParameter(command, "@work_version", workVersion);
            AddNullableParameter(command, "@message", message, DbType.String);
            AddNullableParameter(command, "@rule_concept_id", ruleConceptId, DbType.Guid);
            AddNullableParameter(command, "@source_entity_id", sourceEntityId, DbType.Guid);
            AddNullableParameter(command, "@decision_id", globalRuleDecisionId, DbType.Guid);
            AddNullableParameter(command, "@ruleset_revision_id", rulesetRevisionId, DbType.Guid);
            AddNullableParameter(command, "@dedupe_key", dedupeKey, DbType.String);
            AddParameter(command, "@occurred_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    public async Task<IReadOnlyList<RuleAdjudicationWorkEventView>> GetEventsAsync(
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = """
                SELECT rule_adjudication_work_event_id, event_kind, actor_user_id,
                    work_version, message, rule_concept_id, source_entity_id,
                    global_rule_decision_id, ruleset_revision_id, occurred_at
                FROM rule_adjudication_work_event
                WHERE rule_adjudication_work_item_id = @work_item_id
                ORDER BY occurred_at, rule_adjudication_work_event_id;
                """;
            AddParameter(command, "@work_item_id", workItemId);
            var results = new List<RuleAdjudicationWorkEventView>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new RuleAdjudicationWorkEventView(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    GetNullableString(reader, 2),
                    reader.GetInt32(3),
                    GetNullableString(reader, 4),
                    GetNullableGuid(reader, 5),
                    GetNullableGuid(reader, 6),
                    GetNullableGuid(reader, 7),
                    GetNullableGuid(reader, 8),
                    reader.GetFieldValue<DateTimeOffset>(9)));
            }
            return results;
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    private async Task<StoredRuleAdjudicationWorkItem> UpdateAsync(
        Guid id,
        string assignments,
        Action<DbCommand> configure,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await EnsureOpenAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection);
            command.CommandText = $"""
                UPDATE rule_adjudication_work_item
                SET {assignments}
                WHERE rule_adjudication_work_item_id = @id
                RETURNING rule_adjudication_work_item_id, work_key, work_kind, state, version,
                    rule_concept_id, source_entity_id, source_entity_revision_id, expected_global_rule_decision_id,
                    clarification_question, clarification_requested_by_user_id, clarification_requested_at,
                    clarification_requested_version, clarification_answer, clarification_answered_by_user_id,
                    clarification_answered_at, manual_reason, deferred_reason, created_at, updated_at;
                """;
            AddParameter(command, "@id", id);
            configure(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("The adjudication work item no longer exists.");
            }
            return ReadWorkItem(reader);
        }
        finally
        {
            await CloseIfNeededAsync(connection, openedHere);
        }
    }

    private const string SelectColumns = """
        SELECT rule_adjudication_work_item_id, work_key, work_kind, state, version,
            rule_concept_id, source_entity_id, source_entity_revision_id, expected_global_rule_decision_id,
            clarification_question, clarification_requested_by_user_id, clarification_requested_at,
            clarification_requested_version, clarification_answer, clarification_answered_by_user_id,
            clarification_answered_at, manual_reason, deferred_reason, created_at, updated_at
        FROM rule_adjudication_work_item
        """;

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        if (dbContext.Database.CurrentTransaction is { } transaction)
        {
            command.Transaction = transaction.GetDbTransaction();
        }
        return command;
    }

    private static StoredRuleAdjudicationWorkItem ReadWorkItem(DbDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetInt32(4),
        GetNullableGuid(reader, 5),
        GetNullableGuid(reader, 6),
        GetNullableGuid(reader, 7),
        GetNullableGuid(reader, 8),
        GetNullableString(reader, 9),
        GetNullableString(reader, 10),
        GetNullableDateTimeOffset(reader, 11),
        GetNullableInt32(reader, 12),
        GetNullableString(reader, 13),
        GetNullableString(reader, 14),
        GetNullableDateTimeOffset(reader, 15),
        GetNullableString(reader, 16),
        GetNullableString(reader, 17),
        reader.GetFieldValue<DateTimeOffset>(18),
        reader.GetFieldValue<DateTimeOffset>(19));

    private static Guid? GetNullableGuid(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    private static int? GetNullableInt32(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    private static string? GetNullableString(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static DateTimeOffset? GetNullableDateTimeOffset(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);

    private static async Task<bool> EnsureOpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State == ConnectionState.Open) return false;
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

    private static void AddNullableParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
