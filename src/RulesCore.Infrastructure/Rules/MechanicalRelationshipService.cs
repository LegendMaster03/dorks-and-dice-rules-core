using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class MechanicalRelationshipService(RulesCoreDbContext dbContext)
{
    private RelationshipReadSnapshot? readSnapshot;

    public async Task<IReadOnlyList<MechanicalRelationshipRecommendationView>> GetForConceptAsync(
        Guid ruleConceptId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty)
        {
            throw new ArgumentException("Rule concept ID can not be empty.", nameof(ruleConceptId));
        }

        var snapshot = await GetReadSnapshotAsync(cancellationToken);
        if (!snapshot.ConceptsById.TryGetValue(ruleConceptId, out var concept))
        {
            return [];
        }

        var definitions = KnownMechanicalRelationships.FindAllByConceptKey(concept.Key);
        if (definitions.Count == 0)
        {
            return [];
        }

        return definitions
            .Select(definition => BuildRecommendation(definition, concept.Key, snapshot))
            .ToArray();
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

        readSnapshot = null;
        var snapshot = await GetReadSnapshotAsync(cancellationToken);
        return BuildRecommendation(
            definition,
            definition.Parent.ConceptKey,
            snapshot);
    }

    private async Task<RelationshipReadSnapshot> GetReadSnapshotAsync(
        CancellationToken cancellationToken)
    {
        if (readSnapshot is not null)
        {
            return readSnapshot;
        }

        var conceptKeys = KnownMechanicalRelationships.All
            .SelectMany(definition => new[] { definition.Parent }
                .Concat(definition.Components))
            .Select(reference => reference.ConceptKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var concepts = await dbContext.RuleConcepts
            .AsNoTracking()
            .Where(value => conceptKeys.Contains(value.Key))
            .Select(value => new ConceptSnapshot(value.Id, value.Key))
            .ToArrayAsync(cancellationToken);
        var conceptsById = concepts.ToDictionary(value => value.Id);
        var conceptsByKey = concepts.ToDictionary(value => value.Key, StringComparer.Ordinal);
        var conceptIds = concepts.Select(value => value.Id).ToArray();

        var boundConceptIds = new HashSet<Guid>();
        var decidedConceptIds = new HashSet<Guid>();
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

        var relationshipKeys = KnownMechanicalRelationships.All
            .Select(value => value.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var latestRulings = await ReadLatestRulingsAsync(
            relationshipKeys,
            cancellationToken);

        readSnapshot = new RelationshipReadSnapshot(
            conceptsById,
            conceptsByKey,
            boundConceptIds,
            decidedConceptIds,
            latestRulings);
        return readSnapshot;
    }

    private static MechanicalRelationshipRecommendationView BuildRecommendation(
        MechanicalRelationshipDefinition definition,
        string currentConceptKey,
        RelationshipReadSnapshot snapshot)
    {
        var references = new[] { definition.Parent }
            .Concat(definition.Components)
            .ToArray();

        MechanicalRelationshipCompetencyStateView State(MechanicalCompetencyReference reference)
        {
            snapshot.ConceptsByKey.TryGetValue(reference.ConceptKey, out var concept);
            return new MechanicalRelationshipCompetencyStateView(
                concept?.Id,
                reference.ConceptKey,
                reference.EntityType,
                reference.DisplayName,
                concept is not null && snapshot.BoundConceptIds.Contains(concept.Id),
                concept is not null && snapshot.DecidedConceptIds.Contains(concept.Id));
        }

        var parent = State(definition.Parent);
        var components = definition.Components.Select(State).ToArray();
        var missing = references
            .Where(reference => !snapshot.ConceptsByKey.ContainsKey(reference.ConceptKey))
            .Select(reference => reference.ConceptKey)
            .ToArray();
        snapshot.LatestRulings.TryGetValue(definition.Key, out var latestRuling);
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

    private async Task<IReadOnlyDictionary<string, MechanicalRelationshipRulingView>> ReadLatestRulingsAsync(
        IReadOnlyCollection<string> relationshipKeys,
        CancellationToken cancellationToken)
    {
        if (relationshipKeys.Count == 0)
        {
            return new Dictionary<string, MechanicalRelationshipRulingView>(StringComparer.Ordinal);
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
                SELECT DISTINCT ON (relationship_key)
                    rule_mechanical_relationship_ruling_id,
                    relationship_key,
                    ruling_number,
                    resolution_kind,
                    note,
                    created_by_user_id,
                    created_at
                FROM rule_mechanical_relationship_ruling
                WHERE relationship_key = ANY(@relationship_keys)
                ORDER BY relationship_key, ruling_number DESC;
                """;
            AddParameter(command, "@relationship_keys", relationshipKeys.ToArray());
            var result = new Dictionary<string, MechanicalRelationshipRulingView>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var ruling = new MechanicalRelationshipRulingView(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetString(5),
                    reader.GetFieldValue<DateTimeOffset>(6));
                result[ruling.RelationshipKey] = ruling;
            }
            return result;
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

    private sealed record ConceptSnapshot(Guid Id, string Key);

    private sealed record RelationshipReadSnapshot(
        IReadOnlyDictionary<Guid, ConceptSnapshot> ConceptsById,
        IReadOnlyDictionary<string, ConceptSnapshot> ConceptsByKey,
        HashSet<Guid> BoundConceptIds,
        HashSet<Guid> DecidedConceptIds,
        IReadOnlyDictionary<string, MechanicalRelationshipRulingView> LatestRulings);
}
