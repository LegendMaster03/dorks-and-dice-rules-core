using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// Common table-rule decision records for all Dorks & Dice gameplay subsystems.
/// Authenticated campaign permission checks are separate from rule evaluation.
/// First-party definitions remain unchanged outside the declared scope.
/// </summary>
public sealed record NativeTableAuthority(
    string ActorId, string AuthorizationEvidenceId);
public sealed record NativeTableDecisionScope(string Kind, string Id);
public sealed record NativeTableDecisionTarget(string Kind, string Id);

/// <summary>
/// EffectiveOutcome may contain replacement rules, altered characteristics, or a
/// complete custom configuration, not just an exemption from a restriction.
/// A serialized authorization claim must still be verified by application policy.
/// </summary>
public sealed record NativeTableRuleDecision(
    string Id, NativeTableAuthority Authority, NativeTableDecisionScope Scope,
    NativeTableDecisionTarget Target, JsonElement EffectiveOutcome,
    string? Rationale = null, string? SupersedesDecisionId = null);

