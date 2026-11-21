using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Projects only selected, verified native Darkvision characteristics. A
/// separately selected canonical upgrade can increase the range at its authored
/// character level. This is a read-only view, not a construction eligibility
/// rule, automatic inheritance or permission to see magical darkness.
/// </summary>
public static class NativeOriginDarkvisionProjector
{
    public sealed record NativeDarkvisionExtension(
        int AddedRangeFeet, int RequiredCharacterLevel,
        string GrantId, string SourceId, string OccurrencePath)
    {
        // Preserve both the stand-alone range and the original authored
        // limitations for future (not yet active) unlocks as well.
        public int StandaloneRangeFeet { get; init; }
        public JsonElement CompleteEffect { get; init; }
    }

    public sealed record GrantedDarkvision(
        int RangeFeet, string GrantId, string SourceId, string OccurrencePath,
        int? BaseRangeFeet = null,
        IReadOnlyList<NativeDarkvisionExtension>? Extensions = null);

    /// <summary>
    /// A ruleset-derived condition for total innate range above the configured
    /// threshold. This is not a separate inheritable gene or a refund.
    /// </summary>
    public sealed record SunlightSensitivity(
        string TraitDefinitionId, string ConditionId, string Trigger, string End);

    public sealed record View(
        string Status, GrantedDarkvision? Darkvision,
        IReadOnlyList<string> UnresolvedReasons, bool PreviewOnly = true)
    {
        /// <summary>
        /// Verified selected extensions not yet unlocked. These are not part
        /// of Darkvision.RangeFeet or the active Extensions collection.
        /// </summary>
        public IReadOnlyList<NativeDarkvisionExtension> PendingExtensions
        { get; init; } = [];

        public int? InnateSensitivityThresholdFeet { get; init; }
        public SunlightSensitivity? RequiredSunlightSensitivity { get; init; }
    }

    public static View Project(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result, int? characterLevel = null) =>
        ProjectCore(catalog, result, false, characterLevel);

    public static View ProjectApprovedMixed(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result, int? characterLevel = null) =>
        ProjectCore(catalog, result, true, characterLevel);

    private static View ProjectCore(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result, bool mixed, int? characterLevel)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(result);
        if (characterLevel is <= 0)
            return Unresolved("invalid-character-level");

        var foundation = mixed ?
            NativeOriginApprovedMixedIntrinsicProjector.Project(result) :
            NativeOriginIntrinsicProjector.Project(result);
        if (foundation.Status != "resolved" ||
            result.Profile is not { } profile)
            return Unresolved(mixed ? "mixed-intrinsics-not-verified" :
                "direct-intrinsics-not-verified");

        var baseline = profile.Current.InherentGrants.Where(x =>
            x.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
            x.Identity.IntrinsicSlotId == "darkvisionFeet").ToArray();
        if (baseline.Length > 1)
            return Unresolved("duplicate-darkvision");

        NativeOriginEffectiveGrant? baseGrant = null;
        int initialRange = 0;
        if (baseline.Length == 1)
        {
            var grant = baseline[0];
            var source = grant.Identity.Occurrence.Source;
            if (source.Kind != NativeOriginSourceKind.Native ||
                !catalog.TryGetById(source.Id, out var native) ||
                native is null || native.Category is not ("species" or "ancestry") ||
                !native.Definition.TryGetProperty("darkvisionFeet", out var authored) ||
                authored.ValueKind != JsonValueKind.Number ||
                !authored.TryGetInt32(out var authoredRange) ||
                authoredRange is <= 0 or > 1_000_000 ||
                grant.EffectiveValue is not { ValueKind: JsonValueKind.Number } effective ||
                !effective.TryGetInt32(out var range) ||
                range != authoredRange || !VerifiedGrant(grant))
                return Unresolved("darkvision-not-verified");
            baseGrant = grant;
            initialRange = range;
        }

