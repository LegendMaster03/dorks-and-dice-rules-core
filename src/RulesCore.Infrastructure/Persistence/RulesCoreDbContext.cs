using Microsoft.EntityFrameworkCore;
using RulesCore.Domain.Rules;
using RulesCore.Domain.Sources;

namespace RulesCore.Infrastructure.Persistence;

public sealed class RulesCoreDbContext(DbContextOptions<RulesCoreDbContext> options) : DbContext(options)
{
    public DbSet<SourcePackage> SourcePackages => Set<SourcePackage>();
    public DbSet<SourceRepresentation> SourceRepresentations => Set<SourceRepresentation>();
    public DbSet<SourceEntity> SourceEntities => Set<SourceEntity>();
    public DbSet<SourceEntityRevision> SourceEntityRevisions => Set<SourceEntityRevision>();
    public DbSet<UserSourceGrant> UserSourceGrants => Set<UserSourceGrant>();
    public DbSet<RuleConcept> RuleConcepts => Set<RuleConcept>();
    public DbSet<RuleConceptSourceBinding> RuleConceptSourceBindings => Set<RuleConceptSourceBinding>();
    public DbSet<GlobalRuleDecision> GlobalRuleDecisions => Set<GlobalRuleDecision>();
    public DbSet<RulesetRevision> RulesetRevisions => Set<RulesetRevision>();
    public DbSet<RulesetRevisionEntry> RulesetRevisionEntries => Set<RulesetRevisionEntry>();
    public DbSet<CampaignRulesetSelection> CampaignRulesetSelections => Set<CampaignRulesetSelection>();
    public DbSet<CampaignRuleDecision> CampaignRuleDecisions => Set<CampaignRuleDecision>();
    public DbSet<CampaignRulesetRevision> CampaignRulesetRevisions => Set<CampaignRulesetRevision>();
    public DbSet<CampaignRulesetRevisionEntry> CampaignRulesetRevisionEntries => Set<CampaignRulesetRevisionEntry>();

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

