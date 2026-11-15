namespace RulesCore.Mechanics;

/// <summary>
/// Composes a trusted sheet resolution with the existing native Class/mechanics
/// projector without creating another Class advancement engine. The native
/// projection is a base calculation, NOT a fully authorized manifestation:
/// its Class FeatureIds/resources still require form-specific applicability
/// policies before downstream tools can treat them as currently usable.
/// Only UsableCapabilities represents verified sheet-owned entitlements.
/// </summary>
public static class NativeCharacterSheetProjector
{
    public static NativeCharacterSheetProjection Project(
        NativeCharacterState state,
        string targetSheetId,
        INativeCharacterSheetAuthority authority,
        NativeCharacterProjector nativeProjector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nativeProjector);
        var resolved = NativeCharacterSheetResolver.Resolve(state, targetSheetId, authority);
        if (resolved.Status != "resolved" || resolved.EffectiveMechanics is null)
            return new("invalid", null, [], [], resolved.Issues);

        var native = nativeProjector.Project(resolved.EffectiveMechanics, cancellationToken);
        if (native.Status == "invalid")
            return new("invalid", null, [], [],
                Array.AsReadOnly(native.Issues.Concat(resolved.Issues).ToArray()));

        var usable = new List<NativeSheetUsableCapability>();
        var inapplicable = new List<NativeSheetResolvedGrant>();
        var issues = new List<NativeIssue>(resolved.Issues);
        var input = resolved.EffectiveMechanics;
        var hasValidLevel = native.Values.TryGetValue("advancement.total-level", out var total) &&
            total.Status == "resolved" && total.Value is not null;

        foreach (var grant in resolved.Grants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (grant.Applicability != NativeOriginGrantApplicabilityState.Applicable)
            {
                inapplicable.Add(grant);
                if (grant.Applicability == NativeOriginGrantApplicabilityState.Unresolved)
                    issues.Add(new("sheet-grant-applicability-unresolved", grant.Entitlement.Id,
                        "This earned capability has no authorized current-use decision."));
                continue;
            }
            NativeNumeric? referencedLevel = null;
            var reference = grant.Entitlement.LevelReference;
            if (reference is not null)
            {
                if (!hasValidLevel)
                {
                    issues.Add(new("sheet-level-reference-unresolved", grant.Entitlement.Id,
                        "The native Class projector could not validate underlying Class progression."));
                    inapplicable.Add(grant);
                    continue;
                }
                if (reference.Source == NativeSheetLevelSource.TotalClassLevels)
                    referencedLevel = total;
                else
                {
                    var matches = input.Advancements
                        .Where(x => x.ClassId == reference.ClassId).ToArray();
                    if (matches.Length != 1)
                    {
                        issues.Add(new("sheet-level-reference-unresolved", grant.Entitlement.Id,
                            "The referenced Class has no uniquely resolved progression."));
                        inapplicable.Add(grant);
                        continue;
                    }
                    referencedLevel = new("resolved", matches[0].Level);
                }
            }
            usable.Add(new(grant, referencedLevel));
        }

        // Do not leak native Class feature IDs or usable resource pools as
        // currently available through an alternate form. No verified mapping
        // from Class grants to form applicability exists in this increment.
        // Keep derived numeric values explicitly partial until such policies
        // are implemented. The authoritative records remain unchanged.
        var baseView = native;
        if (targetSheetId != NativeCharacterSheetResolver.PrimarySheetId &&
            (native.FeatureIds.Count > 0 || native.Resources.Count > 0))
        {
            issues.Add(new("sheet-class-applicability-unresolved", "baseMechanics",
                "Class features and resource use require authored form applicability rules."));
            baseView = native with
            {
                FeatureIds = Array.Empty<string>(),
                Resources = Array.Empty<NativeResource>()
            };
        }
        var status = native.Status == "resolved" && issues.Count == 0
            ? "resolved" : "partial";
        return new(status, baseView,
            Array.AsReadOnly(usable.ToArray()),
            Array.AsReadOnly(inapplicable.ToArray()),
            Array.AsReadOnly(issues.Concat(native.Issues).ToArray()));
    }
}
