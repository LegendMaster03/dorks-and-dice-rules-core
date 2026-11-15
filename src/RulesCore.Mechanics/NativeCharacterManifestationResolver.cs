using System.Collections.Frozen;

namespace RulesCore.Mechanics;

/// <summary>
/// Validates the host-owned manifestation ledger and resolves its currently
/// contributing sheet through the existing sheet and influence evaluators.
/// This does not select an unverified form, restore a snapshot, mutate resources,
/// or imply that an alternate form has fully playable Class/HP mechanics.
/// </summary>
public static class NativeCharacterManifestationResolver
{
    internal static NativeIssue[] Validate(
        NativeCharacterState state, NativeManifestationLedger ledger,
        INativeCharacterManifestationAuthority authority, bool authenticate = true)
    {
        var issues = new List<NativeIssue>();
        void Issue(string code, string path, string detail) =>
            issues.Add(new(code, path, detail));

        if (state is null || ledger is null || authority is null ||
            string.IsNullOrWhiteSpace(state.CharacterId) ||
            state.CharacterId != ledger.CharacterId || ledger.Revision < 0 ||
            ledger.Instances is null || ledger.Resources is null ||
            ledger.ProcessedEventIds is null || ledger.Instances.Count > 512 ||
            ledger.Resources.Count > 4096 || ledger.ProcessedEventIds.Count > 16384 ||
            (ledger.Interactions?.Count ?? 0) > 512 ||
            state.AlternateSheets is null)
            return [new("invalid-manifestation-ledger", "manifestations",
                "The character, revision, or bounded ledger shape is invalid.")];

        var sheetIds = new Dictionary<string, NativePersistentCharacterSheet>(StringComparer.Ordinal);
        foreach (var sheet in state.AlternateSheets)
            if (sheet is null || string.IsNullOrWhiteSpace(sheet.Id) ||
                !sheetIds.TryAdd(sheet.Id, sheet))
                Issue("invalid-manifestation-sheet-reference", "alternateSheets",
                    "Form references require unique, nonempty sheet identities.");
        if (issues.Count != 0) return Sort(issues);
        var influenceIds = new HashSet<string>(
            (state.PersistentInfluences ?? []).Where(x => x is not null).Select(x => x.Id),
            StringComparer.Ordinal);
        var instances = new Dictionary<string, NativeManifestationInstance>(StringComparer.Ordinal);
        var resources = new HashSet<string>(StringComparer.Ordinal);
        var interactions = new HashSet<string>(StringComparer.Ordinal);
        var events = new HashSet<string>(StringComparer.Ordinal);
        foreach (var eventId in ledger.ProcessedEventIds)
            if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > 160 || !events.Add(eventId))
                Issue("invalid-manifestation-event", "processedEventIds",
                    "Event identities must be distinct and nonempty.");

