using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// ExistingProfile must come from authoritative, already-authorized character state,
/// not from a client-authored history snapshot. TrustedScope and authority must come
/// from the authenticated application boundary. No persistence is performed here.
/// </summary>
public sealed record NativeOriginPermanentReconstructionRequest(
    NativeOriginProfile ExistingProfile,
    NativeOriginProposal TargetProposal,
    string CompletionId,
    NativeTableDecisionScope? TrustedScope = null);

/// <summary>
/// ResultingProfile is present only after the complete next baseline and its
/// authorization have been validated. An incomplete preview never commits a Rite.
/// </summary>
public sealed record NativeOriginPermanentReconstructionResult(
    NativeOriginConstructionStatus Status,
    NativeOriginProfile? ResultingProfile,
    NativeOriginConstructionResult? TargetConstruction,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool IsMechanicallyAuthorized =>
        Status == NativeOriginConstructionStatus.Authorized && ResultingProfile is not null;
}

/// <summary>
/// Atomic, source-attributed permanent Species/Ancestry reconstruction. The shared
/// construction engine builds and validates the new configuration; this service
/// governs only the authorized transition from the prior authoritative baseline.
/// </summary>
public sealed class NativeOriginPermanentReconstructionEngine(NativeCatalogSnapshot catalog)
{
    private readonly NativeOriginContractValidator _validator = new(catalog);
    private readonly NativeOriginConstructionEngine _construction = new(catalog);

    public NativeOriginPermanentReconstructionResult Execute(NativeOriginPermanentReconstructionRequest request,
        INativeOriginAuthority? authority = null,
        INativeOriginNativeCompletionAuthority? nativeCompletionAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExistingProfile);
        ArgumentNullException.ThrowIfNull(request.TargetProposal);
        var issues = new List<NativeOriginContractIssue>();
        NativeOriginConstructionResult? target = null;
        var proposal = request.TargetProposal;
        var existing = request.ExistingProfile;
        var rite = proposal.Method?.MethodId == "rite-of-rebirth";
        var eventId = (rite ? "rite:" : "reconstruction:" + proposal.Method?.MethodId + ":") +
            proposal.Id;

        void Add(string code, string path, NativeOriginIssueSeverity severity, string detail) =>
            issues.Add(new(code, path, severity, detail));

        NativeTableRuleDecision? Verified(string kind, string id)
        {
            if (authority is null || request.TrustedScope is null) return null;
            foreach (var d in proposal.TableDecisions ?? [])
            {
                if (d?.Target?.Kind != kind || d.Target.Id != id ||
                    d.Scope is null || !(
                        d.Scope.Kind == request.TrustedScope.Kind &&
                        d.Scope.Id == request.TrustedScope.Id ||
                        d.Scope.Kind == "proposal" && d.Scope.Id == proposal.Id))
                    continue;
                if (authority.Authorizes(d, request.TrustedScope))
                    return d;
            }
            return null;
        }

