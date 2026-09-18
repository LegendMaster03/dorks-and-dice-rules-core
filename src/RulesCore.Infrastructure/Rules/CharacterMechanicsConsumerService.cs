using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class CharacterMechanicsConsumerService(RulesCoreDbContext dbContext)
    : ICharacterMechanicsConsumerService
{
    private readonly IResolvedRulesCatalogService resolvedRules =
        new ResolvedRulesCatalogService(dbContext);
    private const int PageSize = 500;

    public async Task<CharacterMechanicsCatalogView> GetGlobalAsync(
        string? userId,
        bool includeUnavailable = false,
        CancellationToken cancellationToken = default)
    {
        var rules = await ReadAllGlobalRulesAsync(userId, cancellationToken);
        return await BuildCatalogAsync(rules, includeUnavailable, cancellationToken);
    }

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

        var rules = await ReadAllCampaignRulesAsync(campaignId, userId.Trim(), cancellationToken);
        return await BuildCatalogAsync(rules, includeUnavailable, cancellationToken);
    }

    public async Task<CharacterMechanicEvaluationView?> EvaluateGlobalAsync(
        string mechanicKey,
        CharacterMechanicEvaluationRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetGlobalAsync(userId, includeUnavailable: false, cancellationToken);
        return EvaluateFromCatalog(catalog, mechanicKey, request);
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
        return EvaluateFromCatalog(catalog, mechanicKey, request);
    }

    private async Task<CharacterMechanicsCatalogView> BuildCatalogAsync(
        ResolvedRulesCatalogView rules,
        bool includeUnavailable,
        CancellationToken cancellationToken)
    {
        var effectivePackageKeys = rules.Rules
            .Select(value => value.PackageKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var providerByPackageKey = await ReadProvidersAsync(rules.Rules, cancellationToken);
        var sourceUriByRevision = await ReadSourceUrisAsync(rules.Rules, cancellationToken);
        var publicationByRevision = await ReadPublicationAttributionsAsync(
            rules.Rules,
            cancellationToken);
        var editions = await ReadEffectiveEditionsAsync(
            rules.Rules,
            publicationByRevision,
            cancellationToken);

        var mechanics = new List<CharacterMechanicView>();

        foreach (var definition in KnownCharacterMechanics.All)
        {
            var applicable = IsApplicable(definition, editions, effectivePackageKeys);
            if (!applicable && !includeUnavailable)
            {
                continue;
            }

            mechanics.Add(ToStaticView(definition, applicable));
        }

        var relationshipViews = await BuildCompetencyRelationshipsAsync(
            rules.Rules,
            cancellationToken);
        var relationshipByMechanic = relationshipViews
            .SelectMany(value => new[] { value.ParentMechanicKey }.Concat(value.ComponentMechanicKeys)
                .Select(key => new { key, relationship = value }))
            .GroupBy(value => value.key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CharacterMechanicRelationshipView>)group
                    .Select(value => value.relationship)
                    .OrderBy(value => value.RelationshipKey, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        foreach (var rule in rules.Rules
                     .Where(IsCompetencyRule)
                     .OrderBy(value => value.ConceptKey, StringComparer.Ordinal))
        {
            var mechanicKey = CompetencyMechanicKey(rule.ConceptKey);
            relationshipByMechanic.TryGetValue(mechanicKey, out var relationships);
            relationships ??= [];

            var derivation = relationships.SingleOrDefault(value =>
                string.Equals(value.ParentMechanicKey, mechanicKey, StringComparison.Ordinal)
                && string.Equals(
                    value.EffectiveResolutionKind,
                    MechanicalRelationshipResolutionKinds.DeriveParent,
                    StringComparison.Ordinal)
                && value.CanResolve);

            var inputs = derivation is null
                ? (IReadOnlyList<CharacterMechanicInputView>)
                    [new CharacterMechanicInputView(
                        "value",
                        CharacterMechanicInputValueKinds.Integer,
                        CharacterMechanicInputOrigins.CharacterState,
                        Required: true,
                        ParticipatesInValue: true,
                        DefaultInteger: null)]
                : derivation.ComponentMechanicKeys
                    .Select(value => new CharacterMechanicInputView(
                        ConceptKeyFromCompetencyMechanicKey(value),
                        CharacterMechanicInputValueKinds.Integer,
                        CharacterMechanicInputOrigins.CharacterState,
                        Required: true,
                        ParticipatesInValue: true,
                        DefaultInteger: null))
                    .ToArray();

            var sourceUri = sourceUriByRevision.GetValueOrDefault(rule.SourceEntityRevisionId);
            var provider = providerByPackageKey.GetValueOrDefault(rule.PackageKey)
                ?? rule.PackageDisplayName;
            mechanics.Add(new CharacterMechanicView(
                mechanicKey,
                CharacterMechanicKinds.Competency,
                rule.DisplayName,
                rule.ConceptKey,
                rule.RuleConceptId,
                IsApplicableUnderRuleset: true,
                new CharacterMechanicApplicabilityView(
                    CharacterMechanicApplicabilityKinds.Always,
                    RequiresCharacterState: true,
                    EditionKeys: [],
                    SourcePackageKey: rule.PackageKey),
                derivation is null
                    ? CharacterMechanicEvaluationKinds.SourceValue
                    : CharacterMechanicEvaluationKinds.CompositeCompetency,
                CanEvaluate: true,
                Constant: 0,
                TargetInputKey: null,
                BaseMechanicKey: null,
                inputs,
                relationships,
                ConditionalRollRules: [],
                BooleanRequirements: [],
                SourceAttributions: BuildRuleAttributions(
                    rule,
                    provider,
                    sourceUri,
                    publicationByRevision.GetValueOrDefault(
                        rule.SourceEntityRevisionId,
                        []))));
        }

        mechanics = AttachStaticRelationships(mechanics);

        return new CharacterMechanicsCatalogView(
            rules.Scope,
            rules.CampaignId,
            rules.RevisionNumber,
            rules.PublishedAt,
            mechanics
                .OrderBy(value => value.Kind, StringComparer.Ordinal)
                .ThenBy(value => value.DisplayName, StringComparer.Ordinal)
                .ThenBy(value => value.MechanicKey, StringComparer.Ordinal)
                .ToArray());
    }

    private static CharacterMechanicEvaluationView? EvaluateFromCatalog(
        CharacterMechanicsCatalogView catalog,
        string mechanicKey,
        CharacterMechanicEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalizedKey = RequireMechanicKey(mechanicKey);
        var mechanic = catalog.Mechanics.SingleOrDefault(value =>
            string.Equals(value.MechanicKey, normalizedKey, StringComparison.OrdinalIgnoreCase));
        if (mechanic is null || !mechanic.IsApplicableUnderRuleset)
        {
            return null;
        }

        var integerInputs = request.IntegerInputs
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        var booleanInputs = request.BooleanInputs
            ?? new Dictionary<string, bool>(StringComparer.Ordinal);
        var stringInputs = request.StringInputs
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        var known = KnownCharacterMechanics.FindByKey(mechanic.MechanicKey);
        if (known is not null)
        {
            var evaluation = CharacterMechanicEvaluator.Evaluate(
                known,
                integerInputs,
                booleanInputs,
                stringInputs);
            return ToEvaluationView(mechanic.MechanicKey, known.EvaluationKind, evaluation);
        }

        if (mechanic.ConceptKey is null)
        {
            throw new InvalidOperationException(
                $"Mechanic '{mechanic.MechanicKey}' has no evaluatable definition or rule concept.");
        }

        var composite = mechanic.Relationships.SingleOrDefault(value =>
            string.Equals(value.ParentMechanicKey, mechanic.MechanicKey, StringComparison.Ordinal)
            && string.Equals(value.Kind, MechanicalRelationshipKinds.CompositeSkill, StringComparison.Ordinal)
            && string.Equals(
                value.EffectiveResolutionKind,
                MechanicalRelationshipResolutionKinds.DeriveParent,
                StringComparison.Ordinal)
            && value.CanResolve);
        if (composite is not null)
        {
            var definition = KnownMechanicalRelationships.FindByKey(composite.RelationshipKey)
                ?? throw new InvalidOperationException(
                    $"Mechanical relationship '{composite.RelationshipKey}' is not registered.");
            var componentValues = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var component in definition.Components)
            {
                if (!integerInputs.TryGetValue(component.ConceptKey, out var value))
                {
                    throw new KeyNotFoundException(
                        $"Composite competency '{mechanic.MechanicKey}' requires integer input '{component.ConceptKey}'.");
                }
                componentValues.Add(component.ConceptKey, value);
            }

            var modifiers = (request.Modifiers ?? [])
                .Select(value => new CompetencyModifier(value.TargetConceptKey, value.Value))
                .ToArray();
            var evaluation = CompositeCompetencyEvaluator.Evaluate(
                definition,
                componentValues,
                modifiers);
            return new CharacterMechanicEvaluationView(
                mechanic.MechanicKey,
                CharacterMechanicEvaluationKinds.CompositeCompetency,
                evaluation.ParentValue,
                Target: null,
                MeetsTarget: null,
                RequirementsSatisfied: true,
                UnsatisfiedRequirementKeys: [],
                AppliedRollRules: []);
        }

        if (!integerInputs.TryGetValue("value", out var sourceValue))
        {
            throw new KeyNotFoundException(
                $"Competency mechanic '{mechanic.MechanicKey}' requires integer input 'value'.");
        }

        return new CharacterMechanicEvaluationView(
            mechanic.MechanicKey,
            CharacterMechanicEvaluationKinds.SourceValue,
            sourceValue,
            Target: null,
            MeetsTarget: null,
            RequirementsSatisfied: true,
            UnsatisfiedRequirementKeys: [],
            AppliedRollRules: []);
    }

    private async Task<IReadOnlyList<CharacterMechanicRelationshipView>> BuildCompetencyRelationshipsAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var byConceptKey = rules
            .Where(IsCompetencyRule)
            .GroupBy(value => value.ConceptKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var result = new List<CharacterMechanicRelationshipView>();
        var service = new MechanicalRelationshipService(dbContext);
        foreach (var definition in KnownMechanicalRelationships.All)
        {
            var references = new[] { definition.Parent }.Concat(definition.Components).ToArray();
            var present = references
                .Where(value => byConceptKey.ContainsKey(value.ConceptKey))
                .ToArray();
            if (present.Length == 0)
            {
                continue;
            }

            var current = byConceptKey[present[0].ConceptKey];
            var recommendation = (await service.GetForConceptAsync(
                    current.RuleConceptId,
                    cancellationToken))
                .Single(value => string.Equals(
                    value.Relationship.Key,
                    definition.Key,
                    StringComparison.Ordinal));

            var missing = references
                .Where(value => !byConceptKey.ContainsKey(value.ConceptKey))
                .Select(value => CompetencyMechanicKey(value.ConceptKey))
                .ToArray();
            var canResolve = missing.Length == 0
                && string.Equals(
                    recommendation.EffectiveResolutionKind,
                    MechanicalRelationshipResolutionKinds.DeriveParent,
                    StringComparison.Ordinal);

            result.Add(new CharacterMechanicRelationshipView(
                definition.Key,
                definition.Kind,
                CompetencyMechanicKey(definition.Parent.ConceptKey),
                definition.Components
                    .Select(value => CompetencyMechanicKey(value.ConceptKey))
                    .ToArray(),
                definition.Composition,
                definition.Direction,
                recommendation.EffectiveResolutionKind,
                canResolve,
                missing));
        }

        return result;
    }

    private static List<CharacterMechanicView> AttachStaticRelationships(
        List<CharacterMechanicView> mechanics)
    {
        var byKey = mechanics.ToDictionary(value => value.MechanicKey, StringComparer.Ordinal);
        foreach (var definition in KnownCharacterMechanics.Relationships)
        {
            var memberKeys = new[] { definition.ParentMechanicKey }
                .Concat(definition.ComponentMechanicKeys)
                .ToArray();
            if (!memberKeys.Any(byKey.ContainsKey))
            {
                continue;
            }

            var missing = memberKeys.Where(value => !byKey.ContainsKey(value)).ToArray();
            var relationship = new CharacterMechanicRelationshipView(
                definition.Key,
                definition.Kind,
                definition.ParentMechanicKey,
                definition.ComponentMechanicKeys,
                definition.Composition,
                definition.Direction,
                EffectiveResolutionKind: null,
                CanResolve: missing.Length == 0,
                MissingMechanicKeys: missing);

            foreach (var key in memberKeys.Where(byKey.ContainsKey))
            {
                var current = byKey[key];
                var updated = current with
                {
                    Relationships = current.Relationships
                        .Concat([relationship])
                        .OrderBy(value => value.RelationshipKey, StringComparer.Ordinal)
                        .ToArray()
                };
                byKey[key] = updated;
                var index = mechanics.FindIndex(value =>
                    string.Equals(value.MechanicKey, key, StringComparison.Ordinal));
                mechanics[index] = updated;
            }
        }

        return mechanics;
    }

    private static CharacterMechanicView ToStaticView(
        CharacterMechanicDefinition definition,
        bool applicable)
    {
        var sourceAttributions = definition.Source is null
            ? (IReadOnlyList<CharacterMechanicSourceAttributionView>)[]
            :
            [
                new CharacterMechanicSourceAttributionView(
                    definition.Source.PackageKey,
                    definition.Source.PackageDisplayName,
                    definition.Source.Provider,
                    null,
                    null,
                    definition.Source.WorkKey,
                    definition.Source.WorkDisplayName,
                    definition.Source.GameEdition,
                    definition.Source.ReleaseKind,
                    definition.Source.PublicationDate,
                    definition.Source.WorkKey,
                    definition.Source.WorkDisplayName,
                    definition.Source.ReferenceUri,
                    definition.Source.PresentationRequired,
                    definition.Source.ReferenceLinkRequired)
            ];

        return new CharacterMechanicView(
            definition.Key,
            definition.Kind,
            definition.DisplayName,
            ConceptKey: null,
            RuleConceptId: null,
            applicable,
            new CharacterMechanicApplicabilityView(
                definition.Applicability.Kind,
                definition.Applicability.RequiresCharacterState,
                definition.Applicability.EditionKeys,
                definition.Applicability.SourcePackageKey),
            definition.EvaluationKind,
            applicable
                && definition.EvaluationKind != CharacterMechanicEvaluationKinds.None,
            definition.Constant,
            definition.TargetInputKey,
            definition.BaseMechanicKey,
            definition.Inputs.Select(value => new CharacterMechanicInputView(
                value.Key,
                value.ValueKind,
                value.Origin,
                value.Required,
                value.ParticipatesInValue,
                value.DefaultInteger)).ToArray(),
            Relationships: [],
            definition.ConditionalRollRules.Select(value => new CharacterMechanicConditionalRollRuleView(
                value.Key,
                value.BooleanInputKey,
                value.WhenValue,
                value.RollMode,
                value.TargetMechanicKeys)).ToArray(),
            definition.BooleanRequirements.Select(value => new CharacterMechanicBooleanRequirementView(
                value.InputKey,
                value.ExpectedValue)).ToArray(),
            sourceAttributions);
    }

    private static CharacterMechanicEvaluationView ToEvaluationView(
        string mechanicKey,
        string evaluationKind,
        CharacterMechanicEvaluation evaluation) =>
        new(
            mechanicKey,
            evaluationKind,
            evaluation.Value,
            evaluation.Target,
            evaluation.MeetsTarget,
            evaluation.RequirementsSatisfied,
            evaluation.UnsatisfiedRequirementKeys,
            evaluation.AppliedRollRules.Select(value => new CharacterMechanicAppliedRollRuleView(
                value.Key,
                value.RollMode,
                value.TargetMechanicKeys)).ToArray());

    private static bool IsApplicable(
        CharacterMechanicDefinition definition,
        IReadOnlySet<string> effectiveEditions,
        IReadOnlySet<string> effectivePackageKeys) =>
        definition.Applicability.Kind switch
        {
            CharacterMechanicApplicabilityKinds.Always => true,
            CharacterMechanicApplicabilityKinds.RulesetEdition =>
                definition.Applicability.EditionKeys.Any(effectiveEditions.Contains),
            CharacterMechanicApplicabilityKinds.AccessibleSource =>
                definition.Applicability.SourcePackageKey is string packageKey
                && effectivePackageKeys.Contains(packageKey),
            _ => false
        };

    private async Task<IReadOnlySet<string>> ReadEffectiveEditionsAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        IReadOnlyDictionary<Guid, IReadOnlyList<PublicationAttribution>> publicationByRevision,
        CancellationToken cancellationToken)
    {
        var editions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var publication in publicationByRevision.Values.SelectMany(value => value))
        {
            if (publication.GameEdition is not null
                && (string.Equals(publication.GameEdition, "3e", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(publication.GameEdition, "3.5e", StringComparison.OrdinalIgnoreCase)))
            {
                editions.Add(publication.GameEdition);
            }
        }

        foreach (var rule in rules)
        {
            if (string.Equals(rule.SourceCode, "SRD3", StringComparison.OrdinalIgnoreCase))
            {
                editions.Add("3e");
            }
            else if (string.Equals(rule.SourceCode, "SRD35", StringComparison.OrdinalIgnoreCase))
            {
                editions.Add("3.5e");
            }
        }

        var revisionIds = rules.Select(value => value.SourceEntityRevisionId).Distinct().ToArray();
        if (revisionIds.Length == 0)
        {
            return editions;
        }

        var documents = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => revisionIds.Contains(value.Id))
            .Select(value => new
            {
                value.ContentJson,
                value.RawJson
            })
            .ToArrayAsync(cancellationToken);
        foreach (var value in documents)
        {
            TryAddEdition(value.ContentJson, editions);
            TryAddEdition(value.RawJson, editions);
        }

        return editions;
    }

    private static void TryAddEdition(string? json, ISet<string> editions)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("_rulesCore", out var rulesCore)
                || rulesCore.ValueKind != JsonValueKind.Object
                || !rulesCore.TryGetProperty("context", out var context)
                || context.ValueKind != JsonValueKind.Object
                || !context.TryGetProperty("edition", out var edition)
                || edition.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(edition.GetString()))
            {
                return;
            }

            var normalized = edition.GetString()!.Trim();
            if (string.Equals(normalized, "3e", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "3.5e", StringComparison.OrdinalIgnoreCase))
            {
                editions.Add(normalized);
            }
        }
        catch (JsonException)
        {
            // Source validation owns malformed JSON. Applicability detection remains conservative.
        }
    }

    private static IReadOnlyList<CharacterMechanicSourceAttributionView> BuildRuleAttributions(
        ResolvedRuleCatalogItemView rule,
        string provider,
        string? sourceUri,
        IReadOnlyList<PublicationAttribution> publications)
    {
        if (publications.Count == 0)
        {
            return
            [
                new CharacterMechanicSourceAttributionView(
                    rule.PackageKey,
                    rule.PackageDisplayName,
                    provider,
                    rule.SourceCode,
                    rule.SourceRevisionNumber,
                    WorkKey: null,
                    WorkDisplayName: null,
                    GameEdition: null,
                    ReleaseKind: null,
                    PublicationDate: null,
                    ReferenceKey: null,
                    ReferenceTitle: rule.SourceEntityName,
                    ReferenceUri: sourceUri,
                    PresentationRequired: false,
                    ReferenceLinkRequired: false)
            ];
        }

        return publications
            .OrderBy(value => value.WorkDisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.WorkKey, StringComparer.Ordinal)
            .Select(value => new CharacterMechanicSourceAttributionView(
                rule.PackageKey,
                rule.PackageDisplayName,
                provider,
                rule.SourceCode,
                rule.SourceRevisionNumber,
                value.WorkKey,
                value.WorkDisplayName,
                value.GameEdition,
                value.ReleaseKind,
                value.PublicationDate,
                ReferenceKey: null,
                ReferenceTitle: rule.SourceEntityName,
                ReferenceUri: sourceUri,
                PresentationRequired: false,
                ReferenceLinkRequired: false))
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PublicationAttribution>>> ReadPublicationAttributionsAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var revisionIds = rules.Select(value => value.SourceEntityRevisionId).Distinct().ToArray();
        if (revisionIds.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<PublicationAttribution>>();
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT
                    binding.source_entity_revision_id,
                    publication.canonical_key,
                    publication.display_name,
                    publication.game_edition,
                    publication.release_kind,
                    publication.publication_date
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                WHERE binding.source_entity_revision_id = ANY(@revision_ids);
                """;
            AddParameter(command, "@revision_ids", revisionIds);

            var values = new Dictionary<Guid, List<PublicationAttribution>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var revisionId = reader.GetGuid(0);
                if (!values.TryGetValue(revisionId, out var publications))
                {
                    publications = [];
                    values.Add(revisionId, publications);
                }

                publications.Add(new PublicationAttribution(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5)));
            }

            return values.ToDictionary(
                value => value.Key,
                value => (IReadOnlyList<PublicationAttribution>)value.Value
                    .Distinct()
                    .ToArray());
        }
        catch (DbException)
        {
            return new Dictionary<Guid, IReadOnlyList<PublicationAttribution>>();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> ReadProvidersAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var packageKeys = rules.Select(value => value.PackageKey).Distinct().ToArray();
        if (packageKeys.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value => packageKeys.Contains(value.Key))
            .ToDictionaryAsync(
                value => value.Key,
                value => value.Provider,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> ReadSourceUrisAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var revisionIds = rules.Select(value => value.SourceEntityRevisionId).Distinct().ToArray();
        if (revisionIds.Length == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        return await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => revisionIds.Contains(value.Id))
            .Select(value => new
            {
                value.Id,
                value.SourceRepresentation.SourceUri
            })
            .ToDictionaryAsync(
                value => value.Id,
                value => value.SourceUri,
                cancellationToken);
    }

    private async Task<ResolvedRulesCatalogView> ReadAllGlobalRulesAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var all = new List<ResolvedRuleCatalogItemView>();
        ResolvedRulesCatalogView? page = null;
        for (var offset = 0; ; offset += PageSize)
        {
            page = await resolvedRules.GetGlobalPageAsync(
                userId,
                limit: PageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            all.AddRange(page.Rules);
            if (page.Rules.Count < PageSize)
            {
                break;
            }
        }

        page ??= new ResolvedRulesCatalogView("global", null, null, null, []);
        return page with { Rules = all };
    }

    private async Task<ResolvedRulesCatalogView> ReadAllCampaignRulesAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken)
    {
        var all = new List<ResolvedRuleCatalogItemView>();
        ResolvedRulesCatalogView? page = null;
        for (var offset = 0; ; offset += PageSize)
        {
            page = await resolvedRules.GetCampaignPageAsync(
                campaignId,
                userId,
                limit: PageSize,
                offset: offset,
                cancellationToken: cancellationToken);
            all.AddRange(page.Rules);
            if (page.Rules.Count < PageSize)
            {
                break;
            }
        }

        page ??= new ResolvedRulesCatalogView("campaign", campaignId, null, null, []);
        return page with { Rules = all };
    }

    private static bool IsCompetencyRule(ResolvedRuleCatalogItemView value) =>
        string.Equals(value.EntityType, "skill", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value.EntityType, "tool", StringComparison.OrdinalIgnoreCase);

    private static string CompetencyMechanicKey(string conceptKey) =>
        $"competency.{conceptKey.Trim().ToLowerInvariant()}";

    private static string ConceptKeyFromCompetencyMechanicKey(string mechanicKey)
    {
        const string prefix = "competency.";
        if (!mechanicKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Mechanic key '{mechanicKey}' is not a competency mechanic key.",
                nameof(mechanicKey));
        }
        return mechanicKey[prefix.Length..];
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record PublicationAttribution(
        string WorkKey,
        string WorkDisplayName,
        string? GameEdition,
        string? ReleaseKind,
        DateOnly? PublicationDate);

    private static string RequireMechanicKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Mechanic key can not be blank.", nameof(value));
        }
        var normalized = value.Trim();
        if (normalized.Length > 400)
        {
            throw new ArgumentException("Mechanic key can not exceed 400 characters.", nameof(value));
        }
        return normalized;
    }
}
