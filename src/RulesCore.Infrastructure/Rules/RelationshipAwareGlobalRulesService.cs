using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Keeps direct resolved-rule reads aligned with the catalog contract by attaching stable
/// concept-to-concept relationships. Mutation and publication behavior remains owned by
/// <see cref="GlobalRulesService"/>.
/// </summary>
public sealed class RelationshipAwareGlobalRulesService(
    GlobalRulesService inner,
    RulesCoreDbContext dbContext) : IGlobalRulesService
{
    public Task<RuleMutationResult<RuleConceptView>> CreateConceptAsync(
        CreateRuleConceptRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        inner.CreateConceptAsync(request, actorUserId, cancellationToken);

    public Task<RuleMutationResult<RuleConceptSourceBindingView>> BindSourceEntityAsync(
        Guid ruleConceptId,
        BindRuleConceptSourceRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        inner.BindSourceEntityAsync(ruleConceptId, request, actorUserId, cancellationToken);

    public Task<RuleMutationResult<GlobalRuleDecisionView>> SetDecisionAsync(
        Guid ruleConceptId,
        SetGlobalRuleDecisionRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        inner.SetDecisionAsync(ruleConceptId, request, actorUserId, cancellationToken);

    public Task<PublishedRulesetRevisionView> PublishAsync(
        string actorUserId,
        CancellationToken cancellationToken = default) =>
        inner.PublishAsync(actorUserId, cancellationToken);

    public async Task<ResolvedRuleView?> ResolveLatestAsync(
        string conceptKey,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var resolved = await inner.ResolveLatestAsync(conceptKey, userId, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        var outgoing = await RuleConceptRelationshipStore.GetOutgoingAsync(
            dbContext,
            [resolved.RuleConceptId],
            cancellationToken);
        var relationships = outgoing.TryGetValue(resolved.RuleConceptId, out var related)
            ? related.Select(value => new ResolvedRuleRelationshipView(
                    value.Kind,
                    value.RelatedRuleConceptId,
                    value.RelatedConceptKey,
                    value.RelatedEntityType,
                    value.RelatedDisplayName))
                .ToArray()
            : [];

        return resolved with { Relationships = relationships };
    }

    public Task<RuleConceptVersionsView?> GetAccessibleVersionsAsync(
        string conceptKey,
        string? userId,
        CancellationToken cancellationToken = default) =>
        inner.GetAccessibleVersionsAsync(conceptKey, userId, cancellationToken);
}
