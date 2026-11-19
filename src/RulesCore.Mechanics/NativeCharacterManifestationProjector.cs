using System.Collections.Frozen;

namespace RulesCore.Mechanics;

/// <summary>
/// Optional native numerical projection of a verified manifestation result.
/// The effective request is composed by the existing sheet and influence
/// resolvers; the normal native Class projector computes supported numbers.
/// Selecting a form never authorizes Class features, resource use or HP policy.
/// </summary>
public sealed record NativeManifestationProjectedCharacter(
    string Status,
    NativeManifestationEffectiveCharacter EffectiveCharacter,
    NativeCharacterProjection? BaseMechanics,
    IReadOnlyList<NativeIssue> Issues);

public static class NativeCharacterManifestationProjector
{
    public static NativeManifestationProjectedCharacter Project(
        NativeCharacterState state, NativeManifestationLedger ledger,
        INativeCharacterManifestationAuthority authority,
        NativeCharacterProjector nativeProjector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nativeProjector);
        var effective = NativeCharacterManifestationResolver.Resolve(state, ledger, authority);
        if (effective.EffectiveMechanics is null ||
            effective.Status is "invalid" or "unresolved")
            return new(effective.Status, effective, null, effective.Issues);

        var native = nativeProjector.Project(effective.EffectiveMechanics, cancellationToken);
        var issues = new List<NativeIssue>(effective.Issues.Concat(native.Issues));
        if (native.Status == "invalid")
            return new("invalid", effective, null, Array.AsReadOnly(issues.ToArray()));

        if (effective.ManifestationInstanceId is not null ||
            effective.ContributingManifestationInstanceIds?.Count > 0)
        {
            var formIssues = new List<NativeIssue>();
            if (native.FeatureIds.Count > 0)
                formIssues.Add(new("manifestation-native-features-withheld", "class",
                    "Computed native Class features are not verified for this active form."));
            if (native.Resources.Count > 0)
                formIssues.Add(new("manifestation-native-resources-withheld", "resources",
                    "Computed native Class resources have no form-specific use policy."));

            var values = native.Values.ToDictionary(x => x.Key, x => x.Value,
                StringComparer.Ordinal);
            if (values.TryGetValue("hit-points.maximum", out var hp) &&
                hp.Status == "resolved")
            {
                values["hit-points.maximum"] = new("unsupported", null,
                    "No source-authorized form-specific current/maximum Hit Point policy.");
                formIssues.Add(new("manifestation-native-hit-points-withheld", "hit-points",
                    "A resolved normal-form maximum does not authorize transformed HP transfer."));
            }
            issues.AddRange(formIssues);
            native = native with
            {
                Status = "partial",
                Values = values.ToFrozenDictionary(StringComparer.Ordinal),
                FeatureIds = Array.Empty<string>(),
                Resources = Array.Empty<NativeResource>(),
                Issues = Array.AsReadOnly(native.Issues.Concat(formIssues).ToArray())
            };
        }
        var status = effective.Status == "resolved" && native.Status == "resolved" &&
            issues.Count == 0 ? "resolved" : "partial";
        return new(status, effective, native, Array.AsReadOnly(issues.ToArray()));
    }
}
