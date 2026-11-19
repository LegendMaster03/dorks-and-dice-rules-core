using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Materializes source-authored, contributor-local linked selections using
/// *actual* native offers. It does not select the missing counterpart, grant
/// biological eligibility, or authorize a table waiver.
/// </summary>
internal sealed class NativeOriginNativeLinkedSelectionEvaluator(
    NativeCatalogSnapshot catalog)
{
    internal static string ConstraintId(string candidateId, string ruleId) =>
        "native-link:" + candidateId + ":" + ruleId;

    public (NativeOriginProposal Proposal, IReadOnlyList<NativeOriginContractIssue> Issues)
        Expand(NativeOriginProposal proposal)
    {
        var issues = new List<NativeOriginContractIssue>();
        var generated = new List<NativeOriginConstraint>();
        var candidates = proposal.Candidates ?? [];
        foreach (var trigger in candidates.Where(x =>
            x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
            x.Identity.Occurrence.Source.Kind == NativeOriginSourceKind.Native))
        {
            var source = trigger.Identity.Occurrence.Source.Id;
            if (!catalog.TryGetById(source, out var entity) ||
                entity is null || entity.Category is not ("species" or "ancestry") ||
                !entity.Definition.TryGetProperty("originLinkedSelections", out var links) ||
                links.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var link in links.EnumerateArray())
            {
                if (link.ValueKind != JsonValueKind.Object ||
                    Text(link, "kind") != nameof(NativeOriginRelationshipKind.Requires) ||
                    Text(link, "scope") != "same-contributor" ||
                    Text(link, "id") is not { Length: > 0 } name ||
                    Text(link, "triggerIntrinsicSlot") != trigger.Identity.IntrinsicSlotId ||
                    Text(link, "requiresTraitDefinitionId") is not { Length: > 0 } required)
                    continue;
                var peers = candidates.Where(x =>
                    x.ContributorId == trigger.ContributorId &&
                    x.Identity.Kind == NativeOriginCharacteristicKind.Trait &&
                    x.Identity.Occurrence.Source.Kind == NativeOriginSourceKind.Native &&
                    x.Identity.Occurrence.Source.Id == source &&
                    x.Identity.MechanicalDefinitionId == required).ToArray();
                if (peers.Length != 1)
                {
                    issues.Add(new("native-linked-source-invalid", source + ":" + name,
                        NativeOriginIssueSeverity.Unresolved,
                        "The authored linked trait does not have one native source offer."));
                    continue;
                }
                generated.Add(new(
                    ConstraintId(trigger.Id, name),
                    NativeOriginRelationshipKind.Requires,
                    [new(CandidateId: trigger.Id)],
                    [new(CandidateId: peers[0].Id)],
                    Minimum: 1, NativeRuleReference: source));
            }
        }

        // Supplied relationship IDs cannot replace catalog-generated
        // participant selectors. Preserve unrelated general/DM constraints.
        var existing = proposal.Constraints ?? [];
        var ids = generated.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var supplied in existing.Where(x => ids.Contains(x.Id)))
        {
            var expected = generated.Single(x => x.Id == supplied.Id);
            // The same native link may legitimately be loaded from JSON.
            // Compare values, not IReadOnlyList object references.
            if (supplied.Kind != expected.Kind ||
                supplied.NativeRuleReference != expected.NativeRuleReference ||
                supplied.Minimum != expected.Minimum ||
                supplied.Maximum != expected.Maximum ||
                supplied.TableDecisionId != expected.TableDecisionId ||
                supplied.LinkedParameterPaths is not null ||
                supplied.Subjects is not { Count: 1 } ||
                supplied.Conditions is not { Count: 1 } ||
                supplied.Subjects[0] != expected.Subjects[0] ||
                supplied.Conditions[0] != expected.Conditions[0])
                issues.Add(new("native-linked-constraint-tampered", supplied.Id,
                    NativeOriginIssueSeverity.Invalid,
                    "A proposal cannot rewrite the native source's linked selection."));
        }
        return (proposal with
        {
            Constraints = existing.Where(x => !ids.Contains(x.Id))
                .Concat(generated).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray()
        }, issues);
    }

    /// <summary>
    /// Perform the final link closure over *effective* selected grants after
    /// reconciliation and dependent-resource binding. A counterpart selected
    /// initially but suppressed by another disposition no longer satisfies a
    /// source-authored requirement. The same verified, scoped DM waiver used
    /// by the pre-reconciliation constraint engine remains in force.
    /// </summary>
    public (IReadOnlyList<NativeOriginEffectiveGrant> Grants,
        IReadOnlyList<NativeOriginContractIssue> Issues) VerifyEffectiveLinks(
        NativeOriginProposal proposal,
        IReadOnlyList<NativeOriginEffectiveGrant> effective,
        Func<string, string, NativeTableRuleDecision?> verified)
    {
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var issues = new List<NativeOriginContractIssue>();
        foreach (var link in proposal.Constraints ?? [])
        {
            if (link?.NativeRuleReference is not { } source ||
                link.Kind != NativeOriginRelationshipKind.Requires ||
                !link.Id.StartsWith("native-link:", StringComparison.Ordinal) ||
                !catalog.TryGetById(source, out var entity) ||
                entity is null || entity.Category is not ("species" or "ancestry") ||
                !entity.Definition.TryGetProperty("originLinkedSelections", out _))
                continue;
            var decision = verified("constraint", link.Id);
            if (decision is not null &&
                decision.EffectiveOutcome.ValueKind == JsonValueKind.Object &&
                decision.EffectiveOutcome.TryGetProperty("allow", out var allow) &&
                allow.ValueKind == JsonValueKind.True)
                continue;

            var subject = link.Subjects is { Count: 1 } ?
                link.Subjects[0].CandidateId : null;
            var required = link.Conditions is { Count: 1 } ?
                link.Conditions[0].CandidateId : null;
            if (subject is null || required is null) continue;
            var beneficiaries = effective.Where(g =>
                g.SourceCandidateIds.Contains(subject, StringComparer.Ordinal)).ToArray();
            if (beneficiaries.Length == 0 ||
                effective.Any(g => g.SourceCandidateIds.Contains(required,
                    StringComparer.Ordinal))) continue;
            foreach (var grant in beneficiaries)
            {
                blocked.Add(grant.Id);
                issues.Add(new("native-linked-effective-requirement-missing",
                    "constraints." + link.Id, NativeOriginIssueSeverity.Incomplete,
                    "A linked characteristic was selected initially but its required " +
                    "native counterpart is missing from the final effective Origin."));
            }
        }

        return (effective.Where(g => !blocked.Contains(g.Id)).ToArray(), issues);
    }

    private static string? Text(JsonElement json, string property) =>
        json.ValueKind == JsonValueKind.Object &&
        json.TryGetProperty(property, out var item) &&
        item.ValueKind == JsonValueKind.String ? item.GetString() : null;
}
