using Microsoft.EntityFrameworkCore;
using RulesCore.Domain.Sources;

namespace RulesCore.Infrastructure.Persistence;

public sealed class RulesCoreDbContext(DbContextOptions<RulesCoreDbContext> options) : DbContext(options)
{
    public DbSet<SourcePackage> SourcePackages => Set<SourcePackage>();
    public DbSet<SourceWork> SourceWorks => Set<SourceWork>();
    public DbSet<SourceEdition> SourceEditions => Set<SourceEdition>();
    public DbSet<SourceEntity> SourceEntities => Set<SourceEntity>();
    public DbSet<SourceEntityRevision> SourceEntityRevisions => Set<SourceEntityRevision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SourcePackage>(entity =>
        {
            entity.ToTable("source_package");
            entity.HasKey(value => value.Id).HasName("pk_source_package");
            entity.Property(value => value.Id).HasColumnName("source_package_id");
            entity.Property(value => value.Key).HasColumnName("package_key").HasMaxLength(200);
            entity.Property(value => value.DisplayName).HasColumnName("display_name").HasMaxLength(300);
            entity.Property(value => value.Provider).HasColumnName("provider").HasMaxLength(200);
            entity.Property(value => value.License).HasColumnName("license").HasMaxLength(300);
            entity.Property(value => value.IsPublic).HasColumnName("is_public");
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(value => value.Key).IsUnique().HasDatabaseName("ux_source_package_key");
        });

        modelBuilder.Entity<SourceWork>(entity =>
        {
            entity.ToTable("source_work");
            entity.HasKey(value => value.Id).HasName("pk_source_work");
            entity.Property(value => value.Id).HasColumnName("source_work_id");
            entity.Property(value => value.SourcePackageId).HasColumnName("source_package_id");
            entity.Property(value => value.Key).HasColumnName("work_key").HasMaxLength(200);
            entity.Property(value => value.DisplayName).HasColumnName("display_name").HasMaxLength(300);
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(value => new { value.SourcePackageId, value.Key })
                .IsUnique()
                .HasDatabaseName("ux_source_work_package_key");
            entity.HasOne(value => value.SourcePackage)
                .WithMany(value => value.Works)
                .HasForeignKey(value => value.SourcePackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceEdition>(entity =>
        {
            entity.ToTable("source_edition");
            entity.HasKey(value => value.Id).HasName("pk_source_edition");
            entity.Property(value => value.Id).HasColumnName("source_edition_id");
            entity.Property(value => value.SourceWorkId).HasColumnName("source_work_id");
            entity.Property(value => value.Key).HasColumnName("edition_key").HasMaxLength(200);
            entity.Property(value => value.DisplayName).HasColumnName("display_name").HasMaxLength(300);
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(value => new { value.SourceWorkId, value.Key })
                .IsUnique()
                .HasDatabaseName("ux_source_edition_work_key");
            entity.HasOne(value => value.SourceWork)
                .WithMany(value => value.Editions)
                .HasForeignKey(value => value.SourceWorkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceEntity>(entity =>
        {
            entity.ToTable("source_entity");
            entity.HasKey(value => value.Id).HasName("pk_source_entity");
            entity.Property(value => value.Id).HasColumnName("source_entity_id");
            entity.Property(value => value.SourceEditionId).HasColumnName("source_edition_id");
            entity.Property(value => value.EntityType).HasColumnName("entity_type").HasMaxLength(120);
            entity.Property(value => value.Name).HasColumnName("entity_name").HasMaxLength(300);
            entity.Property(value => value.SourceCode).HasColumnName("source_code").HasMaxLength(120);
            entity.Property(value => value.NaturalKey).HasColumnName("natural_key").HasMaxLength(800);
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(value => new { value.SourceEditionId, value.NaturalKey })
                .IsUnique()
                .HasDatabaseName("ux_source_entity_edition_natural_key");
            entity.HasOne(value => value.SourceEdition)
                .WithMany(value => value.Entities)
                .HasForeignKey(value => value.SourceEditionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceEntityRevision>(entity =>
        {
            entity.ToTable("source_entity_revision");
            entity.HasKey(value => value.Id).HasName("pk_source_entity_revision");
            entity.Property(value => value.Id).HasColumnName("source_entity_revision_id");
            entity.Property(value => value.SourceEntityId).HasColumnName("source_entity_id");
            entity.Property(value => value.RevisionNumber).HasColumnName("revision_number");
            entity.Property(value => value.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
            entity.Property(value => value.RawJson).HasColumnName("raw_json").HasColumnType("jsonb");
            entity.Property(value => value.ImportedAt).HasColumnName("imported_at");
            entity.HasIndex(value => new { value.SourceEntityId, value.RevisionNumber })
                .IsUnique()
                .HasDatabaseName("ux_source_entity_revision_number");
            entity.HasIndex(value => value.Fingerprint).HasDatabaseName("ix_source_entity_revision_fingerprint");
            entity.HasOne(value => value.SourceEntity)
                .WithMany(value => value.Revisions)
                .HasForeignKey(value => value.SourceEntityId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
