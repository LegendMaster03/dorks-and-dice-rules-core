using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Rules.CharacterProjection;

namespace RulesCore.Infrastructure.Rules;

public sealed class CharacterAdvancementEligibilityService(
    IResolvedRulesCatalogService resolvedRules,
    ICharacterRulesProjectionService projection)
    : ICharacterAdvancementEligibilityService
{
    private const int CandidatePageSize = 200;

    public async Task<CharacterAdvancementEligibilityView?> EvaluateGlobalAsync(
        CharacterAdvancementEligibilityRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var lookup = await FindGlobalCandidateAsync(
            request.CandidateConceptKey,
            userId,
            cancellationToken);
        if (lookup.Candidate is null)
        {
            return null;
        }

        ValidateCandidateKind(lookup.Candidate);
        var projected = await projection.ResolveGlobalAsync(
            SelectCandidate(request.Character, lookup.Candidate.ConceptKey),
            userId,
            cancellationToken);
        return BuildView(lookup.Catalog, lookup.Candidate, request, projected);
    }

    public async Task<CharacterAdvancementEligibilityView?> EvaluateCampaignAsync(
        Guid campaignId,
        CharacterAdvancementEligibilityRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("Campaign ID can not be empty.", nameof(campaignId));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }

        ValidateRequest(request);
        var normalizedUserId = userId.Trim();
        var lookup = await FindCampaignCandidateAsync(
            campaignId,
            request.CandidateConceptKey,
            normalizedUserId,
            cancellationToken);
        if (lookup.Candidate is null)
        {
            return null;
        }

        ValidateCandidateKind(lookup.Candidate);
        var projected = await projection.ResolveCampaignAsync(
            campaignId,
            SelectCandidate(request.Character, lookup.Candidate.ConceptKey),
            normalizedUserId,
            cancellationToken);
        return BuildView(lookup.Catalog, lookup.Candidate, request, projected);
    }

    private async Task<CandidateLookup> FindGlobalCandidateAsync(
        string conceptKey,
        string? userId,
        CancellationToken cancellationToken)
    {
        var normalized = conceptKey.Trim();
        for (var offset = 0; ; offset += CandidatePageSize)
        {
            var page = await resolvedRules.GetGlobalPageAsync(
                userId,
                query: normalized,
                limit: CandidatePageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            var candidate = FindCandidate(page, normalized);
            if (candidate is not null || page.Rules.Count < CandidatePageSize)
            {
                return new CandidateLookup(page, candidate);
            }
        }
    }

    private async Task<CandidateLookup> FindCampaignCandidateAsync(
        Guid campaignId,
        string conceptKey,
        string userId,
        CancellationToken cancellationToken)
    {
        var normalized = conceptKey.Trim();
        for (var offset = 0; ; offset += CandidatePageSize)
        {
            var page = await resolvedRules.GetCampaignPageAsync(
                campaignId,
                userId,
                query: normalized,
                limit: CandidatePageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            var candidate = FindCandidate(page, normalized);
            if (candidate is not null || page.Rules.Count < CandidatePageSize)
            {
                return new CandidateLookup(page, candidate);
            }
        }
    }

    private static CharacterAdvancementEligibilityView BuildView(
        ResolvedRulesCatalogView catalog,
        ResolvedRuleCatalogItemView candidate,
        CharacterAdvancementEligibilityRequest request,
        CharacterRulesProjectionView projected)
    {
        var prerequisite = projected.Prerequisites.FirstOrDefault(value =>
            string.Equals(
                value.ConceptKey,
                candidate.ConceptKey,
                StringComparison.OrdinalIgnoreCase));
        var conflicts = projected.Conflicts
            .Where(value => value.RelatedConceptKeys.Any(key =>
                string.Equals(key, candidate.ConceptKey, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        CharacterAdvancementParentRequirementView? parent = null;
        bool? parentSatisfied = true;
        if (string.Equals(candidate.EntityType, "subclass", StringComparison.OrdinalIgnoreCase))
        {
            parent = BuildSubclassParentRequirement(candidate, request);
            parentSatisfied = parent?.Satisfied;
        }

        bool? prerequisiteSatisfied = prerequisite is null
            ? true
            : prerequisite.Satisfied;
        var prerequisiteDefinitionKnown = !string.Equals(
                candidate.EntityType,
                "prestigeClass",
                StringComparison.OrdinalIgnoreCase)
            || HasCompletePrestigePrerequisiteDefinition(candidate);
        var resolutionKnown = candidate.Resolution?.RequiresAdjudication != true;
        bool? eligible;
        if (parentSatisfied == false
            || prerequisiteSatisfied == false
            || conflicts.Length > 0)
        {
            eligible = false;
        }
        else if (parentSatisfied is null
                 || prerequisiteSatisfied is null
                 || !prerequisiteDefinitionKnown
                 || !resolutionKnown)
        {
            eligible = null;
        }
        else
        {
            eligible = true;
        }

        var state = eligible switch
        {
            true => CharacterAdvancementEligibilityStates.Eligible,
            false => CharacterAdvancementEligibilityStates.Ineligible,
            null => CharacterAdvancementEligibilityStates.Unresolved
        };

        return new CharacterAdvancementEligibilityView(
            catalog.Scope,
            catalog.CampaignId,
            candidate.ConceptKey,
            candidate.DisplayName,
            candidate.EntityType,
            state,
            eligible,
            parent,
            prerequisite,
            conflicts);
    }

    private static CharacterAdvancementParentRequirementView? BuildSubclassParentRequirement(
        ResolvedRuleCatalogItemView candidate,
        CharacterAdvancementEligibilityRequest request)
    {
        var parents = candidate.Relationships
            .Where(value =>
                string.Equals(value.Kind, "parent-class", StringComparison.OrdinalIgnoreCase)
                && string.Equals(value.RelatedEntityType, "class", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (parents.Length != 1)
        {
            return null;
        }

        var parent = parents[0];
        var requiredLevel = CharacterSubclassAdvancementProjector.AcquisitionLevel(candidate);
        var (currentLevel, ambiguousOccurrence) = ParentClassLevel(
            request.Character,
            parent.RelatedConceptKey,
            request.ParentAdvancementOccurrenceKey);

        bool? satisfied = requiredLevel is null || ambiguousOccurrence
            ? null
            : (currentLevel ?? 0) >= requiredLevel.Value;
        var state = satisfied.HasValue
            ? "resolved"
            : "applicable-unresolved";
        var reason = ambiguousOccurrence
            ? "More than one matching parent Class occurrence exists; identify the parent occurrence being evaluated."
            : requiredLevel is null
                ? "The effective Subclass acquisition level is unresolved."
                : $"{parent.RelatedDisplayName} level {currentLevel ?? 0}; Subclass acquisition requires level {requiredLevel.Value}.";

        return new CharacterAdvancementParentRequirementView(
            parent.RelatedConceptKey,
            parent.RelatedDisplayName,
            requiredLevel,
            currentLevel,
            satisfied,
            state,
            reason);
    }

    private static (int? Level, bool AmbiguousOccurrence) ParentClassLevel(
        CharacterRulesProjectionRequest character,
        string parentConceptKey,
        string? requestedOccurrenceKey)
    {
        var matches = (character.Advancements ?? [])
            .Where(value => value.Level > 0)
            .Where(value => string.Equals(
                value.ConceptKey?.Trim(),
                parentConceptKey,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(requestedOccurrenceKey))
        {
            var occurrence = matches.FirstOrDefault(value => string.Equals(
                value.OccurrenceKey?.Trim(),
                requestedOccurrenceKey.Trim(),
                StringComparison.Ordinal));
            return (occurrence?.Level ?? 0, false);
        }

        return matches.Length switch
        {
            0 => (0, false),
            1 => (matches[0].Level, false),
            _ => (null, true)
        };
    }

    private static bool HasCompletePrestigePrerequisiteDefinition(
        ResolvedRuleCatalogItemView candidate)
    {
        if (candidate.Document is not JsonElement document
            || document.ValueKind != JsonValueKind.Object
            || !CharacterProjectionJson.TryGetProperty(document, "_rulesCore", out var rulesCore)
            || !CharacterProjectionJson.TryGetProperty(rulesCore, "character", out var character)
            || !CharacterProjectionJson.TryGetProperty(character, "prerequisitesComplete", out var complete))
        {
            return false;
        }

        return complete.ValueKind == JsonValueKind.True;
    }

    private static CharacterRulesProjectionRequest SelectCandidate(
        CharacterRulesProjectionRequest character,
        string candidateConceptKey)
    {
        var selected = (character.SelectedConcepts ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value.ConceptKey))
            .ToList();
        if (!selected.Any(value => string.Equals(
                value.ConceptKey.Trim(),
                candidateConceptKey,
                StringComparison.OrdinalIgnoreCase)))
        {
            selected.Add(new CharacterSelectedConceptInput(
                candidateConceptKey,
                "eligibility-candidate"));
        }
        return character with { SelectedConcepts = selected };
    }

    private static ResolvedRuleCatalogItemView? FindCandidate(
        ResolvedRulesCatalogView catalog,
        string conceptKey) =>
        catalog.Rules.FirstOrDefault(value => string.Equals(
            value.ConceptKey,
            conceptKey,
            StringComparison.OrdinalIgnoreCase));

    private static void ValidateCandidateKind(ResolvedRuleCatalogItemView candidate)
    {
        if (!string.Equals(candidate.EntityType, "subclass", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate.EntityType, "prestigeClass", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Advancement eligibility currently supports Subclass and Prestige Class candidates.");
        }
    }

    private static void ValidateRequest(CharacterAdvancementEligibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Character);
        if (string.IsNullOrWhiteSpace(request.CandidateConceptKey))
        {
            throw new ArgumentException(
                "Candidate concept key can not be blank.",
                nameof(request));
        }
    }

    private sealed record CandidateLookup(
        ResolvedRulesCatalogView Catalog,
        ResolvedRuleCatalogItemView? Candidate);
}
