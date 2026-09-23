using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Materializes the effective rule documents for a published global or campaign scope.
/// </summary>
internal static class CharacterSupportEffectiveDocumentReader
{
    internal static async Task<IReadOnlyDictionary<Guid, JsonElement>> ReadAsync(
        RulesCoreDbContext dbContext,
        ResolvedRulesCatalogView rules,
        CancellationToken cancellationToken)
    {
        var conceptIds = rules.Rules
            .Select(value => value.RuleConceptId)
            .ToArray();
        if (conceptIds.Length == 0 || rules.RevisionNumber is null)
        {
            return new Dictionary<Guid, JsonElement>();
        }
    
        if (string.Equals(rules.Scope, "global", StringComparison.OrdinalIgnoreCase))
        {
            var revisionId = await dbContext.RulesetRevisions
                .AsNoTracking()
                .Where(value => value.RevisionNumber == rules.RevisionNumber.Value)
                .Select(value => value.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (revisionId == Guid.Empty)
            {
                return new Dictionary<Guid, JsonElement>();
            }
    
            var entries = await dbContext.RulesetRevisionEntries
                .AsNoTracking()
                .Include(value => value.SourceEntityRevision)
                .Include(value => value.GlobalRuleDecision)
                .Where(value => value.RulesetRevisionId == revisionId
                    && conceptIds.Contains(value.RuleConceptId))
                .ToArrayAsync(cancellationToken);
    
            return entries.ToDictionary(
                value => value.RuleConceptId,
                value =>
                {
                    using var source = JsonDocument.Parse(
                        value.SourceEntityRevision.GetMechanicalContentJson());
                    return ApplyGlobalDecision(
                        source.RootElement,
                        value.GlobalRuleDecision);
                });
        }
    
        if (!string.Equals(rules.Scope, "campaign", StringComparison.OrdinalIgnoreCase)
            || rules.CampaignId is null)
        {
            throw new InvalidOperationException(
                $"Unsupported Character support rules scope '{rules.Scope}'.");
        }
    
        var campaignRevisionId = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Where(value => value.CampaignId == rules.CampaignId.Value
                && value.RevisionNumber == rules.RevisionNumber.Value)
            .Select(value => value.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (campaignRevisionId == Guid.Empty)
        {
            return new Dictionary<Guid, JsonElement>();
        }
    
        var campaignEntries = await dbContext.CampaignRulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.SourceEntityRevision)
            .Include(value => value.CampaignRuleDecision)
            .Include(value => value.BaselineRulesetRevisionEntry)
                .ThenInclude(value => value.GlobalRuleDecision)
            .Where(value => value.CampaignRulesetRevisionId == campaignRevisionId
                && conceptIds.Contains(value.RuleConceptId))
            .ToArrayAsync(cancellationToken);
    
        return campaignEntries.ToDictionary(
            value => value.RuleConceptId,
            value =>
            {
                using var source = JsonDocument.Parse(
                    value.SourceEntityRevision.GetMechanicalContentJson());
                var resolved = source.RootElement.Clone();
                if (value.CampaignRuleDecision?.DecisionKind
                    != CampaignRuleDecisionKinds.SelectSource)
                {
                    resolved = ApplyGlobalDecision(
                        resolved,
                        value.BaselineRulesetRevisionEntry.GlobalRuleDecision);
                }
                if (value.CampaignRuleDecision is not null)
                {
                    resolved = ApplyCampaignDecision(
                        resolved,
                        value.CampaignRuleDecision);
                }
                return resolved;
            });
    }
    
    private static JsonElement ApplyGlobalDecision(
        JsonElement source,
        GlobalRuleDecision decision) => decision.DecisionKind switch
    {
        RuleDecisionKinds.JsonMergePatch =>
            JsonMergePatch.Apply(source, decision.PatchJson),
        RuleDecisionKinds.JsonRulePatch =>
            JsonRulePatch.Apply(source, decision.PatchJson),
        _ => source.Clone()
    };
    
    private static JsonElement ApplyCampaignDecision(
        JsonElement source,
        CampaignRuleDecision decision) => decision.DecisionKind switch
    {
        CampaignRuleDecisionKinds.JsonMergePatch =>
            JsonMergePatch.Apply(source, decision.PatchJson),
        CampaignRuleDecisionKinds.JsonRulePatch =>
            JsonRulePatch.Apply(source, decision.PatchJson),
        _ => source.Clone()
    };
    
}
