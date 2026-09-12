using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Application.Sources;

const string HewnHeroRevision = "d06d1dadee357857767b1e4da985df6609509bcf";
const string ThreeFiveRevision = "c7f30a0ce11a579f75456746f278a4c75f67b4c1";
const string HewnHeroRoot = $"https://raw.githubusercontent.com/CoolFireGiant/hewnhero-srd/{HewnHeroRevision}/data/";
const string ThreeEIndex = "https://www.dragon.ee/30srd/";
const string ThreeFiveTree = $"https://api.github.com/repos/olimot/srd-v3.5-md/git/trees/{ThreeFiveRevision}?recursive=1";

var output = Path.GetFullPath(args.Length == 0
    ? "src/RulesCore.Infrastructure/BundledSources"
    : args[0]);
Directory.CreateDirectory(output);

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("DorksAndDice-RulesCore-SnapshotGenerator/1.0");

await GenerateThreeEAsync();
await GenerateThreeFiveAsync();
await GenerateFiveEAsync(
    sourceCode: "SRD51",
    editionKey: "5.1",
    fileName: "srd-5-1.json",
    editionSpecific:
    [
        "bestiary/bestiary-srd51.json",
        "spells/spells-srd51.json",
        "deities.json"
    ]);
await GenerateFiveEAsync(
    sourceCode: "SRD52",
    editionKey: "5.2.1",
    fileName: "srd-5-2-1.json",
    editionSpecific:
    [
        "bestiary/bestiary-srd52.json",
        "spells/spells-srd52.json"
    ]);

async Task GenerateThreeEAsync()
{
    Console.WriteLine("Generating SRD 3e snapshot...");
    var indexHtml = await GetStringAsync(ThreeEIndex);
    var references = LegacySrdDocumentInspector.ResolveHtmlIndexReferences(
        indexHtml,
        new Uri(ThreeEIndex));
    if (references.Count != 139)
    {
        throw new InvalidDataException(
            $"Reviewed 3e corpus expected 139 documents but resolved {references.Count}.");
    }

    var converted = new ConcurrentBag<string>();
    await Parallel.ForEachAsync(
        references,
        new ParallelOptions { MaxDegreeOfParallelism = 12 },
        async (uri, cancellationToken) =>
        {
            var content = await GetStringAsync(uri.AbsoluteUri, cancellationToken);
            var json = LegacySrdDocumentInspector.ConvertToCanonicalJson(
                content,
                uri.AbsoluteUri,
                "SRD3",
                out _);
            converted.Add(json);
        });

    WriteSnapshot("srd-3e.json", "SRD3", converted);
}

async Task GenerateThreeFiveAsync()
{
    Console.WriteLine("Generating SRD 3.5e snapshot...");
    var treeJson = await GetStringAsync(ThreeFiveTree);
    using var tree = JsonDocument.Parse(treeJson);
    if (tree.RootElement.TryGetProperty("truncated", out var truncated)
        && truncated.ValueKind == JsonValueKind.True)
    {
        throw new InvalidDataException("GitHub truncated the pinned SRD 3.5 tree.");
    }

    var prefixes = new[]
    {
        "basic-rules-and-legal/",
        "divine/",
        "epic/",
        "magic-items/",
        "monsters/",
        "psionics/",
        "spells/"
    };
    var paths = tree.RootElement.GetProperty("tree")
        .EnumerateArray()
        .Where(entry =>
            entry.TryGetProperty("type", out var type)
            && type.GetString() == "blob"
            && entry.TryGetProperty("path", out var path)
            && path.ValueKind == JsonValueKind.String)
        .Select(entry => entry.GetProperty("path").GetString()!)
        .Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)))
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

    if (paths.Length == 0)
    {
        throw new InvalidDataException("Pinned SRD 3.5 tree contained no reviewed Markdown documents.");
    }

    var converted = new ConcurrentBag<string>();
    await Parallel.ForEachAsync(
        paths,
        new ParallelOptions { MaxDegreeOfParallelism = 12 },
        async (path, cancellationToken) =>
        {
            var escaped = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
            var uri = $"https://raw.githubusercontent.com/olimot/srd-v3.5-md/{ThreeFiveRevision}/{escaped}";
            var content = await GetStringAsync(uri, cancellationToken);
            var json = LegacySrdDocumentInspector.ConvertToCanonicalJson(
                content,
                uri,
                "SRD35",
                out _);
            converted.Add(json);
        });

    WriteSnapshot("srd-3-5e.json", "SRD35", converted);
}

