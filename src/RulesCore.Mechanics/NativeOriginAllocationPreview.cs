using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A complete allocation price schedule. Preview assessments are always
/// non-authoritative; the construction engine can separately enforce a
/// host-supplied schedule during initial character creation. A policy does
/// not grant traits or establish table authority. No numerical default
/// is implied while calibration remains incomplete.
/// </summary>
public sealed record NativeOriginAllocationPolicy(
    string Revision, string RuleId, string MethodId, int Allowance,
    IReadOnlyList<NativeOriginAllocationPrice> Prices,
    IReadOnlyList<NativeOriginAllocationInteractionPrice>? InteractionPrices = null);

/// <summary>
/// An additional cost for a specific coexisting set of effective mechanics.
/// Interaction costs do not replace ordinary grant charges and can span
/// different contributors; they apply only when every required key is active.
/// </summary>
public sealed record NativeOriginAllocationInteractionPrice(
    string Id, int Load, IReadOnlyList<string> RequiredKeys);

/// <summary>
/// A configuration selector reads a complete effective grant, not a contributor
/// offer. Paths are slash-separated JSON properties rooted at value, parameters,
/// choices, definition, or resourceOwners. Missing is distinct from JSON null.
/// </summary>
public enum NativeOriginAllocationComparison { Equals, Present, Absent }

public sealed record NativeOriginAllocationPriceCondition(
    string MemberKey, string Path, NativeOriginAllocationComparison Comparison,
    JsonElement? ExpectedValue = null);

/// <summary>
/// A configuration replaces the containing price's load; it is not an extra
/// charge. Highest explicit priority wins, and tied matches fail closed.
/// </summary>
public sealed record NativeOriginAllocationPriceConfiguration(
    string Id, int Load, int Priority,
    IReadOnlyList<NativeOriginAllocationPriceCondition> Conditions);

/// <summary>
/// Prices a complete characteristic or one inseparable same-lineage group.
/// MemberKeys use intrinsic:slot-id and trait:canonical-origin-trait-id.
/// Whole-progression load covers future unlocks. When Configurations is
/// supplied, a nonmatching effective configuration is unpriced unless
/// the author explicitly opts into the base-load fallback.
/// A null Configurations list retains the existing fixed-price contract.
/// </summary>
public sealed record NativeOriginAllocationPrice(
    string Id, int Load, IReadOnlyList<string> MemberKeys,
    IReadOnlyList<NativeOriginAllocationPriceConfiguration>? Configurations = null,
    bool AllowUnmatchedConfigurationFallback = false);

public sealed record NativeOriginAllocationComponent(
    string Key, string GrantId, IReadOnlyList<string> SourceCandidateIds,
    int? UnlockLevel, string? AssignedPriceId = null,
    string? AssignedConfigurationId = null);

public sealed record NativeOriginAllocationCharge(
    string PriceId, int Load, IReadOnlyList<string> GrantIds,
    IReadOnlyList<string> CharacteristicKeys, string? ConfigurationId = null);

