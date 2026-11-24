using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A rest replacement is a revision-pinned plan, not reconstruction, a new
/// grant, or an operation that may be replayed after a completed rest.
/// </summary>
public sealed record NativeOriginCantripReplacementPlan(string Status, string? Reason,
    NativeOriginConstructionResult? Replacement, NativeOriginInnateSpellLedgerState? Ledger,
    string? GrantId = null, string? PreviousSpellId = null, string? NewSpellId = null,
    bool Committed = false, NativeOriginProposal? UpdatedSourceProposal = null);

public static class NativeOriginCantripReplacementPlanner
{
    /// <summary>
    /// The application supplies the original direct proposal without proof
    /// claims, the actual saved profile and ledger, verified character
    /// projection, and an independently authorized unconsumed Long Rest.
    /// Rebuild the source baseline through the universal construction engine
    /// before changing exactly one existing selection.
    /// </summary>
    public static NativeOriginCantripReplacementPlan Plan(NativeCatalogSnapshot catalog,
        NativeOriginProposal savedProposal, NativeOriginProfile savedProfile,
        NativeCharacterProjection character, NativeOriginInnateSpellLedgerState savedLedger,
        long expectedLedgerRevision, string grantId, string replacementSpellId)
    {
        static NativeOriginCantripReplacementPlan Fail(string reason) =>
            new("unresolved", reason, null, null);

        if (savedProposal.Intent != NativeOriginIntent.Direct ||
            savedProposal.Method is not { RuleId: "rule.creature-origination", MethodId: "biological" } ||
            savedProposal.ExistingConfiguration is not null || savedProposal.ExistingEffectiveConfiguration is not null ||
            (savedProposal.History?.Count ?? 0) != 0 || (savedProposal.TableDecisions?.Count ?? 0) != 0 ||
            !NativeOriginChoiceCatalog.IsMember(catalog, "spell:wizard", replacementSpellId))
            return Fail("cantrip-replacement-source-or-option-invalid");

        var evaluator = new NativeOriginFirstPartyChoiceEvaluator(catalog);
        var before = evaluator.Evaluate(savedProposal);
        if (before.Status != NativeOriginConstructionStatus.Authorized || before.Profile is null ||
            before.Issues.Count != 0 || !Equal(before.Profile, savedProfile))
            return Fail("cantrip-replacement-saved-origin-mismatch");

        var targets = before.Profile.Underlying.InherentGrants.Where(grant => grant.Id == grantId).ToArray();
        if (targets.Length != 1) return Fail("cantrip-replacement-grant-not-selected");
        var target = targets[0];
        if (target.Identity.MechanicalDefinitionId != "trait.arcane-cantrip" ||
            target.ResolvedChoices is not { Count: 1 } choices ||
            choices[0].RequirementId != "selection" ||
            choices[0].SelectedOptionIds is not { Count: 1 } selected ||
            selected[0] == replacementSpellId || target.SourceCandidateIds is not { Count: 1 })
            return Fail("cantrip-replacement-not-authorized-source-choice");

        // Only the original manifestation's choice changes. Retain the
        // same occurrence, grant identity, ability, and every other trait.
        var original = savedProposal.Manifestations.Where(manifest =>
            manifest.Id == target.ManifestationId && manifest.CandidateId == target.SourceCandidateIds[0]).ToArray();
        if (original.Length != 1 || original[0].Choices is not { Count: 1 } originalChoices ||
            originalChoices[0].RequirementId != "selection" ||
            originalChoices[0].SelectedOptionIds is not { Count: 1 } originalIds ||
            originalIds[0] != selected[0])
            return Fail("cantrip-replacement-original-selection-mismatch");

        var proposed = savedProposal with
        {
            Manifestations = savedProposal.Manifestations.Select(manifest => manifest.Id == original[0].Id
                ? manifest with { Choices = [new("selection", [replacementSpellId])] } : manifest).ToArray()
        };
        var after = evaluator.Evaluate(proposed);
        if (after.Status != NativeOriginConstructionStatus.Authorized || after.Profile is null ||
            after.Issues.Count != 0) return Fail("cantrip-replacement-construction-unresolved");
        var updatedTargets = after.Profile.Underlying.InherentGrants.Where(grant => grant.Id == grantId).ToArray();
        if (updatedTargets.Length != 1) return Fail("cantrip-replacement-grant-changed");
        var updated = updatedTargets[0];
        if (!Equal(target with { ResolvedChoices = updated.ResolvedChoices }, updated))
            return Fail("cantrip-replacement-mechanics-changed");

        NativeOriginCurrentConfiguration Replace(NativeOriginCurrentConfiguration configuration) =>
            configuration with
            {
                InherentGrants = configuration.InherentGrants.Select(grant =>
                    grant.Id == grantId ? updated : grant).ToArray()
            };
        var expected = savedProfile with
        {
            Underlying = Replace(savedProfile.Underlying),
            Current = Replace(savedProfile.Current)
        };
        // Explicitly reject any profile where this source-choice change
        // would also alter ancestry, genes, history, influences or other
        // configuration. Those require their own reviewed integration.
        if (!Equal(expected, after.Profile)) return Fail("cantrip-replacement-unexpected-profile-change");

        var recovered = NativeOriginInnateSpellResourceLedger.ReplaceCantripAfterLongRest(
            catalog, before, after, character, savedLedger, expectedLedgerRevision, grantId);
        if (recovered.Status != "resolved" || recovered.State is null)
            return Fail(recovered.Reason ?? "cantrip-replacement-ledger-unresolved");
        // Return the proof-free source proposal separately from the evaluated
        // result. Persisting only the latter would replay transient evidence on
        // the next rest instead of independently deriving it again.
        return new("resolved", null, after, recovered.State, grantId, selected[0], replacementSpellId,
            UpdatedSourceProposal: proposed);
    }

    private static bool Equal<T>(T left, T right) =>
        JsonElement.DeepEquals(JsonSerializer.SerializeToElement(left), JsonSerializer.SerializeToElement(right));
}