async Task GenerateFiveEAsync(
    string sourceCode,
    string editionKey,
    string fileName,
    IReadOnlyList<string> editionSpecific)
{
    Console.WriteLine($"Generating {sourceCode} snapshot...");
    var direct = new[]
    {
        "actions.json",
        "backgrounds.json",
        "conditionsdiseases.json",
        "feats.json",
        "items-base.json",
        "items.json",
        "languages.json",
        "magicvariants.json",
        "objects.json",
        "optionalfeatures.json",
        "races.json",
        "senses.json",
        "skills.json",
        "tables.json",
        "trapshazards.json",
        "variantrules.json",
        "vehicles.json"
    }.Concat(editionSpecific).ToArray();

    var documents = new List<string>();
    foreach (var path in direct)
    {
        var json = await GetStringAsync(HewnHeroRoot + path);
        var filtered = FiveEToolsDocumentInspector.FilterBySourceCodes(
            json,
            editionKey,
            [sourceCode],
            out var selected);
        if (selected > 0)
        {
            documents.Add(filtered);
        }
    }

    var classIndexUri = HewnHeroRoot + "class/index.json";
    var classIndex = await GetStringAsync(classIndexUri);
    using (var indexDocument = JsonDocument.Parse(classIndex))
    {
        var references = new List<string>();
        CollectJsonReferences(indexDocument.RootElement, references);
        foreach (var reference in references.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var resolved = new Uri(new Uri(classIndexUri), reference).AbsoluteUri;
            var json = await GetStringAsync(resolved);
            var filtered = FiveEToolsDocumentInspector.FilterBySourceCodes(
                json,
                editionKey,
                [sourceCode],
                out var selected);
            if (selected > 0)
            {
                documents.Add(filtered);
            }
        }
    }

    WriteSnapshot(fileName, sourceCode, documents);
}

void WriteSnapshot(string fileName, string fallbackSourceCode, IEnumerable<string> documents)
{
    var buckets = new SortedDictionary<string, List<JsonElement>>(StringComparer.Ordinal);
    var exact = new HashSet<string>(StringComparer.Ordinal);

    foreach (var json in documents)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!FiveEToolsDocumentInspector.IsImportableArray(property))
            {
                continue;
            }

            if (!buckets.TryGetValue(property.Name, out var items))
            {
                items = [];
                buckets[property.Name] = items;
            }

            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
          || !item.TryGetProperty("name", out var nameValue)
          || nameValue.ValueKind != JsonValueKind.String
          || string.IsNullOrWhiteSpace(nameValue.GetString()))
      {
          continue;
      }

                var normalized = AddStableIdentity(property.Name, item);
                var canonical = Canonicalize(normalized);
                var fingerprint = property.Name + "\n" + Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
                if (!exact.Add(fingerprint))
                {
                    continue;
                }
                items.Add(normalized.Clone());
            }
        }
    }

    ValidateNaturalKeys(buckets, fallbackSourceCode);

    var destination = Path.Combine(output, fileName);
    using var stream = File.Create(destination);
    using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
    writer.WriteStartObject();
    foreach (var bucket in buckets)
    {
        writer.WritePropertyName(bucket.Key);
        writer.WriteStartArray();
        foreach (var item in bucket.Value)
        {
            item.WriteTo(writer);
        }
        writer.WriteEndArray();
    }
    writer.WriteEndObject();
    writer.Flush();
    Console.WriteLine($"Wrote {destination} ({buckets.Sum(value => value.Value.Count)} entities).");
}

