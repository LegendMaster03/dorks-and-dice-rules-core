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
        """;
}
