using System.Text.Json;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Builds Source Layer identity from the same entity fields 5e.tools uses for its
/// native hashes/UIDs. Content/provenance fields such as page, edition, parentSource,
/// and representation path are deliberately excluded: changes to those fields are
/// revisions or publication evidence, not a new 5e.tools entity identity.
/// </summary>
internal static class FiveEToolsNativeIdentity
{
    public static FiveEToolsNativeIdentityValue Create(
        string entityType,
        JsonElement item,
        string name,
        string sourceCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCode);

        var fields = new List<KeyValuePair<string, string?>>
        {
            new("entityType", entityType),
            new("name", name),
            new("source", sourceCode)
        };

        switch (entityType)
        {
            case "classFeature":
                Add(fields, item, "className");
                Add(fields, item, "classSource");
                Add(fields, item, "level");
                break;
            case "subclassFeature":
                Add(fields, item, "className");
                Add(fields, item, "classSource");
                Add(fields, item, "subclassShortName");
                Add(fields, item, "subclassSource");
                Add(fields, item, "level");
                break;
            case "subclass":
                fields.Add(new("shortName", ReadScalar(item, "shortName") ?? name));
                Add(fields, item, "className");
                Add(fields, item, "classSource");
                break;
            case "subrace":
                Add(fields, item, "raceName");
                break;
            case "deity":
                Add(fields, item, "pantheon");
                break;
            case "card":
                Add(fields, item, "set");
                break;
            case "name":
                Add(fields, item, "option");
                break;
            case "encounter":
                fields.Add(new("minlvl", ReadScalar(item, "minlvl") ?? "0"));
                fields.Add(new("maxlvl", ReadScalar(item, "maxlvl") ?? "0"));
                fields.Add(new("caption", ReadScalar(item, "caption") ?? string.Empty));
                break;
        }

        var identityJson = JsonSerializer.Serialize(
            fields.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal));
        var hasSpecialIdentity = fields.Count > 3;
        var nativeKey = hasSpecialIdentity
            ? $"{entityType}|{sourceCode}|{name}|uid-{CanonicalSourceIdentity.Fingerprint(identityJson)[..24]}"
            : $"{entityType}|{sourceCode}|{name}|";
        return new FiveEToolsNativeIdentityValue(nativeKey, identityJson);
    }

    private static void Add(
        ICollection<KeyValuePair<string, string?>> fields,
        JsonElement item,
        string propertyName) =>
        fields.Add(new(propertyName, ReadScalar(item, propertyName)));

    private static string? ReadScalar(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }
}

internal sealed record FiveEToolsNativeIdentityValue(
    string NativeKey,
    string IdentityJson);
