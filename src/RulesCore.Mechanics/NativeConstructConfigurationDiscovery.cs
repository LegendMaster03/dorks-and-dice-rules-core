using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Bounded native configuration metadata for a builder, not a raw catalog
/// response or authorization to install, operate or manufacture a component.
/// </summary>
public sealed record NativeConstructConfigurationOption(string Id, string Name);
public sealed record NativeConstructConfigurationVariant(string Id, string RecipeId, string? BodySize);
public sealed record NativeConstructPowerSourcePresentation(
    string Id, string Name, string? Description, string? RulesStatus,
    string? OperatingPrinciple, int? RatedOutput, int? RatedOverdriveOutput);
public sealed record NativeConstructComponentConfiguration(
    string DefinitionId, string Name, IReadOnlyList<string> MaterialSlots,
    string? DefaultRecipeId, IReadOnlyList<NativeConstructConfigurationVariant> ConstructionVariants,
    IReadOnlyList<string> ModeIds, IReadOnlyList<string> BaseCapabilities,
    IReadOnlyList<string[]> RequiresAnyOf,
    IReadOnlyList<string> MountInterfaces, IReadOnlyList<string> AcceptsMountInterfaces,
    bool PowerSourceRequired, bool RequiresContinuousPower,
    IReadOnlyList<NativeConstructConfigurationOption> Designs,
    IReadOnlyList<NativeConstructConfigurationOption> Enhancements,
    IReadOnlyList<NativeConstructConfigurationOption> PowerSources,
    IReadOnlyList<NativeOriginContractIssue> Issues)
{
    public string? ComponentCategory { get; init; }
    // These are catalog-authored descriptions and rated values, not proof
    // of fuel, usable stored energy, current output, or a gameplay transaction.
    public IReadOnlyList<NativeConstructPowerSourcePresentation> PowerSourceDetails { get; init; } = [];
}

