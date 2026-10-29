using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;

namespace RulesCore.Content;

/// <summary>A playable native definition. The JSON value is an owned, immutable clone.</summary>
public sealed record NativeRuleEntity(
    string RulesetId,
    string Id,
    string Name,
    string Category,
    string SourcePath,
    JsonElement Definition);

public sealed record CatalogSearchPage(
    ImmutableArray<NativeRuleEntity> Items,
    int Total,
    int Offset,
    int Limit);

/// <summary>
/// An immutable, pre-indexed view of one validated native package. Paths are
/// provenance only, never used to read rule definitions during requests.
/// </summary>
public sealed class NativeCatalogSnapshot
{
    private sealed record SearchEntry(NativeRuleEntity Entity, string SearchText);

    private readonly FrozenDictionary<string, NativeRuleEntity> _byId;
    private readonly ImmutableArray<SearchEntry> _all;
    private readonly FrozenDictionary<string, ImmutableArray<SearchEntry>> _byCategory;
    private readonly FrozenDictionary<string, ImmutableArray<SearchEntry>> _trigrams;

    public string RulesetId { get; }
    public int SchemaVersion { get; }
    public string Fingerprint { get; }
    public int Count => _all.Length;
    public IReadOnlyDictionary<string, int> CountsByCategory { get; }

    internal NativeCatalogSnapshot(
        string rulesetId,
        int schemaVersion,
        string fingerprint,
        IReadOnlyCollection<NativeRuleEntity> entities)
    {
        RulesetId = rulesetId;
        SchemaVersion = schemaVersion;
        Fingerprint = fingerprint;

        var ordered = entities.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
        _byId = ordered.ToFrozenDictionary(e => e.Id, StringComparer.Ordinal);
        _all = ordered.Select(e => new SearchEntry(
            e, (e.Id + "\n" + e.Name).ToUpperInvariant())).ToImmutableArray();

        _byCategory = _all
            .GroupBy(e => e.Entity.Category, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(
                g => g.Key,
                g => g.ToImmutableArray(),
                StringComparer.OrdinalIgnoreCase);

        CountsByCategory = _byCategory.ToFrozenDictionary(
            p => p.Key, p => p.Value.Length, StringComparer.OrdinalIgnoreCase);

        var postings = new Dictionary<string, List<SearchEntry>>(StringComparer.Ordinal);
        foreach (var entry in _all)
        {
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i <= entry.SearchText.Length - 3; i++)
            {
                distinct.Add(entry.SearchText.Substring(i, 3));
            }

            foreach (var gram in distinct)
            {
                if (!postings.TryGetValue(gram, out var bucket))
                {
                    bucket = new List<SearchEntry>();
                    postings.Add(gram, bucket);
                }
                bucket.Add(entry);
            }
        }

        // HashSet iteration does not affect search ordering: entries are appended
        // to each posting list in the deterministic _all order.
        _trigrams = postings.ToFrozenDictionary(
            p => p.Key, p => p.Value.ToImmutableArray(), StringComparer.Ordinal);
    }

    public bool TryGetById(string id, out NativeRuleEntity? entity)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _byId.TryGetValue(id, out entity);
    }

    public int CountByCategory(string category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return CountsByCategory.TryGetValue(category, out var count) ? count : 0;
    }

    /// <summary>
    /// Case-insensitive substring search over stable IDs and names. Results
    /// are always ordered by ID. A three-character posting index narrows
    /// searches; short queries use the precomputed category or full index.
    /// </summary>
    public CatalogSearchPage Search(
        string? query = null, string? category = null, int offset = 0, int limit = 50)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));

        var term = (query ?? string.Empty).Trim().ToUpperInvariant();
        ImmutableArray<SearchEntry> source;
        if (term.Length >= 3)
        {
            source = _trigrams.TryGetValue(term[..3], out var hits)
                ? hits : ImmutableArray<SearchEntry>.Empty;
        }
        else if (string.IsNullOrWhiteSpace(category))
        {
            source = _all;
        }
        else
        {
            source = _byCategory.TryGetValue(category.Trim(), out var members)
                ? members : ImmutableArray<SearchEntry>.Empty;
        }

        var selected = ImmutableArray.CreateBuilder<NativeRuleEntity>();
        var total = 0;
        foreach (var entry in source)
        {
            if (!string.IsNullOrWhiteSpace(category) &&
                !string.Equals(entry.Entity.Category, category.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (term.Length != 0 &&
                !entry.SearchText.Contains(term, StringComparison.Ordinal))
            {
                continue;
            }

            if (total >= offset && selected.Count < limit) selected.Add(entry.Entity);
            total++;
        }

        return new CatalogSearchPage(selected.ToImmutable(), total, offset, limit);
    }
}
