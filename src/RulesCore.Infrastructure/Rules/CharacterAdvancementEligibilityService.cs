using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Rules.CharacterProjection;

namespace RulesCore.Infrastructure.Rules;

public sealed class CharacterAdvancementEligibilityService(
    IResolvedRulesCatalogService resolvedRules,
    ICharacterRulesProjectionService projection)
    : ICharacterAdvancementEligibilityService
{
    private readonly ResolvedRulesSnapshotReader rules = new(resolvedRules);

    public async Task<CharacterAdvancementEligibilityView?> EvaluateGlobalAsync(
        CharacterAdvancementEligibilityRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var catalog = await rules.ReadAllGlobalAsync(userId, cancellationToken);
        var candidate = FindCandidate(catalog, request.CandidateConceptKey);
        if (candidate is null)
        {
            return null;
        }

        ValidateCandidateKind(candidate);
        var projected = await projection.ResolveGlobalAsync(
            SelectCandidate(request.Character, candidate.ConceptKey),
            userId,
            cancellationToken);
        return BuildView(catalog, candidate, request, projected);
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
        var catalog = await rules.ReadAllCampaignAsync(
            campaignId,
            userId.Trim(),
            cancellationToken);
        var candidate = FindCandidate(catalog, request.CandidateConceptKey);
        if (candidate is null)
        {
            return null;
        }

        ValidateCandidateKind(candidate);
        var projected = await projection.ResolveCampaignAsync(
            campaignId,
            SelectCandidate(request.Character, candidate.ConceptKey),
            userId.Trim(),
            cancellationToken);
        return BuildView(catalog, candidate, request, projected);
    }

    private static CharacterAdvancementEligibilityView BuildView(
        ResolvedRulesCatalogView catalog,
        ResolvedRuleCatalogItemView candidate,
        CharacterAdvancementEligibilityRequest request,
        CharacterRulesProjectionView projected)
    {
        var projectedPrerequisite = projected.Prerequisites.FirstOrDefault(value =>
            string.Equals(
                value.ConceptKey,
                candidate.ConceptKey,
                StringComparison.OrdinalIgnoreCase));
        var prerequisite = CharacterCandidatePrerequisiteSupplement.Project(
            candidate,
            catalog,
            request.Character,
            projectedPrerequisite);
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
        var requiredLevel = SubclassAcquisitionLevel(candidate);
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

    private static int? SubclassAcquisitionLevel(ResolvedRuleCatalogItemView candidate)
    {
        if (candidate.Document is not JsonElement document
            || document.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetProperty(document, "_rulesCore", out var rulesCore)
            && TryGetProperty(rulesCore, "character", out var character))
        {
            var normalized = ReadPositiveInteger(character, "acquisitionLevel")
                ?? ReadPositiveInteger(character, "subclassAcquisitionLevel");
            if (normalized is not null)
            {
                return normalized;
            }
        }

        var levels = ClassFamilyFeatureReferenceParser
            .Project("subclass", document)
            .Where(value => value.Level is > 0)
            .Select(value => value.Level!.Value)
            .ToArray();
        return levels.Length == 0 ? null : levels.Min();
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
            conceptKey.Trim(),
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

    private static int? ReadPositiveInteger(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var numeric)
            && numeric > 0)
        {
            return numeric;
        }
        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), out numeric)
            && numeric > 0)
        {
            return numeric;
        }
        return null;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out value))
        {
            return true;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}
