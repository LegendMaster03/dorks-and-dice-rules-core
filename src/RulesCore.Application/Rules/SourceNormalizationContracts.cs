namespace RulesCore.Application.Rules;

public static class SourceNormalizationSuggestionKinds
{
    public const string NewConcept = "new-concept";
    public const string ExistingConcept = "existing-concept";
    public const string Conflict = "conflict";
}

public sealed record SourceNormalizationCandidateView(
    Guid SourceEntityId,
    string EntityType,
    string Name,
    string SourceCode,
    int LatestRevisionNumber,
    DateTimeOffset LatestImportedAt,
    string PackageKey,
    string PackageDisplayName,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    string SuggestedConceptKey,
    Guid? SuggestedConceptId,
    string? SuggestedConceptDisplayName,
    string SuggestionKind);

public sealed record AcceptedSourceNormalizationView(
    RuleConceptView Concept,
    RuleConceptSourceBindingView Binding,
    bool CreatedConcept,
    bool CreatedBinding);

public interface ISourceNormalizationService
{
    Task<IReadOnlyList<SourceNormalizationCandidateView>> GetCandidatesAsync(
        string userId,
        string? entityType = null,
        string? query = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceNormalizationCandidateView>> GetCandidatesPageAsync(
        string userId,
        string? entityType = null,
        string? query = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    Task<AcceptedSourceNormalizationView?> AcceptAsync(
        Guid sourceEntityId,
        string userId,
        CancellationToken cancellationToken = default);
}
