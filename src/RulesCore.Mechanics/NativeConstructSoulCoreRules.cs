using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A DM-adjudicated source state, not a fuel balance. No numeric soul
/// capacity, energy yield, consumption timer, or processing order is implied.
/// </summary>
public sealed record NativeConstructSoulCoreSourceState(
    string CoreInstanceId, bool HasUsableSoulEnergy,
    bool OverdriveActive, int AvailablePowerOutput);

/// <summary>
/// Advisory projection of a core's current operating capacity. The DM
/// authenticates soul-energy sufficiency; these values do not consume souls,
/// alter Origin, or grant permission to invent an energy reserve.
/// </summary>
public sealed record NativeConstructSoulCoreOperationPreview(
    NativeConstructOperatingStateResult? Evaluation,
    IReadOnlyList<NativeConstructSoulCoreSourceState> Sources,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool CanOperate => Issues.Count == 0 &&
        Evaluation?.IsOperatingConfigurationValid == true;
}

/// <summary>
/// A passive potential capture within the native core's range. This is
/// independent of whether the Construct is running or the power network
/// has been disconnected. Soul eligibility and true distances are supplied
/// by the DM/game host; this preview cannot imprison a soul by itself.
/// </summary>
public sealed record NativeConstructSoulCaptureCandidate(
    string CoreInstanceId, int DistanceFeet, int CaptureRangeFeet);

/// <summary>
/// The host must record a death and capture in its authoritative soul history;
/// multiple candidate cores require the DM to resolve the destination.
/// There is deliberately no fixed cap on captured souls.
/// </summary>
public sealed record NativeConstructSoulCapturePreview(
    bool SoulEligible,
    IReadOnlyList<NativeConstructSoulCaptureCandidate> CandidateCores,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public bool HasAutomaticCapture => SoulEligible &&
        Issues.Count == 0 && CandidateCores.Count > 0;
    public bool NeedsCompetingCoreAdjudication =>
        HasAutomaticCapture && CandidateCores.Count > 1;
}
