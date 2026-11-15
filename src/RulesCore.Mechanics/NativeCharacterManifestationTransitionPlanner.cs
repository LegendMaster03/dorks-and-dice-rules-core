namespace RulesCore.Mechanics;

/// <summary>
/// A pure optimistic-concurrency transition planner. Its Proposed ledger is a
/// detached candidate, not a committed character mutation. A Character Sheet
/// host must atomically compare the expected revision, record the stable event
/// ID and commit all steps, resources and interaction decisions together.
/// </summary>
public static class NativeCharacterManifestationTransitionPlanner
{
    public static NativeManifestationTransitionPlan Plan(
        NativeCharacterState state, NativeManifestationLedger current,
        NativeManifestationTransitionRequest request,
        INativeCharacterManifestationAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authority);
        var issues = new List<NativeIssue>();
        void Issue(string code, string path, string detail) =>
            issues.Add(new(code, path, detail));
        NativeManifestationTransitionPlan Failed(string status) =>
            new(status, request.ExpectedRevision, null,
                issues.OrderBy(x => x.Path, StringComparer.Ordinal)
                    .ThenBy(x => x.Code, StringComparer.Ordinal).ToArray());

        var ledgerIssues = NativeCharacterManifestationResolver.Validate(state, current, authority);
        if (ledgerIssues.Length != 0)
        {
            issues.AddRange(ledgerIssues);
            return Failed("invalid");
        }
        // Resolve primary state before considering an alternate target.
        // Source-authorized conflicts on the underlying character are not
        // canceled by switching into a form that does not target that field.
        var underlying = NativeCharacterInfluenceResolver.Resolve(state,
            NativeCharacterSheetResolver.PrimarySheetId, authority);
        if (underlying.Status is "invalid" or "unresolved")
        {
            issues.AddRange(underlying.Issues);
            return Failed(underlying.Status);
        }

        if (request.CharacterId != state.CharacterId ||
            request.ExpectedRevision != current.Revision ||
            current.Revision == long.MaxValue ||
            string.IsNullOrWhiteSpace(request.EventId) ||
            request.EventId.Length > 160 ||
            request.Steps is null || request.Steps.Count is < 1 or > 512 ||
            (request.ReplacementInteractions?.Count ?? 0) > 512)
        {
            Issue("invalid-manifestation-transition", "transition",
                "A valid character, matching revision, event and bounded step list are required.");
            return Failed("invalid");
        }
        if (current.ProcessedEventIds.Contains(request.EventId, StringComparer.Ordinal))
        {
            Issue("duplicate-manifestation-event", "transition.eventId",
                "The event was already applied; replay must be rejected by the host.");
            return Failed("invalid");
        }

        // Check all user-shaped records before invoking the host's transition
        // verifier. A claimed receipt or native rule is never self-authenticating.
        foreach (var step in request.Steps)
        {
            if (step is null || !Enum.IsDefined(step.Action) ||
                string.IsNullOrWhiteSpace(step.TargetInstanceId) ||
                !NativeCharacterManifestationResolver.ValidOccurrence(step.RuleOccurrence) ||
                !NativeCharacterManifestationResolver.ValidBasis(step.Basis) ||
                string.IsNullOrWhiteSpace(step.AuthorizationEvidenceId) ||
                (step.Action == NativeManifestationTransitionAction.Activate) !=
                    (step.NewInstance is not null) ||
                // Even an empty activation payload is invalid for a
                // non-activation step; the event shape must have one meaning.
                (step.Action != NativeManifestationTransitionAction.Activate &&
                    step.NewResources is not null) ||
                (step.NewResources?.Count ?? 0) > 256 ||
                (step.Action == NativeManifestationTransitionAction.End &&
                    (string.IsNullOrWhiteSpace(step.EndingConditionId) ||
                     step.EndingConditionId.Length > 160)) ||
                (step.Action != NativeManifestationTransitionAction.End &&
                    step.EndingConditionId is not null))
                Issue("invalid-manifestation-step", "transition.steps",
                    "Each operation needs a valid source, evidence and correctly shaped payload.");
        }
        if (issues.Count != 0) return Failed("invalid");

