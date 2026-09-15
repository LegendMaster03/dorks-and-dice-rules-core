namespace RulesCore.Application.Rules;

public static class MechanicalRelationshipResolutionKinds
{
    public const string DeriveParent = "derive-parent";
    public const string IndependentParent = "independent-parent";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [DeriveParent, IndependentParent],
        StringComparer.Ordinal);
}

public sealed record MechanicalRelationshipCompetencyStateView(
    Guid? RuleConceptId,
    string ConceptKey,
    string EntityType,
    string DisplayName,
    bool HasRuleBinding,
    bool HasRuleDecision);

public sealed record MechanicalRelationshipDefinitionView(
    string Key,
    string Kind,
    MechanicalRelationshipCompetencyStateView Parent,
    IReadOnlyList<MechanicalRelationshipCompetencyStateView> Components,
    string Composition,
    string Direction);

public sealed record MechanicalRelationshipRulingView(
    Guid Id,
    string RelationshipKey,
    int RulingNumber,
    string ResolutionKind,
    string? Note,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record MechanicalRelationshipRecommendationView(
    MechanicalRelationshipDefinitionView Relationship,
    string ConceptRole,
    string RecommendedResolutionKind,
    string EffectiveResolutionKind,
    bool IsOverridden,
    bool CanResolveStructurally,
    bool RequiresAdjudication,
    IReadOnlyList<string> MissingConceptKeys,
    MechanicalRelationshipRulingView? LatestRuling,
    string Explanation);

public sealed record SetMechanicalRelationshipRulingRequest(
    string ResolutionKind,
    string? Note = null);
