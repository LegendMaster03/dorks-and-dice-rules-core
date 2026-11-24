namespace RulesCore.Mechanics;

/// <summary>
/// Proposed normalized inventory debit and source consequences. A null
/// empowerment state means this event has no Infernal Empowerment effect.
/// </summary>
public sealed record NativeConstructSustenanceSourcePlan(
    string InstanceId, string PowerSourceId, string ResourceKind,
    int RequiredUnits, int ProposedDebitUnits, bool FullSustenanceProvided,
    bool RequiresInstabilityAdjudication, bool? EmpowermentActiveAfter);

public sealed record NativeConstructSustenancePlan(
    bool CanApply, IReadOnlyList<NativeConstructSustenanceSourcePlan> Sources,
    NativeDailyRationAssessment? DailyDeprivation,
    IReadOnlyList<NativeOriginContractIssue> Issues);
