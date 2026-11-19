using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A creature type grants ordinary origination-method defaults, not exclusive
/// permissions or a trait package. Missing defaults remain reviewable; they do
/// not prohibit an authored exception or an authenticated table decision.
/// </summary>
public enum NativeOriginMethodDefaultStatus { Available, NotGranted, Unresolved }

public sealed record NativeOriginMethodAvailability(
    NativeOriginMethodDefaultStatus Status, string? DefiningCreatureTypeId = null,
    IReadOnlyList<string>? DefaultMethodIds = null);

/// <summary>
/// Reads the selected native creature-type hierarchy without copying its method
/// registry into C#. The nearest authored list replaces the inherited default.
/// </summary>
public sealed class NativeOriginMethodAvailabilityEvaluator(NativeCatalogSnapshot catalog)
{
    public NativeOriginMethodAvailability Resolve(
        string creatureTypeId, NativeOriginMethodReference method)
    {
        if (string.IsNullOrWhiteSpace(creatureTypeId) || method is null ||
            new NativeOriginContractValidator(catalog).FindMethod(method) is null)
            return new(NativeOriginMethodDefaultStatus.Unresolved);

        var next = creatureTypeId.StartsWith("creature-type.", StringComparison.Ordinal)
            ? creatureTypeId : "creature-type." + creatureTypeId;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (visited.Add(next) && visited.Count <= 64)
        {
            if (!catalog.TryGetById(next, out var type) ||
                type?.Category != "creatureType")
                return new(NativeOriginMethodDefaultStatus.Unresolved);

            if (type.Definition.TryGetProperty("originationMethods", out var list))
            {
                if (list.ValueKind != JsonValueKind.Array ||
                    list.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String))
                    return new(NativeOriginMethodDefaultStatus.Unresolved);
                var ids = list.EnumerateArray().Select(x => x.GetString()!).ToArray();
                return new(ids.Contains(method.MethodId, StringComparer.Ordinal)
                    ? NativeOriginMethodDefaultStatus.Available
                    : NativeOriginMethodDefaultStatus.NotGranted, type.Id, ids);
            }
            var parent = type.Definition.TryGetProperty("parent", out var parentValue) &&
                parentValue.ValueKind == JsonValueKind.String
                    ? parentValue.GetString() : null;
            if (parent == "creature")
                return new(NativeOriginMethodDefaultStatus.NotGranted, "creature", []);
            if (string.IsNullOrWhiteSpace(parent))
                return new(NativeOriginMethodDefaultStatus.Unresolved);
            next = parent.StartsWith("creature-type.", StringComparison.Ordinal)
                ? parent : "creature-type." + parent;
        }
        return new(NativeOriginMethodDefaultStatus.Unresolved);
    }
}
