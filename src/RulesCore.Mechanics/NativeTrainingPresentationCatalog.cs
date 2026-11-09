using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

public static class NativeTrainingPresentationCatalog
{
    public static IReadOnlyList<NativeTrainingPresentation> Read(NativeCatalogSnapshot catalog)
    {
        var result = new List<NativeTrainingPresentation>();
        foreach (var category in new[] { "skill", "skill-family", "competency" })
        {
            for (var offset = 0; ; offset += 100)
            {
                var page = catalog.Search(category: category, offset: offset, limit: 100);
                foreach (var item in page.Items)
                {
                    var definition = item.Definition;
                    var components = definition.TryGetProperty("childSkills", out var children) &&
                        children.ValueKind == JsonValueKind.Array
                        ? children.EnumerateArray().Where(child => child.ValueKind == JsonValueKind.String)
                            .Select(child => child.GetString()!).ToArray() : [];
                    var family = category == "skill-family";
                    var ability = Text(definition, "defaultAbility");
                    if (category == "competency" && definition.TryGetProperty("facets", out var facets) &&
                        facets.ValueKind == JsonValueKind.Array)
                        ability ??= facets.EnumerateArray().Select(facet => Text(facet, "governingAbility"))
                            .FirstOrDefault(value => value is not null);
                    var composition = definition.TryGetProperty("composition", out var operation)
                        ? Text(operation, "operation") : null;
                    // Families are headings; composites derive their value from
                    // children. Neither has an independently editable rank.
                    result.Add(new(item.Id, item.Name, category, ability,
                        Text(definition, "family"), components, composition,
                        family, !family && components.Length == 0));
                }
                if (offset + page.Items.Length >= page.Total) break;
            }
        }
        return result.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    private static string? Text(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var text) &&
        text.ValueKind == JsonValueKind.String ? text.GetString() : null;
}
