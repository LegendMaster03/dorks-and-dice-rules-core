using Microsoft.EntityFrameworkCore;
using RulesCore.Domain.Rules;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Selects one temporary, accessible source revision when Rules Core does not yet have a
/// published effective ruling. Selection is deterministic and never writes a rule decision.
/// </summary>
internal sealed class EffectiveRuleFallbackResolver(RulesCoreDbContext dbContext)
{
    internal async Task<EffectiveRuleFallbackCandidate?> ResolveAsync(
        string conceptKey,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var concept = await dbContext.RuleConcepts
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Key == conceptKey, cancellationToken);
        return concept is null
            ? null
            : await ResolveAsync(concept, userId, cancellationToken);
    }

    internal async Task<EffectiveRuleFallbackCandidate?> ResolveAsync(
        RuleConcept concept,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var sourceIds = await CanonicalRuleBindingStore.GetAccessibleSourceEntityIdsForConceptAsync(
            dbContext,
            concept.Id,
            userId,
            cancellationToken);
        if (sourceIds.Count == 0)
        {
            return null;
        }

        var ignoredPackageIds = (await new GlobalSourceDispositionService(dbContext)
                .GetIgnoredPackageIdsAsync(cancellationToken))
            .ToHashSet();

        var sources = await dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.SourcePackage)
            .Include(value => value.Revisions)
            .Where(value => sourceIds.Contains(value.Id)
                && !ignoredPackageIds.Contains(value.SourcePackageId))
            .ToArrayAsync(cancellationToken);

        var candidates = new List<EffectiveRuleFallbackCandidate>();
        foreach (var source in sources)
        {
            var revision = source.Revisions
                .OrderByDescending(value => value.RevisionNumber)
                .ThenByDescending(value => value.ImportedAt)
                .FirstOrDefault();
            if (revision is null)
            {
                continue;
            }

            var publication = await CanonicalPublicationMetadataReader.ReadAsync(
                dbContext,
                source.Id,
                cancellationToken);
            candidates.Add(new EffectiveRuleFallbackCandidate(
                concept,
                source,
                revision,
                publication));
        }

        return candidates
            .OrderByDescending(value => value.Publication?.PublicationDate ?? DateOnly.MinValue)
            .ThenByDescending(value => EditionSortKey(value.Publication?.GameEdition))
            .ThenByDescending(value => value.Revision.RevisionNumber)
            .ThenByDescending(value => value.Revision.ImportedAt)
            .ThenBy(value => value.Source.SourceCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Source.Id)
            .FirstOrDefault();
    }

    private static int EditionSortKey(string? gameEdition) =>
        gameEdition?.Trim().ToLowerInvariant() switch
        {
            "3e" or "3.0e" => 300,
            "3.5e" => 350,
            "5e" => 500,
            "5.5e" => 550,
            _ => 0
        };
}

internal sealed record EffectiveRuleFallbackCandidate(
    RuleConcept Concept,
    SourceEntity Source,
    SourceEntityRevision Revision,
    CanonicalPublicationMetadata? Publication);
