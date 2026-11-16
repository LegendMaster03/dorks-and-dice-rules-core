using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A hypothetical, read-only allocation policy. It is never part of the
/// authorization pipeline: passing a policy here neither creates a grant nor
/// establishes table authority. Its revision must identify an explicit design
/// candidate, not the ruleset's implicit default.
/// </summary>
public sealed record NativeOriginAllocationPolicy(
    string Revision, string RuleId, string MethodId, int Allowance,
    IReadOnlyList<NativeOriginAllocationPrice> Prices);

/// <summary>
/// Prices a complete characteristic or one inseparable group. MemberKeys use
/// "intrinsic:slot-id" and "trait:canonical-origin-trait-id" notation.
/// Whole-progression load covers every authored future unlock, not only the
/// capabilities available at the current character level. A bundle requires
/// all members to be selected from a shared contributing lineage.
/// </summary>
public sealed record NativeOriginAllocationPrice(
    string Id, int Load, IReadOnlyList<string> MemberKeys);

public sealed record NativeOriginAllocationComponent(
    string Key, string GrantId, IReadOnlyList<string> SourceCandidateIds,
    int? UnlockLevel, string? AssignedPriceId = null);

public sealed record NativeOriginAllocationCharge(
    string PriceId, int Load, IReadOnlyList<string> GrantIds,
    IReadOnlyList<string> CharacteristicKeys);

/// <summary>
/// An allocation assessment never changes the status of its Origin. Even a
/// within-allowance result is an experimental comparison, not a mechanical
/// authorization, a power score, or an approved construction budget.
/// </summary>
public sealed record NativeOriginAllocationAssessment(
    string Status, string? PolicyRevision, int? Allowance, int? CommittedLoad,
    IReadOnlyList<NativeOriginAllocationComponent> Components,
    IReadOnlyList<NativeOriginAllocationCharge> Charges,
    IReadOnlyList<string> Issues,
    bool PreviewOnly = true);

public static class NativeOriginAllocationPreview
{
    public static NativeOriginAllocationAssessment Assess(
        NativeCatalogSnapshot catalog, NativeOriginConstructionResult result,
        NativeOriginAllocationPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(result);
        var grants = result.Profile?.Underlying.InherentGrants ?? [];
        var components = grants
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => new NativeOriginAllocationComponent(
                Key(x), x.Id, x.SourceCandidateIds.OrderBy(id => id,
                    StringComparer.Ordinal).ToArray(), Unlock(catalog, x)))
            .ToArray();
        var issues = new List<string>();
        if (result.EvaluatedProposal.Intent != NativeOriginIntent.Mixed ||
            result.EvaluatedProposal.Method is null ||
            result.EvaluatedProposal.Method.RuleId != "rule.creature-origination" ||
            result.EvaluatedProposal.Method.MethodId != "biological")
            return Finish("not-applicable", null, null, null, [], issues);

        if (policy is null)
            return Finish("not-configured", null, null, null, [],
                ["biological-allocation-policy-not-approved"]);

        if (string.IsNullOrWhiteSpace(policy.Revision) ||
            policy.RuleId != result.EvaluatedProposal.Method.RuleId ||
            policy.MethodId != result.EvaluatedProposal.Method.MethodId ||
            policy.Allowance < 0 || policy.Prices is null ||
            policy.Prices.Any(x => x is null ||
                string.IsNullOrWhiteSpace(x.Id) || x.Load <= 0 ||
                x.MemberKeys is not { Count: > 0 } ||
                x.MemberKeys.Any(string.IsNullOrWhiteSpace) ||
                x.MemberKeys.Distinct(StringComparer.Ordinal).Count() != x.MemberKeys.Count) ||
            policy.Prices.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() !=
                policy.Prices.Count ||
            policy.Prices.SelectMany(x => x.MemberKeys)
                .Distinct(StringComparer.Ordinal).Count() !=
                policy.Prices.Sum(x => x.MemberKeys.Count))
            return Finish("invalid-policy", policy.Revision, policy.Allowance,
                null, [], ["allocation-policy-malformed-or-overlapping"]);

        // A configured policy may be tried against an incomplete construction
        // without declaring that missing grants are free. Such a preview is
        // explicitly incomplete regardless of the hypothetical cost result.
        if (result.Status != NativeOriginConstructionStatus.Authorized)
            issues.Add("origin-not-authorized-for-allocation");

        var candidates = result.EvaluatedProposal.Candidates
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var assigned = new Dictionary<string, string>(StringComparer.Ordinal);
        var charges = new List<NativeOriginAllocationCharge>();

