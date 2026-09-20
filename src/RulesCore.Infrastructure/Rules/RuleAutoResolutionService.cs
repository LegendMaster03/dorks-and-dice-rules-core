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
                && string.Equals(
                    ComputeSemanticFingerprint(selectedRevision.GetMechanicalContentJson()),
                    evaluation.ResolvedSemanticFingerprint,
                    StringComparison.Ordinal))
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
                Contributions: contributions,
                ExpectedLatestDecisionId: latestDecision?.Id,
                EnforceExpectedLatestDecision: true),
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

    /// <summary>
    /// Applies the existing equivalence/additive resolution policy to an explicitly bounded
    /// source set. The resulting decision is a reviewed bootstrap decision rather than a
    /// continuously auto-maintained decision, so unrelated later source imports can not become
    /// part of the baseline merely because they reconcile to the same canonical entity.
    /// </summary>
    internal static async Task<RuleAutoResolutionResult> TryResolveReviewedBaselineAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        string actorUserId,
        IReadOnlyCollection<Guid> reviewedSourceEntityRevisionIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reviewedSourceEntityRevisionIds);
        var revisionScope = reviewedSourceEntityRevisionIds
            .Where(value => value != Guid.Empty)
            .ToHashSet();
        if (revisionScope.Count == 0)
        {
            return NotEligible("The reviewed competency baseline revision scope is empty.");
        }

        var sourceScope = (await dbContext.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => revisionScope.Contains(value.Id))
                .Select(value => value.SourceEntityId)
                .Distinct()
                .ToArrayAsync(cancellationToken))
            .ToHashSet();
        if (sourceScope.Count == 0)
        {
            return NotEligible("The reviewed competency baseline revisions do not resolve to source entities.");
        }

        var existingDecision = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Where(value => value.RuleConceptId == ruleConceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingDecision is not null)
        {
            return new RuleAutoResolutionResult(
                Eligible: true,
                Applied: false,
                existingDecision.Id,
                existingDecision.DecisionNumber,
                "An existing global decision is authoritative and was preserved.");
        }

        var evaluation = await EvaluateAsync(
            dbContext,
            ruleConceptId,
            actorUserId,
            cancellationToken,
            sourceScope,
            revisionScope);
        if (!evaluation.Eligible
            || evaluation.SelectedRevisionId is null
            || evaluation.ResolvedSemanticFingerprint is null
            || evaluation.Contexts.Count == 0)
        {
            return NotEligible(evaluation.Reason);
        }

        var actor = RequireActor(actorUserId);
        var baseContext = evaluation.Contexts.Single(value =>
            value.Revision.Id == evaluation.SelectedRevisionId.Value);
        var contributions = evaluation.Contexts
            .Where(value => value.Revision.Id != baseContext.Revision.Id)
            .OrderBy(value => value.Metadata.GameEdition, StringComparer.Ordinal)
            .ThenBy(value => value.Source.SourceCode, StringComparer.Ordinal)
            .Select(value => BuildContribution(baseContext, value, evaluation.Mode))
            .ToArray();

        JsonElement? mergePatch = null;
        if (!string.IsNullOrWhiteSpace(evaluation.MergePatchJson))
        {
            using var patchDocument = JsonDocument.Parse(evaluation.MergePatchJson);
            mergePatch = patchDocument.RootElement.Clone();
        }

        var note = evaluation.Mode == AdditiveMode
            ? "Built-in reviewed SRD competency baseline: compatible reviewed representations were combined using the existing additive-resolution policy."
            : "Built-in reviewed SRD competency baseline: mechanically equivalent reviewed representations were resolved using the existing equivalence policy.";
        var rules = new GlobalRulesService(dbContext);
        var decision = await rules.SetDecisionAsync(
            ruleConceptId,
            new SetGlobalRuleDecisionRequest(
                baseContext.Revision.Id,
                note,
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
                ? "The reviewed competency baseline decision was established by the existing safe-resolution policy."
                : "The reviewed competency baseline decision was already current.");
    }

    internal static string ComputeSemanticFingerprint(string contentJson) =>
        RuleSemanticCompatibility.ComputeFingerprint(contentJson);

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
            baseContext.Revision.GetMechanicalContentJson(),
            [contribution.Revision.GetMechanicalContentJson()]);
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
        CancellationToken cancellationToken,
        IReadOnlySet<Guid>? sourceEntityScope = null,
        IReadOnlySet<Guid>? sourceRevisionScope = null)
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

        var allSourceIds = await CanonicalRuleBindingStore.GetSourceEntityIdsForConceptAsync(
            dbContext,
            ruleConceptId,
            cancellationToken);
        var accessibleSourceIds = await CanonicalRuleBindingStore.GetAccessibleSourceEntityIdsForConceptAsync(
            dbContext,
            ruleConceptId,
            actor,
            cancellationToken);
        if (sourceEntityScope is not null)
        {
            allSourceIds = allSourceIds
                .Where(sourceEntityScope.Contains)
                .ToArray();
            accessibleSourceIds = accessibleSourceIds
                .Where(sourceEntityScope.Contains)
                .ToArray();
        }

        var ignoredPackageIds = (await new GlobalSourceDispositionService(dbContext)
                .GetIgnoredPackageIdsAsync(cancellationToken))
            .ToHashSet();
        var sourcePackageIds = await dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => allSourceIds.Contains(value.Id))
            .Select(value => new { value.Id, value.SourcePackageId })
            .ToArrayAsync(cancellationToken);
        var activeSourceIds = sourcePackageIds
            .Where(value => !ignoredPackageIds.Contains(value.SourcePackageId))
            .Select(value => value.Id)
            .ToHashSet();
        if (activeSourceIds.Count == 0)
        {
            return NotEligibleEvaluation("No non-ignored source implementations remain for automatic global resolution.");
        }

        var accessibleActiveSourceIds = accessibleSourceIds
            .Where(activeSourceIds.Contains)
            .ToHashSet();
        if (accessibleActiveSourceIds.Count != activeSourceIds.Count)
        {
            return NotEligibleEvaluation(
                "Automatic global resolution requires access to every active source implementation in the bound canonical revision closure.");
        }

        var sources = await dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.Revisions)
            .Include(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => accessibleActiveSourceIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);

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
                return NotEligibleEvaluation(
                    "An active source implementation could not be reconciled to the bound canonical revision closure.");
            }

            var latest = source.Revisions
                .Where(value => sourceRevisionScope is null || sourceRevisionScope.Contains(value.Id))
                .OrderByDescending(value => value.RevisionNumber)
                .FirstOrDefault();
            if (latest is null)
            {
                return NotEligibleEvaluation(
                    "An active source implementation has no revision in the automatic-resolution review scope.");
            }

            var metadata = await CanonicalPublicationMetadataReader.ReadAsync(dbContext, source.Id, cancellationToken);
            if (metadata is null || string.IsNullOrWhiteSpace(metadata.GameEdition))
            {
                return NotEligibleEvaluation(
                    "Automatic global resolution requires canonical game-edition metadata for every active source implementation.");
            }

            allContexts.Add(new SourceContext(
                canonicalEntityId,
                source,
                latest,
                metadata,
                ComputeSemanticFingerprint(latest.GetMechanicalContentJson())));
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

        var contextGroups = allContexts
            .GroupBy(value => new { value.CanonicalEntityId, value.Metadata.GameEdition })
            .ToArray();
        if (contextGroups.Any(group =>
                group.Select(value => value.SemanticFingerprint)
                    .Distinct(StringComparer.Ordinal)
                    .Skip(1)
                    .Any()))
        {
            return NotEligibleEvaluation(
                "Multiple active source implementations for the same canonical entity and game edition differ mechanically.");
        }

        var contexts = contextGroups
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
            .Select(value => value.Revision.GetMechanicalContentJson())
            .ToArray();
        var preferredBaseJson = preferredBase.Revision.GetMechanicalContentJson();
        var additiveMerge = RuleSemanticCompatibility.TryCreateAdditiveUnion(preferredBaseJson, mergeOrder);
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
            patch = RuleSemanticCompatibility.CreateAdditivePatch(preferredBaseJson, additiveMerge.MergedJson);
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
