using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

public sealed record NativeConstructModificationPlan(
    bool CanApply, NativeConstructOriginConstructionResult? Target,
    IReadOnlyList<string> AddedInstanceIds, IReadOnlyList<string> RemovedInstanceIds,
    IReadOnlyList<string> ChangedInstanceIds, IReadOnlyList<NativeOriginContractIssue> Issues);

/// <summary>
/// Plan an independently authenticated completed installation event. The host
/// must prove acquisition, recipe completion, competency, distinct component
/// identity, resource reservations and installation authorization. Whole-body
/// recipe quotations are not transaction costs or salvage credits.
/// </summary>
public sealed class NativeConstructModificationPlanner(NativeCatalogSnapshot catalog)
{
    public NativeConstructModificationPlan Plan(
        NativeConstructOriginConstructionResult existingConstruction,
        NativeOriginProfile existingProfile, NativeConstructAssemblyRequest targetAssembly,
        NativeOriginProposal targetProposal, string eventId)
    {
        NativeConstructModificationPlan Reject(string code, string message,
            NativeConstructOriginConstructionResult? target = null) =>
            new(false, Uncommittable(target), [], [], [], [new(code, "modification",
                NativeOriginIssueSeverity.Invalid, message)]);

        if (existingConstruction?.IsMechanicallyAuthorized != true ||
            existingConstruction.OperatingSession is not { } previousSession ||
            existingConstruction.Origin.Profile is not { } constructedProfile ||
            existingProfile is null || existingProfile.Adjudication?.Adjudication is not
                (NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized) ||
            !NativeOriginContractValidator.SameConfiguration(constructedProfile.Underlying, existingProfile.Underlying) ||
            string.IsNullOrWhiteSpace(eventId) || eventId.Length > 200)
            return Reject("construct-modification-baseline-unverified",
                "An independently verified current Construct installation and matching underlying Origin are required.");

        if (existingProfile.ActiveTemporaryTransformation is not null ||
            (existingProfile.OtherActiveTemporaryTransformations?.Count ?? 0) != 0 ||
            !NativeOriginContractValidator.SameConfiguration(existingProfile.Underlying, existingProfile.Current))
            return Reject("construct-modification-form-interaction-unresolved",
                "Modification during an alternate effective form requires a separately authorized interaction.");
        // Applicability evidence may depend on the former body. The host must
        // re-evaluate such evidence rather than carry a potentially stale ruling.
        if ((existingProfile.IndependentGrantApplicability?.Count ?? 0) != 0)
            return Reject("construct-modification-applicability-unresolved",
                "Existing independent grant applicability requires revalidation against the new body.");

        var historyId = "construct-modification:" + eventId;
        if (existingProfile.History.Any(entry => entry.Id == historyId))
            return Reject("construct-modification-event-duplicate", "The installation event was already recorded.");
        if (targetAssembly is null || targetAssembly.InitialConstruction ||
            targetAssembly.InitialAllowance is not null || targetAssembly.Components is null ||
            targetProposal is null || targetProposal.Intent != NativeOriginIntent.Custom ||
            targetProposal.Method?.MethodId != "construct-assembly" ||
            targetProposal.Method.RuleId != "rule.creature-origination" ||
            targetProposal.ExistingConfiguration is not null ||
            targetProposal.ExistingEffectiveConfiguration is not null ||
            targetProposal.ActiveTemporaryTransformation is not null ||
            (targetProposal.OtherActiveTemporaryTransformations?.Count ?? 0) != 0 ||
            targetProposal.TemporaryTransformationPrecedence is not null ||
            targetProposal.ExistingEffectiveConfigurationBasis is not null ||
            (targetProposal.History?.Count ?? 0) != 0 ||
            (targetProposal.TableDecisions?.Count ?? 0) != 0 ||
            targetProposal.Id == existingProfile.ProposalId)
            return Reject("construct-modification-target-invalid",
                "Use a distinct native Construct proposal and a non-initial assembly without a creation allowance or prior state.");

        var request = new NativeConstructOriginConstructionRequest(
            new(targetProposal, ExistingIndependentGrants: existingProfile.IndependentGrants,
                PersistentInfluences: existingProfile.PersistentInfluences), targetAssembly);
        var target = new NativeConstructOriginConstructionService(catalog).EvaluateModificationTarget(request);
        if (!target.IsMechanicallyAuthorized || target.Origin.Profile is not { } next)
            return new(false, target, [], [], [], target.Origin.Issues);

        var before = previousSession.Installation;
        var oldParts = before.Components.ToDictionary(part => part.InstanceId, StringComparer.Ordinal);
        var newParts = targetAssembly.Components.ToDictionary(part => part.InstanceId, StringComparer.Ordinal);
        var added = newParts.Keys.Except(oldParts.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var removed = oldParts.Keys.Except(newParts.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var changed = newParts.Keys.Intersect(oldParts.Keys, StringComparer.Ordinal)
            .Where(id => !SamePart(oldParts[id], newParts[id])).Order(StringComparer.Ordinal).ToArray();

        // Source identities and their ledgers must migrate together. Until
        // that contract exists, never replace or remove a generator here.
        if (added.Any(id => newParts[id].PowerSourceId is not null) ||
            removed.Any(id => oldParts[id].PowerSourceId is not null) ||
            changed.Any(id => oldParts[id].PowerSourceId != newParts[id].PowerSourceId ||
                (oldParts[id].PowerSourceId is not null &&
                    !SameHardware(oldParts[id], newParts[id]))))
            return Reject("construct-modification-source-migration-unresolved",
                "Power-source acquisition or replacement requires a verified resource/custody migration.", target);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.UnavailablePowerSourceInstanceIds ?? []),
                JsonSerializer.SerializeToElement(targetAssembly.UnavailablePowerSourceInstanceIds ?? [])))
            return Reject("construct-modification-outage-migration-unresolved",
                "Installation changes must not clear existing source outages.", target);

