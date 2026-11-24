using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Resolves one replacement load for each effective characteristic or linked
/// bundle. The containing ledger owns exclusivity and additive interactions.
/// These selectors never inspect latent genes, raw donor offers, or player
/// authorization claims.
/// </summary>
internal static class NativeOriginAllocationPriceResolver
{
    internal sealed record Selection(int? Load, string? ConfigurationId = null,
        bool Ambiguous = false);

    // A missing property is not the same as a missing effective payload.
    // In particular, the latter must not match a cheaper "Absent" tier.
    private enum ReadStatus { Found, Absent, Unresolved }

    /// <summary>
    /// A zero load is legal only for inherent physical identity, ordinary walking,
    /// or the derived Sunlight Sensitivity limitation. A zero price can never
    /// be used as a generic catch-all for an unknown benefit.
    /// </summary>
    internal static bool PermitsZero(IReadOnlyList<string> memberKeys) =>
        memberKeys is { Count: 1 } &&
        memberKeys[0] is "intrinsic:creatureType" or "intrinsic:size" or
            "intrinsic:walkingSpeedFeet" or "trait:trait.sunlight-sensitivity";

    internal static bool Valid(NativeOriginAllocationPrice entry)
    {
        if (entry.Configurations is null)
            return !entry.AllowUnmatchedConfigurationFallback;
        if (entry.Configurations.Count == 0 ||
            entry.Configurations.Any(x => x is null ||
                string.IsNullOrWhiteSpace(x.Id) || x.Load < 0 ||
                (x.Load == 0 && !PermitsZero(entry.MemberKeys)) ||
                x.Conditions is not { Count: > 0 } ||
                x.Conditions.Any(condition => condition is null ||
                    !entry.MemberKeys.Contains(condition.MemberKey,
                        StringComparer.Ordinal) ||
                    !ValidPath(condition.Path) ||
                    condition.Comparison is not
                        (NativeOriginAllocationComparison.Equals or
                         NativeOriginAllocationComparison.Present or
                         NativeOriginAllocationComparison.Absent) ||
                    (condition.Comparison == NativeOriginAllocationComparison.Equals
                        ? condition.ExpectedValue is not { ValueKind: not JsonValueKind.Undefined }
                        : condition.ExpectedValue is not null)) ||
                // Multiple assertions about one field are contradictory or
                // redundant, including Present and Absent on the same path.
                x.Conditions.Select(c => (c.MemberKey, c.Path))
                    .Distinct().Count() != x.Conditions.Count) ||
            entry.Configurations.Select(x => x.Id)
                .Distinct(StringComparer.Ordinal).Count() != entry.Configurations.Count)
            return false;
        return true;
    }

    internal static Selection Select(NativeCatalogSnapshot catalog,
        NativeOriginAllocationPrice entry,
        IReadOnlyDictionary<string, NativeOriginEffectiveGrant> grants)
    {
        if (entry.Configurations is null)
            return new(entry.Load);

        // An explicitly allowed fallback covers a known but unlisted variant,
        // not a missing effective payload or malformed persistent choices.
        // Otherwise an unknown ability could be mistaken for a cheap tier.
        if (entry.Configurations.SelectMany(x => x.Conditions).Any(condition =>
            grants.TryGetValue(condition.MemberKey, out var grant) &&
            ((condition.Path is "value" ||
                condition.Path.StartsWith("value/", StringComparison.Ordinal)) &&
                (grant.EffectiveValue is null ||
                 grant.EffectiveValue.Value.ValueKind == JsonValueKind.Undefined) ||
             (condition.Path is "definition" ||
                condition.Path.StartsWith("definition/", StringComparison.Ordinal)) &&
                Definition(catalog, grant) is null ||
             condition.Path.StartsWith("choices", StringComparison.Ordinal) &&
                grant.ResolvedChoices is { } choices &&
                choices.GroupBy(x => x.RequirementId, StringComparer.Ordinal)
                    .Any(x => x.Count() != 1))))
            return new(null);

        var matches = entry.Configurations.Where(configuration =>
            configuration.Conditions.All(condition =>
                grants.TryGetValue(condition.MemberKey, out var grant) &&
                Matches(catalog, grant, condition))).ToArray();
        if (matches.Length == 0)
        {
            // A different, well-formed configuration may use the generic
            // fallback. An unreadable nested selector is not evidence of a
            // legitimate unmatched configuration.
            if (entry.Configurations.SelectMany(x => x.Conditions).Any(condition =>
                grants.TryGetValue(condition.MemberKey, out var grant) &&
                Read(catalog, grant, condition.Path, out _) == ReadStatus.Unresolved))
                return new(null);
            return entry.AllowUnmatchedConfigurationFallback
                ? new(entry.Load) : new(null);
        }
        var highest = matches.Max(x => x.Priority);
        var winners = matches.Where(x => x.Priority == highest).ToArray();
        return winners.Length == 1
            ? new(winners[0].Load, winners[0].Id)
            : new(null, Ambiguous: true);
    }