        modelBuilder.Entity<SourceRepresentation>(entity =>
        {
            entity.ToTable("source_representation");
            entity.HasKey(value => value.Id).HasName("pk_source_representation");
            entity.Property(value => value.Id).HasColumnName("source_representation_id");
            entity.Property(value => value.SourcePackageId).HasColumnName("source_package_id");
            entity.Property(value => value.PreviousSourceRepresentationId).HasColumnName("previous_source_representation_id");
            entity.Property(value => value.FormatKey).HasColumnName("format_key").HasMaxLength(80);
            entity.Property(value => value.OriginIdentity).HasColumnName("origin_identity").HasMaxLength(2000);
            entity.Property(value => value.FileName).HasColumnName("file_name").HasMaxLength(500);
            entity.Property(value => value.SourceUri).HasColumnName("source_uri").HasMaxLength(2000);
            entity.Property(value => value.MediaType).HasColumnName("media_type").HasMaxLength(200);
            entity.Property(value => value.ContentSha256).HasColumnName("content_sha256").HasMaxLength(64);
            entity.Property(value => value.ContentLength).HasColumnName("content_length");
            entity.Property(value => value.ContentBytes).HasColumnName("content_bytes").HasColumnType("bytea");
            entity.Property(value => value.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
            entity.Property(value => value.ImportedAt).HasColumnName("imported_at");
            entity.HasIndex(value => new { value.SourcePackageId, value.OriginIdentity, value.ContentSha256 })
                .IsUnique()
                .HasDatabaseName("ux_source_representation_identity");
            entity.HasIndex(value => new { value.SourcePackageId, value.OriginIdentity, value.ImportedAt })
                .HasDatabaseName("ix_source_representation_origin_history");
            entity.HasOne(value => value.SourcePackage)
                .WithMany(value => value.Representations)
                .HasForeignKey(value => value.SourcePackageId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.PreviousSourceRepresentation)
                .WithMany(value => value.SupersedingRepresentations)
                .HasForeignKey(value => value.PreviousSourceRepresentationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SourceEntity>(entity =>
        {
            entity.ToTable("source_entity");
            entity.HasKey(value => value.Id).HasName("pk_source_entity");
            entity.Property(value => value.Id).HasColumnName("source_entity_id");
            entity.Property(value => value.SourcePackageId).HasColumnName("source_package_id");
            entity.Property(value => value.FormatKey).HasColumnName("format_key").HasMaxLength(80);
            entity.Property(value => value.EntityType).HasColumnName("entity_type").HasMaxLength(120);
            entity.Property(value => value.Name).HasColumnName("entity_name").HasMaxLength(300);
            entity.Property(value => value.SourceCode).HasColumnName("source_code").HasMaxLength(120);
            entity.Property(value => value.NativeKey).HasColumnName("native_key").HasMaxLength(1000);
            entity.Property(value => value.NativeIdentityJson).HasColumnName("native_identity_json").HasColumnType("jsonb");
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(value => new { value.SourcePackageId, value.FormatKey, value.NativeKey })
                .IsUnique()
                .HasDatabaseName("ux_source_entity_package_native_key");
            entity.HasOne(value => value.SourcePackage)
                .WithMany(value => value.Entities)
                .HasForeignKey(value => value.SourcePackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceEntityRevision>(entity =>
        {
            entity.ToTable("source_entity_revision");
            entity.HasKey(value => value.Id).HasName("pk_source_entity_revision");
            entity.Property(value => value.Id).HasColumnName("source_entity_revision_id");
            entity.Property(value => value.SourceEntityId).HasColumnName("source_entity_id");
            entity.Property(value => value.SourceRepresentationId).HasColumnName("source_representation_id");
            entity.Property(value => value.RevisionNumber).HasColumnName("revision_number");
            entity.Property(value => value.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
            entity.Property(value => value.RawJson).HasColumnName("raw_json").HasColumnType("jsonb");
            entity.Property(value => value.LocatorKey).HasColumnName("locator_key").HasMaxLength(500);
            entity.Property(value => value.ImportedAt).HasColumnName("imported_at");
            entity.HasIndex(value => new { value.SourceEntityId, value.RevisionNumber })
                .IsUnique()
                .HasDatabaseName("ux_source_entity_revision_number");
            entity.HasIndex(value => value.Fingerprint).HasDatabaseName("ix_source_entity_revision_fingerprint");
            entity.HasIndex(value => value.SourceRepresentationId).HasDatabaseName("ix_source_entity_revision_representation");
            entity.HasOne(value => value.SourceEntity)
                .WithMany(value => value.Revisions)
                .HasForeignKey(value => value.SourceEntityId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.SourceRepresentation)
                .WithMany(value => value.EntityRevisions)
                .HasForeignKey(value => value.SourceRepresentationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSourceGrant>(entity =>
        {
            entity.ToTable("user_source_grant");
            entity.HasKey(value => value.Id).HasName("pk_user_source_grant");
            entity.Property(value => value.Id).HasColumnName("user_source_grant_id");
            entity.Property(value => value.SourcePackageId).HasColumnName("source_package_id");
            entity.Property(value => value.UserId).HasColumnName("user_id").HasMaxLength(200);
            entity.Property(value => value.GrantedAt).HasColumnName("granted_at");
            entity.HasIndex(value => new { value.UserId, value.SourcePackageId })
                .IsUnique()
                .HasDatabaseName("ux_user_source_grant_user_package");
            entity.HasIndex(value => value.SourcePackageId)
                .HasDatabaseName("ix_user_source_grant_package");
            entity.HasOne(value => value.SourcePackage)
                .WithMany(value => value.UserGrants)
                .HasForeignKey(value => value.SourcePackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RuleConcept>(entity =>
        {
            entity.ToTable("rule_concept");
            entity.HasKey(value => value.Id).HasName("pk_rule_concept");
            entity.Property(value => value.Id).HasColumnName("rule_concept_id");
            entity.Property(value => value.Key).HasColumnName("concept_key").HasMaxLength(300);
            entity.Property(value => value.EntityType).HasColumnName("entity_type").HasMaxLength(120);
            entity.Property(value => value.DisplayName).HasColumnName("display_name").HasMaxLength(300);
            entity.Property(value => value.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(200);
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(value => value.Key).IsUnique().HasDatabaseName("ux_rule_concept_key");
        });

        modelBuilder.Entity<RuleConceptSourceBinding>(entity =>
        {
            entity.ToTable("rule_concept_source_binding");
            entity.HasKey(value => value.Id).HasName("pk_rule_concept_source_binding");
            entity.Property(value => value.Id).HasColumnName("rule_concept_source_binding_id");
            entity.Property(value => value.RuleConceptId).HasColumnName("rule_concept_id");
            entity.Property(value => value.CanonicalEntityId).HasColumnName("canonical_entity_id");
            entity.Property(value => value.SourceEntityId).HasColumnName("source_entity_id");
            entity.Property(value => value.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(200);
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(value => new { value.RuleConceptId, value.CanonicalEntityId })
                .IsUnique()
                .HasDatabaseName("ux_rule_concept_source_binding_concept_canonical_entity");
            entity.HasOne(value => value.RuleConcept)
                .WithMany(value => value.SourceBindings)
                .HasForeignKey(value => value.RuleConceptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.SourceEntity)
                .WithMany()
                .HasForeignKey(value => value.SourceEntityId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GlobalRuleDecision>(entity =>
        {
            entity.ToTable("global_rule_decision");
            entity.HasKey(value => value.Id).HasName("pk_global_rule_decision");
            entity.Property(value => value.Id).HasColumnName("global_rule_decision_id");
            entity.Property(value => value.RuleConceptId).HasColumnName("rule_concept_id");
            entity.Property(value => value.DecisionNumber).HasColumnName("decision_number");
            entity.Property(value => value.DecisionKind).HasColumnName("decision_kind").HasMaxLength(80);
            entity.Property(value => value.SelectedSourceEntityRevisionId)
                .HasColumnName("selected_source_entity_revision_id");
            entity.Property(value => value.Note).HasColumnName("note").HasMaxLength(2000);
            entity.Property(value => value.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(200);
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(value => new { value.RuleConceptId, value.DecisionNumber })
                .IsUnique()
                .HasDatabaseName("ux_global_rule_decision_concept_number");
            entity.HasIndex(value => value.SelectedSourceEntityRevisionId)
                .HasDatabaseName("ix_global_rule_decision_source_revision");
            entity.HasOne(value => value.RuleConcept)
                .WithMany(value => value.GlobalDecisions)
                .HasForeignKey(value => value.RuleConceptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.SelectedSourceEntityRevision)
                .WithMany()
                .HasForeignKey(value => value.SelectedSourceEntityRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RulesetRevision>(entity =>
        {
            entity.ToTable("ruleset_revision");
            entity.HasKey(value => value.Id).HasName("pk_ruleset_revision");
            entity.Property(value => value.Id).HasColumnName("ruleset_revision_id");
            entity.Property(value => value.RevisionNumber).HasColumnName("revision_number");
            entity.Property(value => value.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
            entity.Property(value => value.PublishedByUserId).HasColumnName("published_by_user_id").HasMaxLength(200);
            entity.Property(value => value.PublishedAt).HasColumnName("published_at");
            entity.HasIndex(value => value.RevisionNumber)
                .IsUnique()
                .HasDatabaseName("ux_ruleset_revision_number");
            entity.HasIndex(value => value.Fingerprint)
                .HasDatabaseName("ix_ruleset_revision_fingerprint");
        });

        modelBuilder.Entity<RulesetRevisionEntry>(entity =>
        {
            entity.ToTable("ruleset_revision_entry");
            entity.HasKey(value => value.Id).HasName("pk_ruleset_revision_entry");
            entity.Property(value => value.Id).HasColumnName("ruleset_revision_entry_id");
            entity.Property(value => value.RulesetRevisionId).HasColumnName("ruleset_revision_id");
            entity.Property(value => value.RuleConceptId).HasColumnName("rule_concept_id");
            entity.Property(value => value.GlobalRuleDecisionId).HasColumnName("global_rule_decision_id");
            entity.Property(value => value.SourceEntityRevisionId).HasColumnName("source_entity_revision_id");
            entity.HasIndex(value => new { value.RulesetRevisionId, value.RuleConceptId })
                .IsUnique()
                .HasDatabaseName("ux_ruleset_revision_entry_revision_concept");
            entity.HasOne(value => value.RulesetRevision)
                .WithMany(value => value.Entries)
                .HasForeignKey(value => value.RulesetRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.RuleConcept)
                .WithMany(value => value.RulesetEntries)
                .HasForeignKey(value => value.RuleConceptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.GlobalRuleDecision)
                .WithMany(value => value.RulesetEntries)
                .HasForeignKey(value => value.GlobalRuleDecisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SourceEntityRevision)
                .WithMany()
                .HasForeignKey(value => value.SourceEntityRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        CampaignRulesModelConfiguration.Configure(modelBuilder);
    }
}