        foreach (var entry in policy.Prices.OrderBy(x => x.Id,
            StringComparer.Ordinal))
        {
            var groups = entry.MemberKeys.Select(key => components
                .Where(x => x.Key == key).ToArray()).ToArray();
            if (groups.All(g => g.Length == 0))
                continue;

            // Bundle identity is not simply a shared display name. The policy
            // must describe one of each member and their effective grants must
            // have contributor evidence from the *same* actual lineage.
            if (entry.MemberKeys.Count > 1)
            {
                if (groups.Any(g => g.Length != 1))
                {
                    issues.Add("allocation-bundle-members-unresolved:" + entry.Id);
                    continue;
                }
                var members = groups.Select(g => g[0]).ToArray();
                if (!SharesContributor(members, candidates))
                {
                    issues.Add("allocation-bundle-source-mismatch:" + entry.Id);
                    continue;
                }
                foreach (var component in members)
                    assigned.Add(component.GrantId, entry.Id);
                charges.Add(new(entry.Id, entry.Load,
                    members.Select(x => x.GrantId).ToArray(), entry.MemberKeys));
                continue;
            }

            // Two separately authorized effective occurrences of one canonical
            // trait remain two possible benefits; don't count donor evidence
            // twice, but also don't treat independently kept benefits as free.
            foreach (var component in groups[0])
            {
                assigned.Add(component.GrantId, entry.Id);
                charges.Add(new(entry.Id, entry.Load, [component.GrantId],
                    [component.Key]));
            }
        }

        foreach (var component in components.Where(x =>
            !assigned.ContainsKey(x.GrantId)))
            issues.Add("allocation-load-unpriced:" + component.Key + ":" +
                component.GrantId);

        var checkedComponents = components.Select(x =>
            x with
            {
                AssignedPriceId = assigned.GetValueOrDefault(x.GrantId)
            }).ToArray();

        if (issues.Count > 0)
            return Finish("unresolved", policy.Revision, policy.Allowance,
                null, charges, issues, checkedComponents);

        // Use checked arithmetic so malicious or accidental large weights
        // cannot wrap into a deceptively low positive cost.
        long sum = charges.Sum(x => (long)x.Load);
        if (sum > int.MaxValue)
            return Finish("invalid-policy", policy.Revision, policy.Allowance,
                null, charges, ["allocation-total-overflow"], checkedComponents);
        var total = (int)sum;
        return Finish(total <= policy.Allowance ? "within-allowance" :
            "over-allowance", policy.Revision, policy.Allowance, total, charges,
            issues, checkedComponents);

        NativeOriginAllocationAssessment Finish(string status, string? revision,
            int? allowance, int? total,
            IReadOnlyList<NativeOriginAllocationCharge> charges,
            IReadOnlyList<string> errors,
            IReadOnlyList<NativeOriginAllocationComponent>? included = null) =>
            new(status, revision, allowance, total, included ?? components,
                charges, errors.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static string Key(NativeOriginEffectiveGrant grant) =>
        grant.Identity.Kind switch
        {
            NativeOriginCharacteristicKind.Intrinsic when
                !string.IsNullOrWhiteSpace(grant.Identity.IntrinsicSlotId) =>
                "intrinsic:" + grant.Identity.IntrinsicSlotId,
            NativeOriginCharacteristicKind.Trait when
                !string.IsNullOrWhiteSpace(grant.Identity.MechanicalDefinitionId) =>
                "trait:" + grant.Identity.MechanicalDefinitionId,
            _ => "unknown:" + grant.Id
        };

    private static int? Unlock(NativeCatalogSnapshot catalog,
        NativeOriginEffectiveGrant grant)
    {
        if (grant.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic)
            return 1;
        if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
            grant.Identity.MechanicalDefinitionId is not { } id ||
            !catalog.TryGetById(id, out var trait) || trait is null ||
            trait.Category != "origin-trait" ||
            !trait.Definition.TryGetProperty("trait", out var definition) ||
            definition.ValueKind != JsonValueKind.Object)
            return null;
        if (!definition.TryGetProperty("requiredCharacterLevel", out var level))
            return 1;
        return level.ValueKind == JsonValueKind.Number &&
            level.TryGetInt32(out var value) && value >= 1 ? value : null;
    }

    private static bool SharesContributor(
        IReadOnlyList<NativeOriginAllocationComponent> members,
        IReadOnlyDictionary<string, NativeOriginCandidate> candidates)
    {
        var contributors = members.Select(member => member.SourceCandidateIds
            .Where(candidates.ContainsKey)
            .Select(id => candidates[id].ContributorId)
            .Where(id => id is not null)
            .ToHashSet(StringComparer.Ordinal)).ToArray();
        if (contributors.Any(x => x.Count == 0)) return false;
        var common = contributors[0];
        foreach (var donorSet in contributors.Skip(1))
            common.IntersectWith(donorSet);
        return common.Count > 0;
    }
}
