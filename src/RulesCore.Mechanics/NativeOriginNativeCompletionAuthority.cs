namespace RulesCore.Mechanics;

/// <summary>
/// An internal application service must verify a particular authored native
/// completion against the selected catalog and trusted character state.
/// The presence of a post-origination method alone is never completion evidence.
/// Do not implement this callback using browser-supplied JSON claims.
/// </summary>
public interface INativeOriginNativeCompletionAuthority
{
    NativeOriginNativeCompletionEvidence? VerifyNativeCompletion(
        NativeOriginMethodDefinition method, string completionId,
        NativeOriginProfile existing, NativeOriginConstructionResult target);
}

/// <summary>
/// Source identity plus server-verified completion evidence. This does not
/// replace account/campaign permission or persistent revision checks.
/// </summary>
public sealed record NativeOriginNativeCompletionEvidence(
    string NativeRuleReference, string CompletionEvidenceId);