static JsonElement AddStableIdentity(string entityType, JsonElement item)
{
    if (item.TryGetProperty("uniqueId", out _)
        || item.TryGetProperty("id", out _))
    {
        return item.Clone();
    }

    string? uniqueId = entityType switch
    {
        "itemType" => JoinIdentity(item, "abbreviation"),
        "deity" => JoinIdentity(item, "pantheon"),
        "classFeature" => JoinIdentity(
            item,
            "className",
            "classSource",
            "level"),
        "subclassFeature" => JoinIdentity(
            item,
            "className",
            "classSource",
            "subclassShortName",
            "subclassSource",
            "level"),
        _ => null
    };

    if (string.IsNullOrWhiteSpace(uniqueId))
    {
        return item.Clone();
    }

    var node = JsonNode.Parse(item.GetRawText())!.AsObject();
    node["uniqueId"] = uniqueId;
    using var normalized = JsonDocument.Parse(node.ToJsonString());
    return normalized.RootElement.Clone();
}

static string? JoinIdentity(JsonElement item, params string[] names)
{
    var values = new List<string>();
    foreach (var name in names)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return null;
        }
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        values.Add(text.Trim());
    }
    return string.Join('|', values);
}

static void ValidateNaturalKeys(
    IReadOnlyDictionary<string, List<JsonElement>> buckets,
    string fallbackSourceCode)
{
    var keys = new HashSet<string>(StringComparer.Ordinal);
    foreach (var bucket in buckets)
    {
        foreach (var item in bucket.Value)
        {
            if (!item.TryGetProperty("name", out var nameValue)
                || nameValue.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    $"Bundled array '{bucket.Key}' contains an entity without a name.");
            }
            var name = nameValue.GetString()!.Trim();
            var source = item.TryGetProperty("source", out var sourceValue)
                && sourceValue.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(sourceValue.GetString())
                    ? sourceValue.GetString()!.Trim()
                    : fallbackSourceCode;
            var suffix = GetScalar(item, "uniqueId") ?? GetScalar(item, "id");
            var key = $"{bucket.Key}|{source}|{name}{(suffix is null ? string.Empty : $"|{suffix}")}"
                .Trim()
                .ToLowerInvariant();
            if (!keys.Add(key))
            {
                throw new InvalidDataException(
                    $"Generated snapshot still contains duplicate entity identity '{key}'.");
            }
        }
    }
}

static string? GetScalar(JsonElement item, string name)
{
    if (!item.TryGetProperty(name, out var value))
    {
        return null;
    }
    return value.ValueKind switch
    {
        JsonValueKind.String when !string.IsNullOrWhiteSpace(value.GetString()) => value.GetString()!.Trim(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null
    };
}

static string Canonicalize(JsonElement element)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
        WriteCanonical(writer, element);
    }
    return Encoding.UTF8.GetString(stream.ToArray());
}

static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
            break;
        case JsonValueKind.Array:
            writer.WriteStartArray();
            foreach (var child in element.EnumerateArray())
            {
                WriteCanonical(writer, child);
            }
            writer.WriteEndArray();
            break;
        default:
            element.WriteTo(writer);
            break;
    }
}

static void CollectJsonReferences(JsonElement element, ICollection<string> references)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            foreach (var property in element.EnumerateObject())
            {
                CollectJsonReferences(property.Value, references);
            }
            break;
        case JsonValueKind.Array:
            foreach (var item in element.EnumerateArray())
            {
                CollectJsonReferences(item, references);
            }
            break;
        case JsonValueKind.String:
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value)
                && value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                references.Add(value.Trim());
            }
            break;
    }
}

async Task<string> GetStringAsync(string uri, CancellationToken cancellationToken = default)
{
    using var response = await http.GetAsync(uri, cancellationToken);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync(cancellationToken);
}