    private static bool Matches(NativeCatalogSnapshot catalog,
        NativeOriginEffectiveGrant grant, NativeOriginAllocationPriceCondition condition)
    {
        var status = Read(catalog, grant, condition.Path, out var actual);
        return condition.Comparison switch
        {
            NativeOriginAllocationComparison.Present => status == ReadStatus.Found,
            NativeOriginAllocationComparison.Absent => status == ReadStatus.Absent,
            NativeOriginAllocationComparison.Equals => status == ReadStatus.Found &&
                condition.ExpectedValue is { } expected &&
                JsonElement.DeepEquals(actual, expected),
            _ => false
        };
    }

    /// <summary>
    /// "value" is the actual post-reconciliation value; "parameters" are
    /// the final bound values, not authored symbolic references. Choices retain
    /// their complete option lists. Resource owners are exposed by presence,
    /// never by unstable grant IDs. Definition is read only from the canonical
    /// catalog, allowing complete progression to be matched explicitly.
    /// </summary>
    private static ReadStatus Read(NativeCatalogSnapshot catalog,
        NativeOriginEffectiveGrant grant, string path, out JsonElement actual)
    {
        actual = default;
        var parts = path.Split('/').Select(Unescape).ToArray();
        if (parts[0] == "choices" && grant.ResolvedChoices is { } choices &&
            choices.GroupBy(x => x.RequirementId, StringComparer.Ordinal)
                .Any(x => x.Count() != 1))
            return ReadStatus.Unresolved;
        JsonElement? root = parts[0] switch
        {
            "value" => grant.EffectiveValue,
            "parameters" => grant.Parameters is null ? null :
                JsonSerializer.SerializeToElement(grant.Parameters),
            "choices" => Choices(grant),
            "resourceOwners" => grant.ResourceOwners is null ? null :
                JsonSerializer.SerializeToElement(grant.ResourceOwners.Keys
                    .ToDictionary(key => key, _ => true, StringComparer.Ordinal)),
            "definition" => Definition(catalog, grant),
            _ => null
        };
        if (root is not { } current ||
            current.ValueKind is JsonValueKind.Undefined)
            return parts[0] is "value" or "definition"
                ? ReadStatus.Unresolved : ReadStatus.Absent;
        foreach (var part in parts.Skip(1))
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(part, out var property))
                    return ReadStatus.Absent;
                current = property;
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                // Only canonical zero-based indices are recognized. "01",
                // "+1", and whitespace cannot alias another selector path.
                if (!int.TryParse(part, out var index) || index < 0 ||
                    part != index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    return ReadStatus.Unresolved;
                if (index >= current.GetArrayLength())
                    return ReadStatus.Absent;
                current = current[index];
            }
            else
                return ReadStatus.Unresolved;
        }
        actual = current;
        return ReadStatus.Found;
    }

    private static JsonElement? Choices(NativeOriginEffectiveGrant grant)
    {
        if (grant.ResolvedChoices is null)
            return null;
        if (grant.ResolvedChoices.GroupBy(x => x.RequirementId,
                StringComparer.Ordinal).Any(x => x.Count() != 1))
            return null;
        return JsonSerializer.SerializeToElement(grant.ResolvedChoices
            .ToDictionary(x => x.RequirementId,
                x => x.SelectedOptionIds, StringComparer.Ordinal));
    }

    private static JsonElement? Definition(NativeCatalogSnapshot catalog,
        NativeOriginEffectiveGrant grant)
    {
        if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
            grant.Identity.MechanicalDefinitionId is not { } id ||
            !catalog.TryGetById(id, out var entity) || entity is null ||
            entity.Category != "origin-trait" ||
            !entity.Definition.TryGetProperty("trait", out var definition))
            return null;
        return definition;
    }

    private static bool ValidPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var parts = path.Split('/');
        if (parts.Any(string.IsNullOrEmpty) ||
            parts[0] is not ("value" or "parameters" or "choices" or
                "resourceOwners" or "definition"))
            return false;
        // JSON pointer escaping is explicit; reject an unescaped tilde rather
        // than accidentally matching a different authored property.
        foreach (var part in parts)
            for (var i = 0; i < part.Length; i++)
                if (part[i] == '~' &&
                    (++i >= part.Length || part[i] is not ('0' or '1')))
                    return false;
        return true;
    }

    private static string Unescape(string part) =>
        part.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);
}