        var method = _validator.FindMethod(proposal.Method);
        if (proposal.Intent != NativeOriginIntent.Direct ||
            method?.Stage != "post-origination" ||
            method.CreatesNewIndividual != false)
            Add(rite ? "rebirth-method-invalid" : "reconstruction-method-invalid",
                "method", NativeOriginIssueSeverity.Invalid,
                "Permanent reconstruction requires a selected post-origination method on the same individual.");
        if (method is not null &&
            method.Operation != "replace-complete-species-and-ancestry-configuration")
            Add("reconstruction-operation-unavailable", "method",
                NativeOriginIssueSeverity.Unresolved,
                "The listed method has no executable complete-replacement policy.");
        if (rite && method?.Reference.MethodId != "rite-of-rebirth")
            Add("rebirth-method-invalid", "method", NativeOriginIssueSeverity.Invalid,
                "A Rite must use the selected native Rite method.");
        if (string.IsNullOrWhiteSpace(proposal.Id) || string.IsNullOrWhiteSpace(request.CompletionId) ||
            proposal.ExistingConfiguration is not null ||
            proposal.ExistingEffectiveConfiguration is not null ||
            proposal.ActiveTemporaryTransformation is not null ||
            (proposal.OtherActiveTemporaryTransformations?.Count ?? 0) != 0 ||
            proposal.TemporaryTransformationPrecedence is not null ||
            proposal.ExistingEffectiveConfigurationBasis is not null ||
            (proposal.History?.Count ?? 0) != 0)
            Add("rebirth-request-invalid", "targetProposal", NativeOriginIssueSeverity.Invalid,
                "A Rite must use a distinct target construction proposal; prior state comes only from the authoritative profile.");
        if (existing.Adjudication?.Adjudication is not
            (NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized))
            Add("rebirth-existing-unresolved", "existingProfile", NativeOriginIssueSeverity.Unresolved,
                "The existing underlying Origin must already be mechanically authorized.");
        if (existing.History?.Any(x => x?.Id == eventId) == true)
            Add("rebirth-event-duplicate", "targetProposal.id", NativeOriginIssueSeverity.Invalid,
                "A Rite event must have an identity distinct from previously completed events.");

        // Old state may include earlier independently authorized DM grants. Structural
        // consistency is checked without pretending a new proposal reauthorizes them.
        var priorReview = _validator.ReviewProfile(existing);
        issues.AddRange(priorReview.Issues.Where(x =>
            x.Code is not ("profile-dm-decision-unverified" or "table-decision-unverified")));

        if (issues.Any(x => x.Severity == NativeOriginIssueSeverity.Invalid))
            return Result();

        target = _construction.EvaluateReconstructionTarget(
            new NativeOriginConstructionRequest(proposal, request.TrustedScope), authority);
        issues.AddRange(target.Issues);
        if (!target.IsMechanicallyAuthorized || target.Profile is null)
            return Result();
        proposal = target.EvaluatedProposal;

        // The method entry alone never authorizes a completed transition.
        // Rite currently has no selected-native individual procedure, so it
        // retains its exact scoped-DM authorization requirement. Other methods
        // may later use a native completion service verifying authored rules.
        var completion = Verified(rite ? "rite" : "permanent-reconstruction",
            request.CompletionId);
        var outcome = completion?.EffectiveOutcome ?? default;
        var speciesId = target.Profile.Underlying.SpeciesId;
        var ancestryId = target.Profile.Underlying.AncestryId;
        var authorizedDm = completion is not null &&
            ReadBool(outcome, "allow") == true &&
            MatchesTarget(outcome, "targetSpeciesId", speciesId) &&
            MatchesTarget(outcome, "targetAncestryId", ancestryId);
        // A verified but denying or mismatching DM decision cannot be silently
        // overridden by an unrelated first-party completion.
        var nativeProof = !rite && completion is null && method is not null
            ? nativeCompletionAuthority?.VerifyNativeCompletion(
                method, request.CompletionId, existing, target)
            : null;
        if (nativeProof is not null &&
            (string.IsNullOrWhiteSpace(nativeProof.CompletionEvidenceId) ||
             string.IsNullOrWhiteSpace(nativeProof.NativeRuleReference) ||
             nativeProof.NativeRuleReference == method?.Reference.RuleId ||
             !catalog.TryGetById(nativeProof.NativeRuleReference, out var sourceRule) ||
             sourceRule?.Category != "rule"))
            Add("reconstruction-native-evidence-invalid", "completionId",
                NativeOriginIssueSeverity.Invalid,
                "Native completion requires a distinct, selected-native rule and host-verified evidence.");
        if ((!authorizedDm && nativeProof is null) ||
            (completion is not null && !authorizedDm) ||
            string.IsNullOrWhiteSpace(speciesId))
            Add(rite ? "rebirth-authorization-unresolved" : "reconstruction-authorization-unresolved",
                "completionId", NativeOriginIssueSeverity.Unresolved,
                "The exact completed transition must be authorized by a trusted native procedure or scoped DM ruling.");

