using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class TravelEnvironmentConsumerService(
    RulesCoreDbContext dbContext,
    IResolvedRulesCatalogService resolvedRules)
    : ITravelEnvironmentConsumerService
{
    public async Task<TravelEnvironmentCatalogView> GetGlobalAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var rules = await new ResolvedRulesSnapshotReader(resolvedRules)
            .ReadAllGlobalAsync(userId, cancellationToken);
        return await BuildCatalogAsync(rules, cancellationToken);
    }

    public async Task<TravelEnvironmentCatalogView> GetCampaignAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }

        var rules = await new ResolvedRulesSnapshotReader(resolvedRules)
            .ReadAllCampaignAsync(campaignId, userId.Trim(), cancellationToken);
        return await BuildCatalogAsync(rules, cancellationToken);
    }

    public async Task<TravelEnvironmentEvaluationView?> ResolveGlobalAsync(
        string mechanicKey,
        TravelEnvironmentResolutionRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetGlobalAsync(userId, cancellationToken);
        return Resolve(catalog, mechanicKey, request);
    }

    public async Task<TravelEnvironmentEvaluationView?> ResolveCampaignAsync(
        Guid campaignId,
        string mechanicKey,
        TravelEnvironmentResolutionRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetCampaignAsync(campaignId, userId, cancellationToken);
        return Resolve(catalog, mechanicKey, request);
    }

    private async Task<TravelEnvironmentCatalogView> BuildCatalogAsync(
        ResolvedRulesCatalogView rules,
        CancellationToken cancellationToken)
    {
        var documents = await EffectiveRuleDocumentReader.ReadAsync(
            dbContext,
            rules,
            cancellationToken);
        var metadata = new CharacterMechanicsSourceMetadataReader(dbContext);
        var providers = await metadata.ReadProvidersAsync(rules.Rules, cancellationToken);
        var sourceUris = await metadata.ReadSourceUrisAsync(rules.Rules, cancellationToken);
        var publications = await metadata.ReadCharacterMechanicsPublicationAttributionsAsync(
            rules.Rules,
            cancellationToken);

        var candidates = new List<TravelEnvironmentMechanicCandidate>();
        foreach (var rule in rules.Rules)
        {
            if (!documents.TryGetValue(rule.RuleConceptId, out var document))
            {
                continue;
            }

            providers.TryGetValue(rule.PackageKey, out var provider);
            sourceUris.TryGetValue(rule.SourceEntityRevisionId, out var sourceUri);
            publications.TryGetValue(rule.SourceEntityRevisionId, out var publicationRows);
            var attributions = CharacterMechanicsSourceMetadataReader.BuildRuleAttributions(
                rule,
                provider ?? rule.PackageDisplayName,
                sourceUri,
                publicationRows ?? []);
            candidates.AddRange(TravelEnvironmentProfileFactory.Build(
                rule,
                document,
                attributions));
        }

        var mechanics = candidates
            .GroupBy(value => value.Definition.MechanicKey, StringComparer.Ordinal)
            .Select(BuildMechanic)
            .OrderBy(value => value.MechanicKey, StringComparer.Ordinal)
            .ToArray();

        return new TravelEnvironmentCatalogView(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            mechanics);
    }

    private static TravelEnvironmentMechanicView BuildMechanic(
        IGrouping<string, TravelEnvironmentMechanicCandidate> group)
    {
        var values = group.ToArray();
        var distinctDefinitions = values
            .GroupBy(value => SemanticDefinition(value.Definition), StringComparer.Ordinal)
            .Select(value => value.First().Definition)
            .ToArray();
        var resolutions = values
            .Select(value => value.Rule.Resolution)
            .Where(value => value is not null)
            .Cast<EffectiveRuleResolutionView>()
            .GroupBy(value => JsonSerializer.Serialize(value), StringComparer.Ordinal)
            .Select(value => value.First())
            .ToArray();
        var requiresAdjudication = resolutions.Any(value => value.RequiresAdjudication);
        var conflict = distinctDefinitions.Length > 1;
        var state = conflict
            ? TravelEnvironmentMechanicStates.Conflicted
            : requiresAdjudication
                ? TravelEnvironmentMechanicStates.RequiresAdjudication
                : TravelEnvironmentMechanicStates.Resolved;
        var attributions = values
            .SelectMany(value => value.SourceAttributions)
            .Distinct()
            .OrderBy(value => value.GameEdition, StringComparer.Ordinal)
            .ThenBy(value => value.WorkDisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.PackageKey, StringComparer.Ordinal)
            .ToArray();

        return new TravelEnvironmentMechanicView(
            group.Key,
            state,
            !conflict && !requiresAdjudication,
            conflict ? null : distinctDefinitions.Single(),
            values.Select(value => value.Rule.ConceptKey)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            values.Select(value => value.Rule.RuleConceptId)
                .Distinct()
                .OrderBy(value => value)
                .ToArray(),
            attributions,
            resolutions);
    }

    private static TravelEnvironmentEvaluationView? Resolve(
        TravelEnvironmentCatalogView catalog,
        string mechanicKey,
        TravelEnvironmentResolutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(mechanicKey))
        {
            throw new ArgumentException("Mechanic key can not be blank.", nameof(mechanicKey));
        }
        ArgumentNullException.ThrowIfNull(request);

        var mechanic = catalog.Mechanics.SingleOrDefault(value =>
            string.Equals(value.MechanicKey, mechanicKey.Trim(), StringComparison.OrdinalIgnoreCase));
        if (mechanic is null)
        {
            return null;
        }
        if (!mechanic.CanResolve || mechanic.Definition is null)
        {
            return new TravelEnvironmentEvaluationView(
                mechanic.MechanicKey,
                mechanic.State,
                mechanic.State,
                null,
                null,
                null,
                [],
                mechanic.SourceAttributions,
                mechanic.RuleResolutions);
        }

        var evaluation = TravelEnvironmentMechanicEvaluator.Evaluate(
            mechanic.Definition,
            new TravelEnvironmentResolutionInput(
                Copy(request.IntegerInputs),
                Copy(request.BooleanInputs),
                Copy(request.StringInputs),
                CopyLists(request.StringListInputs)));

        return new TravelEnvironmentEvaluationView(
            mechanic.MechanicKey,
            mechanic.State,
            evaluation.State,
            evaluation.Quantity,
            evaluation.Factor,
            evaluation.Check,
            evaluation.MissingInputKeys,
            mechanic.SourceAttributions,
            mechanic.RuleResolutions);
    }

    private static string SemanticDefinition(TravelEnvironmentMechanicDefinition definition) =>
        JsonSerializer.Serialize(definition);

    private static IReadOnlyDictionary<string, T>? Copy<T>(Dictionary<string, T>? source) =>
        source is null
            ? null
            : new Dictionary<string, T>(source, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? CopyLists(
        Dictionary<string, IReadOnlyList<string>>? source) =>
        source is null
            ? null
            : source.ToDictionary(
                value => value.Key,
                value => value.Value,
                StringComparer.OrdinalIgnoreCase);
}