        var instances = current.Instances.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var resources = current.Resources.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var sheets = state.AlternateSheets.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var touched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in request.Steps)
        {
            var path = "transition.steps." + step.TargetInstanceId;
            if (!touched.Add(step.TargetInstanceId))
            {
                Issue("duplicate-manifestation-step", path,
                    "The same instance may only change lifecycle once per transaction.");
                continue;
            }
            if (step.Action == NativeManifestationTransitionAction.Activate)
            {
                var item = step.NewInstance!;
                if (string.IsNullOrWhiteSpace(item.Id) ||
                    string.IsNullOrWhiteSpace(item.SheetId) ||
                    item.ResourcePolicies is null ||
                    instances.ContainsKey(item.Id) || item.Id != step.TargetInstanceId ||
                    item.ActivationEventId != request.EventId ||
                    item.LastEventId != request.EventId ||
                    item.Lifecycle != NativeManifestationLifecycle.Active ||
                    item.Contribution != NativeManifestationContribution.Contributing ||
                    !sheets.ContainsKey(item.SheetId))
                {
                    Issue("invalid-manifestation-activation", path,
                        "Activation must mint one new, authorized instance of an owned alternate sheet.");
                    continue;
                }
                if (step.NewResources?.Any(x => x is null ||
                    x.OwnerKind != NativeManifestationResourceOwner.Manifestation ||
                    x.OwnerId != item.Id ||
                    !item.ResourcePolicies.Any(policy => policy.ResourceId == x.Id)) == true ||
                    step.NewResources?.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count()
                        != step.NewResources?.Count)
                {
                    Issue("invalid-manifestation-activation-resource", path,
                        "New resources must belong to this activation and have an explicit policy.");
                    continue;
                }
                instances.Add(item.Id, item);
                foreach (var resource in step.NewResources ?? [])
                    if (!resources.TryAdd(resource.Id, resource))
                        Issue("duplicate-manifestation-resource", path,
                            "New resources can not replace an existing pool.");
            }
            else
            {
                if (!instances.TryGetValue(step.TargetInstanceId, out var item) ||
                    item.Lifecycle != NativeManifestationLifecycle.Active ||
                    step.Action == NativeManifestationTransitionAction.Suppress &&
                        item.Contribution != NativeManifestationContribution.Contributing ||
                    step.Action == NativeManifestationTransitionAction.Restore &&
                        item.Contribution != NativeManifestationContribution.Suppressed)
                {
                    Issue("invalid-manifestation-lifecycle", path,
                        "The requested transition is not valid for the instance lifecycle.");
                    continue;
                }
                instances[item.Id] = item with
                {
                    Lifecycle = step.Action == NativeManifestationTransitionAction.End
                        ? NativeManifestationLifecycle.Ended : item.Lifecycle,
                    Contribution = step.Action switch
                    {
                        NativeManifestationTransitionAction.Suppress =>
                            NativeManifestationContribution.Suppressed,
                        NativeManifestationTransitionAction.Restore =>
                            NativeManifestationContribution.Contributing,
                        _ => item.Contribution
                    },
                    LastEventId = request.EventId
                };
            }
        }
        if (issues.Count != 0) return Failed("invalid");

        var proposed = new NativeManifestationLedger(
            current.CharacterId, checked(current.Revision + 1),
            instances.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            resources.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            current.ProcessedEventIds.Append(request.EventId).ToArray(),
            request.ReplacementInteractions ?? current.Interactions);

        // Validate the entire proposed state before checking scoped host rule
        // authority. Do not ask the host to treat the uncommitted next revision
        // as if it were already authoritative.
        var proposedIssues = NativeCharacterManifestationResolver.Validate(
            state, proposed, authority, authenticate: false);
        if (proposedIssues.Length != 0)
        {
            issues.AddRange(proposedIssues);
            return Failed("invalid");
        }

        // Interaction metadata is not itself a lifecycle transition. A
        // Terminate decision must be backed by an explicit End in this event.
        var explicitlyEnded = request.Steps
            .Where(x => x.Action == NativeManifestationTransitionAction.End)
            .Select(x => x.TargetInstanceId).ToHashSet(StringComparer.Ordinal);
        foreach (var decision in request.ReplacementInteractions ?? [])
        {
            if (decision.Kind != NativeManifestationInteractionKind.Terminate)
                continue;
            if (!decision.InstanceIds.Any(explicitlyEnded.Contains))
                Issue("manifestation-termination-unexecuted", "interactions." + decision.Id,
                    "A termination interaction requires an explicit authorized End step.");
            if (decision.SelectedInstanceId is not null &&
                (!instances.TryGetValue(decision.SelectedInstanceId, out var survivor) ||
                 survivor.Lifecycle != NativeManifestationLifecycle.Active))
                Issue("manifestation-termination-selection-invalid", "interactions." + decision.Id,
                    "A selected survivor must remain active after termination.");
        }
        if (issues.Count != 0) return Failed("invalid");

        foreach (var step in request.Steps)
        {
            // Later source-defined ending events and scoped DM decisions are
            // possible, but require separate authenticated host verification.
            // An unlisted label alone can never terminate a manifestation.
            if (step.Action == NativeManifestationTransitionAction.End)
            {
                var target = current.Instances.Single(x => x.Id == step.TargetInstanceId);
                if (!target.EndingConditions.Contains(step.EndingConditionId!, StringComparer.Ordinal) &&
                    !authority.AuthorizesUnlistedEnding(state.CharacterId, current, target, step))
                    Issue("manifestation-ending-unverified", "transition.steps." + step.TargetInstanceId,
                        "This ending event was not declared or independently authorized.");
            }
            if (!authority.AuthorizesTransition(state.CharacterId, current, request, step))
                Issue("unauthorized-manifestation-transition", "transition.steps." + step.TargetInstanceId,
                    "The host did not verify this event, trigger, rule or DM scope.");
            if (step.Action != NativeManifestationTransitionAction.Activate) continue;
            var instance = step.NewInstance!;
            if (!authority.AuthorizesInstance(state.CharacterId, instance, sheets[instance.SheetId]))
                Issue("unauthorized-manifestation-activation", "instances." + instance.Id,
                    "Possession of the exact form and its activation eligibility must be verified.");
            foreach (var policy in instance.ResourcePolicies)
                if (!authority.AuthorizesResourcePolicy(state.CharacterId, instance, policy))
                    Issue("unauthorized-manifestation-resource-policy", "instances." + instance.Id,
                        "The policy has no authenticated source rule.");
            foreach (var resource in step.NewResources ?? [])
                if (!authority.AuthorizesResource(state.CharacterId, resource))
                    Issue("unauthorized-manifestation-resource", "resources." + resource.Id,
                        "The source did not authorize this new resource and its initial quantity.");
        }
        if (request.ReplacementInteractions is not null)
            foreach (var decision in request.ReplacementInteractions)
                if (!authority.AuthorizesInteraction(state.CharacterId, decision))
                    Issue("unauthorized-manifestation-interaction", "interactions." + decision.Id,
                        "The decision requires the exact source or authenticated DM scope.");
        if (issues.Count != 0) return Failed("invalid");

        // Resolve against current parent/influences/resources before offering a
        // commit candidate. Do not treat an uncommitted next revision as host-
        // verified persistent state; all new records were authorized above.
        var evaluation = NativeCharacterManifestationResolver.ResolveProposed(
            state, proposed, authority);
        if (evaluation.Status is "invalid" or "unresolved")
        {
            issues.AddRange(evaluation.Issues);
            return Failed(evaluation.Status);
        }
        return new("planned", request.ExpectedRevision,
            NativeCharacterManifestationResolver.Snapshot(proposed), [],
            evaluation);
    }
}
