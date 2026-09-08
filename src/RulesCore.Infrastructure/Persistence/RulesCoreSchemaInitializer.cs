using Microsoft.EntityFrameworkCore;

namespace RulesCore.Infrastructure.Persistence;

public interface IRulesCoreSchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class RulesCoreSchemaInitializer(RulesCoreDbContext dbContext) : IRulesCoreSchemaInitializer
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlRawAsync(PostgresSourceSchema, cancellationToken);

    private const string PostgresSourceSchema = """
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

        CREATE TABLE IF NOT EXISTS source_work (
            source_work_id uuid NOT NULL,
            source_package_id uuid NOT NULL,
            work_key varchar(200) NOT NULL,
            display_name varchar(300) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_work PRIMARY KEY (source_work_id),
            CONSTRAINT fk_source_work_package FOREIGN KEY (source_package_id)
                REFERENCES source_package(source_package_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_work_package_key
            ON source_work(source_package_id, work_key);

        CREATE TABLE IF NOT EXISTS source_edition (
            source_edition_id uuid NOT NULL,
            source_work_id uuid NOT NULL,
            edition_key varchar(200) NOT NULL,
            display_name varchar(300) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_edition PRIMARY KEY (source_edition_id),
            CONSTRAINT fk_source_edition_work FOREIGN KEY (source_work_id)
                REFERENCES source_work(source_work_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_edition_work_key
            ON source_edition(source_work_id, edition_key);

        CREATE TABLE IF NOT EXISTS source_entity (
            source_entity_id uuid NOT NULL,
            source_edition_id uuid NOT NULL,
            entity_type varchar(120) NOT NULL,
            entity_name varchar(300) NOT NULL,
            source_code varchar(120) NOT NULL,
            natural_key varchar(800) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_entity PRIMARY KEY (source_entity_id),
            CONSTRAINT fk_source_entity_edition FOREIGN KEY (source_edition_id)
                REFERENCES source_edition(source_edition_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_entity_edition_natural_key
            ON source_entity(source_edition_id, natural_key);

        CREATE TABLE IF NOT EXISTS source_entity_revision (
            source_entity_revision_id uuid NOT NULL,
            source_entity_id uuid NOT NULL,
            revision_number integer NOT NULL,
            fingerprint varchar(64) NOT NULL,
            raw_json jsonb NOT NULL,
            imported_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_entity_revision PRIMARY KEY (source_entity_revision_id),
            CONSTRAINT fk_source_entity_revision_entity FOREIGN KEY (source_entity_id)
                REFERENCES source_entity(source_entity_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_entity_revision_number
            ON source_entity_revision(source_entity_id, revision_number);
        CREATE INDEX IF NOT EXISTS ix_source_entity_revision_fingerprint
            ON source_entity_revision(fingerprint);

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
            note varchar(2000) NULL,
            created_by_user_id varchar(200) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_global_rule_decision PRIMARY KEY (global_rule_decision_id),
            CONSTRAINT fk_global_rule_decision_concept FOREIGN KEY (rule_concept_id)
                REFERENCES rule_concept(rule_concept_id) ON DELETE CASCADE,
            CONSTRAINT fk_global_rule_decision_source_revision FOREIGN KEY (selected_source_entity_revision_id)
                REFERENCES source_entity_revision(source_entity_revision_id));
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
        """;
}
