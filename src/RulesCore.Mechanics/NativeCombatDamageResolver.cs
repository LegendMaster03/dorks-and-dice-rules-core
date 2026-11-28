namespace RulesCore.Mechanics;

/// <summary>
/// Stateless arithmetic preview. The consumer must verify which source grants
/// and non-bypassed Damage Reduction entries apply and that damage is authorized.
/// </summary>
public sealed record NativeCombatDamageRequest(
    string RulesetId,
    string Fingerprint,
    int IncomingDamage,
    int DamageModificationPercent = 0,
    IReadOnlyList<int>? ApplicableDamageReduction = null,
    int? CurrentHitPoints = null,
    int? MaximumHitPoints = null);

public sealed record NativeCombatDamageResult(
    string Status,
    int? ModifiedDamage = null,
    int? AppliedDamageReduction = null,
    int? FinalDamage = null,
    int? ProposedHitPoints = null,
    bool? AtZeroHitPoints = null,
    string? Reason = null);

/// <summary>
/// Implements only the approved ordering: percentage change, then summed flat
/// DR, then a nonnegative damage floor. Fractional rounding is deliberately
/// unresolved in the native rules and is not invented here.
/// </summary>
public static class NativeCombatDamageResolver
{
    public static NativeCombatDamageResult Resolve(NativeCombatDamageRequest? request)
    {
        if (request is null || request.IncomingDamage < 0 ||
            request.DamageModificationPercent is < -100 or > 100 ||
            (request.ApplicableDamageReduction?.Count ?? 0) > 100 ||
            (request.ApplicableDamageReduction?.Any(v => v < 0) ?? false) ||
            request.CurrentHitPoints.HasValue != request.MaximumHitPoints.HasValue ||
            request.CurrentHitPoints is < 0 || request.MaximumHitPoints is < 0 ||
            (request.CurrentHitPoints.HasValue &&
             request.CurrentHitPoints > request.MaximumHitPoints))
            return new("invalid-input", Reason: "Invalid damage or HP input.");

        // Use 64-bit intermediates before narrowing the approved integer result.
        var numerator = (long)request.IncomingDamage *
            (100 - request.DamageModificationPercent);
        if (numerator % 100 != 0)
            return new("rounding-unresolved",
                Reason: "The native fractional-damage rounding rule is not finalized.");

        var scaled = numerator / 100;
        if (scaled > int.MaxValue)
            return new("invalid-input", Reason: "Modified damage exceeds integer limits.");

        var totalReduction = request.ApplicableDamageReduction is null
            ? 0L
            : request.ApplicableDamageReduction.Sum(x => (long)x);
        var appliedReduction = Math.Min(scaled, totalReduction);
        var final = (int)(scaled - appliedReduction);
        int? proposedHp = request.CurrentHitPoints is int hp
            ? Math.Max(0, hp - final)
            : null;
        return new("resolved",
            ModifiedDamage: (int)scaled,
            AppliedDamageReduction: (int)appliedReduction,
            FinalDamage: final,
            ProposedHitPoints: proposedHp,
            AtZeroHitPoints: proposedHp.HasValue ? proposedHp.Value == 0 : null);
    }
}
