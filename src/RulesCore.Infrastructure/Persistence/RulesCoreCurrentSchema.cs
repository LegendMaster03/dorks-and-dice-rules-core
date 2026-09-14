using Microsoft.EntityFrameworkCore;

namespace RulesCore.Infrastructure.Persistence;

/// <summary>
/// Brings the source/rules schema to the current canonical-identity shape during normal
/// application initialization. Service-level schema guards remain idempotent so development
/// databases created by older feature-branch commits can still be reopened safely.
/// </summary>
internal static class RulesCoreCurrentSchema
{
    public static Task ApplyAsync(
        RulesCoreDbContext dbContext,
        CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlRawAsync(PostgresCurrentSchema, cancellationToken);

    private const string PostgresCurrentSchema = """
        CREATE TABLE IF NOT EXISTS canonical_entity (
            canonical_entity_id uuid NOT NULL,
            canonical_key varchar(300) NOT NULL,
            entity_type varchar(120) NOT NULL,
            normalized_name varchar(500) NOT NULL,
            display_name varchar(500) NOT NULL,
            semantic_fingerprint varchar(64) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_canonical_entity PRIMARY KEY (canonical_entity_id));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_entity_key
            ON canonical_entity(canonical_key);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_entity_exact_identity
            ON canonical_entity(entity_type, normalized_name, semantic_fingerprint);
        CREATE INDEX IF NOT EXISTS ix_canonical_entity_name
            ON canonical_entity(entity_type, normalized_name);
        CREATE INDEX IF NOT EXISTS ix_canonical_entity_semantic_fingerprint
            ON canonical_entity(semantic_fingerprint);

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

        ALTER TABLE canonical_publication
            ADD COLUMN IF NOT EXISTS release_kind varchar(40) NULL;

        ALTER TABLE canonical_source_occurrence
            ADD COLUMN IF NOT EXISTS canonical_entity_id uuid NULL;
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'fk_canonical_source_occurrence_entity'
                  AND conrelid = 'canonical_source_occurrence'::regclass)
            THEN
                ALTER TABLE canonical_source_occurrence
                    ADD CONSTRAINT fk_canonical_source_occurrence_entity
                    FOREIGN KEY (canonical_entity_id)
                    REFERENCES canonical_entity(canonical_entity_id) ON DELETE RESTRICT;
            END IF;
        END $$;
        CREATE INDEX IF NOT EXISTS ix_canonical_source_occurrence_entity
            ON canonical_source_occurrence(canonical_entity_id);

        ALTER TABLE source_entity_occurrence_binding
            ADD COLUMN IF NOT EXISTS source_entity_revision_id uuid NULL;
        UPDATE source_entity_occurrence_binding binding
        SET source_entity_revision_id = (
            SELECT revision.source_entity_revision_id
            FROM source_entity_revision revision
            WHERE revision.source_entity_id = binding.source_entity_id
            ORDER BY revision.revision_number DESC
            LIMIT 1)
        WHERE binding.source_entity_revision_id IS NULL;
        DELETE FROM source_entity_occurrence_binding
        WHERE source_entity_revision_id IS NULL;
        DROP INDEX IF EXISTS ux_source_entity_occurrence_binding_entity;
        CREATE INDEX IF NOT EXISTS ux_source_entity_occurrence_binding_entity
            ON source_entity_occurrence_binding(source_entity_id);
        CREATE INDEX IF NOT EXISTS ix_source_entity_occurrence_binding_entity
            ON source_entity_occurrence_binding(source_entity_id);
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'fk_source_entity_occurrence_binding_revision'
                  AND conrelid = 'source_entity_occurrence_binding'::regclass)
            THEN
                ALTER TABLE source_entity_occurrence_binding
                    ADD CONSTRAINT fk_source_entity_occurrence_binding_revision
                    FOREIGN KEY (source_entity_revision_id)
                    REFERENCES source_entity_revision(source_entity_revision_id) ON DELETE CASCADE;
            END IF;
        END $$;
        ALTER TABLE source_entity_occurrence_binding
            ALTER COLUMN source_entity_revision_id SET NOT NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_entity_occurrence_binding_revision
            ON source_entity_occurrence_binding(source_entity_revision_id);

        ALTER TABLE rule_concept_source_binding
            ADD COLUMN IF NOT EXISTS canonical_entity_id uuid NULL;
        UPDATE rule_concept_source_binding target
        SET canonical_entity_id = occurrence.canonical_entity_id
        FROM source_entity_revision revision
        JOIN source_entity_occurrence_binding source_binding
            ON source_binding.source_entity_revision_id = revision.source_entity_revision_id
        JOIN canonical_source_occurrence occurrence
            ON occurrence.canonical_source_occurrence_id = source_binding.canonical_source_occurrence_id
        WHERE target.canonical_entity_id IS NULL
            AND target.source_entity_id = revision.source_entity_id
            AND occurrence.canonical_entity_id IS NOT NULL
            AND revision.revision_number = (
                SELECT MAX(candidate.revision_number)
                FROM source_entity_revision candidate
                WHERE candidate.source_entity_id = revision.source_entity_id);
        DELETE FROM rule_concept_source_binding target
        WHERE target.canonical_entity_id IS NULL;
        DELETE FROM rule_concept_source_binding duplicate
        USING rule_concept_source_binding keeper
        WHERE duplicate.rule_concept_id = keeper.rule_concept_id
            AND duplicate.canonical_entity_id = keeper.canonical_entity_id
            AND (
                duplicate.created_at > keeper.created_at
                OR (duplicate.created_at = keeper.created_at
                    AND duplicate.rule_concept_source_binding_id > keeper.rule_concept_source_binding_id));
        ALTER TABLE rule_concept_source_binding
            ALTER COLUMN canonical_entity_id SET NOT NULL;
        ALTER TABLE rule_concept_source_binding
            ALTER COLUMN source_entity_id DROP NOT NULL;
        ALTER TABLE rule_concept_source_binding
            DROP CONSTRAINT IF EXISTS fk_rule_concept_source_binding_entity;
        ALTER TABLE rule_concept_source_binding
            ADD CONSTRAINT fk_rule_concept_source_binding_entity
            FOREIGN KEY (source_entity_id)
            REFERENCES source_entity(source_entity_id) ON DELETE SET NULL;
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'fk_rule_concept_source_binding_canonical_entity'
                  AND conrelid = 'rule_concept_source_binding'::regclass)
            THEN
                ALTER TABLE rule_concept_source_binding
                    ADD CONSTRAINT fk_rule_concept_source_binding_canonical_entity
                    FOREIGN KEY (canonical_entity_id)
                    REFERENCES canonical_entity(canonical_entity_id) ON DELETE RESTRICT;
            END IF;
        END $$;
        DROP INDEX IF EXISTS ux_rule_concept_source_binding_concept_entity;
        CREATE INDEX IF NOT EXISTS ux_rule_concept_source_binding_concept_entity
            ON rule_concept_source_binding(rule_concept_id, source_entity_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_rule_concept_source_binding_concept_canonical_entity
            ON rule_concept_source_binding(rule_concept_id, canonical_entity_id);

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
}