        var active = existing.ActiveTemporaryTransformation;
        var hasTemporary = active is not null ||
            (existing.OtherActiveTemporaryTransformations?.Count ?? 0) > 0;
        if (hasTemporary)
        {
            var interaction = Verified(rite ? "rite-form-interaction" : "permanent-form-interaction",
                request.CompletionId);
            if (active is null || interaction is null ||
                ReadBool(interaction.EffectiveOutcome, "preserveEffectiveForm") != true ||
                ReadString(interaction.EffectiveOutcome, "effectiveFormId") != active.FormId)
                Add("rebirth-temporary-interaction-unresolved", "activeTemporaryTransformation",
                    NativeOriginIssueSeverity.Unresolved,
                    "The Rite must not guess how active alternate forms interact with a new underlying baseline.");
        }
        else if (!SameConfiguration(existing.Underlying, existing.Current))
            Add("rebirth-effective-interaction-unresolved", "current",
                NativeOriginIssueSeverity.Unresolved,
                "The existing effective modification needs an authored or DM ruling before baseline replacement.");

        if (issues.Count > 0)
            return Result();

        var oldGrants = existing.Underlying.InherentGrants
            .ToDictionary(x => x.Id, StringComparer.Ordinal);
        var newGrants = target.Profile.Underlying.InherentGrants
            .ToDictionary(x => x.Id, StringComparer.Ordinal);

        var retainedIds = ReadIds(outcome, "retainPreviousGrantIds", Add);
        var suppressedIds = ReadIds(outcome, "suppressTargetGrantIds", Add);
        foreach (var id in retainedIds)
            if (!oldGrants.ContainsKey(id))
                Add("rebirth-retention-unknown", "riteId",
                    NativeOriginIssueSeverity.Invalid,
                    "Retained inherent grant IDs must identify the prior authoritative baseline.");
        foreach (var id in suppressedIds)
            if (!newGrants.ContainsKey(id))
                Add("rebirth-suppression-unknown", "riteId",
                    NativeOriginIssueSeverity.Invalid,
                    "Suppressed grant IDs must identify a grant in the authorized target.");
        if (issues.Count > 0)
            return Result();