        foreach (var instance in ledger.Instances)
        {
            if (instance is null || string.IsNullOrWhiteSpace(instance.Id) ||
                instance.Id.Length > 160 || !instances.TryAdd(instance.Id, instance))
            {
                Issue("invalid-manifestation-instance", "instances",
                    "Instance identities must be unique and present.");
                continue;
            }
            var path = "instances." + instance.Id;
            if (string.IsNullOrWhiteSpace(instance.SheetId) ||
                !sheetIds.ContainsKey(instance.SheetId) ||
                string.IsNullOrWhiteSpace(instance.DefinitionId) ||
                !ValidOccurrence(instance.DefinitionOccurrence) ||
                (instance.SourceInfluenceId is not null &&
                 !influenceIds.Contains(instance.SourceInfluenceId)) ||
                string.IsNullOrWhiteSpace(instance.ActivationEventId) ||
                string.IsNullOrWhiteSpace(instance.ActivationEvidenceId) ||
                !ValidBasis(instance.ActivationBasis) ||
                !Enum.IsDefined(instance.Lifecycle) ||
                !Enum.IsDefined(instance.Contribution) ||
                instance.ActivationCaptures is null || instance.ActivationCaptures.Count > 128 ||
                instance.ActivationCaptures.Any(x => string.IsNullOrWhiteSpace(x.Key) ||
                    x.Key.Length > 160 || x.Value is < 0 or > 1000000) ||
                instance.EndingConditions is null || instance.EndingConditions.Count > 128 ||
                instance.EndingConditions.Any(string.IsNullOrWhiteSpace) ||
                instance.ResourcePolicies is null || instance.ResourcePolicies.Count > 256 ||
                string.IsNullOrWhiteSpace(instance.LastEventId) ||
                !events.Contains(instance.ActivationEventId) ||
                !events.Contains(instance.LastEventId))
                Issue("invalid-manifestation-instance", path,
                    "The verified form, activation, capture or event evidence is incomplete.");
            var policyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var policy in instance.ResourcePolicies ?? [])
                if (policy is null || string.IsNullOrWhiteSpace(policy.ResourceId) ||
                    !policyIds.Add(policy.ResourceId) || !Enum.IsDefined(policy.Mode) ||
                    (policy.Mode == NativeManifestationResourceMode.AdjustMaximum) !=
                        (policy.MaximumAdjustment is not null) ||
                    (policy.Mode == NativeManifestationResourceMode.CaptureMaximum) !=
                        (policy.CaptureKey is not null) ||
                    (policy.CaptureKey is not null &&
                     (instance.ActivationCaptures is null ||
                     !instance.ActivationCaptures.Keys.Contains(policy.CaptureKey, StringComparer.Ordinal))))
                    Issue("invalid-manifestation-resource-policy", path + ".resourcePolicies",
                        "Resource policies require an explicit, distinct pool and valid mode fields.");
        }

        foreach (var resource in ledger.Resources)
        {
            if (resource is null || string.IsNullOrWhiteSpace(resource.Id) ||
                !resources.Add(resource.Id) || !Enum.IsDefined(resource.OwnerKind) ||
                string.IsNullOrWhiteSpace(resource.OwnerId) ||
                string.IsNullOrWhiteSpace(resource.DefinitionId) ||
                !ValidOccurrence(resource.SourceOccurrence) ||
                string.IsNullOrWhiteSpace(resource.EvidenceId) ||
                resource.Current < 0 || resource.Maximum is < 0 or > 1000000 ||
                resource.Current > resource.Maximum ||
                !(resource.OwnerKind switch
                {
                    NativeManifestationResourceOwner.Primary =>
                        resource.OwnerId == NativeCharacterSheetResolver.PrimarySheetId,
                    NativeManifestationResourceOwner.Sheet => sheetIds.ContainsKey(resource.OwnerId),
                    NativeManifestationResourceOwner.Influence => influenceIds.Contains(resource.OwnerId),
                    NativeManifestationResourceOwner.Manifestation => instances.ContainsKey(resource.OwnerId),
                    _ => false
                }))
                Issue("invalid-manifestation-resource", "resources",
                    "A resource must have one authenticated owner, source and valid quantity.");
        }
        foreach (var instance in instances.Values)
            foreach (var policy in instance.ResourcePolicies ?? [])
                if (policy is not null && !resources.Contains(policy.ResourceId))
                    Issue("missing-manifestation-resource", "instances." + instance.Id,
                        "A form can not reference a resource absent from the ledger.");

        foreach (var decision in ledger.Interactions ?? [])
        {
            if (decision is null || string.IsNullOrWhiteSpace(decision.Id) ||
                !interactions.Add(decision.Id) || !Enum.IsDefined(decision.Kind) ||
                !ValidOccurrence(decision.DefinitionOccurrence) ||
                !ValidBasis(decision.Basis) ||
                string.IsNullOrWhiteSpace(decision.AuthorizationEvidenceId) ||
                decision.InstanceIds is null || decision.InstanceIds.Count is < 2 or > 512 ||
                decision.InstanceIds.Distinct(StringComparer.Ordinal).Count() != decision.InstanceIds.Count ||
                decision.InstanceIds.Any(id =>
                    string.IsNullOrWhiteSpace(id) || !instances.ContainsKey(id)) ||
                (decision.SelectedInstanceId is not null &&
                 !decision.InstanceIds.Contains(decision.SelectedInstanceId, StringComparer.Ordinal)) ||
                (decision.Kind is NativeManifestationInteractionKind.Suppress or
                    NativeManifestationInteractionKind.Replace &&
                 decision.SelectedInstanceId is null))
                Issue("invalid-manifestation-interaction", "interactions",
                    "Interaction evidence, participants or selection is invalid.");
        }
        if (issues.Count != 0) return Sort(issues);
        if (!authenticate) return [];

        if (!authority.AuthorizesLedger(state.CharacterId, ledger))
            Issue("unauthorized-manifestation-ledger", "manifestations",
                "The host did not verify the revision-current manifestation journal.");
        foreach (var instance in instances.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (!authority.AuthorizesInstance(state.CharacterId, instance, sheetIds[instance.SheetId]))
                Issue("unauthorized-manifestation-instance", "instances." + instance.Id,
                    "Form acquisition, source and eligibility were not independently verified.");
            foreach (var policy in instance.ResourcePolicies)
                if (!authority.AuthorizesResourcePolicy(state.CharacterId, instance, policy))
                    Issue("unauthorized-manifestation-resource-policy", "instances." + instance.Id,
                        "The source did not authorize this form-specific resource policy.");
        }
        foreach (var resource in ledger.Resources)
            if (!authority.AuthorizesResource(state.CharacterId, resource))
                Issue("unauthorized-manifestation-resource", "resources." + resource.Id,
                    "Resource identity, owner, quantity and source were not verified.");
        foreach (var interaction in ledger.Interactions ?? [])
            if (!authority.AuthorizesInteraction(state.CharacterId, interaction))
                Issue("unauthorized-manifestation-interaction", "interactions." + interaction.Id,
                    "A source rule or appropriately scoped DM authorization is required.");
        return Sort(issues);
    }

    private static NativeIssue[] Sort(IEnumerable<NativeIssue> issues) => issues
        .OrderBy(x => x.Path, StringComparer.Ordinal)
        .ThenBy(x => x.Code, StringComparer.Ordinal)
        .ThenBy(x => x.Detail, StringComparer.Ordinal).ToArray();

    internal static bool ValidOccurrence(NativeOriginSourceOccurrence? occurrence) =>
        occurrence?.Source is { } source && Enum.IsDefined(source.Kind) &&
        !string.IsNullOrWhiteSpace(source.Id) &&
        !string.IsNullOrWhiteSpace(source.Version) &&
        !string.IsNullOrWhiteSpace(occurrence.OccurrencePath);

    internal static bool ValidBasis(NativeOriginDecisionBasis? basis) =>
        basis is not null &&
        basis.Adjudication is NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized &&
        (!string.IsNullOrWhiteSpace(basis.NativeRuleReference) ||
         !string.IsNullOrWhiteSpace(basis.TableDecisionId));

    internal static NativeManifestationLedger Snapshot(NativeManifestationLedger ledger) =>
        ledger with
        {
            Instances = Array.AsReadOnly(ledger.Instances.Select(instance => instance with
            {
                ActivationCaptures = instance.ActivationCaptures.ToFrozenDictionary(StringComparer.Ordinal),
                EndingConditions = Array.AsReadOnly(instance.EndingConditions.ToArray()),
                ResourcePolicies = Array.AsReadOnly(instance.ResourcePolicies.ToArray())
            }).ToArray()),
            Resources = Array.AsReadOnly(ledger.Resources.ToArray()),
            ProcessedEventIds = Array.AsReadOnly(ledger.ProcessedEventIds.ToArray()),
            Interactions = ledger.Interactions is null ? null :
                Array.AsReadOnly(ledger.Interactions.Select(x => x with
                {
                    InstanceIds = Array.AsReadOnly(x.InstanceIds.ToArray())
                }).ToArray())
        };

    internal static NativeManifestationInstance? Select(
        NativeManifestationLedger ledger, List<NativeIssue> issues)
    {
        var contributing = ledger.Instances
            .Where(x => x.Lifecycle == NativeManifestationLifecycle.Active &&
                x.Contribution == NativeManifestationContribution.Contributing)
            .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        if (contributing.Length == 0) return null;
        if (contributing.Length == 1) return contributing[0];

        var activeIds = contributing.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var matching = (ledger.Interactions ?? [])
            .Where(x => x.InstanceIds.Count == activeIds.Count &&
                x.InstanceIds.All(activeIds.Contains)).Take(2).ToArray();
        if (matching.Length != 1 ||
            matching[0].Kind is not (NativeManifestationInteractionKind.Suppress or
                NativeManifestationInteractionKind.Replace) ||
            matching[0].SelectedInstanceId is null)
        {
            issues.Add(new("manifestation-interaction-unresolved", "manifestations",
                "Overlapping manifestations have no single authorized effective selection."));
            return null;
        }
        return contributing.Single(x => x.Id == matching[0].SelectedInstanceId);
    }

    public static NativeManifestationEffectiveCharacter Resolve(
        NativeCharacterState state, NativeManifestationLedger ledger,
        INativeCharacterManifestationAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(authority);
        static NativeManifestationEffectiveCharacter Failed(string status,
            IEnumerable<NativeIssue> issues) => new(status,
                NativeCharacterSheetResolver.PrimarySheetId, null, null,
                [], [], [], [], [], [], Sort(issues));

        var ledgerIssues = Validate(state, ledger, authority);
        if (ledgerIssues.Length != 0) return Failed("invalid", ledgerIssues);
        // Validate the complete owned sheet graph and influence receipts even
        // when simultaneous manifestations have no resolved effective winner.
        var baseline = NativeCharacterInfluenceResolver.Resolve(state,
            NativeCharacterSheetResolver.PrimarySheetId, authority);
        if (baseline.Status == "invalid")
            return Failed("invalid", baseline.Issues);
        var selectionIssues = new List<NativeIssue>();
        var selected = Select(ledger, selectionIssues);
        if (selectionIssues.Count != 0) return Failed("unresolved", selectionIssues);
        var target = selected?.SheetId ?? NativeCharacterSheetResolver.PrimarySheetId;
        var sheet = NativeCharacterSheetResolver.Resolve(state, target, authority);
        var influence = NativeCharacterInfluenceResolver.Resolve(state, target, authority);
        if (sheet.Status != "resolved" || sheet.EffectiveMechanics is null ||
            influence.Status is "invalid" or "unresolved" ||
            influence.EffectiveMechanics is null)
            return Failed(influence.Status == "unresolved" ? "unresolved" : "invalid",
                sheet.Issues.Concat(influence.Issues));

        var issues = new List<NativeIssue>(sheet.Issues.Concat(influence.Issues));
        var visible = new List<NativeManifestationEffectiveResource>();
        if (selected is null)
        {
            foreach (var resource in ledger.Resources.Where(x =>
                x.OwnerKind == NativeManifestationResourceOwner.Primary)
                .OrderBy(x => x.Id, StringComparer.Ordinal))
                visible.Add(new(resource.Id, resource.OwnerKind, resource.OwnerId,
                    resource.Current, resource.Maximum, resource.Maximum,
                    NativeManifestationResourceMode.Inherit));
        }
        else
        {
            var byId = ledger.Resources.ToDictionary(x => x.Id, StringComparer.Ordinal);
            foreach (var policy in selected.ResourcePolicies.OrderBy(x => x.ResourceId, StringComparer.Ordinal))
            {
                var resource = byId[policy.ResourceId];
                if (policy.Mode == NativeManifestationResourceMode.Inherit &&
                    resource.OwnerKind == NativeManifestationResourceOwner.Manifestation ||
                    policy.Mode == NativeManifestationResourceMode.Separate &&
                    resource.OwnerKind == NativeManifestationResourceOwner.Primary)
                {
                    issues.Add(new("manifestation-resource-policy-unresolved", "resources." + resource.Id,
                        "The declared mode conflicts with the resource's owner."));
                    continue;
                }
                int maximum;
                try
                {
                    maximum = policy.Mode switch
                    {
                        NativeManifestationResourceMode.AdjustMaximum =>
                            checked(resource.Maximum + policy.MaximumAdjustment!.Value),
                        NativeManifestationResourceMode.CaptureMaximum =>
                            selected.ActivationCaptures.Single(x =>
                            string.Equals(x.Key, policy.CaptureKey, StringComparison.Ordinal)).Value,
                        _ => resource.Maximum
                    };
                }
                catch (OverflowException)
                {
                    issues.Add(new("manifestation-resource-capacity-unresolved", "resources." + resource.Id,
                        "The authorized effective maximum overflowed."));
                    continue;
                }
                if (maximum is < 0 or > 1000000)
                {
                    issues.Add(new("manifestation-resource-capacity-unresolved", "resources." + resource.Id,
                        "The effective maximum is outside supported bounds."));
                    continue;
                }
                visible.Add(new(resource.Id, resource.OwnerKind, resource.OwnerId,
                    resource.Current, resource.Maximum, maximum, policy.Mode));
            }
            // The sheet has a verified manifestation, but the numeric native
            // Class-feature, HP, Origin-grant and equipment interaction policies
            // have not yet been established. Never publish it as fully playable.
            issues.Add(new("manifestation-class-policy-unresolved", "class",
                "Form-specific Class-feature and Class-resource use require authored policies."));
            issues.Add(new("manifestation-hit-points-policy-unresolved", "hit-points",
                "Current/maximum Hit Point transfer and recovery policies remain unsupported."));
            issues.Add(new("manifestation-origin-policy-unresolved", "origin",
                "The form's complete underlying Origin grant applicability remains unverified."));
        }
        var mechanics = NativeCharacterSheetResolver.CopyMechanics(influence.EffectiveMechanics);
        if (selected is not null)
            mechanics = mechanics with { CurrentResources = null };
        return new(issues.Count == 0 && influence.Status == "resolved" ? "resolved" : "partial",
            target, selected?.Id, mechanics,
            sheet.Progression, sheet.Grants, influence.Progression, influence.Grants,
            influence.AppliedEffects, Array.AsReadOnly(visible.ToArray()), Sort(issues));
    }
}
