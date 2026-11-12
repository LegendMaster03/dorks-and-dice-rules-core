using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// The authenticated host proves provenance and scoped table authority. This
/// contract must never be implemented using only client-supplied status or
/// serialized table decisions. A catalog can prove rule text, not ownership.
/// </summary>
public interface INativeEffectiveCatalogTrust
{
    bool AuthorizesOrigin(NativeOriginConstructionResult result);
    bool AuthorizesEarnedGrant(NativeOriginEffectiveGrant grant, NativeOriginProfile profile);
    bool AuthorizesApplicability(NativeOriginIndependentGrantApplicability applicability,
        NativeOriginProfile profile);
    bool AuthorizesForm(NativeOriginActiveTemporaryTransformation form,
        NativeOriginProfile profile);
    bool AuthorizesTableDecision(NativeTableRuleDecision decision,
        NativeTableDecisionScope trustedScope);
}

/// <summary>
/// Checks exact authored effect semantics against the selected immutable native
/// catalog. Optional DM effects are permitted only through a scoped, verified
/// table decision that specifies the entire effective directive. Neither
/// operation creates, imports, or upgrades source entitlements.
/// </summary>
public sealed class NativeCatalogEffectiveEffectAuthority(
    NativeCatalogSnapshot catalog,
    INativeEffectiveCatalogTrust trusted,
    NativeTableDecisionScope? trustedScope = null,
    IReadOnlyList<NativeTableRuleDecision>? tableDecisions = null)
    : INativeEffectiveEffectAuthority
{
    public bool AuthorizesOrigin(NativeOriginConstructionResult result) =>
        result is { IsMechanicallyAuthorized: true, Profile: not null } &&
        result.Issues is { Count: 0 } && trusted.AuthorizesOrigin(result);

    public bool AuthorizesApplicability(
        NativeOriginIndependentGrantApplicability applicability,
        NativeOriginProfile profile) =>
        applicability is not null && profile is not null &&
        profile.IndependentGrants?.Any(x => x is not null &&
            x.Id == applicability.GrantId && trusted.AuthorizesEarnedGrant(x, profile)) == true &&
        trusted.AuthorizesApplicability(applicability, profile);

    public bool AuthorizesForm(
        NativeOriginActiveTemporaryTransformation form,
        NativeOriginProfile profile) =>
        form is not null && profile is not null &&
        trusted.AuthorizesForm(form, profile) &&
        HasVerifiedFormOverride(form);

    public bool AuthorizesDirective(
        NativeEffectiveEffectDirective directive,
        NativeOriginProfile profile)
    {
        if (directive is null || profile is null ||
            string.IsNullOrWhiteSpace(directive.SourceGrantId) ||
            string.IsNullOrWhiteSpace(directive.RuleId))
            return false;

        // Source identities are exact occurrences. No name-based lookup or
        // "matching" unearned monster/ancestry feature is permission to act.
        var sources = (profile.IndependentGrants ?? [])
            .Concat(profile.Current?.InherentGrants ?? [])
            .Where(g => g is not null && g.Id == directive.SourceGrantId)
            .ToArray();
        if (sources.Length != 1 || !trusted.AuthorizesEarnedGrant(sources[0], profile))
            return false;

        return AuthorizesNativeDefinition(directive, sources[0]) ||
            HasVerifiedDirectiveOverride(directive);
    }

    private bool AuthorizesNativeDefinition(
        NativeEffectiveEffectDirective directive,
        NativeOriginEffectiveGrant grant)
    {
        if (string.IsNullOrWhiteSpace(directive.DefinitionId) ||
            grant.Identity?.Occurrence?.Source is null ||
            grant.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
            !catalog.TryGetById(grant.Identity.Occurrence.Source.Id, out var nativeSource) ||
            nativeSource is null || nativeSource.RulesetId != catalog.RulesetId ||
            !catalog.TryGetById(directive.RuleId, out var rule) || rule is null ||
            rule.Category != "rule" || rule.RulesetId != catalog.RulesetId ||
            !rule.Definition.TryGetProperty("effectiveEffectDefinitions", out var definitions) ||
            definitions.ValueKind != JsonValueKind.Array)
            return false;

        // Duplicate definition identifiers are ambiguous. The identity names
        // a rule definition, not an occurrence of the effect on one character.
        JsonElement? selected = null;
        foreach (var definition in definitions.EnumerateArray())
        {
            if (definition.ValueKind != JsonValueKind.Object ||
                Text(definition, "id") != directive.DefinitionId)
                continue;
            if (selected is not null) return false;
            selected = definition;
        }
        if (selected is null) return false;
        var candidate = selected.Value;

        // Require full source binding, not just a rule ID or feature label.
        if (Text(candidate, "sourceId") != grant.Identity.Occurrence.Source.Id ||
            Text(candidate, "sourceOccurrencePath") !=
                grant.Identity.Occurrence.OccurrencePath ||
            Text(candidate, "sourceMechanicalDefinitionId") is not { } definitionId ||
            definitionId != grant.Identity.MechanicalDefinitionId ||
            Text(candidate, "target") != TargetName(directive.Target) ||
            Text(candidate, "targetId") != directive.TargetId ||
            Text(candidate, "operation") != OperationName(directive.Operation) ||
            !OptionalTextMatches(candidate, "requiredFormId", directive.RequiredFormId) ||
            !OptionalIntMatches(candidate, "numericValue", directive.NumericValue))
            return false;

        return true;
    }

    private bool HasVerifiedFormOverride(NativeOriginActiveTemporaryTransformation form)
    {
        foreach (var decision in ScopedDecisions("effective-form", form.Id))
        {
            if (decision.EffectiveOutcome.ValueKind != JsonValueKind.Object ||
                decision.EffectiveOutcome.EnumerateObject().Count() != 4 ||
                Text(decision.EffectiveOutcome, "formId") != form.FormId ||
                Text(decision.EffectiveOutcome, "sourceId") != form.EffectSource?.Id ||
                Text(decision.EffectiveOutcome, "activationEvidenceId") !=
                    form.ActivationEvidenceId ||
                !OptionalTextMatches(decision.EffectiveOutcome,
                    "persistentInfluenceId", form.PersistentInfluenceId))
                continue;
            return true;
        }
        return false;
    }

    private bool HasVerifiedDirectiveOverride(NativeEffectiveEffectDirective directive)
    {
        foreach (var decision in ScopedDecisions("effective-effect", directive.Id))
        {
            var value = decision.EffectiveOutcome;
            if (value.ValueKind != JsonValueKind.Object ||
                value.EnumerateObject().Count() != 8 ||
                Text(value, "sourceGrantId") != directive.SourceGrantId ||
                Text(value, "ruleId") != directive.RuleId ||
                Text(value, "target") != TargetName(directive.Target) ||
                Text(value, "targetId") != directive.TargetId ||
                Text(value, "operation") != OperationName(directive.Operation) ||
                !OptionalIntMatches(value, "numericValue", directive.NumericValue) ||
                !OptionalTextMatches(value, "requiredFormId", directive.RequiredFormId) ||
                !OptionalTextMatches(value, "definitionId", directive.DefinitionId))
                continue;
            return true;
        }
        return false;
    }

    private IEnumerable<NativeTableRuleDecision> ScopedDecisions(string kind, string target)
    {
        if (trustedScope is null || string.IsNullOrWhiteSpace(trustedScope.Kind) ||
            string.IsNullOrWhiteSpace(trustedScope.Id))
            yield break;
        foreach (var decision in tableDecisions ?? [])
        {
            if (decision is null || decision.Scope is null ||
                decision.Target is null ||
                decision.Scope != trustedScope ||
                decision.Target.Kind != kind || decision.Target.Id != target ||
                !trusted.AuthorizesTableDecision(decision, trustedScope))
                continue;
            yield return decision;
        }
    }

    private static string? Text(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool OptionalTextMatches(JsonElement obj, string name, string? expected)
    {
        if (obj.ValueKind != JsonValueKind.Object ||
            !obj.TryGetProperty(name, out var value))
            return false;
        return expected is null ? value.ValueKind == JsonValueKind.Null :
            value.ValueKind == JsonValueKind.String &&
            value.GetString() == expected;
    }

    private static bool OptionalIntMatches(JsonElement obj, string name, int? expected)
    {
        if (obj.ValueKind != JsonValueKind.Object ||
            !obj.TryGetProperty(name, out var value))
            return false;
        return expected is null ? value.ValueKind == JsonValueKind.Null :
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var actual) && actual == expected;
    }

    private static string? TargetName(NativeEffectiveEffectTarget target) => target switch
    {
        NativeEffectiveEffectTarget.AbilityScore => "ability-score",
        NativeEffectiveEffectTarget.IndependentGrant => "independent-grant",
        _ => null
    };

    private static string? OperationName(NativeEffectiveEffectOperation operation) =>
        operation switch
        {
            NativeEffectiveEffectOperation.Add => "add",
            NativeEffectiveEffectOperation.Replace => "replace",
            NativeEffectiveEffectOperation.Suspend => "suspend",
            _ => null
        };
}