        // Preserve independent entitlements by identity. Previous inherent grants
        // are excluded by default regardless of trait name or matching mechanics.
        // Only the authenticated Rite outcome may explicitly retain old ones.
        // Exact retained grants are historical exceptions; sorting avoids a
        // HashSet's iteration order becoming an observable reconstruction rule.
        var grants = retainedIds.OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => oldGrants[id]).ToList();
        var usedIds = existing.IndependentGrants.Select(x => x.Id)
            .Concat(grants.Select(x => x.Id)).ToHashSet(StringComparer.Ordinal);
        foreach (var grant in newGrants.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (suppressedIds.Contains(grant.Id)) continue;
            var id = grant.Id;
            var sequence = 0;
            while (!usedIds.Add(id))
                id = eventId + ":" + grant.Id + ":" + ++sequence;
            grants.Add(grant with { Id = id });
        }

        var baseline = target.Profile.Underlying with
        {
            InherentGrants = grants,
            ProfileId = "origin:" + proposal.Id
        };
        var next = new NativeOriginProfile(existing.Id, baseline,
            hasTemporary ? existing.Current : baseline,
            existing.IndependentGrants,
            [.. (existing.History ?? []), new NativeOriginHistoryEvent(eventId, proposal.Method,
                NativeOriginEventStage.Completed,
                proposal.Contributors.Select(x => x.Id)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                true, existing.Underlying, baseline.ProfileId, completion?.Id)],
            authorizedDm
                ? new(NativeOriginAdjudication.DmAuthorized, TableDecisionId: completion!.Id)
                : new(NativeOriginAdjudication.Supported,
                    NativeRuleReference: nativeProof!.NativeRuleReference),
            proposal.Id, existing.PersistentInfluences,
            existing.ActiveTemporaryTransformation,
            existing.OtherActiveTemporaryTransformations,
            existing.TemporaryTransformationPrecedence,
            existing.EffectiveConfigurationBasis,
            existing.IndependentGrantApplicability);

        // The target's validated manifests account for new grants; old grants
        // retained by explicit Rite permission were validated when first earned.
        // Their historic candidate IDs are not falsely inserted as new offers.
        var review = _validator.ReviewProfile(next, proposal);
        issues.AddRange(review.Issues.Where(x =>
            !(retainedIds.Any(id => x.Path == "underlying.grants." + id) &&
              x.Code is ("unknown-manifestation-grant" or "unknown-grant-offer" or
                  "table-decision-unverified")) &&
            !(hasTemporary && x.Code == "table-decision-unverified" &&
              x.Path.StartsWith("current.grants.", StringComparison.Ordinal)) &&
            !(x.Code == "table-decision-unverified" &&
              x.Path.StartsWith("independentGrantApplicability.", StringComparison.Ordinal)) &&
            !(hasTemporary && x.Code == "table-decision-unverified" &&
              x.Path.StartsWith("activeTemporaryTransformation", StringComparison.Ordinal))));
        if (issues.Count > 0)
            return Result();

        return new(NativeOriginConstructionStatus.Authorized, next, target, []);

        NativeOriginPermanentReconstructionResult Result() => new(Status(issues), null, target,
            issues.OrderBy(x => x.Path, StringComparer.Ordinal)
                .ThenBy(x => x.Code, StringComparer.Ordinal).Distinct().ToArray());
    }

    private static HashSet<string> ReadIds(JsonElement value, string property,
        Action<string, string, NativeOriginIssueSeverity, string> add)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(property, out var array)) return ids;
        if (array.ValueKind != JsonValueKind.Array)
        {
            add("rebirth-grant-ids-invalid", property, NativeOriginIssueSeverity.Invalid,
                "Grant modifications must be exact arrays of distinct grant IDs.");
            return ids;
        }
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()) ||
                !ids.Add(item.GetString()!))
                add("rebirth-grant-ids-invalid", property, NativeOriginIssueSeverity.Invalid,
                    "Grant modification IDs must be nonempty and unique.");
        }
        return ids;
    }

    private static bool MatchesTarget(JsonElement outcome, string property, string? expected) =>
        outcome.ValueKind == JsonValueKind.Object &&
        outcome.TryGetProperty(property, out var actual) &&
        (actual.ValueKind == JsonValueKind.Null && expected is null ||
         actual.ValueKind == JsonValueKind.String && actual.GetString() == expected);

    private static bool? ReadBool(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var item) &&
        item.ValueKind is JsonValueKind.True or JsonValueKind.False ?
            item.GetBoolean() : null;

    private static string? ReadString(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var item) &&
        item.ValueKind == JsonValueKind.String ? item.GetString() : null;

    private static bool SameConfiguration(
        NativeOriginCurrentConfiguration first, NativeOriginCurrentConfiguration second)
    {
        var a = first with { ProfileId = null, InherentGrants =
            first.InherentGrants.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray() };
        var b = second with { ProfileId = null, InherentGrants =
            second.InherentGrants.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray() };
        return JsonElement.DeepEquals(JsonSerializer.SerializeToElement(a),
            JsonSerializer.SerializeToElement(b));
    }

    private static NativeOriginConstructionStatus Status(
        IReadOnlyList<NativeOriginContractIssue> issues) =>
        issues.Any(x => x.Severity == NativeOriginIssueSeverity.Invalid) ?
            NativeOriginConstructionStatus.Invalid :
        issues.Any(x => x.Severity == NativeOriginIssueSeverity.Conflict) ?
            NativeOriginConstructionStatus.Conflicting :
        issues.Any(x => x.Severity == NativeOriginIssueSeverity.Incomplete) ?
            NativeOriginConstructionStatus.Incomplete :
        issues.Any(x => x.Severity == NativeOriginIssueSeverity.Unresolved) ?
            NativeOriginConstructionStatus.Unresolved :
            NativeOriginConstructionStatus.Authorized;
}
