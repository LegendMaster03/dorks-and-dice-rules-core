using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class CampaignRuleBaselineService(RulesCoreDbContext dbContext)
{
    public async Task<CampaignRuleBaselineView?> ResolveAsync(
        Guid campaignId,
        string conceptKey,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        }
        if (string.IsNullOrWhiteSpace(conceptKey))
        {
            throw new ArgumentException("Rule concept key can not be blank.", nameof(conceptKey));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }

        var normalizedKey = conceptKey.Trim().ToLowerInvariant();
        var latestCampaignRevision = await dbContext.CampaignRulesetRevisions
            .AsNoTracking()
            .Include(value => value.BaselineSelection)
                .ThenInclude(value => value.RulesetRevision)
            .Where(value => value.CampaignId == campaignId)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestCampaignRevision is null)
        {
            return null;
        }

        var baselineRevision = latestCampaignRevision.BaselineSelection.RulesetRevision;
        var entry = await dbContext.RulesetRevisionEntries
            .AsNoTracking()
            .Include(value => value.RuleConcept)
            .Include(value => value.GlobalRuleDecision)
                .ThenInclude(value => value.SelectedSourceEntityRevision)
                .ThenInclude(value => value.SourceEntity)
                .ThenInclude(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .SingleOrDefaultAsync(
                value => value.RulesetRevisionId == baselineRevision.Id
                    && value.RuleConcept.Key == normalizedKey,
                cancellationToken);
        if (entry is null)
        {
            return null;
        }

        var decision = entry.GlobalRuleDecision;
        var revision = decision.SelectedSourceEntityRevision;
        var source = revision.SourceEntity;
        var edition = source.SourceEdition;
        var work = edition.SourceWork;
        var package = work.SourcePackage;
        if (!package.IsPublic && !package.UserGrants.Any(grant => grant.UserId == userId))
        {
            return null;
        }

        var contributionResolution = await RuleContributionResolution.ResolveAsync(
            dbContext,
            decision.Id,
            userId,
            cancellationToken);
        if (!contributionResolution.Accessible)
        {
            return null;
        }

        using var sourceDocument = JsonDocument.Parse(revision.RawJson);
        var document = decision.DecisionKind switch
        {
            RuleDecisionKinds.JsonMergePatch => JsonMergePatch.Apply(sourceDocument.RootElement, decision.PatchJson),
            RuleDecisionKinds.JsonRulePatch => JsonRulePatch.Apply(sourceDocument.RootElement, decision.PatchJson),
            _ => sourceDocument.RootElement.Clone()
        };

        var concept = entry.RuleConcept;
        return new CampaignRuleBaselineView(
            campaignId,
            concept.Id,
            concept.Key,
            concept.EntityType,
            concept.DisplayName,
            baselineRevision.RevisionNumber,
            baselineRevision.Fingerprint,
            decision.Id,
            decision.DecisionNumber,
            decision.DecisionKind,
            decision.Note,
            source.Id,
            revision.Id,
            revision.RevisionNumber,
            source.Name,
            source.SourceCode,
            package.DisplayName,
            edition.DisplayName,
            contributionResolution.Contributions,
            document);
    }
}
