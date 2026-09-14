using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

public sealed record RuleAutoResolutionResult(
    bool Eligible,
    bool Applied,
    Guid? DecisionId,
    int? DecisionNumber,
    string Reason);

public static class RuleAutoResolutionService
{
    private const string IdenticalMode = "identical";
    private const string AdditiveMode = "additive";

    private const string NoChangeAutoResolutionNote =
        "Auto-resolved: no rule-bearing content changed across the bound editions.";
    private const string AdditiveAutoResolutionNote =
        "Auto-resolved-additive: bound editions were combined using only non-destructive compatible additions.";

    public static bool IsAutomaticDecision(GlobalRuleDecision decision)
    {
        if (decision.Note?.StartsWith("Auto-resolved:", StringComparison.Ordinal) == true)
        {
            return string.Equals(decision.DecisionKind, RuleDecisionKinds.SelectSource, StringComparison.Ordinal)
                && decision.PatchFingerprint is null;
        }

        return decision.Note?.StartsWith("Auto-resolved-additive:", StringComparison.Ordinal) == true
            && (string.Equals(decision.DecisionKind, RuleDecisionKinds.SelectSource, StringComparison.Ordinal)
                || string.Equals(decision.DecisionKind, RuleDecisionKinds.JsonMergePatch, StringComparison.Ordinal));
    }

    public static async Task<bool> IsCurrentAutomaticDecisionAsync(
        RulesCoreDbContext dbContext,
        GlobalRuleDecision decision,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!IsAutomaticDecision(decision)) return false;

        var evaluation = await EvaluateAsync(dbContext, decision.RuleConceptId, actorUserId, cancellationToken);
        if (!evaluation.Eligible || evaluation.SelectedRevisionId is null) return false;

