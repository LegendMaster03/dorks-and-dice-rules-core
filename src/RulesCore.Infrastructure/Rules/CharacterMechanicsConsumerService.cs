using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

public sealed class CharacterMechanicsConsumerService(RulesCoreDbContext dbContext)
    : ICharacterMechanicsConsumerService
{
    private readonly CharacterResolvedRulesReader rulesReader =
        new(new ResolvedRulesCatalogService(dbContext));
    private readonly CharacterMechanicsCatalogBuilder catalogBuilder =
        new(dbContext);

    public async Task<CharacterMechanicsCatalogView> GetGlobalAsync(
        string? userId,
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default)
    {
        var rules = await rulesReader.ReadAllGlobalAsync(userId, cancellationToken);
        return await catalogBuilder.BuildAsync(rules, userId, includeUnavailable, cancellationToken);
    }

    public async Task<CharacterMechanicsCatalogView> GetGlobalEffectiveAsync(
        string? userId,
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default) =>
        CharacterMechanicsConsumerBoundary.ProjectEffective(
            await GetGlobalAsync(userId, includeUnavailable, cancellationToken));

    public async Task<CharacterMechanicsCatalogView> GetCampaignAsync(
        Guid campaignId,
        string userId,
        bool includeUnavailable = false,
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

        var normalizedUserId = userId.Trim();
        var rules = await rulesReader.ReadAllCampaignAsync(campaignId, normalizedUserId, cancellationToken);
        return await catalogBuilder.BuildAsync(
            rules,
            normalizedUserId,
            includeUnavailable,
            cancellationToken);
    }

    public async Task<CharacterMechanicsCatalogView> GetCampaignEffectiveAsync(
        Guid campaignId,
        string userId,
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default) =>
        CharacterMechanicsConsumerBoundary.ProjectEffective(
            await GetCampaignAsync(
                campaignId,
                userId,
                includeUnavailable,
                cancellationToken));

    public async Task<CharacterMechanicEvaluationView?> EvaluateGlobalEffectiveAsync(
        string mechanicKey,
        CharacterMechanicEvaluationRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var detailed = await GetGlobalAsync(userId, includeUnavailable: false, cancellationToken);
        var effective = CharacterMechanicsConsumerBoundary.ProjectEffective(detailed);
        CharacterMechanicsConsumerBoundary.RequireEffectiveMechanic(effective, mechanicKey);
        var allowed = effective.Mechanics.Select(value => value.MechanicKey).ToHashSet(StringComparer.Ordinal);
        var sanitized = CharacterMechanicsConsumerBoundary.SanitizeEvaluationRequest(request, allowed);
        return CharacterMechanicsCatalogEvaluator.EvaluateFromCatalog(detailed, mechanicKey, sanitized);
    }

    public async Task<CharacterMechanicsBatchEvaluationView> EvaluateGlobalEffectiveBatchAsync(
        CharacterMechanicsBatchEvaluationRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var detailed = await GetGlobalAsync(userId, includeUnavailable: false, cancellationToken);
        var effective = CharacterMechanicsConsumerBoundary.ProjectEffective(detailed);
        var allowed = effective.Mechanics.Select(value => value.MechanicKey).ToHashSet(StringComparer.Ordinal);
        var sanitizedItems = request.Evaluations.Select(value =>
        {
            CharacterMechanicsConsumerBoundary.RequireEffectiveMechanic(effective, value.MechanicKey);
            return value with
            {
                Evaluation = CharacterMechanicsConsumerBoundary.SanitizeEvaluationRequest(
                    value.Evaluation,
                    allowed)
            };
        }).ToArray();
        return CharacterMechanicsCatalogEvaluator.EvaluateBatchFromCatalog(
            detailed,
            request with { Evaluations = sanitizedItems });
    }

    public async Task<CharacterMechanicEvaluationView?> EvaluateGlobalAsync(
        string mechanicKey,
        CharacterMechanicEvaluationRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetGlobalAsync(userId, includeUnavailable: false, cancellationToken);
        return CharacterMechanicsCatalogEvaluator.EvaluateFromCatalog(catalog, mechanicKey, request);
    }

    public async Task<CharacterMechanicsBatchEvaluationView> EvaluateGlobalBatchAsync(
        CharacterMechanicsBatchEvaluationRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetGlobalAsync(userId, includeUnavailable: false, cancellationToken);
        return CharacterMechanicsCatalogEvaluator.EvaluateBatchFromCatalog(catalog, request);
    }

    public async Task<CharacterMechanicEvaluationView?> EvaluateCampaignAsync(
        Guid campaignId,
        string mechanicKey,
        CharacterMechanicEvaluationRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetCampaignAsync(
            campaignId,
            userId,
            includeUnavailable: false,
            cancellationToken);
        return CharacterMechanicsCatalogEvaluator.EvaluateFromCatalog(catalog, mechanicKey, request);
    }

    public async Task<CharacterMechanicsBatchEvaluationView> EvaluateCampaignBatchAsync(
        Guid campaignId,
        CharacterMechanicsBatchEvaluationRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetCampaignAsync(
            campaignId,
            userId,
            includeUnavailable: false,
            cancellationToken);
        return CharacterMechanicsCatalogEvaluator.EvaluateBatchFromCatalog(catalog, request);
    }

    public async Task<CharacterMechanicEvaluationView?> EvaluateCampaignEffectiveAsync(
        Guid campaignId,
        string mechanicKey,
        CharacterMechanicEvaluationRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var detailed = await GetCampaignAsync(
            campaignId,
            userId,
            includeUnavailable: false,
            cancellationToken);
        var effective = CharacterMechanicsConsumerBoundary.ProjectEffective(detailed);
        CharacterMechanicsConsumerBoundary.RequireEffectiveMechanic(effective, mechanicKey);
        var allowed = effective.Mechanics.Select(value => value.MechanicKey).ToHashSet(StringComparer.Ordinal);
        var sanitized = CharacterMechanicsConsumerBoundary.SanitizeEvaluationRequest(request, allowed);
        return CharacterMechanicsCatalogEvaluator.EvaluateFromCatalog(detailed, mechanicKey, sanitized);
    }

    public async Task<CharacterMechanicsBatchEvaluationView> EvaluateCampaignEffectiveBatchAsync(
        Guid campaignId,
        CharacterMechanicsBatchEvaluationRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var detailed = await GetCampaignAsync(
            campaignId,
            userId,
            includeUnavailable: false,
            cancellationToken);
        var effective = CharacterMechanicsConsumerBoundary.ProjectEffective(detailed);
        var allowed = effective.Mechanics.Select(value => value.MechanicKey).ToHashSet(StringComparer.Ordinal);
        var sanitizedItems = request.Evaluations.Select(value =>
        {
            CharacterMechanicsConsumerBoundary.RequireEffectiveMechanic(effective, value.MechanicKey);
            return value with
            {
                Evaluation = CharacterMechanicsConsumerBoundary.SanitizeEvaluationRequest(
                    value.Evaluation,
                    allowed)
            };
        }).ToArray();
        return CharacterMechanicsCatalogEvaluator.EvaluateBatchFromCatalog(
            detailed,
            request with { Evaluations = sanitizedItems });
    }

    public async Task<CharacterSupportProjectionView> ProjectGlobalSupportAsync(
        CharacterSupportProjectionRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rules = await rulesReader.ReadAllGlobalAsync(userId, cancellationToken);
        var catalog = await CharacterSupportCatalogBuilder.BuildAsync(
            dbContext,
            rules,
            cancellationToken);
        return CharacterSupportResolver.Project(catalog, request);
    }

    public async Task<CharacterSupportProjectionView> ProjectCampaignSupportAsync(
        Guid campaignId,
        CharacterSupportProjectionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalizedUserId = ValidateCampaignSupportRequest(campaignId, userId);
        var rules = await rulesReader.ReadAllCampaignAsync(
            campaignId,
            normalizedUserId,
            cancellationToken);
        var catalog = await CharacterSupportCatalogBuilder.BuildAsync(
            dbContext,
            rules,
            cancellationToken);
        return CharacterSupportResolver.Project(catalog, request);
    }

    public async Task<CharacterRecoveryResolutionView?> ResolveGlobalRecoveryAsync(
        string procedureKey,
        CharacterRecoveryResolutionRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rules = await rulesReader.ReadAllGlobalAsync(userId, cancellationToken);
        var catalog = await CharacterSupportCatalogBuilder.BuildAsync(
            dbContext,
            rules,
            cancellationToken);
        return CharacterSupportResolver.ResolveRecovery(
            catalog,
            procedureKey,
            request);
    }

    public async Task<CharacterRecoveryResolutionView?> ResolveCampaignRecoveryAsync(
        Guid campaignId,
        string procedureKey,
        CharacterRecoveryResolutionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalizedUserId = ValidateCampaignSupportRequest(campaignId, userId);
        var rules = await rulesReader.ReadAllCampaignAsync(
            campaignId,
            normalizedUserId,
            cancellationToken);
        var catalog = await CharacterSupportCatalogBuilder.BuildAsync(
            dbContext,
            rules,
            cancellationToken);
        return CharacterSupportResolver.ResolveRecovery(
            catalog,
            procedureKey,
            request);
    }

    private static string ValidateCampaignSupportRequest(
        Guid campaignId,
        string userId)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException(
                "Campaign ID can not be empty.",
                nameof(campaignId));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID can not be blank.",
                nameof(userId));
        }
        return userId.Trim();
    }

}