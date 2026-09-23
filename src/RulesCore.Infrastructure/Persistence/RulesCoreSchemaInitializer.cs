using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Persistence;

public interface IRulesCoreSchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class RulesCoreSchemaInitializer(RulesCoreDbContext dbContext) : IRulesCoreSchemaInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        var lockAcquired = false;
        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_lock({SchemaInitializationLockKey});",
                cancellationToken);
            lockAcquired = true;

            if (await HasAppliedSchemaRevisionAsync(connection, cancellationToken))
            {
                return;
            }

            await dbContext.Database.ExecuteSqlRawAsync(SchemaRevisionTableSql, cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync(PostgresSourceSchema, cancellationToken);
            await RulesCoreCurrentSchema.ApplyAsync(dbContext, cancellationToken);
            await SourceFrameworkStore.InitializeSchemaAsync(dbContext, cancellationToken);
            await RuleConceptRelationshipStore.InitializeSchemaAsync(dbContext, cancellationToken);
            await RecordSchemaRevisionAsync(connection, cancellationToken);
        }
        finally
        {
            try
            {
                if (lockAcquired)
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_unlock({SchemaInitializationLockKey});",
                        CancellationToken.None);
                }
            }
            finally
            {
                if (openedHere)
                {
                    await dbContext.Database.CloseConnectionAsync();
                }
            }
        }
    }

    private static async Task<bool> HasAppliedSchemaRevisionAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var tableCommand = connection.CreateCommand())
        {
            tableCommand.CommandText =
                "SELECT to_regclass('rules_core_schema_revision') IS NOT NULL;";
            if (!Convert.ToBoolean(await tableCommand.ExecuteScalarAsync(cancellationToken)))
            {
                return false;
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM rules_core_schema_revision
                WHERE schema_revision = @schema_revision);
            """;
        AddParameter(command, "@schema_revision", CurrentSchemaRevision);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task RecordSchemaRevisionAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO rules_core_schema_revision (schema_revision, applied_at)
            VALUES (@schema_revision, @applied_at);
            """;
        AddParameter(command, "@schema_revision", CurrentSchemaRevision);
        AddParameter(command, "@applied_at", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    // Bump this value whenever startup-owned schema SQL changes. The persisted marker
    // makes a schema revision effectively one-time across replicas and restarts.
    private const string CurrentSchemaRevision = "2026-09-23-source-content-blob-dedup-v1";

    // "DNDRCSCH" encoded as a signed 64-bit key. PostgreSQL advisory locks
    // coordinate independent Rules Core processes that share the same database.
    private const long SchemaInitializationLockKey = 4921946562870068040L;

    private const string SchemaRevisionTableSql = """
        CREATE TABLE IF NOT EXISTS rules_core_schema_revision (
            schema_revision varchar(200) NOT NULL,
            applied_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_rules_core_schema_revision PRIMARY KEY (schema_revision));
        """;

    private const string PostgresSourceSchema = """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'source_entity'
                  AND column_name = 'source_edition_id')
            THEN
                DROP TABLE IF EXISTS campaign_ruleset_revision_entry CASCADE;
                DROP TABLE IF EXISTS campaign_ruleset_revision CASCADE;
                DROP TABLE IF EXISTS campaign_rule_decision CASCADE;
                DROP TABLE IF EXISTS campaign_ruleset_selection CASCADE;
                DROP TABLE IF EXISTS ruleset_revision_entry CASCADE;
                DROP TABLE IF EXISTS ruleset_revision CASCADE;
                DROP TABLE IF EXISTS global_rule_decision CASCADE;
                DROP TABLE IF EXISTS rule_concept_source_binding CASCADE;
                DROP TABLE IF EXISTS rule_concept CASCADE;
                DROP TABLE IF EXISTS user_source_grant CASCADE;
                DROP TABLE IF EXISTS source_entity_occurrence_binding CASCADE;
                DROP TABLE IF EXISTS canonical_source_occurrence CASCADE;
                DROP TABLE IF EXISTS canonical_publication_alias CASCADE;
                DROP TABLE IF EXISTS canonical_publication_evidence_conflict CASCADE;
                DROP TABLE IF EXISTS canonical_publication_publisher_evidence CASCADE;
                DROP TABLE IF EXISTS source_representation_publication CASCADE;
                DROP TABLE IF EXISTS source_representation_entity CASCADE;
                DROP TABLE IF EXISTS canonical_publication CASCADE;
                DROP TABLE IF EXISTS source_entity_revision CASCADE;
                DROP TABLE IF EXISTS source_entity CASCADE;
                DROP TABLE IF EXISTS source_representation CASCADE;
                DROP TABLE IF EXISTS source_edition_authority_reference CASCADE;
                DROP TABLE IF EXISTS source_edition_metadata CASCADE;
                DROP TABLE IF EXISTS source_edition CASCADE;
                DROP TABLE IF EXISTS source_work CASCADE;
                DROP TABLE IF EXISTS source_package CASCADE;
            END IF;
        END $$;

        CREATE TABLE IF NOT EXISTS source_package (
            source_package_id uuid NOT NULL,
            package_key varchar(200) NOT NULL,
            display_name varchar(300) NOT NULL,
            provider varchar(200) NOT NULL,
            license varchar(300) NULL,
            is_public boolean NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_package PRIMARY KEY (source_package_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_package_key
            ON source_package(package_key);

        CREATE TABLE IF NOT EXISTS source_content_blob (
            content_sha256 varchar(64) NOT NULL,
            content_length bigint NOT NULL,
            content_bytes bytea NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_content_blob PRIMARY KEY (content_sha256));

        CREATE TABLE IF NOT EXISTS source_representation (
            source_representation_id uuid NOT NULL,
            source_package_id uuid NOT NULL,
            previous_source_representation_id uuid NULL,
            format_key varchar(80) NOT NULL,
            origin_identity varchar(2000) NOT NULL,
            file_name varchar(500) NOT NULL,
            source_uri varchar(2000) NULL,
            media_type varchar(200) NULL,
            content_sha256 varchar(64) NOT NULL,
            content_length bigint NOT NULL,
            metadata_json jsonb NOT NULL,
            imported_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_representation PRIMARY KEY (source_representation_id),
            CONSTRAINT fk_source_representation_package FOREIGN KEY (source_package_id)
                REFERENCES source_package(source_package_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_representation_content_blob FOREIGN KEY (content_sha256)
                REFERENCES source_content_blob(content_sha256) ON DELETE RESTRICT,
            CONSTRAINT fk_source_representation_previous FOREIGN KEY (previous_source_representation_id)
                REFERENCES source_representation(source_representation_id) ON DELETE SET NULL);
        DO $
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'source_representation'
                  AND column_name = 'content_bytes')
            THEN
                INSERT INTO source_content_blob (
                    content_sha256, content_length, content_bytes, created_at)
                SELECT DISTINCT ON (content_sha256)
                    content_sha256,
                    content_length,
                    content_bytes,
                    imported_at
                FROM source_representation
                ORDER BY content_sha256, imported_at
                ON CONFLICT (content_sha256) DO NOTHING;

                ALTER TABLE source_representation DROP COLUMN content_bytes;
            END IF;
        END $;

        ALTER TABLE source_representation
            DROP CONSTRAINT IF EXISTS fk_source_representation_content_blob;
        ALTER TABLE source_representation
            ADD CONSTRAINT fk_source_representation_content_blob
            FOREIGN KEY (content_sha256)
            REFERENCES source_content_blob(content_sha256) ON DELETE RESTRICT;

        ALTER TABLE source_representation
            DROP CONSTRAINT IF EXISTS fk_source_representation_previous;
        ALTER TABLE source_representation
            ADD CONSTRAINT fk_source_representation_previous
            FOREIGN KEY (previous_source_representation_id)
            REFERENCES source_representation(source_representation_id) ON DELETE SET NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_representation_identity
            ON source_representation(source_package_id, origin_identity, content_sha256);
        CREATE INDEX IF NOT EXISTS ix_source_representation_origin_history
            ON source_representation(source_package_id, origin_identity, imported_at DESC);

        CREATE TABLE IF NOT EXISTS source_entity (
            source_entity_id uuid NOT NULL,
            source_package_id uuid NOT NULL,
            format_key varchar(80) NOT NULL,
            entity_type varchar(120) NOT NULL,
            entity_name varchar(300) NOT NULL,
            source_code varchar(120) NULL,
            native_key varchar(1000) NOT NULL,
            native_identity_json jsonb NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_entity PRIMARY KEY (source_entity_id),
            CONSTRAINT fk_source_entity_package FOREIGN KEY (source_package_id)
                REFERENCES source_package(source_package_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_entity_package_native_key
            ON source_entity(source_package_id, format_key, native_key);

        CREATE TABLE IF NOT EXISTS source_entity_revision (
            source_entity_revision_id uuid NOT NULL,
            source_entity_id uuid NOT NULL,
            source_representation_id uuid NOT NULL,
            revision_number integer NOT NULL,
            fingerprint varchar(64) NOT NULL,
            raw_json jsonb NOT NULL,
            locator_key varchar(500) NULL,
            imported_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_entity_revision PRIMARY KEY (source_entity_revision_id),
            CONSTRAINT fk_source_entity_revision_entity FOREIGN KEY (source_entity_id)
                REFERENCES source_entity(source_entity_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_entity_revision_representation FOREIGN KEY (source_representation_id)
                REFERENCES source_representation(source_representation_id) ON DELETE CASCADE);
        ALTER TABLE source_entity_revision
            DROP CONSTRAINT IF EXISTS fk_source_entity_revision_representation;
        ALTER TABLE source_entity_revision
            ADD CONSTRAINT fk_source_entity_revision_representation
            FOREIGN KEY (source_representation_id)
            REFERENCES source_representation(source_representation_id) ON DELETE CASCADE;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_entity_revision_number
            ON source_entity_revision(source_entity_id, revision_number);
        CREATE INDEX IF NOT EXISTS ix_source_entity_revision_fingerprint
            ON source_entity_revision(fingerprint);
        CREATE INDEX IF NOT EXISTS ix_source_entity_revision_representation
            ON source_entity_revision(source_representation_id);

        CREATE TABLE IF NOT EXISTS source_representation_entity (
            source_representation_entity_id uuid NOT NULL,
            source_representation_id uuid NOT NULL,
            source_entity_id uuid NOT NULL,
            source_entity_revision_id uuid NOT NULL,
            locator_key varchar(500) NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_representation_entity PRIMARY KEY (source_representation_entity_id),
            CONSTRAINT fk_source_representation_entity_representation FOREIGN KEY (source_representation_id)
                REFERENCES source_representation(source_representation_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_representation_entity_entity FOREIGN KEY (source_entity_id)
                REFERENCES source_entity(source_entity_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_representation_entity_revision FOREIGN KEY (source_entity_revision_id)
                REFERENCES source_entity_revision(source_entity_revision_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_representation_entity_identity
            ON source_representation_entity(source_representation_id, source_entity_id);
        CREATE INDEX IF NOT EXISTS ix_source_representation_entity_revision
            ON source_representation_entity(source_entity_revision_id);

        CREATE TABLE IF NOT EXISTS user_source_grant (
            user_source_grant_id uuid NOT NULL,
            source_package_id uuid NOT NULL,
            user_id varchar(200) NOT NULL,
            granted_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_user_source_grant PRIMARY KEY (user_source_grant_id),
            CONSTRAINT fk_user_source_grant_package FOREIGN KEY (source_package_id)
                REFERENCES source_package(source_package_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_user_source_grant_user_package
            ON user_source_grant(user_id, source_package_id);
        CREATE INDEX IF NOT EXISTS ix_user_source_grant_package
            ON user_source_grant(source_package_id);

        CREATE TABLE IF NOT EXISTS canonical_publication (
            canonical_publication_id uuid NOT NULL,
            canonical_key varchar(300) NOT NULL,
            display_name varchar(500) NOT NULL,
            publisher varchar(300) NULL,
            game_edition varchar(40) NULL,
            publication_date date NULL,
            bibliographic_fingerprint varchar(64) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_publication PRIMARY KEY (canonical_publication_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_publication_key
            ON canonical_publication(canonical_key);
        CREATE INDEX IF NOT EXISTS ix_canonical_publication_bibliographic_fingerprint
            ON canonical_publication(bibliographic_fingerprint);

        CREATE TABLE IF NOT EXISTS canonical_publication_alias (
            canonical_publication_alias_id uuid NOT NULL,
            canonical_publication_id uuid NOT NULL,
            alias_scheme varchar(100) NOT NULL,
            alias_value varchar(500) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_publication_alias PRIMARY KEY (canonical_publication_alias_id),
            CONSTRAINT fk_canonical_publication_alias_publication FOREIGN KEY (canonical_publication_id)
                REFERENCES canonical_publication(canonical_publication_id) ON DELETE CASCADE);
        DROP INDEX IF EXISTS ux_canonical_publication_alias_identity;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_publication_alias_publication_identity
            ON canonical_publication_alias(canonical_publication_id, alias_scheme, alias_value);
        CREATE INDEX IF NOT EXISTS ix_canonical_publication_alias_lookup
            ON canonical_publication_alias(alias_scheme, alias_value);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_publication_alias_strong_identity
            ON canonical_publication_alias(alias_scheme, alias_value)
            WHERE alias_scheme IN ('isbn', 'isbn-10', 'isbn10', 'isbn-13', 'isbn13');
        CREATE INDEX IF NOT EXISTS ix_canonical_publication_alias_publication
            ON canonical_publication_alias(canonical_publication_id);

        CREATE TABLE IF NOT EXISTS canonical_source_occurrence (
            canonical_source_occurrence_id uuid NOT NULL,
            canonical_publication_id uuid NOT NULL,
            occurrence_key varchar(1000) NOT NULL,
            entity_type varchar(120) NOT NULL,
            display_name varchar(500) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_source_occurrence PRIMARY KEY (canonical_source_occurrence_id),
            CONSTRAINT fk_canonical_source_occurrence_publication FOREIGN KEY (canonical_publication_id)
                REFERENCES canonical_publication(canonical_publication_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_source_occurrence_identity
            ON canonical_source_occurrence(canonical_publication_id, occurrence_key);

        CREATE TABLE IF NOT EXISTS source_entity_occurrence_binding (
            source_entity_occurrence_binding_id uuid NOT NULL,
            source_entity_id uuid NOT NULL,
            canonical_source_occurrence_id uuid NOT NULL,
            semantic_fingerprint varchar(64) NOT NULL,
            locator_key varchar(500) NULL,
            match_kind varchar(100) NOT NULL,
            confidence double precision NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_entity_occurrence_binding PRIMARY KEY (source_entity_occurrence_binding_id),
            CONSTRAINT fk_source_entity_occurrence_binding_entity FOREIGN KEY (source_entity_id)
                REFERENCES source_entity(source_entity_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_entity_occurrence_binding_occurrence FOREIGN KEY (canonical_source_occurrence_id)
                REFERENCES canonical_source_occurrence(canonical_source_occurrence_id) ON DELETE CASCADE,
            CONSTRAINT ck_source_entity_occurrence_binding_confidence CHECK (confidence >= 0 AND confidence <= 1));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_entity_occurrence_binding_entity
            ON source_entity_occurrence_binding(source_entity_id);
        CREATE INDEX IF NOT EXISTS ix_source_entity_occurrence_binding_occurrence
            ON source_entity_occurrence_binding(canonical_source_occurrence_id);
        CREATE INDEX IF NOT EXISTS ix_source_entity_occurrence_binding_semantic_fingerprint
            ON source_entity_occurrence_binding(semantic_fingerprint);

        CREATE TABLE IF NOT EXISTS source_representation_publication (
            source_representation_publication_id uuid NOT NULL,
            source_representation_id uuid NOT NULL,
            canonical_publication_id uuid NOT NULL,
            local_key varchar(500) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_representation_publication PRIMARY KEY (source_representation_publication_id),
            CONSTRAINT fk_source_representation_publication_representation FOREIGN KEY (source_representation_id)
                REFERENCES source_representation(source_representation_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_representation_publication_canonical FOREIGN KEY (canonical_publication_id)
                REFERENCES canonical_publication(canonical_publication_id) ON DELETE RESTRICT);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_representation_publication_identity
            ON source_representation_publication(source_representation_id, local_key);
        CREATE INDEX IF NOT EXISTS ix_source_representation_publication_canonical
            ON source_representation_publication(canonical_publication_id);

        CREATE TABLE IF NOT EXISTS canonical_publication_evidence_conflict (
            canonical_publication_evidence_conflict_id uuid NOT NULL,
            canonical_publication_id uuid NOT NULL,
            source_entity_id uuid NULL,
            source_representation_id uuid NULL,
            field_name varchar(80) NOT NULL,
            canonical_value varchar(1000) NULL,
            observed_value varchar(1000) NULL,
            recorded_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_publication_evidence_conflict PRIMARY KEY (canonical_publication_evidence_conflict_id),
            CONSTRAINT fk_canonical_publication_evidence_conflict_publication FOREIGN KEY (canonical_publication_id)
                REFERENCES canonical_publication(canonical_publication_id) ON DELETE CASCADE,
            CONSTRAINT fk_canonical_publication_evidence_conflict_source_entity FOREIGN KEY (source_entity_id)
                REFERENCES source_entity(source_entity_id) ON DELETE CASCADE,
            CONSTRAINT fk_canonical_publication_evidence_conflict_representation FOREIGN KEY (source_representation_id)
                REFERENCES source_representation(source_representation_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_publication_evidence_conflict_observation
            ON canonical_publication_evidence_conflict(canonical_publication_id, source_entity_id, field_name);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_publication_evidence_conflict_representation_observation
            ON canonical_publication_evidence_conflict(canonical_publication_id, source_representation_id, field_name)
            WHERE source_representation_id IS NOT NULL;
        CREATE INDEX IF NOT EXISTS ix_canonical_publication_evidence_conflict_publication
            ON canonical_publication_evidence_conflict(canonical_publication_id);

        CREATE TABLE IF NOT EXISTS rule_concept (
            rule_concept_id uuid NOT NULL,
            concept_key varchar(300) NOT NULL,
            entity_type varchar(120) NOT NULL,
            display_name varchar(300) NOT NULL,
            created_by_user_id varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_rule_concept PRIMARY KEY (rule_concept_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_rule_concept_key
            ON rule_concept(concept_key);

        CREATE TABLE IF NOT EXISTS rule_concept_source_binding (
            rule_concept_source_binding_id uuid NOT NULL,
            rule_concept_id uuid NOT NULL,
            source_entity_id uuid NOT NULL,
            created_by_user_id varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_rule_concept_source_binding PRIMARY KEY (rule_concept_source_binding_id),
            CONSTRAINT fk_rule_concept_source_binding_concept FOREIGN KEY (rule_concept_id)
                REFERENCES rule_concept(rule_concept_id) ON DELETE CASCADE,
            CONSTRAINT fk_rule_concept_source_binding_entity FOREIGN KEY (source_entity_id)
                REFERENCES source_entity(source_entity_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_rule_concept_source_binding_concept_entity
            ON rule_concept_source_binding(rule_concept_id, source_entity_id);

        CREATE TABLE IF NOT EXISTS global_rule_decision (
            global_rule_decision_id uuid NOT NULL,
            rule_concept_id uuid NOT NULL,
            decision_number integer NOT NULL,
            decision_kind varchar(80) NOT NULL,
            selected_source_entity_revision_id uuid NOT NULL,
            patch_json jsonb NULL,
            patch_fingerprint varchar(64) NULL,
            note varchar(2000) NULL,
            created_by_user_id varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_global_rule_decision PRIMARY KEY (global_rule_decision_id),
            CONSTRAINT fk_global_rule_decision_concept FOREIGN KEY (rule_concept_id)
                REFERENCES rule_concept(rule_concept_id) ON DELETE CASCADE,
            CONSTRAINT fk_global_rule_decision_source_revision FOREIGN KEY (selected_source_entity_revision_id)
                REFERENCES source_entity_revision(source_entity_revision_id));
        ALTER TABLE global_rule_decision ADD COLUMN IF NOT EXISTS patch_json jsonb NULL;
        ALTER TABLE global_rule_decision ADD COLUMN IF NOT EXISTS patch_fingerprint varchar(64) NULL;
        ALTER TABLE global_rule_decision DROP CONSTRAINT IF EXISTS ck_global_rule_decision_kind;
        ALTER TABLE global_rule_decision ADD CONSTRAINT ck_global_rule_decision_kind CHECK (
            (decision_kind = 'select-source' AND patch_json IS NULL AND patch_fingerprint IS NULL)
            OR (decision_kind = 'json-merge-patch' AND patch_json IS NOT NULL AND patch_fingerprint IS NOT NULL)
            OR (decision_kind = 'json-rule-patch' AND patch_json IS NOT NULL AND patch_fingerprint IS NOT NULL));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_global_rule_decision_concept_number
            ON global_rule_decision(rule_concept_id, decision_number);
        CREATE INDEX IF NOT EXISTS ix_global_rule_decision_source_revision
            ON global_rule_decision(selected_source_entity_revision_id);

        CREATE TABLE IF NOT EXISTS ruleset_revision (
            ruleset_revision_id uuid NOT NULL,
            revision_number integer NOT NULL,
            fingerprint varchar(64) NOT NULL,
            published_by_user_id varchar(200) NOT NULL,
            published_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_ruleset_revision PRIMARY KEY (ruleset_revision_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_ruleset_revision_number
            ON ruleset_revision(revision_number);
        CREATE INDEX IF NOT EXISTS ix_ruleset_revision_fingerprint
            ON ruleset_revision(fingerprint);

        CREATE TABLE IF NOT EXISTS ruleset_revision_entry (
            ruleset_revision_entry_id uuid NOT NULL,
            ruleset_revision_id uuid NOT NULL,
            rule_concept_id uuid NOT NULL,
            global_rule_decision_id uuid NOT NULL,
            source_entity_revision_id uuid NOT NULL,
            CONSTRAINT pk_ruleset_revision_entry PRIMARY KEY (ruleset_revision_entry_id),
            CONSTRAINT fk_ruleset_revision_entry_revision FOREIGN KEY (ruleset_revision_id)
                REFERENCES ruleset_revision(ruleset_revision_id) ON DELETE CASCADE,
            CONSTRAINT fk_ruleset_revision_entry_concept FOREIGN KEY (rule_concept_id)
                REFERENCES rule_concept(rule_concept_id),
            CONSTRAINT fk_ruleset_revision_entry_decision FOREIGN KEY (global_rule_decision_id)
                REFERENCES global_rule_decision(global_rule_decision_id),
            CONSTRAINT fk_ruleset_revision_entry_source_revision FOREIGN KEY (source_entity_revision_id)
                REFERENCES source_entity_revision(source_entity_revision_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_ruleset_revision_entry_revision_concept
            ON ruleset_revision_entry(ruleset_revision_id, rule_concept_id);

        CREATE TABLE IF NOT EXISTS campaign_ruleset_selection (
            campaign_ruleset_selection_id uuid NOT NULL,
            campaign_id uuid NOT NULL,
            selection_number integer NOT NULL,
            ruleset_revision_id uuid NOT NULL,
            selected_by_user_id varchar(200) NOT NULL,
            selected_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_campaign_ruleset_selection PRIMARY KEY (campaign_ruleset_selection_id),
            CONSTRAINT fk_campaign_ruleset_selection_ruleset_revision FOREIGN KEY (ruleset_revision_id)
                REFERENCES ruleset_revision(ruleset_revision_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_campaign_ruleset_selection_campaign_number
            ON campaign_ruleset_selection(campaign_id, selection_number);
        CREATE INDEX IF NOT EXISTS ix_campaign_ruleset_selection_ruleset_revision
            ON campaign_ruleset_selection(ruleset_revision_id);

        CREATE TABLE IF NOT EXISTS campaign_rule_decision (
            campaign_rule_decision_id uuid NOT NULL,
            campaign_id uuid NOT NULL,
            rule_concept_id uuid NOT NULL,
            decision_number integer NOT NULL,
            decision_kind varchar(80) NOT NULL,
            selected_source_entity_revision_id uuid NULL,
            patch_json jsonb NULL,
            patch_fingerprint varchar(64) NULL,
            note varchar(2000) NULL,
            created_by_user_id varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_campaign_rule_decision PRIMARY KEY (campaign_rule_decision_id),
            CONSTRAINT fk_campaign_rule_decision_concept FOREIGN KEY (rule_concept_id)
                REFERENCES rule_concept(rule_concept_id),
            CONSTRAINT fk_campaign_rule_decision_source_revision FOREIGN KEY (selected_source_entity_revision_id)
                REFERENCES source_entity_revision(source_entity_revision_id));
        ALTER TABLE campaign_rule_decision ADD COLUMN IF NOT EXISTS patch_json jsonb NULL;
        ALTER TABLE campaign_rule_decision ADD COLUMN IF NOT EXISTS patch_fingerprint varchar(64) NULL;
        ALTER TABLE campaign_rule_decision DROP CONSTRAINT IF EXISTS ck_campaign_rule_decision_kind;
        ALTER TABLE campaign_rule_decision ADD CONSTRAINT ck_campaign_rule_decision_kind CHECK (
            (decision_kind = 'select-source'
                AND selected_source_entity_revision_id IS NOT NULL
                AND patch_json IS NULL
                AND patch_fingerprint IS NULL)
            OR (decision_kind = 'inherit-global'
                AND selected_source_entity_revision_id IS NULL
                AND patch_json IS NULL
                AND patch_fingerprint IS NULL)
            OR (decision_kind = 'json-merge-patch'
                AND selected_source_entity_revision_id IS NULL
                AND patch_json IS NOT NULL
                AND patch_fingerprint IS NOT NULL)
            OR (decision_kind = 'json-rule-patch'
                AND selected_source_entity_revision_id IS NULL
                AND patch_json IS NOT NULL
                AND patch_fingerprint IS NOT NULL));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_campaign_rule_decision_campaign_concept_number
            ON campaign_rule_decision(campaign_id, rule_concept_id, decision_number);
        CREATE INDEX IF NOT EXISTS ix_campaign_rule_decision_source_revision
            ON campaign_rule_decision(selected_source_entity_revision_id);

        CREATE TABLE IF NOT EXISTS campaign_ruleset_revision (
            campaign_ruleset_revision_id uuid NOT NULL,
            campaign_id uuid NOT NULL,
            revision_number integer NOT NULL,
            baseline_selection_id uuid NOT NULL,
            fingerprint varchar(64) NOT NULL,
            published_by_user_id varchar(200) NOT NULL,
            published_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_campaign_ruleset_revision PRIMARY KEY (campaign_ruleset_revision_id),
            CONSTRAINT fk_campaign_ruleset_revision_baseline_selection FOREIGN KEY (baseline_selection_id)
                REFERENCES campaign_ruleset_selection(campaign_ruleset_selection_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_campaign_ruleset_revision_campaign_number
            ON campaign_ruleset_revision(campaign_id, revision_number);
        CREATE INDEX IF NOT EXISTS ix_campaign_ruleset_revision_campaign_fingerprint
            ON campaign_ruleset_revision(campaign_id, fingerprint);

        CREATE TABLE IF NOT EXISTS campaign_ruleset_revision_entry (
            campaign_ruleset_revision_entry_id uuid NOT NULL,
            campaign_ruleset_revision_id uuid NOT NULL,
            rule_concept_id uuid NOT NULL,
            baseline_ruleset_revision_entry_id uuid NOT NULL,
            campaign_rule_decision_id uuid NULL,
            source_entity_revision_id uuid NOT NULL,
            CONSTRAINT pk_campaign_ruleset_revision_entry PRIMARY KEY (campaign_ruleset_revision_entry_id),
            CONSTRAINT fk_campaign_ruleset_revision_entry_revision FOREIGN KEY (campaign_ruleset_revision_id)
                REFERENCES campaign_ruleset_revision(campaign_ruleset_revision_id) ON DELETE CASCADE,
            CONSTRAINT fk_campaign_ruleset_revision_entry_concept FOREIGN KEY (rule_concept_id)
                REFERENCES rule_concept(rule_concept_id),
            CONSTRAINT fk_campaign_ruleset_revision_entry_baseline_entry FOREIGN KEY (baseline_ruleset_revision_entry_id)
                REFERENCES ruleset_revision_entry(ruleset_revision_entry_id),
            CONSTRAINT fk_campaign_ruleset_revision_entry_campaign_decision FOREIGN KEY (campaign_rule_decision_id)
                REFERENCES campaign_rule_decision(campaign_rule_decision_id),
            CONSTRAINT fk_campaign_ruleset_revision_entry_source_revision FOREIGN KEY (source_entity_revision_id)
                REFERENCES source_entity_revision(source_entity_revision_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_campaign_ruleset_revision_entry_revision_concept
            ON campaign_ruleset_revision_entry(campaign_ruleset_revision_id, rule_concept_id);
        """;
}
