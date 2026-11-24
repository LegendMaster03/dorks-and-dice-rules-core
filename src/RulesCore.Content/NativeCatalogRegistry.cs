using System.Collections.Frozen;

namespace RulesCore.Content;

/// <summary>
/// One independent publication slot per explicitly registered ruleset.
/// The legacy default store is the same instance used by existing consumers;
/// looking up another ID never falls back to that store.
/// </summary>
public sealed class NativeCatalogRegistry
{
    private readonly FrozenDictionary<string, NativeCatalogStore> _stores;

    public NativeCatalogRegistry(string defaultRulesetId, NativeCatalogStore defaultStore,
        IEnumerable<string> additionalRulesetIds)
    {
        ArgumentNullException.ThrowIfNull(defaultStore);
        ArgumentNullException.ThrowIfNull(additionalRulesetIds);
        var stores = new Dictionary<string, NativeCatalogStore>(StringComparer.Ordinal)
        {
            [defaultRulesetId] = defaultStore
        };
        foreach (var id in additionalRulesetIds)
            if (!stores.TryAdd(id, new NativeCatalogStore(id)))
                throw new ArgumentException("Duplicate registered ruleset: " + id,
                    nameof(additionalRulesetIds));
        _stores = stores.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> RulesetIds => _stores.Keys;

    public bool TryGetStore(string rulesetId, out NativeCatalogStore? store) =>
        _stores.TryGetValue(rulesetId, out store);

    public NativeCatalogStore GetStore(string rulesetId) =>
        _stores.TryGetValue(rulesetId, out var store)
            ? store
            : throw new KeyNotFoundException("Unregistered ruleset: " + rulesetId);
}