        var oldGrants = existingProfile.Underlying.InherentGrants.ToDictionary(grant => grant.Id, StringComparer.Ordinal);
        var historicalIds = existingProfile.History
            .Where(entry => entry.PreviousConfiguration is not null)
            .SelectMany(entry => entry.PreviousConfiguration!.InherentGrants)
            .Select(grant => grant.Id).ToHashSet(StringComparer.Ordinal);
        // A resource owner ID cannot silently become a different grant. Keep
        // unchanged grants byte-equivalent; new/replaced grants need fresh IDs.
        foreach (var grant in next.Underlying.InherentGrants)
        {
            if (oldGrants.TryGetValue(grant.Id, out var old) ?
                !SameGrant(old, grant) : historicalIds.Contains(grant.Id))
                return Reject("construct-modification-grant-identity-reused",
                    "A changed or previously retired grant requires a new manifestation identity.", target);
        }
        foreach (var old in oldGrants.Values)
        {
            var same = next.Underlying.InherentGrants.FirstOrDefault(grant =>
                SameGrant(old with { Id = grant.Id, ManifestationId = grant.ManifestationId }, grant));
            if (same is not null && same.Id != old.Id)
                return Reject("construct-modification-grant-identity-changed",
                    "An unchanged grant must retain its previous manifestation and resource identity.", target);
        }

        var profile = next with
        {
            Id = existingProfile.Id,
            History = [.. existingProfile.History, new NativeOriginHistoryEvent(
                historyId, targetProposal.Method, NativeOriginEventStage.Completed,
                target.Origin.EvaluatedProposal.Contributors.Select(c => c.Id).ToArray(),
                true, existingProfile.Underlying, next.Underlying.ProfileId)]
        };
        var review = new NativeOriginContractValidator(catalog).ReviewProfile(profile,
            target.Origin.EvaluatedProposal);
        if (review.Issues.Count != 0)
            return new(false, Uncommittable(target), added, removed, changed, review.Issues);

        return new(true, target with { Origin = target.Origin with { Profile = profile } },
            added, removed, changed, []);
    }

    // Never expose a mechanically authorized target when the transition itself
    // was rejected. Keep only the quotation and proposal for diagnostics.
    private static NativeConstructOriginConstructionResult? Uncommittable(
        NativeConstructOriginConstructionResult? target) =>
        target is null ? null : new(target.Assembly, target.Origin with
        { Status = NativeOriginConstructionStatus.Incomplete, Profile = null });

    private static bool SameGrant(NativeOriginEffectiveGrant first, NativeOriginEffectiveGrant second) =>
        JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first), JsonSerializer.SerializeToElement(second));

    // Wiring and mounting may change without replacing the physical generator.
    private static bool SameHardware(NativeConstructComponentInstance first, NativeConstructComponentInstance second) =>
        SamePart(first with { ConnectedTo = null, ParentInstanceId = null, MountInterfaceId = null,
            Enabled = true, ModeId = null, ProtectedRegionId = null },
            second with { ConnectedTo = null, ParentInstanceId = null, MountInterfaceId = null,
            Enabled = true, ModeId = null, ProtectedRegionId = null });

    private static bool SamePart(NativeConstructComponentInstance first, NativeConstructComponentInstance second) =>
        JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first), JsonSerializer.SerializeToElement(second));
}
