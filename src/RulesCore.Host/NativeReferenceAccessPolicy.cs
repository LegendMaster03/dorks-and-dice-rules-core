using System.Collections.Frozen;
using System.Text.RegularExpressions;
using RulesCore.Content;

namespace RulesCore.Host;

/// <summary>
/// Package-level reference access after Site ticket validation. This is not
/// anonymous publication approval, an account entitlement, or mechanics authority.
/// Registration alone grants no additional access.
/// </summary>
public sealed class NativeReferenceAccessPolicy
{
    private static readonly Regex RulesetIdPattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly FrozenSet<string> _allowedRulesets;

    public NativeReferenceAccessPolicy(IConfiguration configuration)
    {
        // Preserve existing first-party access independently of content revisions.
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "first-party-001" };
        var csv = configuration["RulesCore:AuthenticatedReferenceAccess:AdditionalRulesetsCsv"];
        if (!string.IsNullOrEmpty(csv))
        {
            foreach (var id in csv.Split(','))
            {
                if (id.Length > 80 || !RulesetIdPattern.IsMatch(id) || !allowed.Add(id))
                    throw new ArgumentException("Invalid or duplicate authenticated reference ruleset ID.");
            }
        }
        _allowedRulesets = allowed.ToFrozenSet(StringComparer.Ordinal);
    }

    public bool Allows(NativeCatalogSnapshot snapshot) => _allowedRulesets.Contains(snapshot.RulesetId);
}
