using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

public sealed class RuleSemanticComparisonService(RulesCoreDbContext dbContext)
    : IRuleSemanticComparisonService
{
    private static readonly HashSet<string> MetadataOnlyRootProperties = new(
        [
            "name", "source", "page", "id", "uniqueId", "reprintedAs", "otherSources",
            "additionalSources", "previousVersion", "previousVersions", "versions", "seeAlso",
            "edition", "srd", "srd52", "basicRules", "basicRules2024", "freeRules2024"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] ArrayIdentityProperties = ["name", "id", "key"];

    public Task<RuleSemanticComparisonView?> CompareAsync(
        RuleSemanticComparisonRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }

        return CompareCoreAsync(
            request.RuleConceptId,
            request.LeftSourceEntityRevisionId,
            request.RightSourceEntityRevisionId,
            userId.Trim(),
            cancellationToken);
    }

    public Task<RuleSemanticComparisonView?> CompareSourcesAsync(
        RuleSourceComparisonRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompareCoreAsync(
            request.RuleConceptId,
            request.LeftSourceEntityRevisionId,
            request.RightSourceEntityRevisionId,
            string.IsNullOrWhiteSpace(userId) ? null : userId.Trim(),
            cancellationToken);
    }

    private async Task<RuleSemanticComparisonView?> CompareCoreAsync(
        Guid ruleConceptId,
        Guid leftSourceEntityRevisionId,
        Guid rightSourceEntityRevisionId,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (ruleConceptId == Guid.Empty
            || leftSourceEntityRevisionId == Guid.Empty
            || rightSourceEntityRevisionId == Guid.Empty)
        {
            throw new ArgumentException("Rule concept and source revision IDs can not be empty.");
        }

        await CanonicalRuleBindingStore.EnsureSchemaAsync(dbContext, cancellationToken);
        var revisionIds = new[]
        {
            leftSourceEntityRevisionId,
            rightSourceEntityRevisionId
        };

        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Include(value => value.SourceEntity)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => revisionIds.Contains(value.Id)
                && (value.SourceEntity.SourcePackage.IsPublic
                    || (userId != null
                        && value.SourceEntity.SourcePackage.UserGrants
                            .Any(grant => grant.UserId == userId))))
            .ToArrayAsync(cancellationToken);

        if (revisions.Length != revisionIds.Distinct().Count())
        {
            return null;
        }

        foreach (var revision in revisions)
        {
            if (!await CanonicalRuleBindingStore.IsSourceEntityBoundAsync(
                    dbContext,
                    ruleConceptId,
                    revision.SourceEntityId,
                    cancellationToken))
            {
                return null;
            }
        }

        var left = revisions.Single(value => value.Id == leftSourceEntityRevisionId);
        var right = revisions.Single(value => value.Id == rightSourceEntityRevisionId);
        var leftJson = left.GetMechanicalContentJson();
        var rightJson = right.GetMechanicalContentJson();
        using var leftDocument = JsonDocument.Parse(leftJson);
        using var rightDocument = JsonDocument.Parse(rightJson);

        var differences = new List<RuleSemanticDifferenceView>();
        var unchangedCount = 0;
        CompareValue(
            leftDocument.RootElement,
            rightDocument.RootElement,
            "$",
            isRoot: true,
            differences,
            ref unchangedCount);

        var compatibility = RuleSemanticCompatibility.TryCreateAdditiveUnion(leftJson, [rightJson]);
        var metadataCount = differences.Count(value => value.Kind == RuleSemanticDifferenceKinds.MetadataOnly);
        var contradictionCount = differences.Count(value => value.RequiresDecision);
        var compatibleCount = differences.Count(value =>
            value.Kind is RuleSemanticDifferenceKinds.Addition
                or RuleSemanticDifferenceKinds.Omission
                or RuleSemanticDifferenceKinds.CompatibleAdditive);

        return new RuleSemanticComparisonView(
            ruleConceptId,
            left.Id,
            right.Id,
            unchangedCount,
            metadataCount,
            compatibleCount,
            contradictionCount,
            compatibility.Compatible,
            compatibility.Reason,
            differences);
    }

    private static void CompareValue(
        JsonElement left,
        JsonElement right,
        string path,
        bool isRoot,
        ICollection<RuleSemanticDifferenceView> differences,
        ref int unchangedCount)
    {
        if (JsonElement.DeepEquals(left, right))
        {
            unchangedCount++;
            return;
        }

        if (left.ValueKind == JsonValueKind.Object && right.ValueKind == JsonValueKind.Object)
        {
            CompareObjects(left, right, path, isRoot, differences, ref unchangedCount);
            return;
        }

        if (left.ValueKind == JsonValueKind.Array && right.ValueKind == JsonValueKind.Array)
        {
            CompareArrays(left, right, path, differences, ref unchangedCount);
            return;
        }

        differences.Add(new RuleSemanticDifferenceView(
            path,
            RuleSemanticDifferenceKinds.Contradiction,
            left.Clone(),
            right.Clone(),
            "Both source versions define this rule-bearing value differently.",
            RequiresDecision: true));
    }

    private static void CompareObjects(
        JsonElement left,
        JsonElement right,
        string path,
        bool isRoot,
        ICollection<RuleSemanticDifferenceView> differences,
        ref int unchangedCount)
    {
        var leftProperties = left.EnumerateObject().ToDictionary(value => value.Name, StringComparer.Ordinal);
        var rightProperties = right.EnumerateObject().ToDictionary(value => value.Name, StringComparer.Ordinal);
        foreach (var propertyName in leftProperties.Keys
                     .Union(rightProperties.Keys, StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            var propertyPath = path == "$" ? $"$.{propertyName}" : $"{path}.{propertyName}";
            var hasLeft = leftProperties.TryGetValue(propertyName, out var leftProperty);
            var hasRight = rightProperties.TryGetValue(propertyName, out var rightProperty);

            if (isRoot && MetadataOnlyRootProperties.Contains(propertyName))
            {
                if (!hasLeft || !hasRight || !JsonElement.DeepEquals(leftProperty.Value, rightProperty.Value))
                {
                    differences.Add(new RuleSemanticDifferenceView(
                        propertyPath,
                        RuleSemanticDifferenceKinds.MetadataOnly,
                        hasLeft ? leftProperty.Value.Clone() : null,
                        hasRight ? rightProperty.Value.Clone() : null,
                        "This is source identity or provenance metadata and does not change the resolved mechanic.",
                        RequiresDecision: false));
                }
                else
                {
                    unchangedCount++;
                }
                continue;
            }

            if (!hasLeft)
            {
                differences.Add(new RuleSemanticDifferenceView(
                    propertyPath,
                    RuleSemanticDifferenceKinds.Addition,
                    null,
                    rightProperty.Value.Clone(),
                    "This rule-bearing value exists only in the right source and can be preserved as an additive contribution when the rest of the rule is compatible.",
                    RequiresDecision: false));
                continue;
            }

            if (!hasRight)
            {
                differences.Add(new RuleSemanticDifferenceView(
                    propertyPath,
                    RuleSemanticDifferenceKinds.Omission,
                    leftProperty.Value.Clone(),
                    null,
                    "This rule-bearing value is omitted by the right source. Rules Core can preserve it when the omission is otherwise non-destructive.",
                    RequiresDecision: false));
                continue;
            }

            CompareValue(
                leftProperty.Value,
                rightProperty.Value,
                propertyPath,
                isRoot: false,
                differences,
                ref unchangedCount);
        }
    }

    private static void CompareArrays(
        JsonElement left,
        JsonElement right,
        string path,
        ICollection<RuleSemanticDifferenceView> differences,
        ref int unchangedCount)
    {
        if (TryGetIdentifiedItems(left, out var leftItems)
            && TryGetIdentifiedItems(right, out var rightItems))
        {
            var common = leftItems.Keys.Intersect(rightItems.Keys, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            var leftOrder = leftItems.Keys.Where(common.Contains).ToArray();
            var rightOrder = rightItems.Keys.Where(common.Contains).ToArray();
            if (!leftOrder.SequenceEqual(rightOrder, StringComparer.Ordinal))
            {
                differences.Add(new RuleSemanticDifferenceView(
                    path,
                    RuleSemanticDifferenceKinds.Contradiction,
                    left.Clone(),
                    right.Clone(),
                    "The source versions reorder shared named entries, so Rules Core can not choose an ordering automatically.",
                    RequiresDecision: true));
                return;
            }

            foreach (var identity in leftItems.Keys.Union(rightItems.Keys, StringComparer.Ordinal))
            {
                var itemPath = $"{path}[{identity}]";
                var hasLeft = leftItems.TryGetValue(identity, out var leftItem);
                var hasRight = rightItems.TryGetValue(identity, out var rightItem);
                if (!hasLeft)
                {
                    differences.Add(new RuleSemanticDifferenceView(
                        itemPath,
                        RuleSemanticDifferenceKinds.Addition,
                        null,
                        rightItem.Clone(),
                        "This named entry exists only in the right source and is a compatible additive candidate.",
                        RequiresDecision: false));
                }
                else if (!hasRight)
                {
                    differences.Add(new RuleSemanticDifferenceView(
                        itemPath,
                        RuleSemanticDifferenceKinds.Omission,
                        leftItem.Clone(),
                        null,
                        "This named entry exists only in the left source and can be retained if shared mechanics remain compatible.",
                        RequiresDecision: false));
                }
                else
                {
                    CompareValue(leftItem, rightItem, itemPath, isRoot: false, differences, ref unchangedCount);
                }
            }
            return;
        }

        if (IsOrderedSubset(left, right) || IsOrderedSubset(right, left))
        {
            differences.Add(new RuleSemanticDifferenceView(
                path,
                RuleSemanticDifferenceKinds.CompatibleAdditive,
                left.Clone(),
                right.Clone(),
                "One ordered array is an exact subset of the other, so the additional entries can be retained without replacing shared entries.",
                RequiresDecision: false));
            return;
        }

        differences.Add(new RuleSemanticDifferenceView(
            path,
            RuleSemanticDifferenceKinds.Contradiction,
            left.Clone(),
            right.Clone(),
            "This array contains incompatible replacements, reorderings, or anonymous additions that require adjudication.",
            RequiresDecision: true));
    }

    private static bool TryGetIdentifiedItems(
        JsonElement array,
        out IReadOnlyDictionary<string, JsonElement> items)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !TryGetIdentity(item, out var identity) || !result.TryAdd(identity, item.Clone()))
            {
                items = new Dictionary<string, JsonElement>();
                return false;
            }
        }
        items = result;
        return true;
    }

    private static bool TryGetIdentity(JsonElement value, out string identity)
    {
        foreach (var propertyName in ArrayIdentityProperties)
        {
            if (!value.TryGetProperty(propertyName, out var property)
                || property.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null)
            {
                continue;
            }
            identity = $"{propertyName}:{property.GetRawText()}";
            return true;
        }
        identity = string.Empty;
        return false;
    }

    private static bool IsOrderedSubset(JsonElement subset, JsonElement superset)
    {
        var subsetItems = subset.EnumerateArray().ToArray();
        var supersetItems = superset.EnumerateArray().ToArray();
        var next = 0;
        foreach (var subsetItem in subsetItems)
        {
            var matched = false;
            while (next < supersetItems.Length)
            {
                if (JsonElement.DeepEquals(subsetItem, supersetItems[next++]))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched) return false;
        }
        return true;
    }
}
