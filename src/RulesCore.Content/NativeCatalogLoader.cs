using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RulesCore.Content;

/// <summary>
/// Loads one complete catalog from the selected ruleset's native/ tree.
/// Never probes working/, reference/, conversions/, or any fallback path.
/// </summary>
public static class NativeCatalogLoader
{
    private static readonly Regex EntityId = new(
        @"^[a-z0-9-]+(?:\.[a-z0-9-]+)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 128
    };

    public static NativeCatalogSnapshot Load(NativeRulesetLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        try
        {
            return LoadValidated(location);
        }
        catch (IOException exc)
        {
            throw Invalid("content_unavailable", "Native content cannot be read.", exc);
        }
        catch (UnauthorizedAccessException exc)
        {
            throw Invalid("content_unavailable", "Native content cannot be read.", exc);
        }
    }

    private static NativeCatalogSnapshot LoadValidated(NativeRulesetLocation location)
    {
        var expected = NativeRulesetLayout.Resolve(location.ContentRoot, location.RulesetId);
        if (!PathEquals(expected.NativeDirectory, location.NativeDirectory) ||
            !PathEquals(expected.ContentRoot, location.ContentRoot))
        {
            throw Invalid("unsafe_content_path", "Native directory does not match ruleset location.");
        }

        var package = Path.Combine(expected.ContentRoot, expected.RulesetId);
        var native = expected.NativeDirectory;
        RequireDirectory(expected.ContentRoot);
        RequireDirectory(package);
        RequireDirectory(native);

        var manifestPath = Path.Combine(package, "manifest.json");
        EnsureSafeEntry(manifestPath);
        using var manifest = ReadDocument(manifestPath, "invalid_manifest");
        ValidateProperties(manifest.RootElement, "invalid_manifest");
        var manifestRoot = manifest.RootElement;
        if (manifestRoot.ValueKind != JsonValueKind.Object ||
            ReadString(manifestRoot, "id") != expected.RulesetId ||
            ReadString(manifestRoot, "kind") != "ruleset-package" ||
            ReadString(manifestRoot, "nativeRoot") != "native/" ||
            ReadString(manifestRoot, "runtimeContentRoot") != "native/" ||
            ReadString(manifestRoot, "workingRoot") != "working/" ||
            !manifestRoot.TryGetProperty("schemaVersion", out var schema) ||
            schema.ValueKind != JsonValueKind.Number ||
            !schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1)
        {
            throw Invalid("invalid_manifest", "Ruleset manifest identity or native authority is invalid.");
        }

        var entities = new List<NativeRuleEntity>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var files = EnumerateJson(native).ToArray();
        if (files.Length == 0)
            throw Invalid("content_unavailable", "No native JSON definitions exist.");

        foreach (var file in files)
        {
            using var document = ReadDocument(file, "invalid_native_json");
            ValidateProperties(document.RootElement, "invalid_native_json");
            var relative = "native/" + Path.GetRelativePath(native, file)
                .Replace(Path.DirectorySeparatorChar, '/');
            if (relative.StartsWith("native/origination/construct-assembly/components/", StringComparison.Ordinal))
            {
                // A component's stable identity and catalog category come from its
                // type directory and filename. Expand them before ordinary catalog
                // validation so every downstream consumer sees the existing schema.
                using var expanded = ExpandConstructComponent(document.RootElement, relative);
                ParseFile(expanded.RootElement, relative, expected.RulesetId, entities, identities);
            }
            else
                ParseFile(document.RootElement, relative, expected.RulesetId, entities, identities);
        }

        if (entities.Count == 0)
            throw Invalid("invalid_native_json", "Native package has no playable definitions.");

        // References are authoritative in native files; the expanded view keeps
        // existing consumers of source.Definition.traits backward compatible.
        var (resolvedEntities, traitReferences) = ResolveOriginTraitReferences(entities);
        ValidateConditionReferences(resolvedEntities);
        // Validate the complete group graph once at load. Coverage requests
        // subsequently use the immutable snapshot and must not repair or
        // reinterpret malformed catalog relationships on the fly.
        ValidateEquipmentReferences(resolvedEntities);
        var fingerprint = Fingerprint(expected.RulesetId, schemaVersion, resolvedEntities);
        return new NativeCatalogSnapshot(expected.RulesetId, schemaVersion, fingerprint,
            resolvedEntities, traitReferences);
    }

