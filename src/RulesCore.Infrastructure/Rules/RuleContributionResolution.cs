using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

internal sealed record RuleContributionResolutionResult(
    bool Accessible,
    IReadOnlyList<ResolvedRuleContributionView> Contributions);

internal static class RuleContributionResolution
{
    public static async Task<RuleContributionResolutionResult> ResolveAsync(
        RulesCoreDbContext dbContext,
        Guid globalRuleDecisionId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);
        var stored = await SourceFrameworkStore.GetDecisionContributionsAsync(
            dbContext,
            globalRuleDecisionId,
            cancellationToken);
        if (stored.Count == 0)
        {
            return new RuleContributionResolutionResult(true, []);
        }

        var revisionIds = stored
            .Select(value => value.SourceEntityRevisionId)
            .Distinct()
            .ToArray();
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => revisionIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (revisions.Length != revisionIds.Length)
        {
            throw new InvalidOperationException(
                "A published rule references a consolidation contribution revision that no longer exists.");
        }

        var byId = revisions.ToDictionary(value => value.Id);
        var views = new List<ResolvedRuleContributionView>(stored.Count);
        foreach (var contribution in stored)
        {
            var revision = byId[contribution.SourceEntityRevisionId];
            var source = revision.SourceEntity;
            var edition = source.SourceEdition;
            var work = edition.SourceWork;
            var package = work.SourcePackage;
            if (!package.IsPublic
                && (userId is null || !package.UserGrants.Any(grant => grant.UserId == userId)))
            {
                return new RuleContributionResolutionResult(false, []);
            }

            var metadata = await SourceFrameworkStore.GetEditionMetadataAsync(
                dbContext,
                edition.Id,
                cancellationToken);
            views.Add(new ResolvedRuleContributionView(
                revision.Id,
                revision.RevisionNumber,
                revision.Fingerprint,
                source.Id,
                source.Name,
                source.SourceCode,
                package.Key,
                package.DisplayName,
                work.Key,
                work.DisplayName,
                edition.Key,
                edition.DisplayName,
                metadata?.GameEdition,
                contribution.ContributionKind,
                contribution.Note));
        }

        return new RuleContributionResolutionResult(true, views);
    }
}
