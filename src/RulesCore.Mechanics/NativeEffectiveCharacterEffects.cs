namespace RulesCore.Mechanics;

/// <summary>
/// A narrow first executable slice of the shared effective-character layer.
/// These operations describe verified earned effects, not new entitlements.
/// A later native-rule adapter must authorize the exact source and effect.
/// </summary>
public enum NativeEffectiveEffectOperation { Add, Replace, Suspend }
public enum NativeEffectiveEffectTarget { AbilityScore, IndependentGrant }

/// <summary>
/// SourceGrantId is an earned grant occurrence, not a supernatural condition
/// name or a client-defined rule. RequiredFormId limits applicability to
/// one explicitly active form; null means the directive is persistent.
/// </summary>
public sealed record NativeEffectiveEffectDirective(
    string Id, string SourceGrantId, string RuleId,
    NativeEffectiveEffectTarget Target, string TargetId,
    NativeEffectiveEffectOperation Operation,
    int? NumericValue = null, string? RequiredFormId = null);

/// <summary>
/// The authenticated host and authored native-rule evaluator must implement this
/// service. Neither deserialized OriginStatus nor directive RuleId establishes
/// permission. Test doubles are not a production authorization mechanism.
/// </summary>
public interface INativeEffectiveEffectAuthority
{
    bool AuthorizesOrigin(NativeOriginConstructionResult result);
    bool AuthorizesDirective(NativeEffectiveEffectDirective directive,
        NativeOriginProfile profile);
    bool AuthorizesForm(NativeOriginActiveTemporaryTransformation form,
        NativeOriginProfile profile);
    bool AuthorizesApplicability(NativeOriginIndependentGrantApplicability applicability,
        NativeOriginProfile profile);
}

public sealed record NativeEffectiveCharacterRequest(
    NativeOriginConstructionResult Origin,
    IReadOnlyDictionary<string, int> AbilityScores,
    IReadOnlyList<NativeEffectiveEffectDirective> Directives);

/// <summary>
/// A non-resolved result deliberately carries no playable effective projection.
/// The caller must not treat the original Origin/AbilityScores as fallback grants.
/// </summary>
public sealed record NativeEffectiveCharacterResult(
    string Status,
    NativeOriginCurrentConfiguration? EffectiveOrigin,
    IReadOnlyDictionary<string, int> EffectiveAbilityScores,
    IReadOnlyList<string> ApplicableIndependentGrantIds,
    IReadOnlyList<string> SuppressedIndependentGrantIds,
    IReadOnlyList<NativeIssue> Issues)
{
    public bool IsResolved => Status == "resolved";
}

/// <summary>
/// Composes a restricted, provenance-aware effective view without changing the
/// authoritative baseline, earned entitlements, resource counters or Class levels.
/// This is not yet an Origin transition executor or a full character projector.
/// </summary>
public static class NativeEffectiveCharacterEffects
{
    private const int MaxAbilityScore = 1000;

    public static NativeEffectiveCharacterResult Evaluate(
        NativeEffectiveCharacterRequest request,
        INativeEffectiveEffectAuthority? authority)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<NativeIssue>();
        void Issue(string code, string path, string detail) =>
            issues.Add(new(code, path, detail));

        if (request.Origin is null || request.AbilityScores is null ||
            request.Directives is null || request.Origin.Profile is null ||
            request.Origin.EvaluatedProposal is null)
        {
            Issue("effective-input-invalid", "request",
                "Verified Origin, ability scores, and effect directives are required.");
            return Failed("invalid");
        }

