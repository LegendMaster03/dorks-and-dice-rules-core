using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

internal sealed record NativeOriginSelectedGrant(
    NativeOriginCandidate Candidate, NativeOriginManifestation Manifestation,
    NativeOriginEffectiveGrant Grant);

/// <summary>
/// Reconciles actual authorized manifestations, not unselected candidate offers.
/// Repeated occurrences are not multiplied; other apparent equivalences need authored
/// evidence or a verified table disposition. Ordering never selects a conflict winner.
/// </summary>
internal sealed class NativeOriginReconciler(NativeCatalogSnapshot catalog)
{
    public (IReadOnlyList<NativeOriginEffectiveGrant> Grants,
        IReadOnlyList<NativeOriginContractIssue> Issues, bool UsedDm) Reconcile(
        NativeOriginProposal proposal, IReadOnlyList<NativeOriginSelectedGrant> selected,
        Func<string, string, NativeTableRuleDecision?> verified)
    {
        var issues = new List<NativeOriginContractIssue>();
        var active = selected.ToDictionary(x => x.Candidate.Id, StringComparer.Ordinal);
        var ignored = new HashSet<string>(StringComparer.Ordinal);
        var merged = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var usedDm = false;

        foreach (var relation in (proposal.Relationships ?? []).OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (relation?.CandidateIds is null) continue;
            var involved = relation.CandidateIds.Where(active.ContainsKey)
                .Distinct(StringComparer.Ordinal).ToArray();
            if (involved.Length < 2) continue;
            var resolution = proposal.Resolutions?.FirstOrDefault(x => x.RelationshipId == relation.Id);
            if (resolution is null)
            {
                Issue("relationship-unresolved", relation.Id, NativeOriginIssueSeverity.Unresolved,
                    "Selected candidates have an unresolved authored relationship.");
                foreach (var id in involved) ignored.Add(id);
                continue;
            }
            var authenticated = resolution.Basis?.Adjudication == NativeOriginAdjudication.DmAuthorized ?
                verified("relationship", relation.Id) : null;
            var authorized = authenticated is not null &&
                JsonString(authenticated.EffectiveOutcome, "action") == ActionName(resolution.Action);
            var authored = resolution.Basis?.Adjudication == NativeOriginAdjudication.Supported &&
                relation.AuthoredRuleReference == resolution.Basis.NativeRuleReference &&
                NativeRelationshipAuthorizes(relation.Id, resolution.Action,
                    relation.AuthoredRuleReference);
            if (!authorized && !authored)
            {
                Issue("relationship-disposition-unauthorized", relation.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    "The proposed relationship disposition lacks an executable authored rule or verified table decision.");
                foreach (var id in involved) ignored.Add(id);
                continue;
            }
            usedDm |= authorized;
            switch (resolution.Action)
            {
                case NativeOriginResolutionAction.RetainFirst:
                case NativeOriginResolutionAction.RetainSecond:
                    var index = resolution.Action == NativeOriginResolutionAction.RetainFirst ? 0 : 1;
                    if (involved.Length <= index) goto default;
                    foreach (var id in involved)
                        if (id != involved[index]) ignored.Add(id);
                    break;
                case NativeOriginResolutionAction.ConsolidateEvidence:
                    if (!involved.Skip(1).All(id => SameMechanic(active[involved[0]],
                            active[id]))) goto default;
                    foreach (var id in involved.Skip(1))
                    {
                        ignored.Add(id);
                        if (!merged.TryGetValue(involved[0], out var evidence))
                            merged[involved[0]] = evidence = new(StringComparer.Ordinal);
                        evidence.Add(id);
                    }
                    break;
                case NativeOriginResolutionAction.RetainBoth:
                    break;
                default:
                    Issue("relationship-action-unresolved", relation.Id, NativeOriginIssueSeverity.Unresolved,
                        "The disposition lacks a verified and executable result.");
                    foreach (var id in involved) ignored.Add(id);
                    break;
            }
        }

        // One identical authored occurrence can appear through multiple contributors.
        // Its already selected mechanic is one grant with many source-offer references.
        foreach (var group in active.Values.Where(x => !ignored.Contains(x.Candidate.Id))
            .GroupBy(x => x.Candidate.Identity))
        {
            var entries = group.OrderBy(x => x.Candidate.Id, StringComparer.Ordinal).ToArray();
            if (entries.Length < 2) continue;
            if (!entries.Skip(1).All(x => SameMechanic(entries[0], x)))
            {
                foreach (var x in entries) ignored.Add(x.Candidate.Id);
                Issue("same-definition-different-manifestations", entries[0].Candidate.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    "The same authored occurrence has incompatible values or selected choices.");
                continue;
            }
            foreach (var entry in entries.Skip(1))
            {
                ignored.Add(entry.Candidate.Id);
                if (!merged.TryGetValue(entries[0].Candidate.Id, out var evidence))
                    merged[entries[0].Candidate.Id] = evidence = new(StringComparer.Ordinal);
                evidence.Add(entry.Candidate.Id);
            }
        }

        // Even an explicitly shared canonical mechanic does not license duplicate
        // effective benefits from different authored occurrences. Require a real
        // relationship policy instead of treating shared identity as stacking.
        foreach (var group in active.Values.Where(x => !ignored.Contains(x.Candidate.Id) &&
            x.Candidate.Identity.MechanicalDefinitionId is not null)
            .GroupBy(x => x.Candidate.Identity.MechanicalDefinitionId!, StringComparer.Ordinal))
        {
            var entries = group.ToArray();
            if (entries.Select(x => x.Candidate.Identity.Occurrence)
                .Distinct().Count() < 2) continue;
            foreach (var x in entries) ignored.Add(x.Candidate.Id);
            Issue("shared-definition-reconciliation-required", group.Key,
                NativeOriginIssueSeverity.Unresolved,
                "Shared canonical mechanics from distinct occurrences require an authored disposition.");
        }

        // Different occurrences offering the same intrinsic slot have no general
        // first-party winner, even where values happen to coincide.
        foreach (var group in active.Values.Where(x => !ignored.Contains(x.Candidate.Id) &&
            x.Candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
            x.Candidate.Identity.IntrinsicSlotId is not null)
            .GroupBy(x => x.Candidate.Identity.IntrinsicSlotId!, StringComparer.Ordinal))
        {
            var entries = group.ToArray();
            if (entries.Length < 2) continue;
            foreach (var x in entries) ignored.Add(x.Candidate.Id);
            Issue("intrinsic-slot-interaction-unresolved", group.Key,
                NativeOriginIssueSeverity.Unresolved,
                "Different source occurrences cannot choose or combine an intrinsic value without a rule.");
        }

        var grants = active.Values.Where(x => !ignored.Contains(x.Candidate.Id))
            .OrderBy(x => x.Candidate.Id, StringComparer.Ordinal)
            .Select(x =>
            {
                var evidence = new SortedSet<string>(StringComparer.Ordinal) { x.Candidate.Id };
                if (merged.TryGetValue(x.Candidate.Id, out var additional))
                    evidence.UnionWith(additional);
                return x.Grant with { SourceCandidateIds = evidence.ToArray() };
            }).ToArray();
        return (grants, issues, usedDm);

        void Issue(string code, string path, NativeOriginIssueSeverity level, string detail) =>
            issues.Add(new(code, path, level, detail));
    }

