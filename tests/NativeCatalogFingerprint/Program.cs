using System.Text.Json;
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
var ledgerPath = Path.Combine(repository, "docs", "content-promotion", "promotion-ledger.json");
using var ledger = JsonDocument.Parse(File.ReadAllText(ledgerPath));
var expected = ledger.RootElement.GetProperty("inventory").GetProperty("expectedAfter");
var total = expected.GetProperty("total").GetInt32();
var kinds = expected.GetProperty("byKind");
var hasMismatch = catalog.Count != total;

Console.WriteLine($"RULESET_ID={catalog.RulesetId}");
Console.WriteLine($"CATALOG_SHA256={catalog.Fingerprint}");
Console.WriteLine($"NATIVE_ENTITY_COUNT={catalog.Count}");
Console.WriteLine($"RELEASE_SHA={Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local"}");
foreach (var kind in kinds.EnumerateObject())
{
    var actual = catalog.CountByCategory(kind.Name);
    var required = kind.Value.GetInt32();
    Console.WriteLine($"CATEGORY {kind.Name}={actual} expected={required}");
    hasMismatch |= actual != required;
}

if (hasMismatch)
{
    Console.Error.WriteLine("Catalog contents differ from reviewed content ledger.");
    return 1;
}

// The experimental review file is deliberately outside the loader's native root.
if (catalog.TryGetById("class.warlock", out _) ||
    catalog.TryGetById("experiment.warlock-modular-alpha-0-3-1", out _))
{
    Console.Error.WriteLine("Experimental or pending Warlock content entered the native catalog.");
    return 1;
}
Console.WriteLine("CONTENT_FINGERPRINT_VALIDATED=true");
return 0;
