using System.Threading;

namespace RulesCore.Content;

/// <summary>
/// Atomically publishes complete, validated catalogs. The store deliberately
/// performs no I/O and does not implement automatic reload.
/// </summary>
public sealed class NativeCatalogStore : INativeCatalogReadiness
{
    private sealed record State(NativeCatalogSnapshot? Snapshot, string Reason);

    private State _state = new(null, "native_catalog_not_loaded");

    public NativeCatalogSnapshot? Snapshot => Volatile.Read(ref _state).Snapshot;

    public CatalogReadiness Current()
    {
        var state = Volatile.Read(ref _state);
        return state.Snapshot is null
            ? new CatalogReadiness(false, state.Reason)
            : new CatalogReadiness(true, "ready");
    }

    public void Publish(NativeCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _state, new State(snapshot, "ready"));
    }

    /// <summary>Explicitly invalidates readiness; never exposes partial content.</summary>
    public void MarkUnavailable(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason code is required.", nameof(reason));
        Volatile.Write(ref _state, new State(null, reason));
    }
}
