using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Bootstrap;

internal sealed record ReviewedBundledCharacterBuildBaselineSyncResult(
    int EligibleSourceEntityCount,
    int ConceptCount,
    int CreatedConceptCount,
    int CreatedBindingCount,
    int CreatedDecisionCount,
    IReadOnlyList<string> Conflicts);

/// <summary>
/// Publishes a deliberately narrow Character-building baseline from the exact reviewed bundled SRD
/// representations. The policy prefers the reviewed SRD 5.2.1 representation for a concept and
/// falls back to SRD 5.1 only when that concept is absent from 5.2.1 (notably Subspecies).
/// This is a bootstrap policy only; it is not a general edition-precedence rule and never applies
/// to arbitrary imports or later Rules Lawyer decisions.
/// </summary>
internal sealed class ReviewedBundledCharacterBuildBaselineSynchronizer(
    RulesCoreDbContext dbContext,
    IGlobalRulesService globalRules)
{
    private static readonly HashSet<string> EligibleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "race",
        "species",
        "subrace",
        "subspecies",
        "class",
        "subclass",
        "background",
        "feat",
        "item",
        "spell"
    };

    public async Task<ReviewedBundledCharacterBuildBaselineSyncResult> SynchronizeAsync(
        CancellationToken cancellationToken = default)
    {
        var reviewed = await BundledSrdSnapshots.GetReviewedRepresentationsAsync(
            dbContext,
            cancellationToken);
        var reviewedById = reviewed.ToDictionary(value => value.RepresentationId);
        var eligibleRepresentationIds = reviewed
            .Where(value => value.WorkKey is "srd-5-1" or "srd-5-2-1")
            .Select(value => value.RepresentationId)
            .ToArray();

        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
            .Where(value => eligibleRepresentationIds.Contains(value.SourceRepresentationId))
            .ToArrayAsync(cancellationToken);

        var candidates = revisions
            .Where(value => EligibleTypes.Contains(value.SourceEntity.EntityType))
            .GroupBy(value => value.SourceEntityId)
            .Select(group => group
                .OrderByDescending(value => value.RevisionNumber)
                .ThenByDescending(value => value.ImportedAt)
                .First())
            .Select(value => new Candidate(
                value.SourceEntity,
                value,
                SourceNormalizationService.BuildSuggestedConceptKey(
                    value.SourceEntity.EntityType,
                    value.SourceEntity.Name,
                    value.SourceEntity.NativeIdentityJson),
                reviewedById[value.SourceRepresentationId].WorkKey))
            .ToArray();

        var selected = candidates
            .GroupBy(value => value.ConceptKey, StringComparer.Ordinal)
            .SelectMany(group =>
            {
                var current = group
                    .Where(value => string.Equals(value.WorkKey, "srd-5-2-1", StringComparison.Ordinal))
                    .ToArray();
                return current.Length > 0
                    ? current
                    : group.Where(value => string.Equals(value.WorkKey, "srd-5-1", StringComparison.Ordinal)).ToArray();
            })
            .OrderBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ThenBy(value => value.SourceEntity.SourceCode, StringComparer.Ordinal)
            .ThenBy(value => value.SourceEntity.NativeKey, StringComparer.Ordinal)
            .ToArray();

        var canonicalIds = await CanonicalRuleBindingStore.GetCanonicalEntityIdsAsync(
            dbContext,
            selected.Select(value => value.SourceEntity.Id).ToArray(),
            cancellationToken);
        var createdConceptCount = 0;
        var createdBindingCount = 0;
        var createdDecisionCount = 0;
        var conceptCount = 0;
        var conflicts = new List<string>();

        foreach (var group in selected.GroupBy(value => value.ConceptKey, StringComparer.Ordinal))
        {
            var grouped = group.ToArray();
            var expectedType = RuleConceptEntityTypes.Normalize(grouped[0].SourceEntity.EntityType);
            var expectedDisplayName = grouped[0].SourceEntity.Name.Trim();
            if (grouped.Any(value =>
                    !string.Equals(
                        RuleConceptEntityTypes.Normalize(value.SourceEntity.EntityType),
                        expectedType,
                        StringComparison.Ordinal)
                    || !string.Equals(value.SourceEntity.Name.Trim(), expectedDisplayName, StringComparison.Ordinal)))
            {
                conflicts.Add($"{group.Key}: reviewed Character-building records collide on immutable metadata.");
                continue;
            }

            var groupCanonicalIds = grouped
                .Select(value => canonicalIds.TryGetValue(value.SourceEntity.Id, out var canonicalId)
                    ? canonicalId
                    : Guid.Empty)
                .ToArray();
            if (groupCanonicalIds.Any(value => value == Guid.Empty))
            {
                conflicts.Add($"{group.Key}: at least one reviewed source entity has no canonical identity.");
                continue;
            }

            var existingBindings = await dbContext.RuleConceptSourceBindings
                .AsNoTracking()
                .Include(value => value.RuleConcept)
                .Where(value => groupCanonicalIds.Contains(value.CanonicalEntityId))
                .ToArrayAsync(cancellationToken);
            var boundConcepts = existingBindings
                .Select(value => value.RuleConcept)
                .DistinctBy(value => value.Id)
                .ToArray();
            var compatibleExisting = boundConcepts
                .Where(value => string.Equals(
                    RuleConceptEntityTypes.Normalize(value.EntityType),
                    expectedType,
                    StringComparison.Ordinal))
                .ToArray();
            var incompatibleExisting = boundConcepts
                .Where(value => !compatibleExisting.Any(candidate => candidate.Id == value.Id))
                .ToArray();
            if (incompatibleExisting.Length > 0 || compatibleExisting.Length > 1)
            {
                conflicts.Add(
                    $"{group.Key}: reviewed canonical identity is already bound to incompatible or ambiguous concept(s): "
                    + string.Join(", ", boundConcepts.Select(value => value.Key).OrderBy(value => value, StringComparer.Ordinal))
                    + ".");
                continue;
            }

            RuleConceptView concept;
            if (compatibleExisting.Length == 1)
            {
                var existing = compatibleExisting[0];
                concept = new RuleConceptView(
                    existing.Id,
                    existing.Key,
                    RuleConceptEntityTypes.Normalize(existing.EntityType),
                    existing.DisplayName,
                    existing.CreatedByUserId,
                    existing.CreatedAt);
            }
            else
            {
                var mutation = await globalRules.CreateConceptAsync(
                    new CreateRuleConceptRequest(group.Key, expectedType, expectedDisplayName),
                    RulesCoreBaselineCatalog.BootstrapActor,
                    cancellationToken);
                concept = mutation.Value;
                if (mutation.Created)
                {
                    createdConceptCount++;
                }
            }
            conceptCount++;

            foreach (var candidate in grouped)
            {
                var canonicalId = canonicalIds[candidate.SourceEntity.Id];
                if (await dbContext.RuleConceptSourceBindings
                    .AsNoTracking()
                    .AnyAsync(value => value.RuleConceptId == concept.Id
                        && value.CanonicalEntityId == canonicalId, cancellationToken))
                {
                    continue;
                }

                var binding = await globalRules.BindSourceEntityAsync(
                    concept.Id,
                    new BindRuleConceptSourceRequest(candidate.SourceEntity.Id),
                    RulesCoreBaselineCatalog.BootstrapActor,
                    cancellationToken);
                if (binding.Created)
                {
                    createdBindingCount++;
                }
            }

            if (await dbContext.GlobalRuleDecisions
                .AsNoTracking()
                .AnyAsync(value => value.RuleConceptId == concept.Id, cancellationToken))
            {
                continue;
            }

            if (grouped.Length == 1)
            {
                var initial = await RuleAutoResolutionService.TryCreateInitialBaselineDecisionAsync(
                    dbContext,
                    globalRules,
                    concept.Id,
                    new SetGlobalRuleDecisionRequest(
                        grouped[0].Revision.Id,
                        $"Built-in reviewed Character-building baseline from {grouped[0].WorkKey}."),
                    RulesCoreBaselineCatalog.BootstrapActor,
                    cancellationToken);
                if (initial.Created)
                {
                    createdDecisionCount++;
                }
                continue;
            }

            var automatic = await RuleAutoResolutionService.TryResolveReviewedBaselineAsync(
                dbContext,
                globalRules,
                concept.Id,
                RulesCoreBaselineCatalog.BootstrapActor,
                grouped.Select(value => value.Revision.Id).Distinct().ToArray(),
                cancellationToken);
            if (automatic.Applied)
            {
                createdDecisionCount++;
            }
            else if (automatic.DecisionId is null)
            {
                conflicts.Add(
                    $"{group.Key}: reviewed Character-building baseline requires Rules Lawyer adjudication. {automatic.Reason}");
            }
        }

        await SpeciesRelationshipSynchronizer.SynchronizeAsync(
            dbContext,
            RulesCoreBaselineCatalog.BootstrapActor,
            cancellationToken);

        return new ReviewedBundledCharacterBuildBaselineSyncResult(
            selected.Length,
            conceptCount,
            createdConceptCount,
            createdBindingCount,
            createdDecisionCount,
            conflicts.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private sealed record Candidate(
        SourceEntity SourceEntity,
        SourceEntityRevision Revision,
        string ConceptKey,
        string WorkKey);
}