public sealed partial class NativeConstructAssemblyEvaluator
{
    /// <summary>
    /// Read component choice identities from the same native definitions and
    /// recipe/mode validators used by assembly evaluation. The complete
    /// assembly still determines costs, connectivity and operating state.
    /// </summary>
    public NativeConstructComponentConfiguration? DiscoverComponent(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId) ||
            !catalog.TryGetById(definitionId, out var component) ||
            component?.Category != "construct-component")
            return null;
        var raw = component.Definition;
        var issues = new List<NativeOriginContractIssue>();
        void Issue(string code, string path, NativeOriginIssueSeverity severity, string detail) =>
            issues.Add(new(code, path, severity, detail));

        var baseCapabilities = Texts(raw, "provides", definitionId, Issue);
        var materialSlots = Texts(raw, "materialSlots", definitionId, Issue);
        var allowedTraits = Texts(raw, "operationalOriginTraitIds", definitionId, Issue);
        var modes = new List<string>();
        if (raw.TryGetProperty("operatingModes", out var modeDefinitions))
        {
            if (modeDefinitions.ValueKind != JsonValueKind.Array ||
                modeDefinitions.GetArrayLength() is < 1 or > 32)
                Issue("construct-modes-invalid", definitionId + ".operatingModes",
                    NativeOriginIssueSeverity.Invalid, "The native operating modes are malformed.");
            else
                foreach (var mode in modeDefinitions.EnumerateArray())
                {
                    var id = mode.ValueKind == JsonValueKind.Object ? Text(mode, "id") : null;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        Issue("construct-mode-definition-invalid", definitionId + ".operatingModes",
                            NativeOriginIssueSeverity.Invalid, "A native operating mode needs an ID.");
                        continue;
                    }
                    if (SelectOperatingMode(raw, id, allowedTraits, definitionId, Issue) is not null &&
                        !modes.Contains(id, StringComparer.Ordinal))
                        modes.Add(id);
                }
        }

        var variants = new List<NativeConstructConfigurationVariant>();
        if (raw.TryGetProperty("constructionVariants", out var variantDefinitions))
        {
            if (variantDefinitions.ValueKind != JsonValueKind.Array ||
                variantDefinitions.GetArrayLength() is < 1 or > 100)
                Issue("construct-construction-variant-invalid", definitionId + ".constructionVariants",
                    NativeOriginIssueSeverity.Invalid, "The native construction variants are malformed.");
            else
                foreach (var variant in variantDefinitions.EnumerateArray())
                {
                    var id = variant.ValueKind == JsonValueKind.Object ? Text(variant, "id") : null;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        Issue("construct-construction-variant-invalid", definitionId + ".constructionVariants",
                            NativeOriginIssueSeverity.Invalid, "A native construction variant needs an ID.");
                        continue;
                    }
                    var resolved = NativeConstructRecipeVariantResolver.Resolve(catalog,
                        definitionId, id, definitionId + ".constructionVariants", Issue);
                    if (resolved is not null)
                        variants.Add(new(id, resolved.RecipeId, resolved.BodySize));
                }
        }

        var defaultRecipe = NativeConstructRecipeVariantResolver.Resolve(catalog,
            definitionId, null, definitionId + ".craftingRecipeId", Issue);
        bool Flag(string field)
        {
            if (!raw.TryGetProperty(field, out var value)) return false;
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                Issue("construct-flag-invalid", definitionId + "." + field,
                    NativeOriginIssueSeverity.Invalid, "An authored flag must be boolean.");
                return false;
            }
            return value.ValueKind == JsonValueKind.True;
        }

        NativeConstructConfigurationOption[] Options(string category)
        {
            var result = new List<NativeConstructConfigurationOption>();
            for (var offset = 0; offset < catalog.CountByCategory(category); offset += 100)
                foreach (var option in catalog.Search(category: category, offset: offset, limit: 100).Items)
                    if (OptionCompatible(Texts(option.Definition,
                            "compatibleComponentCapabilities", option.Id, Issue), baseCapabilities))
                        result.Add(new(option.Id, option.Name));
            return result.ToArray();
        }

        var needs = Requirements(raw, definitionId, Issue);
        var mountInterfaces = Texts(raw, "mountInterfaces", definitionId, Issue);
        var acceptsMountInterfaces = Texts(raw, "acceptsMountInterfaces", definitionId, Issue);
        var sourceRequired = Flag("powerSourceSlot");
        var continuous = Flag("requiresContinuousPower");
        var designs = Options("construct-design");
        var enhancements = Options("construct-enhancement");
        var sources = sourceRequired ? Options("construct-power-source") : [];
        static string? BoundedText(JsonElement definition, string key, int limit)
        {
            var value = Text(definition, key);
            return !string.IsNullOrWhiteSpace(value) && value.Length <= limit ? value : null;
        }
        var sourceDetails = new List<NativeConstructPowerSourcePresentation>();
        foreach (var option in sources)
        {
            if (!catalog.TryGetById(option.Id, out var source) || source is null) continue;
            var definition = source.Definition;
            sourceDetails.Add(new(option.Id, option.Name,
                BoundedText(definition, "description", 4000),
                BoundedText(definition, "rulesStatus", 2000),
                BoundedText(definition, "operatingPrinciple", 160),
                Number(definition, "powerOutput", option.Id, Issue),
                Number(definition, "overdrivePowerOutput", option.Id, Issue)));
        }
        return new(definitionId, component.Name, materialSlots, defaultRecipe?.RecipeId,
            variants, modes, baseCapabilities, needs, mountInterfaces, acceptsMountInterfaces,
            sourceRequired, continuous, designs, enhancements, sources,
            issues.Distinct().ToArray())
        {
            ComponentCategory = BoundedText(raw, "componentCategory", 160),
            PowerSourceDetails = sourceDetails.ToArray()
        };
    }
}
