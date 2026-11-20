namespace RulesCore.Mechanics;

/// <summary>
/// The outcome of a deliberate excavation at a DM-designated dig site.
/// Finds are advisory results: no fragment or relic enters the inventory
/// until the authorized character/campaign workflow records it.
/// </summary>
public enum NativeExcavationFind
{
    Nothing,
    Fragments,
    CompleteRelic,
    MagicalFragment
}

public sealed record NativeExcavationRequest(
    string OriginId,
    bool? IsDesignatedDigSite,
    bool? HasQualifiedHistoricalMethod,
    int? MinutesSpent,
    int? D20Roll,
    int? CheckTotal,
    int? DifficultyClass,
    int? FragmentD6Roll = null);

public sealed record NativeExcavationResult(
    string Status,
    NativeExcavationFind? Find,
    int? FragmentQuantity,
    string? Reason = null);

/// <summary>
/// An optional explicit dig-site activity adapted from Warcraft archaeology.
/// This is NOT incidental travel gathering. The DM controls dig-site
/// designation and provenance, while the caller supplies actual dice and
/// source-defined DC rather than receiving fabricated outcomes.
/// </summary>
public static class NativeArchaeologyExcavation
{
    public static NativeExcavationResult Evaluate(NativeExcavationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.OriginId) ||
            request.OriginId.Length > 160 ||
            request.MinutesSpent is < 0 or > 1_000_000 ||
            request.D20Roll is < 1 or > 20 ||
            request.DifficultyClass is < 1 or > 100_000 ||
            request.FragmentD6Roll is < 1 or > 6 ||
            request.CheckTotal is < -1_000_000 or > 1_000_000)
            return new("unresolved", null, null, "invalid-excavation-input");

        if (request.IsDesignatedDigSite is null)
            return new("unresolved", null, null, "dig-site-authorization-unresolved");
        if (request.IsDesignatedDigSite == false)
            return new("ineligible", null, null, "not-a-designated-dig-site");

        if (request.HasQualifiedHistoricalMethod is null)
            return new("unresolved", null, null, "historical-method-unresolved");
        if (request.HasQualifiedHistoricalMethod == false)
            return new("ineligible", null, null, "historical-method-missing");

        if (request.MinutesSpent is null)
            return new("unresolved", null, null, "excavation-time-unresolved");
        if (request.MinutesSpent < 60)
            return new("ineligible", null, null, "excavation-time-insufficient");

        if (request.D20Roll is null || request.CheckTotal is null ||
            request.DifficultyClass is null)
            return new("unresolved", null, null, "excavation-check-unresolved");

        // An exceptional natural 20 represents a magical fragment in the
        // adapted procedure. The caller still determines its actual source.
        if (request.D20Roll == 20)
            return new("resolved", NativeExcavationFind.MagicalFragment, 1);

        var margin = (long)request.CheckTotal.Value - request.DifficultyClass.Value;
        if (margin < 0)
            return new("resolved", NativeExcavationFind.Nothing, 0);
        if (margin >= 5)
            return new("resolved", NativeExcavationFind.CompleteRelic, 0);

        // Ordinary fragments need their own actual d6; no random roll occurs
        // in Rules Core. A missing roll does not imply the maximum quantity.
        if (request.FragmentD6Roll is null)
            return new("unresolved", null, null, "fragment-roll-unresolved");
        return new("resolved", NativeExcavationFind.Fragments,
            request.FragmentD6Roll.Value);
    }
}
