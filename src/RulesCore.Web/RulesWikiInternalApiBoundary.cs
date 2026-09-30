using RulesCore.Application.Hosting;

namespace RulesCore.Web;

/// <summary>
/// Enforces the first-party Rules Wiki -> Rules Core caller boundary and provides the narrow
/// compatibility adapter for Wiki workflows that share existing Core application handlers with
/// public consumers. Browser requests never reach this namespace directly; Rules Wiki maps its own
/// browser-facing routes to it after Site Tool-to-Tool delegation.
/// </summary>
public static class RulesWikiInternalApiBoundary
{
    public const string ExpectedDelegatingToolKey = "rules-wiki";
    public const string SharedPrefix = "/internal/wiki/shared";

    public static bool IsRulesWikiCaller(ToolHostAuthenticationContext? context) =>
        context is not null
        && string.Equals(
            context.DelegatedFromToolKey,
            ExpectedDelegatingToolKey,
            StringComparison.Ordinal);

    public static bool TryMapSharedPath(PathString requestPath, out PathString corePath)
    {
        corePath = default;
        if (!requestPath.StartsWithSegments(SharedPrefix, out var remainder)
            || !remainder.HasValue)
        {
            return false;
        }

        if (!IsAllowedSharedCorePath(remainder))
        {
            return false;
        }

        corePath = remainder;
        return true;
    }

    public static bool IsAllowedSharedCorePath(PathString path)
    {
        if (StartsWithSegment(path, "/api/rules")
            || StartsWithSegment(path, "/api/sources")
            || StartsWithSegment(path, "/api/source-admin")
            || StartsWithSegment(path, "/api/global/rules")
            || StartsWithSegment(path, "/api/workspace"))
        {
            return true;
        }

        return IsCampaignRulesPath(path);
    }

    private static bool StartsWithSegment(PathString path, string prefix) =>
        path.StartsWithSegments(prefix, StringComparison.Ordinal);

    private static bool IsCampaignRulesPath(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 4
            && string.Equals(segments[0], "api", StringComparison.Ordinal)
            && string.Equals(segments[1], "campaigns", StringComparison.Ordinal)
            && Guid.TryParse(segments[2], out _)
            && string.Equals(segments[3], "rules", StringComparison.Ordinal);
    }
}
