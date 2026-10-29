namespace RulesCore.Content;

/// <summary>
/// IsReady may be true only when the selected ruleset has a validated,
/// immutable native catalog snapshot available for consumer requests.
/// </summary>
public interface INativeCatalogReadiness
{
    CatalogReadiness Current();
}

public sealed record CatalogReadiness(bool IsReady, string Reason);

/// <summary>
/// Phase 1B has no loader. Do not infer readiness from a directory or
/// PostgreSQL connection; Phase 2 must replace this implementation.
/// </summary>
public sealed class UnavailableNativeCatalogReadiness : INativeCatalogReadiness
{
    public CatalogReadiness Current() => new(false, "native_catalog_not_loaded");
}
