using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Resolves source-authored shared-use dependencies after reconciliation.
/// A reference to another trait's charges never creates a second resource
/// pool or imports an unselected parental feature. Only canonical native
/// origin-trait definitions may supply executable sharing semantics.
/// </summary>
internal static class NativeOriginResourceDependencyBinder
{
    public static (IReadOnlyList<NativeOriginEffectiveGrant> Grants,
        IReadOnlyList<NativeOriginContractIssue> Issues) Bind(
        NativeCatalogSnapshot catalog, NativeOriginProposal proposal,
        IReadOnlyList<NativeOriginEffectiveGrant> reconciled)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(proposal);
        var retained = new List<NativeOriginEffectiveGrant>();
        var issues = new List<NativeOriginContractIssue>();
        var offered = proposal.Candidates
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        foreach (var grant in reconciled)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
                grant.Identity.MechanicalDefinitionId is not { } id ||
                !catalog.TryGetById(id, out var entity) || entity is null ||
                entity.Category != "origin-trait" ||
                !entity.Definition.TryGetProperty("trait", out var authored) ||
                authored.ValueKind != JsonValueKind.Object ||
                !authored.TryGetProperty("sharedUsesFromTrait", out var reference))
            {
                retained.Add(grant);
                continue;
            }

            void Issue(string code, NativeOriginIssueSeverity severity, string detail) =>
                issues.Add(new(code, "grants." + grant.Id + ".resource", severity, detail));

            var value = grant.EffectiveValue;
            if (reference.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(reference.GetString()) ||
                authored.TryGetProperty("extraUsesGranted", out var authoredExtra) &&
                    authoredExtra.ValueKind != JsonValueKind.False ||
                value is not { ValueKind: JsonValueKind.Object } actual ||
                !actual.TryGetProperty("sharedUsesFromTrait", out var claimed) ||
                !JsonElement.DeepEquals(reference, claimed) ||
                !actual.TryGetProperty("extraUsesGranted", out var extra) ||
                extra.ValueKind != JsonValueKind.False ||
                actual.TryGetProperty("uses", out _) ||
                Text(authored, "id") != Text(actual, "id"))
            {
                Issue("origin-shared-resource-contract-unverified",
                    NativeOriginIssueSeverity.Unresolved,
                    "The effective trait does not preserve its canonical shared-use dependency.");
                continue;
            }

            var owners = reconciled.Where(other =>
                other.Id != grant.Id &&
                other.Identity.Kind == NativeOriginCharacteristicKind.Trait &&
                other.Identity.Occurrence.Source == grant.Identity.Occurrence.Source &&
                other.EffectiveValue is { ValueKind: JsonValueKind.Object } owner &&
                Text(owner, "id") == reference.GetString() &&
                HasAuthoredResourcePool(catalog, other) &&
                SharesContributor(grant, other, offered))
                .ToArray();

            if (owners.Length != 1)
            {
                Issue(owners.Length == 0
                        ? "origin-shared-resource-owner-missing"
                        : "origin-shared-resource-owner-ambiguous",
                    owners.Length == 0 ? NativeOriginIssueSeverity.Incomplete :
                        NativeOriginIssueSeverity.Unresolved,
                    "This characteristic must use one selected owner pool from the same native contributor.");
                continue;
            }

            // The dependent has no independent charges. Persist an explicit
            // machine-readable link so consumers do not allocate a new pool.
            retained.Add(grant with
            {
                ResourceOwners = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["uses"] = owners[0].Id
                }
            });
        }

        return (retained, issues);
    }

    private static bool HasAuthoredResourcePool(NativeCatalogSnapshot catalog,
        NativeOriginEffectiveGrant grant)
    {
        var id = grant.Identity.MechanicalDefinitionId;
        if (id is null ||
            !catalog.TryGetById(id, out var entity) || entity is null ||
            entity.Category != "origin-trait" ||
            !entity.Definition.TryGetProperty("trait", out var trait) ||
            trait.ValueKind != JsonValueKind.Object ||
            !trait.TryGetProperty("uses", out var expected) ||
            expected.ValueKind != JsonValueKind.Object ||
            grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } effective ||
            !effective.TryGetProperty("uses", out var actual) ||
            !JsonElement.DeepEquals(expected, actual) ||
            Text(trait, "id") != Text(effective, "id"))
            return false;
        return true;
    }

    private static bool SharesContributor(
        NativeOriginEffectiveGrant dependent, NativeOriginEffectiveGrant owner,
        IReadOnlyDictionary<string, NativeOriginCandidate> offered)
    {
        var dependentCandidates = dependent.SourceCandidateIds
            .Where(offered.ContainsKey).Select(id => offered[id]).ToArray();
        var ownerCandidates = owner.SourceCandidateIds
            .Where(offered.ContainsKey).Select(id => offered[id]).ToArray();
        if (dependentCandidates.Length != dependent.SourceCandidateIds.Count ||
            ownerCandidates.Length != owner.SourceCandidateIds.Count)
            return false;

        // Direct Species/Ancestry traits have no contributor ID, and still
        // require the exact same native authored source occurrence.
        return dependentCandidates.Any(d =>
            ownerCandidates.Any(o =>
                d.ContributorId is not null
                    ? d.ContributorId == o.ContributorId
                    : o.ContributorId is null &&
                      d.Identity.Occurrence.Source == o.Identity.Occurrence.Source));
    }

    private static string? Text(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String ? property.GetString() : null;
}
