using RulesCore.Content;

namespace RulesCore.Host;

/// <summary>
/// No compatibility or combined surface is permitted in the new host.
/// Private Wiki APIs and public consumer APIs are mapped on separate ingresses.
/// </summary>
public enum ApiSurfaceMode
{
    PublicOnly,
    PrivateOnly
}

public sealed record RulesCoreSettings(
    NativeRulesetLocation Ruleset,
    ApiSurfaceMode Surface)
{
    // Additional packages are opt-in and use the same authoritative root.
    public IReadOnlyList<NativeRulesetLocation> AdditionalRulesets { get; init; } = [];

    public static RulesCoreSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var contentRoot = configuration["Rulesets:ContentRoot"] ?? "data/rulesets";
        var rulesetId = configuration["Rulesets:DefaultRuleset"] ?? "first-party-001";
        var surfaceSetting = configuration["RulesCore:ApiSurface"] ?? "PublicOnly";

        // Reject unknown, legacy and numeric modes rather than broadening exposure.
        var surface = surfaceSetting switch
        {
            "PublicOnly" => ApiSurfaceMode.PublicOnly,
            "PrivateOnly" => ApiSurfaceMode.PrivateOnly,
            _ => throw new ArgumentException(
                "RulesCore:ApiSurface must be PublicOnly or PrivateOnly.")
        };

        var defaultLocation = NativeRulesetLayout.Resolve(contentRoot, rulesetId);
        var seen = new HashSet<string>(StringComparer.Ordinal) { rulesetId };
        var additional = new List<NativeRulesetLocation>();
        foreach (var entry in configuration.GetSection("Rulesets:AdditionalRulesets").GetChildren())
        {
            var location = NativeRulesetLayout.Resolve(contentRoot, entry.Value ?? "");
            if (!seen.Add(location.RulesetId))
                throw new ArgumentException("Duplicate configured ruleset: " + location.RulesetId);
            additional.Add(location);
        }

        return new RulesCoreSettings(defaultLocation, surface)
        {
            AdditionalRulesets = additional.AsReadOnly()
        };
    }
}
