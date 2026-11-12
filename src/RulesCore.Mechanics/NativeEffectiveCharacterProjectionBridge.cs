using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// The authenticated application must attest that this exact character input
/// belongs to the verified Origin and its earned advancement history. The
/// effective-effect authority separately checks grants, forms and authored rules.
/// Neither authority should be implemented from deserialized client assertions.
/// </summary>
public interface INativeEffectiveCharacterInputAuthority
{
    bool AuthorizesCharacterInput(NativeCharacterRequest request,
        NativeOriginConstructionResult origin);
}

/// <summary>
/// Derived values only. No features, resource entitlements, action availability,
/// equipment permissions, or playable-character approval are conveyed here.
/// </summary>
public sealed record NativeEffectiveNumericProjection(
    string Status,
    IReadOnlyDictionary<string, NativeNumeric> Values,
    IReadOnlyList<NativeIssue> Issues)
{
    public bool HasCalculatedValues => Status == "calculated-subset";
}

/// <summary>
/// A guarded bridge from verified post-Origin numeric effects to the existing
/// native character projector. It deliberately refuses form-level and feature-
/// suppression scenarios until executable, source-backed subsystem policies
/// exist; those scenarios are not equivalent to simply changing ability scores.
/// </summary>
public sealed class NativeEffectiveCharacterProjectionBridge(NativeCatalogSnapshot catalog)
{
    private readonly NativeCharacterProjector _projector = new(catalog);

    public NativeEffectiveNumericProjection Project(
        NativeCharacterRequest character,
        NativeEffectiveCharacterRequest effects,
        INativeEffectiveEffectAuthority? effectAuthority,
        INativeEffectiveCharacterInputAuthority? inputAuthority,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<NativeIssue>();
        NativeEffectiveNumericProjection Failed(string code, string path, string description,
            string status = "unresolved")
        {
            issues.Add(new(code, path, description));
            return new(status, new Dictionary<string, NativeNumeric>(StringComparer.Ordinal),
                issues.ToArray());
        }

        if (character is null || effects is null ||
            effects.Origin?.Profile is null ||
            character.AbilityScores is null || effects.AbilityScores is null ||
            character.Advancements is null)
            return Failed("effective-bridge-input-invalid", "request",
                "Both character and effective inputs require valid referenced state.", "invalid");

        // Keep authorization at the host boundary. There is no production
        // source-of-truth adapter for class levels, choices, or earned grants yet.
        if (effectAuthority is null || inputAuthority is null ||
            !inputAuthority.AuthorizesCharacterInput(character, effects.Origin))
            return Failed("effective-bridge-input-unverified", "character",
                "Authenticated character identity, advancements and input state are required.");

        if (character.AbilityScores.Count != effects.AbilityScores.Count ||
            character.AbilityScores.Any(entry =>
                !effects.AbilityScores.TryGetValue(entry.Key, out var score) ||
                score != entry.Value))
            return Failed("effective-bridge-baseline-mismatch", "abilityScores",
                "The modifier input must match the verified, unmodified character scores.");

        if (character.RulesetId != catalog.RulesetId ||
            character.Fingerprint != catalog.Fingerprint)
            return Failed("effective-bridge-catalog-mismatch", "ruleset",
                "The character request must use the same selected native catalog.");

        var origin = effects.Origin.Profile;
        if (origin.ActiveTemporaryTransformation is not null ||
            origin.OtherActiveTemporaryTransformations is { Count: > 0 } ||
            origin.EffectiveConfigurationBasis is not null ||
            origin.Underlying is null || origin.Current is null ||
            !SameInherentConfiguration(origin.Underlying, origin.Current))
            return Failed("effective-bridge-form-policy-unimplemented", "origin.current",
                "This bridge cannot yet validate changes to Species/Ancestry or active forms.");

        var evaluated = NativeEffectiveCharacterEffects.Evaluate(effects, effectAuthority);
        if (!evaluated.IsResolved)
            return new("unresolved",
                new Dictionary<string, NativeNumeric>(StringComparer.Ordinal),
                evaluated.Issues.ToArray());

        if (evaluated.SuppressedIndependentGrantIds.Count != 0)
            return Failed("effective-bridge-feature-suppression-unimplemented", "independentGrants",
                "Class/other feature suppression must not be bypassed by the existing projector.");

        // The first bridge passes only the effective ability values; the
        // projector remains the single authority for derived arithmetic.
        // No class, equipment or resource entitlement is minted here.
        var projected = _projector.Project(
            character with { AbilityScores = evaluated.EffectiveAbilityScores },
            cancellationToken);

        if (projected.Status != "resolved" || projected.Issues.Count != 0)
            return new("unresolved",
                new Dictionary<string, NativeNumeric>(StringComparer.Ordinal),
                projected.Issues.Count == 0
                    ? [new NativeIssue("effective-bridge-projection-unresolved", "character",
                        "The native character projection did not resolve completely.")]
                    : projected.Issues.ToArray());

        return new("calculated-subset",
            new Dictionary<string, NativeNumeric>(projected.Values,
                StringComparer.Ordinal),
            []);
    }

    private static bool SameInherentConfiguration(
        NativeOriginCurrentConfiguration underlying,
        NativeOriginCurrentConfiguration effective)
    {
        if (underlying.SpeciesId != effective.SpeciesId ||
            underlying.AncestryId != effective.AncestryId ||
            underlying.InherentGrants.Count != effective.InherentGrants.Count)
            return false;
        // The normal-form current state must not replace an inherited grant
        // with a similarly named but mechanically different occurrence.
        // Exact instance provenance matters; do not use grant display names.
        var grants = underlying.InherentGrants.ToDictionary(x => x.Id,
            StringComparer.Ordinal);
        return effective.InherentGrants.All(x =>
            grants.TryGetValue(x.Id, out var baseline) && Equals(x, baseline));
    }
}
