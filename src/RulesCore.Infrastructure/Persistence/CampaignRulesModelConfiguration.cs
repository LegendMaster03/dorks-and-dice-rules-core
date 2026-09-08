using Microsoft.EntityFrameworkCore;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Persistence;

internal static class CampaignRulesModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GlobalRuleDecision>(entity =>
        {
            entity.Property(value => value.PatchJson)
                .HasColumnName("patch_json")
                .HasColumnType("jsonb");
            entity.Property(value => value.PatchFingerprint)
                .HasColumnName("patch_fingerprint")
                .HasMaxLength(64);
        });

        modelBuilder.Entity<CampaignRulesetSelection>(entity =>
        {
            entity.ToTable("campaign_ruleset_selection");
            entity.HasKey(value => value.Id).HasName("pk_campaign_ruleset_selection");
            entity.Property(value => value.Id).HasColumnName("campaign_ruleset_selection_id");
            entity.Property(value => value.CampaignId).HasColumnName("campaign_id");
            entity.Property(value => value.SelectionNumber).HasColumnName("selection_number");
            entity.Property(value => value.RulesetRevisionId).HasColumnName("ruleset_revision_id");
            entity.Property(value => value.SelectedByUserId).HasColumnName("selected_by_user_id").HasMaxLength(200);
            entity.Property(value => value.SelectedAt).HasColumnName("selected_at");
            entity.HasIndex(value => new { value.CampaignId, value.SelectionNumber })
                .IsUnique()
                .HasDatabaseName("ux_campaign_ruleset_selection_campaign_number");
            entity.HasIndex(value => value.RulesetRevisionId)
                .HasDatabaseName("ix_campaign_ruleset_selection_ruleset_revision");
            entity.HasOne(value => value.RulesetRevision)
                .WithMany()
                .HasForeignKey(value => value.RulesetRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CampaignRuleDecision>(entity =>
        {
            entity.ToTable("campaign_rule_decision");
            entity.HasKey(value => value.Id).HasName("pk_campaign_rule_decision");
            entity.Property(value => value.Id).HasColumnName("campaign_rule_decision_id");
            entity.Property(value => value.CampaignId).HasColumnName("campaign_id");
            entity.Property(value => value.RuleConceptId).HasColumnName("rule_concept_id");
            entity.Property(value => value.DecisionNumber).HasColumnName("decision_number");
            entity.Property(value => value.DecisionKind).HasColumnName("decision_kind").HasMaxLength(80);
            entity.Property(value => value.SelectedSourceEntityRevisionId)
                .HasColumnName("selected_source_entity_revision_id");
            entity.Property(value => value.PatchJson)
                .HasColumnName("patch_json")
                .HasColumnType("jsonb");
            entity.Property(value => value.PatchFingerprint)
                .HasColumnName("patch_fingerprint")
                .HasMaxLength(64);
            entity.Property(value => value.Note).HasColumnName("note").HasMaxLength(2000);
            entity.Property(value => value.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(200);
            entity.Property(value => value.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(value => new { value.CampaignId, value.RuleConceptId, value.DecisionNumber })
                .IsUnique()
                .HasDatabaseName("ux_campaign_rule_decision_campaign_concept_number");
            entity.HasIndex(value => value.SelectedSourceEntityRevisionId)
                .HasDatabaseName("ix_campaign_rule_decision_source_revision");
            entity.HasOne(value => value.RuleConcept)
                .WithMany()
                .HasForeignKey(value => value.RuleConceptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SelectedSourceEntityRevision)
                .WithMany()
                .HasForeignKey(value => value.SelectedSourceEntityRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CampaignRulesetRevision>(entity =>
        {
            entity.ToTable("campaign_ruleset_revision");
            entity.HasKey(value => value.Id).HasName("pk_campaign_ruleset_revision");
            entity.Property(value => value.Id).HasColumnName("campaign_ruleset_revision_id");
            entity.Property(value => value.CampaignId).HasColumnName("campaign_id");
            entity.Property(value => value.RevisionNumber).HasColumnName("revision_number");
            entity.Property(value => value.BaselineSelectionId).HasColumnName("baseline_selection_id");
            entity.Property(value => value.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
            entity.Property(value => value.PublishedByUserId).HasColumnName("published_by_user_id").HasMaxLength(200);
            entity.Property(value => value.PublishedAt).HasColumnName("published_at");
            entity.HasIndex(value => new { value.CampaignId, value.RevisionNumber })
                .IsUnique()
                .HasDatabaseName("ux_campaign_ruleset_revision_campaign_number");
            entity.HasIndex(value => new { value.CampaignId, value.Fingerprint })
                .HasDatabaseName("ix_campaign_ruleset_revision_campaign_fingerprint");
            entity.HasOne(value => value.BaselineSelection)
                .WithMany(value => value.CampaignRulesetRevisions)
                .HasForeignKey(value => value.BaselineSelectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CampaignRulesetRevisionEntry>(entity =>
        {
            entity.ToTable("campaign_ruleset_revision_entry");
            entity.HasKey(value => value.Id).HasName("pk_campaign_ruleset_revision_entry");
            entity.Property(value => value.Id).HasColumnName("campaign_ruleset_revision_entry_id");
            entity.Property(value => value.CampaignRulesetRevisionId).HasColumnName("campaign_ruleset_revision_id");
            entity.Property(value => value.RuleConceptId).HasColumnName("rule_concept_id");
            entity.Property(value => value.BaselineRulesetRevisionEntryId)
                .HasColumnName("baseline_ruleset_revision_entry_id");
            entity.Property(value => value.CampaignRuleDecisionId).HasColumnName("campaign_rule_decision_id");
            entity.Property(value => value.SourceEntityRevisionId).HasColumnName("source_entity_revision_id");
            entity.HasIndex(value => new { value.CampaignRulesetRevisionId, value.RuleConceptId })
                .IsUnique()
                .HasDatabaseName("ux_campaign_ruleset_revision_entry_revision_concept");
            entity.HasOne(value => value.CampaignRulesetRevision)
                .WithMany(value => value.Entries)
                .HasForeignKey(value => value.CampaignRulesetRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.RuleConcept)
                .WithMany()
                .HasForeignKey(value => value.RuleConceptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.BaselineRulesetRevisionEntry)
                .WithMany()
                .HasForeignKey(value => value.BaselineRulesetRevisionEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.CampaignRuleDecision)
                .WithMany(value => value.CampaignRulesetEntries)
                .HasForeignKey(value => value.CampaignRuleDecisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.SourceEntityRevision)
                .WithMany()
                .HasForeignKey(value => value.SourceEntityRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