        var origin = request.Origin.Profile;
        if (!request.Origin.IsMechanicallyAuthorized ||
            request.Origin.Issues is null || request.Origin.Issues.Count > 0 ||
            authority is null || !authority.AuthorizesOrigin(request.Origin))
        {
            Issue("effective-origin-not-authorized", "origin",
                "Origin requires a trusted, mechanically authorized construction result.");
            return Failed("unresolved");
        }
        if (origin.Underlying is null || origin.Current is null ||
            origin.IndependentGrants is null || origin.Current.InherentGrants is null ||
            origin.PersistentInfluences is null && origin.ActiveTemporaryTransformation is not null &&
            origin.ActiveTemporaryTransformation.PersistentInfluenceId is not null)
        {
            Issue("effective-origin-invalid", "origin",
                "Origin configuration, grants, or active influence provenance is incomplete.");
            return Failed("invalid");
        }
        if (origin.OtherActiveTemporaryTransformations is { Count: > 0 })
        {
            Issue("effective-form-precedence-unresolved", "origin.activeForms",
                "Concurrent forms require executable precedence rules; this slice has none.");
            return Failed("unresolved");
        }
        var form = origin.ActiveTemporaryTransformation;
        if (form is not null && (!authority.AuthorizesForm(form, origin) ||
            string.IsNullOrWhiteSpace(form.Id) || string.IsNullOrWhiteSpace(form.FormId) ||
            string.IsNullOrWhiteSpace(form.ActivationEvidenceId)))
        {
            Issue("effective-form-not-authorized", "origin.activeForm",
                "The active form must have verified source, activation, and rules.");
            return Failed("unresolved");
        }
        // An active form claiming a persistent influence must point to that exact
        // source occurrence. The form's name alone never establishes its source.
        if (form?.PersistentInfluenceId is { } influenceId &&
            origin.PersistentInfluences?.Any(influence => influence is not null &&
                influence.Id == influenceId &&
                influence.Source == form.EffectSource) != true)
        {
            Issue("effective-form-influence-mismatch", "origin.activeForm",
                "Active form must reference its actual persistent influence and source.");
            return Failed("unresolved");
        }

