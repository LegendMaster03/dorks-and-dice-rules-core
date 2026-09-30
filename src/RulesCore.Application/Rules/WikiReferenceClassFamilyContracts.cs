namespace RulesCore.Application.Rules;

public sealed record WikiReferenceRelatedReferenceView(
    string ReferenceIdentity,
    Guid? RuleConceptId,
    string? ConceptKey,
    string DisplayName,
    string EntityType,
    RuleLinkTargetView BrowserLink);

public sealed record WikiReferenceClassFamilyView(
    string Scope,
    Guid? CampaignId,
    string ReferenceIdentity,
    IReadOnlyList<WikiReferenceRelatedReferenceView> ParentClasses,
    IReadOnlyList<WikiReferenceRelatedReferenceView> Subclasses);
