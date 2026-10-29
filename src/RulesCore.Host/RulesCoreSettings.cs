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

        return new RulesCoreSettings(
            NativeRulesetLayout.Resolve(contentRoot, rulesetId),
            surface);
    }
}