        var scores = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in request.AbilityScores)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) ||
                pair.Value is < 0 or > MaxAbilityScore ||
                !scores.TryAdd(pair.Key, pair.Value))
                Issue("effective-ability-invalid", pair.Key,
                    "An ability score must have a distinct valid identity and value.");
        }
        if (scores.Count == 0)
            Issue("effective-ability-empty", "abilityScores",
                "At least one source ability score is required.");

        var independent = new Dictionary<string, NativeOriginEffectiveGrant>(StringComparer.Ordinal);
        foreach (var grant in origin.IndependentGrants)
            if (grant is null || string.IsNullOrWhiteSpace(grant.Id) ||
                !independent.TryAdd(grant.Id, grant))
                Issue("effective-grant-invalid", "independentGrants",
                    "Earned independent grant occurrences must have distinct IDs.");

        var effectiveSources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grant in origin.Current.InherentGrants)
            if (grant is null || string.IsNullOrWhiteSpace(grant.Id) ||
                !effectiveSources.Add(grant.Id))
                Issue("effective-source-invalid", "current.grants",
                    "Effective inherent grants must have distinct source IDs.");

        foreach (var id in independent.Keys)
            if (effectiveSources.Contains(id))
                Issue("effective-grant-identity-conflict", id,
                    "Effective inherent and independent grant occurrences must be distinct.");

        var states = new Dictionary<string, NativeOriginGrantApplicabilityState>(StringComparer.Ordinal);
        foreach (var applicability in origin.IndependentGrantApplicability ?? [])
        {
            if (applicability is null ||
                string.IsNullOrWhiteSpace(applicability.GrantId) ||
                !independent.ContainsKey(applicability.GrantId) ||
                !states.TryAdd(applicability.GrantId, applicability.State))
            {
                Issue("effective-applicability-invalid", "independentGrantApplicability",
                    "An applicability claim must identify one earned independent grant.");
                continue;
            }
            if (applicability.State == NativeOriginGrantApplicabilityState.Unresolved ||
                applicability.Basis is null ||
                !authority.AuthorizesApplicability(applicability, origin))
                Issue("effective-applicability-unresolved", applicability.GrantId,
                    "Use or suppression requires a verified, explicit rule basis.");
        }

        // A missing applicability entry is not proof that a Class grant is usable.
        foreach (var id in independent.Keys)
            if (!states.ContainsKey(id))
                Issue("effective-applicability-missing", id,
                    "Independent entitlements need explicit effective-state applicability.");

        var usedDirectives = new HashSet<string>(StringComparer.Ordinal);
        var targeted = new HashSet<string>(StringComparer.Ordinal);
        var activeDirectives = new List<NativeEffectiveEffectDirective>();
        foreach (var effect in request.Directives)
        {
            if (effect is null || string.IsNullOrWhiteSpace(effect.Id) ||
                !usedDirectives.Add(effect.Id) ||
                string.IsNullOrWhiteSpace(effect.SourceGrantId) ||
                string.IsNullOrWhiteSpace(effect.RuleId) ||
                string.IsNullOrWhiteSpace(effect.TargetId) ||
                !Enum.IsDefined(effect.Operation) || !Enum.IsDefined(effect.Target))
            {
                Issue("effective-directive-invalid", "directives",
                    "An effect must identify its earned source, authored rule, operation and target.");
                continue;
            }
            if (effect.RequiredFormId is not null &&
                !string.Equals(effect.RequiredFormId, form?.FormId, StringComparison.Ordinal))
                continue;

            var sourceIsApplicableIndependent = independent.ContainsKey(effect.SourceGrantId) &&
                states.TryGetValue(effect.SourceGrantId, out var sourceApplicability) &&
                sourceApplicability == NativeOriginGrantApplicabilityState.Applicable;
            var sourceIsActiveFormGrant = effectiveSources.Contains(effect.SourceGrantId) &&
                (form is null || form.EffectiveFormGrantIds?.Contains(
                    effect.SourceGrantId, StringComparer.Ordinal) == true);
            if (!(sourceIsApplicableIndependent || sourceIsActiveFormGrant) ||
                !authority.AuthorizesDirective(effect, origin))
            {
                Issue("effective-directive-unauthorized", effect.Id,
                    "The effect has no verified applicable earned source and exact rule authorization.");
                continue;
            }
            var targetKey = effect.Target + ":" + effect.TargetId;
            if (!targeted.Add(targetKey))
            {
                Issue("effective-target-overlap-unresolved", targetKey,
                    "Multiple effects on one target need a sourced composition rule.");
                continue;
            }
            switch (effect.Target)
            {
                case NativeEffectiveEffectTarget.AbilityScore
                    when effect.Operation is NativeEffectiveEffectOperation.Add or
                        NativeEffectiveEffectOperation.Replace &&
                        effect.NumericValue is not null &&
                        scores.ContainsKey(effect.TargetId):
                    activeDirectives.Add(effect);
                    break;
                case NativeEffectiveEffectTarget.IndependentGrant
                    when effect.Operation == NativeEffectiveEffectOperation.Suspend &&
                        effect.NumericValue is null && independent.ContainsKey(effect.TargetId):
                    activeDirectives.Add(effect);
                    break;
                default:
                    Issue("effective-target-unsupported", effect.Id,
                        "Target, operation, required value, or earned grant is unsupported.");
                    break;
            }
        }

        foreach (var effect in activeDirectives)
        {
            if (effect.Target == NativeEffectiveEffectTarget.AbilityScore)
            {
                var original = scores[effect.TargetId];
                long value = effect.Operation == NativeEffectiveEffectOperation.Replace
                    ? effect.NumericValue!.Value
                    : (long)original + effect.NumericValue!.Value;
                if (value is < 0 or > MaxAbilityScore)
                    Issue("effective-numeric-out-of-range", effect.TargetId,
                        "The effective score is outside the supported projector range.");
                else scores[effect.TargetId] = (int)value;
            }
            else states[effect.TargetId] = NativeOriginGrantApplicabilityState.Suppressed;
        }

        if (issues.Count > 0) return Failed("unresolved");
        return new("resolved", origin.Current, scores,
            states.Where(pair => pair.Value == NativeOriginGrantApplicabilityState.Applicable)
                .Select(pair => pair.Key).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            states.Where(pair => pair.Value == NativeOriginGrantApplicabilityState.Suppressed)
                .Select(pair => pair.Key).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            []);

        NativeEffectiveCharacterResult Failed(string status) =>
            new(status, null, new Dictionary<string, int>(StringComparer.Ordinal),
                [], [], issues.ToArray());
    }
}