    private bool NativeRelationshipAuthorizes(string id, NativeOriginResolutionAction action,
        string? ruleId)
    {
        if (ruleId is null || !catalog.TryGetById(ruleId, out var rule) || rule is null ||
            rule.Category != "rule" ||
            !rule.Definition.TryGetProperty("originRelationships", out var items) ||
            items.ValueKind != JsonValueKind.Array) return false;
        return items.EnumerateArray().Any(item =>
            JsonString(item, "id") == id &&
            JsonString(item, "action") == ActionName(action));
    }

    private static bool SameMechanic(NativeOriginSelectedGrant a, NativeOriginSelectedGrant b)
    {
        if (a.Candidate.Identity != b.Candidate.Identity ||
            (a.Grant.EffectiveValue?.GetRawText() ?? "") !=
            (b.Grant.EffectiveValue?.GetRawText() ?? "")) return false;
        static string Parameters(IReadOnlyDictionary<string, JsonElement>? source) =>
            string.Join("|", (source ?? new Dictionary<string, JsonElement>())
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => x.Key + "=" + x.Value.GetRawText()));
        static string Choices(IReadOnlyList<NativeOriginChoiceSelection>? source) =>
            string.Join("|", (source ?? []).OrderBy(x => x.RequirementId, StringComparer.Ordinal)
                .Select(x => x.RequirementId + "=" +
                    string.Join(",", x.SelectedOptionIds.OrderBy(y => y, StringComparer.Ordinal))));
        return Parameters(a.Grant.Parameters) == Parameters(b.Grant.Parameters) &&
            Choices(a.Grant.ResolvedChoices) == Choices(b.Grant.ResolvedChoices);
    }

    private static string ActionName(NativeOriginResolutionAction action) => action switch
    {
        NativeOriginResolutionAction.RetainFirst => "retain-first",
        NativeOriginResolutionAction.RetainSecond => "retain-second",
        NativeOriginResolutionAction.ConsolidateEvidence => "consolidate-evidence",
        NativeOriginResolutionAction.RetainBoth => "retain-both",
        NativeOriginResolutionAction.Replace => "replace",
        _ => "defer"
    };

    private static string? JsonString(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String ? property.GetString() : null;
}
