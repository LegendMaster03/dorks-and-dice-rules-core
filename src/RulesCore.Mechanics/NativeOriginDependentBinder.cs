using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// Applies explicit source-authored bindings between selected inherent characteristics.
/// A choice offered by an Ancestry does not become a parameter of every other spell
/// merely because its value is similar; the native trait must declare that dependency.
/// </summary>
internal static class NativeOriginDependentBinder
{
    public static (IReadOnlyList<NativeOriginEffectiveGrant> Grants,
        IReadOnlyList<NativeOriginContractIssue> Issues) Bind(
        IReadOnlyList<NativeOriginEffectiveGrant> reconciled)
    {
        var grants = new List<NativeOriginEffectiveGrant>();
        var issues = new List<NativeOriginContractIssue>();

        foreach (var grant in reconciled)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
                grant.EffectiveValue is not { } value ||
                value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty("originBindings", out var definitions) ||
                definitions.ValueKind != JsonValueKind.Object)
            {
                grants.Add(grant);
                continue;
            }

            var parameters = (grant.Parameters ?? new Dictionary<string, JsonElement>())
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            var incomplete = false;

            foreach (var binding in definitions.EnumerateObject())
            {
                if (Text(binding.Value, "lookup") != "manifested-intrinsic")
                    continue; // Leaf/ancestor bindings were verified by deconstruction.

                var slot = Text(binding.Value, "intrinsicSlot");
                var scope = Text(binding.Value, "scope");
                if (slot is null || scope != "source")
                {
                    issues.Add(new("origin-binding-unsupported", grant.Id,
                        NativeOriginIssueSeverity.Unresolved,
                        "The authored effective-intrinsic binding has unsupported semantics."));
                    incomplete = true;
                    continue;
                }

                var matching = reconciled.Where(other =>
                    other.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                    other.Identity.IntrinsicSlotId == slot &&
                    other.Identity.Occurrence.Source == grant.Identity.Occurrence.Source)
                    .ToArray();
                // Previously manifested contributors already carry the parameter
                // that was resolved when their actual feature was acquired.
                // Do not invent a new intrinsic choice in the next generation.
                // If a new choice from that source is also present, they must agree.
                if (parameters.TryGetValue(binding.Name, out var inherited))
                {
                    if (matching.Length > 0 &&
                        (matching.Length != 1 || matching[0].EffectiveValue is null ||
                         !JsonElement.DeepEquals(inherited, matching[0].EffectiveValue!.Value)))
                    {
                        issues.Add(new("origin-binding-parameter-conflict",
                            grant.Id + ":" + binding.Name,
                            NativeOriginIssueSeverity.Conflict,
                            "An inherited parameter differs from the currently selected source choice."));
                        incomplete = true;
                    }
                    continue;
                }
                if (matching.Length != 1 || matching[0].EffectiveValue is null)
                {
                    issues.Add(new(matching.Length == 0 ?
                            "origin-binding-choice-missing" : "origin-binding-ambiguous",
                        grant.Id + ":" + binding.Name,
                        matching.Length == 0 ? NativeOriginIssueSeverity.Incomplete :
                            NativeOriginIssueSeverity.Unresolved,
                        "A dependent trait needs exactly one effective intrinsic choice from its authored source."));
                    incomplete = true;
                    continue;
                }
                parameters[binding.Name] = matching[0].EffectiveValue!.Value.Clone();
            }

            // An incomplete dependent trait is not an active grant. Other independently
            // reconciled grants survive; the profile cannot be marked Authorized.
            if (!incomplete)
                grants.Add(grant with { Parameters = parameters });
        }

        return (grants, issues);
    }

    private static string? Text(JsonElement source, string key) =>
        source.ValueKind == JsonValueKind.Object &&
        source.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