    /// <summary>
    /// Expand path-derived identity only within the dedicated Construct component
    /// catalog. Other native entities and legacy component fixtures keep their
    /// explicit id/kind contract. The functional directory supplies a queryable componentCategory, not
    /// a new gameplay mechanic or part of the stable component ID.
    /// </summary>
    private static JsonDocument ExpandConstructComponent(JsonElement root, string path)
    {
        const string prefix = "native/origination/construct-assembly/components/";
        var parts = path[prefix.Length..].Split('/');
        if (parts.Length != 2 || !parts[1].EndsWith(".json", StringComparison.Ordinal))
            throw Invalid("invalid_native_json", "Construct components require a type directory and a JSON filename.");

        var componentType = parts[0];
        var stem = parts[1][..^".json".Length];
        // Lowercase, hyphen-separated, path-safe names are required. Their
        // stability protects IDs and avoids ambiguous names on other platforms.
        static bool ValidSlug(string value) =>
            value.Length > 0 && Regex.IsMatch(value,
                @"^[a-z0-9]+(?:-[a-z0-9]+)*$",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        if (!ValidSlug(componentType) || !ValidSlug(stem) ||
            root.ValueKind != JsonValueKind.Object)
            throw Invalid("invalid_native_json", "Invalid Construct component path or definition.");

        var expectedId = "construct-component." + stem;
        if (root.TryGetProperty("id", out _) && ReadString(root, "id") != expectedId ||
            root.TryGetProperty("kind", out _) && ReadString(root, "kind") != "construct-component" ||
            root.TryGetProperty("componentCategory", out _) &&
                ReadString(root, "componentCategory") != componentType)
            throw Invalid("invalid_native_json", "Construct component metadata conflicts with its path.");

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("id", expectedId);
            writer.WriteString("kind", "construct-component");
            writer.WriteString("componentCategory", componentType);
            foreach (var property in root.EnumerateObject())
                if (property.Name is not ("id" or "kind" or "componentCategory"))
                    property.WriteTo(writer);
            writer.WriteEndObject();
        }
        return JsonDocument.Parse(buffer.ToArray(), JsonOptions);
    }

    private static void ParseFile(JsonElement root, string path, string rulesetId,
        List<NativeRuleEntity> entities, HashSet<string> identities)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                AddEntity(item, path, rulesetId, entities, identities);
            return;
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw Invalid("invalid_native_json", "Native document must be a JSON object or array.");

        if (root.TryGetProperty("id", out _))
        {
            AddEntity(root, path, rulesetId, entities, identities);
            return;
        }

