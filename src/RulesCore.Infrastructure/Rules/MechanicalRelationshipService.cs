using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class MechanicalRelationshipService(RulesCoreDbContext dbContext)
{
    public async Task<IReadOnlyList<MechanicalRelationshipRecommendationView>> GetForConceptAsync(
        Guid ruleConceptId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty)
        {
            throw new ArgumentException("Rule concept ID can not be empty.", nameof(ruleConceptId));
        }

        var concept = await dbContext.RuleConcepts
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == ruleConceptId, cancellationToken);
        if (concept is null)
        {
            return [];
        }

        var definitions = KnownMechanicalRelationships.FindAllByConceptKey(concept.Key);
        if (definitions.Count == 0)
        {
            return [];
        }

        var recommendations = new List<MechanicalRelationshipRecommendationView>(definitions.Count);
        foreach (var definition in definitions)
        {
            recommendations.Add(await BuildRecommendationAsync(
                definition,
                concept.Key,
                cancellationToken));
        }
        return recommendations;
    }

    public async Task<MechanicalRelationshipRecommendationView> SetRulingAsync(
        string relationshipKey,
        SetMechanicalRelationshipRulingRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = KnownMechanicalRelationships.FindByKey(relationshipKey)
            ?? throw new KeyNotFoundException($"Mechanical relationship '{relationshipKey}' does not exist.");
        var resolutionKind = RequireResolutionKind(request.ResolutionKind);
        var actor = RequireText(actorUserId, nameof(actorUserId), 200);
        var note = NormalizeOptional(request.Note, 2000, nameof(request.Note));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO rule_mechanical_relationship_ruling (
                rule_mechanical_relationship_ruling_id,
                relationship_key,
                ruling_number,
                resolution_kind,
                note,
                created_by_user_id,
                created_at)
            SELECT
                {{Guid.NewGuid()}},
                {{definition.Key}},
                COALESCE(MAX(ruling_number), 0) + 1,
                {{resolutionKind}},
                {{note}},
                {{actor}},
                {{DateTimeOffset.UtcNow}}
            FROM rule_mechanical_relationship_ruling
            WHERE relationship_key = {{definition.Key}};
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await BuildRecommendationAsync(
            definition,
            definition.Parent.ConceptKey,
            cancellationToken);
    }

    private async Task<MechanicalRelationshipRecommendationView> BuildRecommendationAsync(
        MechanicalRelationshipDefinition definition,
        string currentConceptKey,
        CancellationToken cancellationToken)
    {
        var references = new[] { definition.Parent }
            .Concat(definition.Components)
            .ToArray();
        var keys = references.Select(value => value.ConceptKey).ToArray();
        var concepts = await dbContext.RuleConcepts
            .AsNoTracking()
            .Where(value => keys.Contains(value.Key))
            .ToArrayAsync(cancellationToken);
        var conceptsByKey = concepts.ToDictionary(value => value.Key, StringComparer.Ordinal);
        var conceptIds = concepts.Select(value => value.Id).ToArray();

        HashSet<Guid> boundConceptIds = [];
        HashSet<Guid> decidedConceptIds = [];
        if (conceptIds.Length > 0)
        {
            boundConceptIds = (await dbContext.RuleConceptSourceBindings
                    .AsNoTracking()
                    .Where(value => conceptIds.Contains(value.RuleConceptId))
                    .Select(value => value.RuleConceptId)
                    .Distinct()
                    .ToArrayAsync(cancellationToken))
                .ToHashSet();
            decidedConceptIds = (await dbContext.GlobalRuleDecisions
                    .AsNoTracking()
                    .Where(value => conceptIds.Contains(value.RuleConceptId))
                    .Select(value => value.RuleConceptId)
                    .Distinct()
                    .ToArrayAsync(cancellationToken))
                .ToHashSet();
        }

        MechanicalRelationshipCompetencyStateView State(MechanicalCompetencyReference reference)
        {
            conceptsByKey.TryGetValue(reference.ConceptKey, out var concept);
            return new MechanicalRelationshipCompetencyStateView(
                concept?.Id,
                reference.ConceptKey,
                reference.EntityType,
                reference.DisplayName,
                concept is not null && boundConceptIds.Contains(concept.Id),
                concept is not null && decidedConceptIds.Contains(concept.Id));
        }

        var parent = State(definition.Parent);
        var components = definition.Components.Select(State).ToArray();
        var missing = references
            .Where(reference => !conceptsByKey.ContainsKey(reference.ConceptKey))
            .Select(reference => reference.ConceptKey)
            .ToArray();
        var latestRuling = await ReadLatestRulingAsync(definition.Key, cancellationToken);
        var recommended = MechanicalRelationshipResolutionKinds.DeriveParent;
        var effective = latestRuling?.ResolutionKind ?? recommended;
        var isOverridden = !string.Equals(effective, recommended, StringComparison.Ordinal);
        var canResolveStructurally = missing.Length == 0;
        var requiresAdjudication = parent.HasRuleBinding && components.Any(value => value.HasRuleBinding);
        var conceptRole = string.Equals(
            definition.Parent.ConceptKey,
            currentConceptKey,
            StringComparison.OrdinalIgnoreCase)
            ? "parent"
            : "component";

        var explanation = !canResolveStructurally
            ? "The consolidation is known, but one or more rule concepts are not present yet. Existing competencies remain independent until the full structure is available."
            : isOverridden
                ? "A Rules Lawyer ruling overrides the default composite recommendation. The granular competencies remain independently addressable."
                : requiresAdjudication
                    ? "The consolidation is structurally resolved from the granular competencies. Source-backed parent and component rules remain visible so mechanical conflicts can still be adjudicated."
                    : "The known consolidation can be resolved structurally without a Rules Lawyer rediscovering the relationship.";

        return new MechanicalRelationshipRecommendationView(
            new MechanicalRelationshipDefinitionView(
                definition.Key,
                definition.Kind,
                parent,
                components,
                definition.Composition,
                definition.Direction),
            conceptRole,
            recommended,
            effective,
            isOverridden,
            canResolveStructurally,
            requiresAdjudication,
            missing,
            latestRuling,
            explanation);
    }

    private async Task<MechanicalRelationshipRulingView?> ReadLatestRulingAsync(
        string relationshipKey,
        CancellationToken cancellationToken)
    {
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
                SELECT
                    rule_mechanical_relationship_ruling_id,
                    relationship_key,
                    ruling_number,
                    resolution_kind,
                    note,
                    created_by_user_id,
                    created_at
                FROM rule_mechanical_relationship_ruling
                WHERE relationship_key = @relationship_key
                ORDER BY ruling_number DESC
                LIMIT 1;
                """;
            AddParameter(command, "@relationship_key", relationshipKey);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new MechanicalRelationshipRulingView(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6));
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string RequireResolutionKind(string value)
    {
        var normalized = RequireText(value, nameof(value), 80);
        if (!MechanicalRelationshipResolutionKinds.All.Contains(normalized))
        {
            throw new ArgumentException($"Unknown mechanical relationship resolution kind '{normalized}'.", nameof(value));
        }
        return normalized;
    }

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
