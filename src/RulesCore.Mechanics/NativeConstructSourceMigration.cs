using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// Verified source-specific persistent state. Mechanical deprivation belongs
/// to the individual, not to the installed engine. Soul energy is a DM
/// determination, not an invented numeric reservoir. New source balances
/// must come from independently authenticated physical item state.
/// </summary>
public sealed record NativeConstructSourceState(
    string PowerSourceId, long? StoredSolarPowerUnitMinutes = null,
    long? SolarDeficitMinutes = null, int? SolarExhaustionEventsIssued = null,
    bool? InfernalSustenancePaid = null, bool? InfernalEmpowermentActive = null,
    bool? UsableSoulEnergy = null, bool? SoulOverdriveActive = null);

public sealed record NativeConstructSourceLedgerState(
    IReadOnlyDictionary<string, NativeConstructSourceState> Sources,
    IReadOnlyList<NativeConstructSoulCustodyRecord> SoulCustody,
    int ConsecutiveDaysWithoutFullMechanicalRation);

/// <summary>
/// Both snapshots are loaded by the trusted host, never submitted as client
/// authority. A replacement does not produce a resource conversion, fuel
/// payment, exhaustion recovery, or release of an imprisoned soul.
/// </summary>
public sealed record NativeConstructSourceMigrationEvidence(
    NativeConstructSourceLedgerState Before, NativeConstructSourceLedgerState After);

public sealed record NativeConstructSourceMigrationPlan(
    NativeConstructSourceLedgerState Before, NativeConstructSourceLedgerState After,
    IReadOnlyList<string> RetiredSourceInstanceIds,
    IReadOnlyList<string> IncomingSourceInstanceIds);

internal static class NativeConstructSourceMigration
{
    internal static (NativeConstructSourceMigrationPlan? Plan, IReadOnlyList<NativeOriginContractIssue> Issues)
        Evaluate(NativeConstructOperatingSession previous, NativeConstructOperatingSession next,
            NativeConstructSourceMigrationEvidence evidence,
            Func<NativeConstructComponentInstance, NativeConstructComponentInstance, bool> sameHardware)
    {
        (NativeConstructSourceMigrationPlan?, IReadOnlyList<NativeOriginContractIssue>) Reject(
            string code, string message) =>
            (null, [new(code, "sourceMigration", NativeOriginIssueSeverity.Unresolved, message)]);

        var before = evidence.Before;
        var after = evidence.After;
        if (before?.Sources is null || after?.Sources is null ||
            before.SoulCustody is null || after.SoulCustody is null ||
            before.ConsecutiveDaysWithoutFullMechanicalRation < 0 ||
            before.ConsecutiveDaysWithoutFullMechanicalRation != after.ConsecutiveDaysWithoutFullMechanicalRation)
            return Reject("construct-source-migration-deprivation-invalid",
                "Complete verified source snapshots must preserve the individual's mechanical deprivation counter.");

        var oldParts = previous.Installation.Components.Where(p => p.PowerSourceId is not null)
            .ToDictionary(p => p.InstanceId, StringComparer.Ordinal);
        var newParts = next.Installation.Components.Where(p => p.PowerSourceId is not null)
            .ToDictionary(p => p.InstanceId, StringComparer.Ordinal);
        if (!ValidShape(oldParts, before) || !ValidShape(newParts, after))
            return Reject("construct-source-migration-shape-invalid",
                "Each installed source requires exactly one matching, source-specific state record.");

        var retained = oldParts.Keys.Intersect(newParts.Keys, StringComparer.Ordinal)
            .Where(id => sameHardware(oldParts[id], newParts[id]))
            .ToHashSet(StringComparer.Ordinal);
        var retired = oldParts.Keys.Except(retained, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var incoming = newParts.Keys.Except(retained, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (retained.Any(id => before.Sources[id] != after.Sources[id]))
            return Reject("construct-source-migration-retained-state-changed",
                "Installation does not change the resource state of a retained physical source.");
        if (retired.Any(id => before.Sources[id].SolarDeficitMinutes > 0))
            return Reject("construct-source-migration-solar-deficit-unresolved",
                "An unresolved solar deprivation episode cannot be erased by replacing its source.");
        if (incoming.Any(id => after.Sources[id].SolarDeficitMinutes > 0 ||
            after.Sources[id].SolarExhaustionEventsIssued > 0 ||
            after.Sources[id].InfernalEmpowermentActive == true ||
            after.Sources[id].SoulOverdriveActive == true))
            return Reject("construct-source-migration-incoming-condition-invalid",
                "Installing a new source does not create deprivation history or activate enhanced operation.");

        // Historical custody is not a transferable quantity. The current
        // ledger requires all records to reference an installed core. A core
        // with custody history cannot be retired here until portable custody
        // (including history and any imprisoned souls) is separately supported.
        if (before.SoulCustody.Any(record => record is null || !retained.Contains(record.CoreInstanceId)) ||
            !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.SoulCustody.OrderBy(r => r.DeathEventId, StringComparer.Ordinal)),
                JsonSerializer.SerializeToElement(after.SoulCustody.OrderBy(r => r?.DeathEventId, StringComparer.Ordinal))))
            return Reject("construct-source-migration-custody-unresolved",
                "A modification must preserve exact soul custody; retiring a core with custody records requires separate resolution.");

        if (!ValidateLedgers(previous, before) || !ValidateLedgers(next, after))
            return Reject("construct-source-migration-ledger-invalid",
                "The verified source state does not satisfy native energy capacity or custody validation.");

        var oldOutages = (previous.Installation.UnavailablePowerSourceInstanceIds ?? []).ToHashSet(StringComparer.Ordinal);
        var newOutages = (next.Installation.UnavailablePowerSourceInstanceIds ?? []).ToHashSet(StringComparer.Ordinal);
        if (retained.Any(id => oldOutages.Contains(id) != newOutages.Contains(id)))
            return Reject("construct-source-migration-outage-changed",
                "A retained source outage cannot be cleared or created by an installation change.");

        // Keep the retired balances and issued exhaustion history in the
        // receipt; this is not an inventory credit or a condition reversal.
        return (new(before, after, retired, incoming), []);
    }