        return decision.SelectedSourceEntityRevisionId == evaluation.SelectedRevisionId.Value
            && string.Equals(decision.DecisionKind, evaluation.DecisionKind, StringComparison.Ordinal)
            && string.Equals(decision.PatchFingerprint, evaluation.PatchFingerprint, StringComparison.Ordinal);
    }

    public static async Task<RuleAutoResolutionResult> TryResolveAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluateAsync(dbContext, ruleConceptId, actorUserId, cancellationToken);
        if (!evaluation.Eligible
            || evaluation.SelectedRevisionId is null
            || evaluation.ResolvedSemanticFingerprint is null
            || evaluation.Contexts.Count == 0)
        {
            return NotEligible(evaluation.Reason);
        }

        var actor = RequireActor(actorUserId);
        var contexts = evaluation.Contexts;
        var baseContext = contexts.Single(value => value.Revision.Id == evaluation.SelectedRevisionId.Value);
        var contributions = contexts
            .Where(value => value.Revision.Id != baseContext.Revision.Id)
            .OrderBy(value => value.Metadata.GameEdition, StringComparer.Ordinal)
            .ThenBy(value => value.Source.SourceCode, StringComparer.Ordinal)
            .Select(value => BuildContribution(baseContext, value, evaluation.Mode))
            .ToArray();

        var latestDecision = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Where(value => value.RuleConceptId == ruleConceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestDecision is not null && !IsAutomaticDecision(latestDecision))
        {
            if (!string.Equals(latestDecision.DecisionKind, RuleDecisionKinds.SelectSource, StringComparison.Ordinal)
                || latestDecision.PatchFingerprint is not null
                || evaluation.PatchFingerprint is not null)
            {
                return NotEligible("An existing manual or patched decision is authoritative and will not be replaced automatically.");
            }

            var selectedRevision = await dbContext.SourceEntityRevisions
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == latestDecision.SelectedSourceEntityRevisionId, cancellationToken);
            if (selectedRevision is not null
                && string.Equals(ComputeSemanticFingerprint(selectedRevision.RawJson), evaluation.ResolvedSemanticFingerprint, StringComparison.Ordinal))
            {
                return new RuleAutoResolutionResult(
                    true, false, latestDecision.Id, latestDecision.DecisionNumber,
                    evaluation.Mode == AdditiveMode
                        ? "The existing exact-source decision already contains the complete non-destructive additive result."
                        : "The existing exact-source decision already resolves the unchanged cross-edition rule.");
            }

            return NotEligible("The current manual decision selects mechanically different content and requires manual review.");
        }

        JsonElement? mergePatch = null;
        if (!string.IsNullOrWhiteSpace(evaluation.MergePatchJson))
        {
            using var patchDocument = JsonDocument.Parse(evaluation.MergePatchJson);
            mergePatch = patchDocument.RootElement.Clone();
        }

        var rules = new GlobalRulesService(dbContext);
        var decision = await rules.SetDecisionAsync(
            ruleConceptId,
            new SetGlobalRuleDecisionRequest(
                baseContext.Revision.Id,
                evaluation.Mode == AdditiveMode ? AdditiveAutoResolutionNote : NoChangeAutoResolutionNote,
                MergePatch: mergePatch,
                Contributions: contributions),
            actor,
            cancellationToken);

        return new RuleAutoResolutionResult(
            true,
            decision.Created,
            decision.Value.Id,
            decision.Value.DecisionNumber,
            decision.Created
                ? evaluation.Mode == AdditiveMode
                    ? evaluation.PatchFingerprint is null
                        ? "A complete compatible source implementation was selected automatically."
                        : "Compatible cross-edition additions were combined automatically without removing rule-bearing content."
                    : "Equivalent cross-edition source implementations were resolved automatically."
                : evaluation.Mode == AdditiveMode
                    ? "The additive cross-edition decision was already current."
                    : "The equivalent cross-edition decision was already current.");
    }

    internal static string ComputeSemanticFingerprint(string rawJson) =>
        RuleSemanticCompatibility.ComputeFingerprint(rawJson);

    private static RuleConsolidationContributionRequest BuildContribution(
        SourceContext baseContext,
        SourceContext contribution,
        string? mode)
    {
        if (mode != AdditiveMode)
        {
            return new RuleConsolidationContributionRequest(
                contribution.Revision.Id,
                RuleConsolidationContributionKinds.Reference,
                "Automatically reviewed as mechanically identical to the selected source revision.");
        }

        var pairMerge = RuleSemanticCompatibility.TryCreateAdditiveUnion(
            baseContext.Revision.RawJson,
            [contribution.Revision.RawJson]);
        var contributesContent = pairMerge.Compatible
            && pairMerge.MergedJson is not null
            && !string.Equals(
                ComputeSemanticFingerprint(pairMerge.MergedJson),
                baseContext.SemanticFingerprint,
                StringComparison.Ordinal);

        return new RuleConsolidationContributionRequest(
            contribution.Revision.Id,
            contributesContent ? RuleConsolidationContributionKinds.Incorporated : RuleConsolidationContributionKinds.Reference,
            contributesContent
                ? "Automatically incorporated compatible rule-bearing additions from this source revision."
                : "Automatically reviewed as compatible context that does not add beyond the selected base revision.");
    }

    private static async Task<AutoResolutionEvaluation> EvaluateAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (ruleConceptId == Guid.Empty)
        {
            throw new ArgumentException("Rule concept ID can not be empty.", nameof(ruleConceptId));
        }
        var actor = RequireActor(actorUserId);
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);
        await CanonicalRuleBindingStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var boundCanonicalEntityIds = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .Where(value => value.RuleConceptId == ruleConceptId)
            .Select(value => value.CanonicalEntityId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (boundCanonicalEntityIds.Length == 0)
        {
            return NotEligibleEvaluation("At least one canonical rule entity must be bound to the concept.");
        }

        var revisionClosures = await new CanonicalEntityRevisionClosureStore(dbContext)
            .ReadAsync(boundCanonicalEntityIds, cancellationToken);
        var allowedCanonicalEntityIds = revisionClosures.Values
            .SelectMany(value => value)
            .ToHashSet();

        var accessibleSourceIds = await CanonicalRuleBindingStore.GetAccessibleSourceEntityIdsForConceptAsync(
            dbContext,
            ruleConceptId,
            actor,
            cancellationToken);
        if (accessibleSourceIds.Count == 0)
        {
            return NotEligibleEvaluation("The current account can not inspect any source implementation for the bound canonical rule entities.");
        }

        var sources = await dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.Revisions)
            .Include(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => accessibleSourceIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);

        var ignoredPackageIds = (await new GlobalSourceDispositionService(dbContext)
                .GetIgnoredPackageIdsAsync(cancellationToken))
            .ToHashSet();
        var activeSources = sources
            .Where(value => !ignoredPackageIds.Contains(value.SourcePackageId))
            .ToArray();
        if (activeSources.Length == 0)
        {
            return NotEligibleEvaluation("No accessible non-ignored source implementations remain for automatic global resolution.");
        }

        var canonicalBySource = await CanonicalRuleBindingStore.GetCanonicalEntityIdsAsync(
            dbContext,
            activeSources.Select(value => value.Id).ToArray(),
            cancellationToken);
        var allContexts = new List<SourceContext>(activeSources.Length);
        foreach (var source in activeSources)
        {
            if (!canonicalBySource.TryGetValue(source.Id, out var canonicalEntityId)
                || !allowedCanonicalEntityIds.Contains(canonicalEntityId))
            {
                continue;
            }

            var latest = source.Revisions.OrderByDescending(value => value.RevisionNumber).FirstOrDefault();
            if (latest is null)
            {
                continue;
            }

            var metadata = await CanonicalPublicationMetadataReader.ReadAsync(dbContext, source.Id, cancellationToken);
            if (metadata is null || string.IsNullOrWhiteSpace(metadata.GameEdition))
            {
                continue;
            }

            allContexts.Add(new SourceContext(
                canonicalEntityId,
                source,
                latest,
                metadata,
                ComputeSemanticFingerprint(latest.RawJson)));
        }

        foreach (var rootCanonicalEntityId in boundCanonicalEntityIds)
        {
            if (!revisionClosures.TryGetValue(rootCanonicalEntityId, out var closure)
                || !allContexts.Any(value => closure.Contains(value.CanonicalEntityId)))
            {
                return NotEligibleEvaluation(
                    "The current account can not inspect an active representation for every canonical entity bound to this rule concept.");
            }
        }

        var contexts = allContexts
            .GroupBy(value => new { value.CanonicalEntityId, value.Metadata.GameEdition })
            .Select(group => SelectRepresentativeContext(group))
            .OrderBy(value => value.Metadata.GameEdition, StringComparer.Ordinal)
            .ThenBy(value => value.CanonicalEntityId)
            .ToArray();
        if (contexts.Select(value => value.Metadata.GameEdition).Distinct(StringComparer.Ordinal).Count() < 2)
        {
            return NotEligibleEvaluation("Auto-resolution only applies to comparisons spanning multiple editions.");
        }

        var semanticFingerprints = contexts.Select(value => value.SemanticFingerprint).Distinct(StringComparer.Ordinal).ToArray();
        if (semanticFingerprints.Length == 1)
        {
            var representative = SelectRepresentativeContext(contexts);
            return new AutoResolutionEvaluation(
                true,
                "The bound editions contain the same rule-bearing content.",
                contexts,
                representative.Revision.Id,
                representative.SemanticFingerprint,
                RuleDecisionKinds.SelectSource,
                null,
                null,
                IdenticalMode);
        }

        var preferredBase = SelectRepresentativeContext(contexts);
        var mergeOrder = contexts
            .Where(value => value.Revision.Id != preferredBase.Revision.Id)
            .OrderByDescending(value => value.Metadata.PublicationDate ?? DateOnly.MinValue)
            .ThenByDescending(value => value.Metadata.GameEdition, StringComparer.Ordinal)
            .ThenBy(value => value.Source.SourceCode, StringComparer.Ordinal)
            .ThenBy(value => value.Source.Id)
            .Select(value => value.Revision.RawJson)
            .ToArray();
        var additiveMerge = RuleSemanticCompatibility.TryCreateAdditiveUnion(preferredBase.Revision.RawJson, mergeOrder);
        if (!additiveMerge.Compatible || additiveMerge.MergedJson is null)
        {
            return NotEligibleEvaluation($"{additiveMerge.Reason} Manual adjudication is required.");
        }

        var resolvedFingerprint = ComputeSemanticFingerprint(additiveMerge.MergedJson);
        var exactCompleteSources = contexts
            .Where(value => string.Equals(value.SemanticFingerprint, resolvedFingerprint, StringComparison.Ordinal))
            .ToArray();
        if (exactCompleteSources.Length > 0)
        {
            var completeSource = SelectRepresentativeContext(exactCompleteSources);
            return new AutoResolutionEvaluation(
                true,
                "The bound editions differ only by compatible additions or omissions, and one exact source revision contains the complete additive result.",
                contexts,
                completeSource.Revision.Id,
                resolvedFingerprint,
                RuleDecisionKinds.SelectSource,
                null,
                null,
                AdditiveMode);
        }

        NormalizedJsonMergePatch? patch;
        try
        {
            patch = RuleSemanticCompatibility.CreateAdditivePatch(preferredBase.Revision.RawJson, additiveMerge.MergedJson);
        }
        catch (InvalidOperationException exception)
        {
            return NotEligibleEvaluation($"{exception.Message} Manual adjudication is required.");
        }

        if (patch is null)
        {
            return NotEligibleEvaluation("The additive comparison did not produce a stable source or patch. Manual adjudication is required.");
        }

        return new AutoResolutionEvaluation(
            true,
            "The bound editions differ only by compatible non-destructive additions and can be represented as an additive merge over the preferred source revision.",
            contexts,
            preferredBase.Revision.Id,
            resolvedFingerprint,
            RuleDecisionKinds.JsonMergePatch,
            patch.Json,
            patch.Fingerprint,
            AdditiveMode);
    }

    private static SourceContext SelectRepresentativeContext(IEnumerable<SourceContext> contexts) =>
        contexts
            .OrderByDescending(value => value.Metadata.PublicationDate ?? DateOnly.MinValue)
            .ThenByDescending(value => value.Metadata.GameEdition, StringComparer.Ordinal)
            .ThenByDescending(value => value.Revision.RevisionNumber)
            .ThenByDescending(value => value.Revision.ImportedAt)
            .ThenBy(value => value.Source.SourceCode, StringComparer.Ordinal)
            .ThenBy(value => value.Source.Id)
            .First();

    private static string RequireActor(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("User ID can not be blank.", nameof(value));
        return value.Trim();
    }

    private static RuleAutoResolutionResult NotEligible(string reason) => new(false, false, null, null, reason);
    private static AutoResolutionEvaluation NotEligibleEvaluation(string reason) =>
        new(false, reason, [], null, null, null, null, null, null);

    private sealed record AutoResolutionEvaluation(
        bool Eligible,
        string Reason,
        IReadOnlyList<SourceContext> Contexts,
        Guid? SelectedRevisionId,
        string? ResolvedSemanticFingerprint,
        string? DecisionKind,
        string? MergePatchJson,
        string? PatchFingerprint,
        string? Mode);

    private sealed record SourceContext(
        Guid CanonicalEntityId,
        SourceEntity Source,
        SourceEntityRevision Revision,
        CanonicalPublicationMetadata Metadata,
        string SemanticFingerprint);
}
