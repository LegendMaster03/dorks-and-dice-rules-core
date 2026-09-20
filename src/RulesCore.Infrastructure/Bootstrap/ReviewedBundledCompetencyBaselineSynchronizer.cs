using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Infrastructure.Bootstrap;

internal sealed record ReviewedBundledCompetencyBaselineSyncResult(
    int EligibleSourceEntityCount,
    int ConceptCount,
    int CreatedConceptCount,
    int CreatedBindingCount,
    int CreatedDecisionCount,
    IReadOnlyList<string> Conflicts);

/// <summary>
/// Publishes only competency identities represented by the exact checked-in reviewed SRD
/// snapshots. This is deliberately narrower than ordinary Source Layer normalization:
/// arbitrary imported packages remain subject to the normal Rules Lawyer publication workflow.
/// </summary>
internal sealed class ReviewedBundledCompetencyBaselineSynchronizer(
    RulesCoreDbContext dbContext,
    IGlobalRulesService globalRules)
{
    public async Task<ReviewedBundledCompetencyBaselineSyncResult> SynchronizeAsync(
        CancellationToken cancellationToken = default)
    {
        var reviewedRepresentations = await BundledSrdSnapshots.GetReviewedRepresentationsAsync(
            dbContext,
            cancellationToken);
        var representationIds = reviewedRepresentations
            .Select(value => value.RepresentationId)
            .ToArray();

        var reviewedRevisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
            .Where(value => representationIds.Contains(value.SourceRepresentationId)
                && (value.SourceEntity.EntityType == "skill"
                    || value.SourceEntity.EntityType == "tool"))
            .ToArrayAsync(cancellationToken);

        var candidates = reviewedRevisions
            .GroupBy(value => value.SourceEntityId)
            .Select(group => group
                .OrderByDescending(value => value.RevisionNumber)
                .ThenByDescending(value => value.ImportedAt)
                .First())
            .Select(value => new ReviewedCompetencyCandidate(
                value.SourceEntity,
                value,
                SourceNormalizationService.BuildSuggestedConceptKey(
                    value.SourceEntity.EntityType,
                    value.SourceEntity.Name,
                    value.SourceEntity.NativeIdentityJson)))
            .OrderBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ThenBy(value => value.SourceEntity.SourceCode, StringComparer.Ordinal)
            .ThenBy(value => value.SourceEntity.NativeKey, StringComparer.Ordinal)
            .ToArray();

        var canonicalIds = await CanonicalRuleBindingStore.GetCanonicalEntityIdsAsync(
            dbContext,
            candidates.Select(value => value.SourceEntity.Id).ToArray(),
            cancellationToken);
        var createdConceptCount = 0;
        var createdBindingCount = 0;
        var createdDecisionCount = 0;
        var conceptCount = 0;
        var conflicts = new List<string>();

        foreach (var group in candidates.GroupBy(value => value.ConceptKey, StringComparer.Ordinal))
        {
            var grouped = group.ToArray();
            var expectedType = RuleConceptEntityTypes.Normalize(grouped[0].SourceEntity.EntityType);
            var expectedDisplayName = grouped[0].SourceEntity.Name.Trim();
            if (grouped.Any(value =>
                    !string.Equals(
                        RuleConceptEntityTypes.Normalize(value.SourceEntity.EntityType),
                        expectedType,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        value.SourceEntity.Name.Trim(),
                        expectedDisplayName,
                        StringComparison.Ordinal)))
            {
                conflicts.Add(
                    $"{group.Key}: reviewed normalized records collide on one concept key with different immutable metadata.");
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

            var existingCanonicalBindings = await dbContext.RuleConceptSourceBindings
                .AsNoTracking()
                .Include(value => value.RuleConcept)
                .Where(value => groupCanonicalIds.Contains(value.CanonicalEntityId))
                .ToArrayAsync(cancellationToken);
            var conflictingBindings = existingCanonicalBindings
                .Where(value => !string.Equals(value.RuleConcept.Key, group.Key, StringComparison.Ordinal))
                .Select(value => value.RuleConcept.Key)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (conflictingBindings.Length > 0)
            {
                conflicts.Add(
                    $"{group.Key}: canonical identity is already bound to different concept(s): {string.Join(", ", conflictingBindings)}.");
                continue;
            }

            var existingConcept = await dbContext.RuleConcepts
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.Key == group.Key, cancellationToken);
            if (existingConcept is not null
                && (!string.Equals(
                        RuleConceptEntityTypes.Normalize(existingConcept.EntityType),
                        expectedType,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        existingConcept.DisplayName,
                        expectedDisplayName,
                        StringComparison.Ordinal)))
            {
                conflicts.Add(
                    $"{group.Key}: existing Rule Concept immutable metadata does not match the reviewed competency identity.");
                continue;
            }

            var conceptMutation = await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(group.Key, expectedType, expectedDisplayName),
                RulesCoreBaselineCatalog.BootstrapActor,
                cancellationToken);
            var concept = conceptMutation.Value;
            conceptCount++;
            if (conceptMutation.Created)
            {
                createdConceptCount++;
            }

            foreach (var candidate in grouped)
            {
                var canonicalEntityId = canonicalIds[candidate.SourceEntity.Id];
                var existingBinding = await dbContext.RuleConceptSourceBindings
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        value => value.RuleConceptId == concept.Id
                            && value.CanonicalEntityId == canonicalEntityId,
                        cancellationToken);
                if (existingBinding is not null)
                {
                    continue;
                }

                dbContext.RuleConceptSourceBindings.Add(new RuleConceptSourceBinding
                {
                    Id = Guid.NewGuid(),
                    RuleConceptId = concept.Id,
                    CanonicalEntityId = canonicalEntityId,
                    SourceEntityId = candidate.SourceEntity.Id,
                    CreatedByUserId = RulesCoreBaselineCatalog.BootstrapActor,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await dbContext.SaveChangesAsync(cancellationToken);
                createdBindingCount++;
            }

            var existingDecision = await dbContext.GlobalRuleDecisions
                .AsNoTracking()
                .Where(value => value.RuleConceptId == concept.Id)
                .OrderByDescending(value => value.DecisionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingDecision is not null)
            {
                continue;
            }

            var reviewedSourceEntityRevisionIds = grouped
                .Select(value => value.Revision.Id)
                .Distinct()
                .ToArray();
            var automatic = await RuleAutoResolutionService.TryResolveReviewedBaselineAsync(
                dbContext,
                concept.Id,
                RulesCoreBaselineCatalog.BootstrapActor,
                reviewedSourceEntityRevisionIds,
                cancellationToken);
            if (automatic.Applied)
            {
                createdDecisionCount++;
                continue;
            }

            existingDecision = await dbContext.GlobalRuleDecisions
                .AsNoTracking()
                .Where(value => value.RuleConceptId == concept.Id)
                .OrderByDescending(value => value.DecisionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingDecision is not null)
            {
                continue;
            }

            var distinctFingerprints = grouped
                .Select(value => RuleAutoResolutionService.ComputeSemanticFingerprint(
                    value.Revision.GetMechanicalContentJson()))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (grouped.Length == 1 || distinctFingerprints.Length == 1)
            {
                var selected = grouped
                    .OrderBy(value => value.SourceEntity.SourceCode, StringComparer.Ordinal)
                    .ThenBy(value => value.SourceEntity.NativeKey, StringComparer.Ordinal)
                    .ThenByDescending(value => value.Revision.RevisionNumber)
                    .First();
                var decision = await globalRules.SetDecisionAsync(
                    concept.Id,
                    new SetGlobalRuleDecisionRequest(
                        selected.Revision.Id,
                        grouped.Length == 1
                            ? "Built-in reviewed SRD competency baseline: the only reviewed implementation was selected."
                            : "Built-in reviewed SRD competency baseline: mechanically identical reviewed implementations were resolved deterministically."),
                    RulesCoreBaselineCatalog.BootstrapActor,
                    cancellationToken);
                if (decision.Created)
                {
                    createdDecisionCount++;
                }
                continue;
            }

            conflicts.Add(
                $"{group.Key}: reviewed implementations differ and the existing automatic resolution policy did not establish a safe baseline ({automatic.Reason}).");
        }

        return new ReviewedBundledCompetencyBaselineSyncResult(
            candidates.Length,
            conceptCount,
            createdConceptCount,
            createdBindingCount,
            createdDecisionCount,
            conflicts.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private sealed record ReviewedCompetencyCandidate(
        SourceEntity SourceEntity,
        SourceEntityRevision Revision,
        string ConceptKey);
}