    private static bool ValidShape(IReadOnlyDictionary<string, NativeConstructComponentInstance> parts,
        NativeConstructSourceLedgerState state)
    {
        if (parts.Count != state.Sources.Count) return false;
        foreach (var (id, part) in parts)
        {
            if (!state.Sources.TryGetValue(id, out var source) || source is null ||
                source.PowerSourceId != part.PowerSourceId) return false;
            var solar = source.PowerSourceId == "construct-power-source.solar-crystal";
            var infernal = source.PowerSourceId == "construct-power-source.infernal-engine";
            var soul = source.PowerSourceId == "construct-power-source.soul-gem";
            if (!solar && !infernal && !soul && source.PowerSourceId != "construct-power-source.mechanical-engine")
                return false;
            if (solar != source.StoredSolarPowerUnitMinutes.HasValue ||
                solar != source.SolarDeficitMinutes.HasValue ||
                solar != source.SolarExhaustionEventsIssued.HasValue ||
                infernal != source.InfernalSustenancePaid.HasValue ||
                infernal != source.InfernalEmpowermentActive.HasValue ||
                soul != source.UsableSoulEnergy.HasValue ||
                soul != source.SoulOverdriveActive.HasValue ||
                (source.SoulOverdriveActive == true && source.UsableSoulEnergy != true))
                return false;
        }
        return true;
    }

    private static bool ValidateLedgers(NativeConstructOperatingSession session,
        NativeConstructSourceLedgerState state)
    {
        var solar = state.Sources.Where(pair => pair.Value.PowerSourceId ==
            "construct-power-source.solar-crystal").ToArray();
        if (solar.Length > 0 && session.OpenTrustedSolarLedger(
                solar.ToDictionary(x => x.Key, x => x.Value.StoredSolarPowerUnitMinutes!.Value, StringComparer.Ordinal),
                solar.ToDictionary(x => x.Key, x => x.Value.SolarDeficitMinutes!.Value, StringComparer.Ordinal),
                solar.ToDictionary(x => x.Key, x => x.Value.SolarExhaustionEventsIssued!.Value, StringComparer.Ordinal)).Ledger is null)
            return false;
        var hasSoulCore = state.Sources.Values.Any(x => x.PowerSourceId == "construct-power-source.soul-gem");
        return hasSoulCore ? session.OpenTrustedSoulCustodyLedger(state.SoulCustody).Ledger is not null
            : state.SoulCustody.Count == 0;
    }
}
