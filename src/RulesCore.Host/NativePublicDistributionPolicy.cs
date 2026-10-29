using System.Collections.Frozen;
using System.Text.RegularExpressions;
using RulesCore.Content;

namespace RulesCore.Host;

/// <summary>
/// Independent package-level public distribution authority. Native promotion is
/// not a publication decision; the exact reviewed catalog fingerprint must match.
/// No per-entity publication status or allowlist exists.
/// </summary>
public sealed class NativePublicDistributionPolicy
{
    private static readonly Regex FingerprintPattern = new(
        "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly FrozenDictionary<string, string> _approvedFingerprints;

    public NativePublicDistributionPolicy(IConfiguration configuration)
    {
        var approved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var package in configuration.GetSection("RulesCore:PublicDistribution:Packages").GetChildren())
        {
            // Misspelling, incomplete review evidence, or an invalid digest denies distribution.
            if (package["Enabled"] != "true" ||
                string.IsNullOrWhiteSpace(package["RightsReviewReference"]) ||
                string.IsNullOrWhiteSpace(package["AttributionStatement"]) ||
                package["ApprovedFingerprint"] is not { } fingerprint ||
                !FingerprintPattern.IsMatch(fingerprint))
                continue;

            approved[package.Key] = fingerprint.ToLowerInvariant();
        }
        _approvedFingerprints = approved.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public bool Allows(NativeCatalogSnapshot snapshot) =>
        _approvedFingerprints.TryGetValue(snapshot.RulesetId, out var fingerprint) &&
        string.Equals(fingerprint, snapshot.Fingerprint, StringComparison.Ordinal);
}
