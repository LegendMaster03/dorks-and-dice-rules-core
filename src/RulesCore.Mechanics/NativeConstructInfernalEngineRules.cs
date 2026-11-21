using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// DM/host-verified Infernal Engine operating state, not a fuel transaction.
/// Normal output remains available even without sustaining fuel. Infernal
/// Empowerment is a temporary condition that expires at the next long rest;
/// its combat benefit has not yet been designed or approved.
/// </summary>
public sealed record NativeConstructInfernalSourceState(
    string InstanceId, bool LongRestFuelProvided,
    bool EmpowermentActive, bool RequiresInstabilityAdjudication,
    int AvailablePowerOutput);

/// <summary>
/// Source-specific operating projection, not permission to manufacture fuel,
/// activate empowerment for free, or impose an unapproved combat benefit.
/// The host must authenticate the extra-fuel activation, end the condition
/// at the next long rest, and adjudicate ordinary fuel-shortage penalties.
/// </summary>
public sealed record NativeConstructInfernalOperationPreview(
    NativeConstructOperatingStateResult? Evaluation,
    IReadOnlyList<NativeConstructInfernalSourceState> Sources,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool CanOperate => Issues.Count == 0 &&
        Evaluation?.IsOperatingConfigurationValid == true;

    public bool RequiresInstabilityAdjudication =>
        Sources.Any(x => x.RequiresInstabilityAdjudication);

    public bool HasActiveEmpowerment =>
        Sources.Any(x => x.EmpowermentActive);
}
