using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A DM/host-verified Infernal Engine state. It is not a fuel transaction.
/// Missing ordinary sustenance does not eliminate normal power. The
/// resulting instability/overheating penalty remains to be authored.
/// </summary>
public sealed record NativeConstructInfernalSourceState(
    string InstanceId, bool LongRestFuelProvided,
    bool OverdriveFuelProvided, bool RequiresInstabilityAdjudication,
    int AvailablePowerOutput);

/// <summary>
/// A source-specific operating projection, not authorization to create
/// infernal fuel, discharge a soul coin or keep Overdrive active for an
/// invented duration. The host must verify fuel expenditure and apply the
/// overdue sustaining-fuel consequence before committing game state.
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
}
