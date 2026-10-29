namespace RulesCore.Content;

/// <summary>
/// Readiness is true only while a complete validated native catalog snapshot
/// is available for consumer requests.
/// </summary>
public interface INativeCatalogReadiness
{
    CatalogReadiness Current();
}

public sealed record CatalogReadiness(bool IsReady, string Reason);
