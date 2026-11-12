namespace RulesCore.Mechanics;

using RulesCore.Content;

/// <summary>
/// A Rite uses the shared permanent reconstruction executor, but retains the
/// existing exact Rite-specific decision contract until a selected-native
/// individual Rite procedure is executable.
/// ExistingProfile must be loaded from authorized server-owned character state.
/// </summary>
public sealed record NativeOriginRebirthRequest(
    NativeOriginProfile ExistingProfile, NativeOriginProposal TargetProposal,
    string RiteId, NativeTableDecisionScope? TrustedScope = null);

public sealed record NativeOriginRebirthResult(
    NativeOriginConstructionStatus Status, NativeOriginProfile? ResultingProfile,
    NativeOriginConstructionResult? TargetConstruction,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool IsMechanicallyAuthorized =>
        Status == NativeOriginConstructionStatus.Authorized && ResultingProfile is not null;
}

public sealed class NativeOriginRebirthEngine(NativeCatalogSnapshot catalog)
{
    public NativeOriginRebirthResult Execute(NativeOriginRebirthRequest request,
        INativeOriginAuthority? authority = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        // A Rite cannot opt into native completion by guessing another method ID.
        if (request.TargetProposal?.Method?.MethodId != "rite-of-rebirth")
            return new(NativeOriginConstructionStatus.Invalid, null, null,
                [new("rebirth-method-invalid", "method", NativeOriginIssueSeverity.Invalid,
                    "Rite execution requires the native rite-of-rebirth method.")]);
        var result = new NativeOriginPermanentReconstructionEngine(catalog).Execute(
            new(request.ExistingProfile, request.TargetProposal, request.RiteId,
                request.TrustedScope), authority);
        return new(result.Status, result.ResultingProfile,
            result.TargetConstruction, result.Issues);
    }
}
