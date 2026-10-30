using RulesCore.Content;

var repository = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(repository, "RulesCore.slnx")))
{
    Console.Error.WriteLine("Run from the repository root.");
    return 2;
}

var location = NativeRulesetLayout.Resolve(
    Path.Combine(repository, "data", "rulesets"), "first-party-001");
var catalog = NativeCatalogLoader.Load(location);

// No static inventory gate: content is intentionally added, revised and removed
// continuously. The loader is authoritative for the current, validated inventory.
var all = Enumerable.Range(0, (catalog.Count + 99) / 100)
    .SelectMany(page => catalog.Search(offset: page * 100, limit: 100).Items)
    .ToArray();
var counts = all
    .GroupBy(rule => rule.Category, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(group => group.Key, group => group.Count(),
        StringComparer.OrdinalIgnoreCase);
if (all.Length != catalog.Count ||
    all.Select(rule => rule.Id).Distinct(StringComparer.Ordinal).Count() != catalog.Count ||
    counts.Sum(entry => entry.Value) != catalog.Count ||
    counts.Count != catalog.CountsByCategory.Count ||
    counts.Any(pair => catalog.CountByCategory(pair.Key) != pair.Value))
{
    Console.Error.WriteLine("Native loader inventory, pagination or category facets disagree.");
    return 1;
}

Console.WriteLine($"RULESET_ID={catalog.RulesetId}");
Console.WriteLine($"CATALOG_SHA256={catalog.Fingerprint}");
Console.WriteLine($"NATIVE_ENTITY_COUNT={catalog.Count}");
Console.WriteLine($"CONTENT_REVIEW_SHA={Environment.GetEnvironmentVariable("CONTENT_REVIEW_SHA") ?? Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local"}");
foreach (var kind in counts.OrderBy(entry => entry.Key, StringComparer.Ordinal))
    Console.WriteLine($"CATEGORY {kind.Key}={kind.Value}");

// The experimental review artifact must not become an ordinary native record.
if (all.Any(entity => entity.Id.StartsWith("experiment.", StringComparison.Ordinal)))
{
    Console.Error.WriteLine("Experimental content entered the authoritative native catalog.");
    return 1;
}
Console.WriteLine("CONTENT_FINGERPRINT_VALIDATED=true");
return 0;