/// <summary>
/// A public preview never changes Origin authorization. The creation gate
/// reuses the same ledger, marks PreviewOnly=false and explicitly incorporates
/// the result in construction Status. A passed budget never authorizes an
/// otherwise unsupported grant or certifies the price schedule as balanced.
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
        NativeOriginAllocationPolicy? policy = null) =>
        AssessCore(catalog, result, policy, creationScope: false);

    /// <summary>
    /// The creation gate uses the same price ledger as the read-only preview.
    /// Direct Species presets and mixed/custom biological Origins are assessed
    /// under one policy; a latent gene has no effective grant to charge.
    /// Call only from the construction engine after all normal grant validation.
    /// </summary>
    internal static NativeOriginAllocationAssessment AssessCreation(
        NativeCatalogSnapshot catalog, NativeOriginConstructionResult result,
        NativeOriginAllocationPolicy? policy) =>
        AssessCore(catalog, result, policy, creationScope: true);

    private static NativeOriginAllocationAssessment AssessCore(
        NativeCatalogSnapshot catalog, NativeOriginConstructionResult result,
        NativeOriginAllocationPolicy? policy, bool creationScope)
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
        var applicableIntent = creationScope
            ? result.EvaluatedProposal.Intent is
                (NativeOriginIntent.Direct or NativeOriginIntent.Mixed or
                    NativeOriginIntent.Custom)
            : result.EvaluatedProposal.Intent == NativeOriginIntent.Mixed;
        if (!applicableIntent ||
            result.EvaluatedProposal.Method is null ||
            result.EvaluatedProposal.Method.RuleId != "rule.creature-origination" ||
            result.EvaluatedProposal.Method.MethodId != "biological")
            return Finish("not-applicable", null, null, null, [], issues);

        if (policy is null)
            return Finish("not-configured", null, null, null, [],
                [creationScope ? "creation-allocation-policy-not-configured" :
                    "biological-allocation-policy-not-approved"]);

        if (string.IsNullOrWhiteSpace(policy.Revision) ||
            policy.RuleId != result.EvaluatedProposal.Method.RuleId ||
            policy.MethodId != result.EvaluatedProposal.Method.MethodId ||
            policy.Allowance < 0 || policy.Prices is null ||
            policy.Prices.Any(x => x is null ||
                string.IsNullOrWhiteSpace(x.Id) || x.Load <= 0 ||
                x.MemberKeys is not { Count: > 0 } ||
                x.MemberKeys.Any(string.IsNullOrWhiteSpace) ||
                x.MemberKeys.Distinct(StringComparer.Ordinal).Count() != x.MemberKeys.Count ||
                !NativeOriginAllocationPriceResolver.Valid(x)) ||
            policy.Prices.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() !=
                policy.Prices.Count ||
            policy.Prices.SelectMany(x => x.MemberKeys)
                .Distinct(StringComparer.Ordinal).Count() !=
                policy.Prices.Sum(x => x.MemberKeys.Count) ||
            (policy.InteractionPrices ?? []).Any(x => x is null ||
                string.IsNullOrWhiteSpace(x.Id) || x.Load <= 0 ||
                x.RequiredKeys is not { Count: >= 2 } ||
                x.RequiredKeys.Any(string.IsNullOrWhiteSpace) ||
                x.RequiredKeys.Distinct(StringComparer.Ordinal).Count() !=
                    x.RequiredKeys.Count) ||
            (policy.InteractionPrices ?? []).Select(x => x.Id)
                .Concat(policy.Prices.Select(x => x.Id))
                .Distinct(StringComparer.Ordinal).Count() !=
                (policy.InteractionPrices?.Count ?? 0) + policy.Prices.Count ||
            (policy.InteractionPrices ?? []).SelectMany(x => x.RequiredKeys)
                .Any(key => !policy.Prices.SelectMany(x => x.MemberKeys)
                    .Contains(key, StringComparer.Ordinal)))
            return Finish("invalid-policy", policy.Revision, policy.Allowance,
                null, [], ["allocation-policy-malformed-or-overlapping"]);

        // Incomplete construction cannot become affordable by omitting
        // unresolved grants. This holds for previews and enforced creation.
        if (result.Status != NativeOriginConstructionStatus.Authorized)
            issues.Add("origin-not-authorized-for-allocation");
        // Explicit cost data does not turn a custom or unrecognized payload
        // into a reviewed mechanic. The native definition and future unlock
        // must be resolvable before any complete-configuration assessment.
        foreach (var component in components.Where(x =>
            x.UnlockLevel is null || x.Key.StartsWith("unknown:", StringComparison.Ordinal)))
            issues.Add("allocation-definition-unresolved:" + component.Key + ":" +
                component.GrantId);

        var candidates = result.EvaluatedProposal.Candidates
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var assigned = new Dictionary<string, string>(StringComparer.Ordinal);
        var selectedConfigurations = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var grantsById = grants.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var charges = new List<NativeOriginAllocationCharge>();
        var ambiguousConfiguration = false;

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
                Price(entry, members);
                continue;
            }

            // Two separately authorized effective occurrences of one canonical
            // trait remain two possible benefits; don't count donor evidence
            // twice, but also don't treat independently kept benefits as free.
            foreach (var component in groups[0])
                Price(entry, [component]);
        }

        void Price(NativeOriginAllocationPrice entry,
            IReadOnlyList<NativeOriginAllocationComponent> members)
        {
            var actual = members.ToDictionary(x => x.Key,
                x => grantsById[x.GrantId], StringComparer.Ordinal);
            var chosen = NativeOriginAllocationPriceResolver.Select(
                catalog, entry, actual);
            if (chosen.Ambiguous)
            {
                ambiguousConfiguration = true;
                issues.Add("allocation-configuration-ambiguous:" + entry.Id);
                return;
            }
            if (chosen.Load is not int load)
            {
                issues.Add("allocation-configuration-unpriced:" + entry.Id + ":" +
                    string.Join(",", members.Select(x => x.GrantId)));
                return;
            }
            foreach (var member in members)
            {
                assigned.Add(member.GrantId, entry.Id);
                if (chosen.ConfigurationId is not null)
                    selectedConfigurations.Add(member.GrantId, chosen.ConfigurationId);
            }
            charges.Add(new(entry.Id, load, members.Select(x => x.GrantId).ToArray(),
                members.Select(x => x.Key).ToArray(), chosen.ConfigurationId));
        }

        foreach (var component in components.Where(x =>
            !assigned.ContainsKey(x.GrantId)))
            issues.Add("allocation-load-unpriced:" + component.Key + ":" +
                component.GrantId);

        // Interaction premiums are additive and explicitly authored. Every
        // required mechanic must already be present as an effective grant.
        // The source need not be the same contributor, unlike inseparable
        // same-lineage bundles. No latent instance triggers an interaction.
        foreach (var interaction in (policy.InteractionPrices ?? [])
            .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var members = interaction.RequiredKeys.Select(key => components
                .Where(component => component.Key == key).ToArray()).ToArray();
            if (members.Any(group => group.Length == 0))
                continue;
            charges.Add(new(interaction.Id, interaction.Load,
                members.SelectMany(group => group.Select(x => x.GrantId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                interaction.RequiredKeys));
        }

        var checkedComponents = components.Select(x =>
            x with
            {
                AssignedPriceId = assigned.GetValueOrDefault(x.GrantId),
                AssignedConfigurationId = selectedConfigurations.GetValueOrDefault(x.GrantId)
            }).ToArray();

        if (ambiguousConfiguration)
            return Finish("invalid-policy", policy.Revision, policy.Allowance,
                null, charges, issues, checkedComponents);
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
                charges, errors.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                PreviewOnly: !creationScope);
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
