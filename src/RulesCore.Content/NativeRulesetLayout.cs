using System.Text.RegularExpressions;

namespace RulesCore.Content;

/// <summary>
/// A layout contract, not a content loader. Only the native directory is eligible
/// for future runtime reads. Working drafts are deliberately never a fallback.
/// </summary>
public static class NativeRulesetLayout
{
    private const string RulesetIdPattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";

    public static NativeRulesetLocation Resolve(string contentRoot, string rulesetId)
    {
        if (string.IsNullOrWhiteSpace(contentRoot))
        {
            throw new ArgumentException("Rulesets:ContentRoot must not be empty.", nameof(contentRoot));
        }

        if (string.IsNullOrWhiteSpace(rulesetId) ||
            !Regex.IsMatch(rulesetId, RulesetIdPattern, RegexOptions.CultureInvariant))
        {
            throw new ArgumentException(
                "Rulesets:DefaultRuleset must be a lowercase, hyphen-separated path-safe ID.",
                nameof(rulesetId));
        }

        var absoluteRoot = Path.GetFullPath(contentRoot);
        return new NativeRulesetLocation(
            absoluteRoot,
            rulesetId,
            Path.Combine(absoluteRoot, rulesetId, "native"));
    }
}

public sealed record NativeRulesetLocation(
    string ContentRoot,
    string RulesetId,
    string NativeDirectory);