        // Keep canonical sense traits separate from numeric intrinsics; a
        // Stone Goliath can manifest this capability at level 4 without
        // inheriting any other ancestry's Darkvision.
        var extensions = new List<NativeDarkvisionExtension>();
        var pendingExtensions = new List<NativeDarkvisionExtension>();
        foreach (var grant in profile.Current.InherentGrants)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait)
                continue;

            var id = grant.Identity.MechanicalDefinitionId;
            NativeRuleEntity? definition = null;
            if (id is not null)
                catalog.TryGetById(id, out definition);
            var hasCanonicalDefinition = definition is not null &&
                definition.Category == "origin-trait" &&
                definition.Definition.ValueKind == JsonValueKind.Object &&
                definition.Definition.TryGetProperty("trait", out var trait) &&
                trait.ValueKind == JsonValueKind.Object;
            var canonical = hasCanonicalDefinition
                ? definition!.Definition.GetProperty("trait") : default;
            var effective = grant.EffectiveValue;
            // Inspect both sides: an effective claim with a removed/changed
            // definition must not disappear, and a stripped effective claim
            // must not hide a canonical Darkvision grant. The characteristic
            // upgrade fields also identify a malformed sense discriminator.
            var canonicalClaim = ClaimsDarkvision(canonical);
            var effectiveClaim = ClaimsDarkvision(effective);
            if (!canonicalClaim && !effectiveClaim)
                continue;

            if (!hasCanonicalDefinition || !canonicalClaim || !effectiveClaim ||
                effective is not { ValueKind: JsonValueKind.Object } actual ||
                !JsonElement.DeepEquals(canonical, actual) ||
                !HasDarkvisionSense(canonical) ||
                !canonical.TryGetProperty("rangeFeet", out var span) ||
                span.ValueKind != JsonValueKind.Number ||
                !span.TryGetInt32(out var sourceRange) || sourceRange is <= 0 or > 1_000_000 ||
                !canonical.TryGetProperty("addToExistingDarkvisionFeet", out var add) ||
                add.ValueKind != JsonValueKind.Number ||
                !add.TryGetInt32(out var added) || added is <= 0 or > 1_000_000 ||
                !canonical.TryGetProperty("requiredCharacterLevel", out var threshold) ||
                threshold.ValueKind != JsonValueKind.Number ||
                !threshold.TryGetInt32(out var unlock) || unlock <= 0 ||
                !canonical.TryGetProperty("stacking", out var stacking) ||
                stacking.ValueKind != JsonValueKind.String ||
                stacking.GetString() != "once-per-feature" ||
                !VerifiedGrant(grant))
                return Unresolved("darkvision-extension-not-verified");

            if (characterLevel is null)
                return Unresolved("darkvision-extension-requires-character-level");
            var grantedExtension = new NativeDarkvisionExtension(added, unlock, grant.Id,
                grant.Identity.Occurrence.Source.Id,
                grant.Identity.Occurrence.OccurrencePath)
            {
                StandaloneRangeFeet = sourceRange,
                CompleteEffect = canonical.Clone()
            };
            if (characterLevel.Value < unlock)
                pendingExtensions.Add(grantedExtension);
            else
                extensions.Add(grantedExtension);
        }

        // The native catalog currently has one authored Darkvision-upgrade
        // grammar. Different upgrade traits need their own compatibility and
        // stacking review; two instances cannot become two free range boosts.
        if (extensions.Count + pendingExtensions.Count > 1)
            return Unresolved("multiple-darkvision-extensions-require-reconciliation");

        // A missing or malformed threshold must not silently make extended
        // native Darkvision drawback-free. Temporary magical vision never
        // enters this projector's native-source calculation.
        if (!TryReadSensitivityRule(catalog, out var threshold, out var drawbackId))
            return Unresolved("darkvision-sensitivity-policy-unavailable");

        if (initialRange == 0 && extensions.Count == 0)
            return new("none", null, [])
            {
                PendingExtensions = pendingExtensions,
                InnateSensitivityThresholdFeet = threshold
            };
        var extension = extensions.SingleOrDefault();
        var total = baseGrant is not null
            ? initialRange + (extension?.AddedRangeFeet ?? 0)
            : (extension is null ? 0 : extension.StandaloneRangeFeet);
        if (total <= 0 || total > 1_000_000)
            return Unresolved("darkvision-range-overflow");

        var primary = baseGrant ?? profile.Current.InherentGrants
            .Single(x => x.Id == extension!.GrantId);
        SunlightSensitivity? sensitivity = null;
        if (total > threshold)
        {
            if (!TryReadSunlightSensitivity(catalog, drawbackId, out sensitivity))
                return Unresolved("darkvision-sunlight-sensitivity-definition-unavailable");
        }

        return new("resolved", new(total, primary.Id,
            primary.Identity.Occurrence.Source.Id,
            primary.Identity.Occurrence.OccurrencePath,
            BaseRangeFeet: initialRange,
            Extensions: extensions), [])
        {
            PendingExtensions = pendingExtensions,
            InnateSensitivityThresholdFeet = threshold,
            RequiredSunlightSensitivity = sensitivity
        };

        bool VerifiedGrant(NativeOriginEffectiveGrant grant)
        {
            if (grant.Authorization?.Adjudication is not
                    (NativeOriginAdjudication.DmAuthorized or NativeOriginAdjudication.Supported) ||
                grant.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
                !catalog.TryGetById(grant.Identity.Occurrence.Source.Id, out var source) ||
                source is null || source.Category is not ("species" or "ancestry"))
                return false;
            if (mixed)
                return NativeOriginApprovedMixedIntrinsicProjector
                    .HasVerifiedContributingEvidence(result, grant);
            return grant.Authorization.Adjudication == NativeOriginAdjudication.Supported &&
                grant.SourceCandidateIds.Count == 1 &&
                result.EvaluatedProposal.Candidates.Any(candidate =>
                    candidate.Id == grant.SourceCandidateIds[0] &&
                    candidate.ContributorId is null &&
                    candidate.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                    candidate.Identity == grant.Identity &&
                    candidate.OfferedValue is { } offered &&
                    grant.EffectiveValue is { } effective &&
                    JsonElement.DeepEquals(offered, effective));
        }
    }

    private static bool TryReadSensitivityRule(
        NativeCatalogSnapshot catalog, out int threshold, out string drawbackId)
    {
        threshold = 0;
        drawbackId = "";
        if (!catalog.TryGetById("rule.creature-origination", out var rule) ||
            rule is null || rule.Category != "rule" ||
            !rule.Definition.TryGetProperty("darkvisionSensitivity", out var policy) ||
            policy.ValueKind != JsonValueKind.Object ||
            !policy.TryGetProperty("innateRangeThresholdFeet", out var limit) ||
            limit.ValueKind != JsonValueKind.Number ||
            !limit.TryGetInt32(out threshold) ||
            threshold is < 0 or > 1_000_000 ||
            !policy.TryGetProperty("drawbackTraitId", out var drawback) ||
            drawback.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(drawback.GetString()) ||
            !policy.TryGetProperty("appliesTo", out var appliesTo) ||
            appliesTo.GetString() != "total-innate-darkvision" ||
            !policy.TryGetProperty("temporaryMagicalVision", out var temporary) ||
            temporary.GetString() != "excluded")
            return false;
        drawbackId = drawback.GetString()!;
        return true;
    }

    private static bool TryReadSunlightSensitivity(
        NativeCatalogSnapshot catalog, string traitId,
        out SunlightSensitivity? sensitivity)
    {
        sensitivity = null;
        if (!catalog.TryGetById(traitId, out var entity) ||
            entity is null || entity.Category != "origin-trait" ||
            !entity.Definition.TryGetProperty("trait", out var effect) ||
            effect.ValueKind != JsonValueKind.Object ||
            !effect.TryGetProperty("condition", out var condition) ||
            condition.ValueKind != JsonValueKind.String ||
            condition.GetString() != "condition.dazzled" ||
            !effect.TryGetProperty("trigger", out var trigger) ||
            trigger.ValueKind != JsonValueKind.String ||
            trigger.GetString() != "direct-unprotected-sunlight" ||
            !effect.TryGetProperty("end", out var end) ||
            end.ValueKind != JsonValueKind.String ||
            end.GetString() != "remove-exposure-or-use-effective-protection")
            return false;
        sensitivity = new(traitId, condition.GetString()!,
            trigger.GetString()!, end.GetString()!);
        return true;
    }

    // The extension marker is distinctive to the reviewed Darkvision upgrade
    // grammar. It catches malformed sense fields without treating unrelated
    // sense types or general range properties as Darkvision.
    private static bool ClaimsDarkvision(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Object } trait &&
        (HasDarkvisionSense(trait) ||
         trait.TryGetProperty("addToExistingDarkvisionFeet", out _));

    private static bool HasDarkvisionSense(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty("sense", out var sense) &&
        sense.ValueKind == JsonValueKind.String &&
        sense.GetString() == "darkvision";

    private static View Unresolved(string reason) =>
        new("unresolved", null, [reason]);
}