        var hasCollection = false;
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.StartsWith('_'))
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                    throw Invalid("invalid_native_json", "Document metadata must be an object.");
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Array)
                throw Invalid("invalid_native_json", "Catalog collections must be arrays.");

            hasCollection = true;
            foreach (var item in property.Value.EnumerateArray())
                AddEntity(item, path, rulesetId, entities, identities);
        }

        if (!hasCollection)
            throw Invalid("invalid_native_json", "Native document contains no entity collection.");
    }

    private static void AddEntity(JsonElement element, string path, string rulesetId,
        List<NativeRuleEntity> entities, HashSet<string> identities)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Invalid("invalid_native_json", "Entity must be an object.");

        var category = ReadString(element, "kind");
        if (string.IsNullOrWhiteSpace(category))
            throw Invalid("invalid_native_json", "Entity category (kind) is required.");

        // Metadata is never a playable rule and is never present in the indexes.
        if (string.Equals(category, "metadata", StringComparison.OrdinalIgnoreCase))
            return;

        var id = ReadString(element, "id");
        var name = ReadString(element, "name");
        if (id is null || !EntityId.IsMatch(id) ||
            string.IsNullOrWhiteSpace(name) ||
            !string.Equals(name, name.Trim(), StringComparison.Ordinal) ||
            !string.Equals(category, category.Trim(), StringComparison.Ordinal))
        {
            throw Invalid("invalid_native_json", "Entity has an invalid ID, name, or category.");
        }

        if (!identities.Add(id))
            throw Invalid("duplicate_entity_id", "Duplicate native entity identity: " + id);

        entities.Add(new NativeRuleEntity(
            rulesetId, id, name, category, path, element.Clone()));
    }

    /// <summary>
    /// Resolve authored Species/Ancestry occurrences against independent trait
    /// entities. Only the canonical entity stores the mechanical definition on
    /// disk; the projection is for existing catalog consumers. Source paths and
    /// array indices remain authored occurrence identities, not definition IDs.
    /// Legacy inline occurrences are accepted for isolated catalog fixtures.
    /// </summary>
    private static (List<NativeRuleEntity> Entities, Dictionary<string, string> References)
        ResolveOriginTraitReferences(IReadOnlyList<NativeRuleEntity> entities)
    {
        var byId = entities.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var references = new Dictionary<string, string>(StringComparer.Ordinal);
        var resolved = new List<NativeRuleEntity>(entities.Count);

        foreach (var entity in entities)
        {
            if (entity.Category is not ("species" or "ancestry") ||
                !entity.Definition.TryGetProperty("traits", out var traits))
            {
                resolved.Add(entity);
                continue;
            }

            if (traits.ValueKind != JsonValueKind.Array)
                throw Invalid("invalid_origin_trait_reference",
                    "Species/Ancestry traits must be an array.");

            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (var property in entity.Definition.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (property.Name != "traits")
                    {
                        property.Value.WriteTo(writer);
                        continue;
                    }

                    writer.WriteStartArray();
                    var index = 0;
                    var seenLocalIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var occurrence in traits.EnumerateArray())
                    {
                        if (occurrence.ValueKind != JsonValueKind.Object)
                            throw Invalid("invalid_origin_trait_reference",
                                "Trait occurrences must be objects.");

                        // Only reference entries acquire a canonical mechanical ID.
                        // Inline entries remain supported for legacy test fixtures.
                        var isReference = occurrence.TryGetProperty("traitId", out var reference);
                        JsonElement payload;
                        if (isReference)
                        {
                            if (occurrence.EnumerateObject().Count() != 1 ||
                                reference.ValueKind != JsonValueKind.String ||
                                !byId.TryGetValue(reference.GetString()!, out var definition) ||
                                definition.Category != "origin-trait" ||
                                !definition.Definition.TryGetProperty("trait", out var trait) ||
                                trait.ValueKind != JsonValueKind.Object)
                                throw Invalid("invalid_origin_trait_reference",
                                    "Trait reference must identify an independent origin-trait definition.");
                            payload = trait;
                            references.Add(entity.Id + "#" + index, definition.Id);
                        }
                        else
                        {
                            payload = occurrence;
                        }

                        // Older consumers also identify traits by their local
                        // source-qualified ID. Two occurrences with that same ID
                        // become indistinguishable in inventories even when their
                        // canonical definition IDs differ.
                        var localId = ReadString(payload, "id");
                        if ((isReference && (string.IsNullOrWhiteSpace(localId) ||
                                !string.Equals(localId, localId.Trim(), StringComparison.Ordinal))) ||
                            (localId is not null && !seenLocalIds.Add(localId)))
                            throw Invalid("invalid_origin_trait_reference",
                                "Source trait local IDs must be nonempty and unique.");

                        payload.WriteTo(writer);
                        index++;
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
                writer.Flush();
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), JsonOptions);
            resolved.Add(entity with { Definition = document.RootElement.Clone() });
        }

        return (resolved, references);
    }

    /// <summary>
    /// Equipment scope IDs are native catalog identities, not display-name
    /// matches. Reject dangling, cross-category, and cyclic memberships.
    /// </summary>
    private static void ValidateEquipmentReferences(IReadOnlyList<NativeRuleEntity> entities)
    {
        var byId = entities.ToDictionary(entity => entity.Id, StringComparer.Ordinal);
        var categories = new HashSet<string>(["weapon", "armor", "shield", "tool"],
            StringComparer.Ordinal);
        var groups = new Dictionary<string, string[]>(StringComparer.Ordinal);

        static string[] References(JsonElement definition, string property, bool required)
        {
            if (!definition.TryGetProperty(property, out var value))
            {
                if (required) throw Invalid("invalid_equipment_reference",
                    "Equipment type is missing its group membership list.");
                return [];
            }
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 100 ||
                value.EnumerateArray().Any(id => id.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(id.GetString())))
                throw Invalid("invalid_equipment_reference",
                    "Equipment membership must be a bounded list of group IDs.");
            var refs = value.EnumerateArray().Select(id => id.GetString()!).ToArray();
            if (refs.Distinct(StringComparer.Ordinal).Count() != refs.Length)
                throw Invalid("invalid_equipment_reference", "Duplicate equipment group references.");
            return refs;
        }

        foreach (var entity in entities)
        {
            if (entity.Category == "item" &&
                entity.Definition.TryGetProperty("equipmentTypeId", out var equipmentType))
            {
                if (equipmentType.ValueKind != JsonValueKind.String ||
                    !byId.TryGetValue(equipmentType.GetString()!, out var resolved) ||
                    resolved.Category != "equipment-type")
                    throw Invalid("invalid_equipment_reference",
                        "Item equipmentTypeId must identify an existing equipment type.");
            }
            if (entity.Category is not ("equipment-type" or "equipment-group"))
                continue;
            var category = ReadString(entity.Definition, "equipmentCategory");
            if (category is null || !categories.Contains(category))
                throw Invalid("invalid_equipment_reference",
                    "Equipment categories must be weapon, armor, shield, or tool.");

            var references = entity.Category == "equipment-type"
                ? References(entity.Definition, "groupIds", required: true)
                : References(entity.Definition, "parentGroupIds", required: false);
            foreach (var reference in references)
                if (!byId.TryGetValue(reference, out var target) ||
                    target.Category != "equipment-group" ||
                    ReadString(target.Definition, "equipmentCategory") != category)
                    throw Invalid("invalid_equipment_reference",
                        "Equipment group identity is missing or has a different category.");

            if (entity.Category == "equipment-group") groups[entity.Id] = references;
        }

        // After validating each edge's target and category, DFS checks that
        // no parent chain can loop. A visited group need not be traversed again.
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in groups.Keys)
            Visit(id);

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id))
                throw Invalid("invalid_equipment_reference",
                    "Equipment group membership must not contain cycles.");
            foreach (var parent in groups[id]) Visit(parent);
            visiting.Remove(id);
            visited.Add(id);
        }
    }

    private static void ValidateConditionReferences(IReadOnlyList<NativeRuleEntity> entities)
    {
        var byId = entities.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var conditionIds = entities
            .Where(e => e.Category is "condition" or "leveled-condition")
            .Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            if (entity.Definition.TryGetProperty("conditionIds", out var chapterIds))
            {
                if (chapterIds.ValueKind != JsonValueKind.Array)
                    throw Invalid("invalid_native_json", "conditionIds must be an array.");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in chapterIds.EnumerateArray())
                {
                    var reference = value.ValueKind == JsonValueKind.String
                        ? value.GetString() : null;
                    if (reference is null || !seen.Add(reference) || !conditionIds.Contains(reference))
                        throw Invalid("invalid_native_json", "Unknown or repeated condition chapter reference.");
                }
                if (entity.Id == "rule.conditions" && !seen.SetEquals(conditionIds))
                    throw Invalid("invalid_native_json", "Condition chapter does not match the condition catalog.");
            }

            if (!conditionIds.Contains(entity.Id)) continue;
            if (entity.Definition.TryGetProperty("appliesConditions", out var applies))
            {
                if (applies.ValueKind != JsonValueKind.Array)
                    throw Invalid("invalid_native_json", "appliesConditions must be an array.");
                foreach (var item in applies.EnumerateArray())
                    ValidateConditionReference(item, conditionIds);
            }

            if (entity.Definition.TryGetProperty("requiresConditions", out var requires))
            {
                if (requires.ValueKind != JsonValueKind.Array)
                    throw Invalid("invalid_native_json", "requiresConditions must be an array.");
                foreach (var item in requires.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("condition", out var condition))
                        throw Invalid("invalid_native_json", "Invalid required condition reference.");
                    ValidateConditionReference(condition, conditionIds);
                }
            }
        }
    }

    private static void ValidateConditionReference(JsonElement value, HashSet<string> conditions)
    {
        if (value.ValueKind != JsonValueKind.String ||
            !conditions.Contains(value.GetString()!))
            throw Invalid("invalid_native_json", "Unknown condition reference.");
    }

    private static string Fingerprint(string id, int schemaVersion,
        IEnumerable<NativeRuleEntity> entities)
    {
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject();
            writer.WriteString("rulesetId", id);
            writer.WriteNumber("schemaVersion", schemaVersion);
            writer.WritePropertyName("entities");
            writer.WriteStartArray();
            foreach (var entity in entities.OrderBy(e => e.Id, StringComparer.Ordinal))
                WriteCanonical(writer, entity.Definition);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject()
                         .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else
        {
            value.WriteTo(writer);
        }
    }

    private static void ValidateProperties(JsonElement element, string reason)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!keys.Add(property.Name))
                    throw Invalid(reason, "Duplicate JSON property name.");
                ValidateProperties(property.Value, reason);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) ValidateProperties(item, reason);
        }
    }

    private static JsonDocument ReadDocument(string path, string invalidReason)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllBytes(path), JsonOptions);
        }
        catch (JsonException exc)
        {
            throw Invalid(invalidReason, "Malformed JSON document.", exc);
        }
        catch (IOException exc)
        {
            throw Invalid("content_unavailable", "Native content cannot be read.", exc);
        }
        catch (UnauthorizedAccessException exc)
        {
            throw Invalid("content_unavailable", "Native content cannot be read.", exc);
        }
    }

    private static IEnumerable<string> EnumerateJson(string root)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(root)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            EnsureContained(root, entry);
            var attributes = EnsureSafeEntry(entry);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                foreach (var child in EnumerateJson(entry))
                    yield return child;
            }
            else if (string.Equals(Path.GetExtension(entry), ".json",
                         StringComparison.OrdinalIgnoreCase))
            {
                yield return entry;
            }
        }
    }

    private static void RequireDirectory(string path)
    {
        if (!Directory.Exists(path))
            throw Invalid("content_unavailable", "Required ruleset content directory is unavailable.");
        var attributes = EnsureSafeEntry(path);
        if ((attributes & FileAttributes.Directory) == 0)
            throw Invalid("content_unavailable", "Required content location is not a directory.");
    }

    private static FileAttributes EnsureSafeEntry(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw Invalid("unsafe_content_path", "Linked native content paths are not permitted.");
            return attributes;
        }
        catch (IOException exc)
        {
            throw Invalid("content_unavailable", "Content path is unavailable.", exc);
        }
        catch (UnauthorizedAccessException exc)
        {
            throw Invalid("content_unavailable", "Content path is unavailable.", exc);
        }
    }

    private static void EnsureContained(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
            throw Invalid("unsafe_content_path", "Native file escapes its content root.");
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase
                                        : StringComparison.Ordinal);

    private static string? ReadString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static NativeCatalogLoadException Invalid(
        string code, string message, Exception? inner = null) => new(code, message, inner);
}
