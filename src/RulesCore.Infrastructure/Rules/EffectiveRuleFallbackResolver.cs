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
        var resolved = await ResolveManyAsync([concept], userId, cancellationToken);
        return resolved.GetValueOrDefault(concept.Id);
    }

    internal async Task<IReadOnlyDictionary<Guid, EffectiveRuleFallbackCandidate>> ResolveManyAsync(
        IReadOnlyCollection<RuleConcept> concepts,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var requested = concepts
            .Where(value => value.Id != Guid.Empty)
            .DistinctBy(value => value.Id)
            .ToArray();
        if (requested.Length == 0)
        {
            return new Dictionary<Guid, EffectiveRuleFallbackCandidate>();
        }

        var sourceIdsByConcept = await CanonicalRuleFallbackSourceReader
            .GetAccessibleSourceEntityIdsByConceptAsync(
                dbContext,
                requested.Select(value => value.Id).ToArray(),
                userId,
                cancellationToken);
        if (sourceIdsByConcept.Count == 0)
        {
            return new Dictionary<Guid, EffectiveRuleFallbackCandidate>();
        }

        var ignoredPackageIds = (await new GlobalSourceDispositionService(dbContext)
                .GetIgnoredPackageIdsAsync(cancellationToken))
            .ToHashSet();
        var sourceIds = sourceIdsByConcept.Values
            .SelectMany(value => value)
            .Distinct()
            .ToArray();
        var sources = await dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.SourcePackage)
            .Where(value => sourceIds.Contains(value.Id)
                && !ignoredPackageIds.Contains(value.SourcePackageId))
            .ToArrayAsync(cancellationToken);
        var sourcesById = sources.ToDictionary(value => value.Id);

        var eligibleSourceIds = sourcesById.Keys.ToArray();
        var latestRevisionNumbers = dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => eligibleSourceIds.Contains(value.SourceEntityId))
            .GroupBy(value => value.SourceEntityId)
            .Select(group => new
            {
                SourceEntityId = group.Key,
                LatestRevisionNumber = group.Max(value => value.RevisionNumber)
            });
        var latestRevisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Join(
                latestRevisionNumbers,
                revision => new { revision.SourceEntityId, revision.RevisionNumber },
                latest => new
                {
                    latest.SourceEntityId,
                    RevisionNumber = latest.LatestRevisionNumber
                },
                (revision, _) => revision)
            .ToArrayAsync(cancellationToken);
        var latestRevisionBySourceId = latestRevisions.ToDictionary(value => value.SourceEntityId);

        var publications = await CanonicalPublicationMetadataBatchReader.ReadAsync(
            dbContext,
            eligibleSourceIds,
            cancellationToken);
        var conceptsById = requested.ToDictionary(value => value.Id);
        var result = new Dictionary<Guid, EffectiveRuleFallbackCandidate>();

        foreach (var (conceptId, conceptSourceIds) in sourceIdsByConcept)
        {
            if (!conceptsById.TryGetValue(conceptId, out var concept))
            {
                continue;
            }

            var candidates = new List<EffectiveRuleFallbackCandidate>();
            foreach (var sourceId in conceptSourceIds)
            {
                if (!sourcesById.TryGetValue(sourceId, out var source)
                    || !latestRevisionBySourceId.TryGetValue(sourceId, out var revision))
                {
                    continue;
                }

                publications.TryGetValue(source.Id, out var publication);
                candidates.Add(new EffectiveRuleFallbackCandidate(
                    concept,
                    source,
                    revision,
                    publication));
            }

            var selected = candidates
                .OrderByDescending(value => value.Publication?.PublicationDate ?? DateOnly.MinValue)
                .ThenByDescending(value => EditionSortKey(value.Publication?.GameEdition))
                .ThenByDescending(value => value.Revision.RevisionNumber)
                .ThenByDescending(value => value.Revision.ImportedAt)
                .ThenBy(value => value.Source.SourceCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.Source.Id)
                .FirstOrDefault();
            if (selected is not null)
            {
                result[conceptId] = selected;
            }
        }

        return result;
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
