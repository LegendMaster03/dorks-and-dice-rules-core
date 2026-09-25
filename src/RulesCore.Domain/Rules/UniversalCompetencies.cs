namespace RulesCore.Domain.Rules;

public sealed record UniversalCompetencyMechanics(
    string GoverningAbilityKey,
    bool SupportsRanks,
    bool SupportsClassSkillState,
    bool SupportsTrainingState,
    bool TrainedOnly,
    bool ArmorCheckPenaltyApplies,
    string EvaluationProfileKey,
    string EvaluationKind,
    bool CanEvaluate,
    string CompetencyKind = CharacterCompetencyKinds.Skill,
    string FacetType = "skill");

public sealed record UniversalCompetencyDefinition(
    string IdentityKey,
    string DisplayName,
    string? FamilyName = null,
    bool IsFamily = false,
    IReadOnlyList<string>? SourceAliases = null,
    UniversalCompetencyMechanics? Mechanics = null,
    string PresentationCategory = "skill")
{
    public string SemanticKey => $"competency.{IdentityKey}";
    public string? TrainingStateKey => IsFamily
        ? null
        : $"competency.{IdentityKey}.training";
}

public sealed record UniversalCompetencyAlias(
    string IdentityKey,
    string DisplayName);

/// <summary>
/// Reviewed, edition-independent Character competency identities.
///
/// Source-native skill/tool names and historical names are inputs to this policy; they are not
/// themselves the Character semantic identity. Numeric mechanics remain on source/rule profiles.
/// </summary>
public static class KnownUniversalCompetencies
{
    private static readonly IReadOnlyList<UniversalCompetencyDefinition> FamilyDefinitions =
    [
        Family(
            "craft",
            "Craft",
            RankedFamily("intelligence", trainedOnly: false),
            presentationCategory: "supporting"),
        Family("perform", "Perform", RankedFamily("charisma", trainedOnly: false)),
        Family("profession", "Profession", RankedFamily("wisdom", trainedOnly: true))
    ];

    private static readonly IReadOnlyList<UniversalCompetencyDefinition> FamilyMemberDefinitions =
    [
        CraftCompetency("Alchemy", "Craft (alchemy)", "Alchemist's Supplies"),
        CraftSupporting("Armorsmithing"),
        CraftSupporting("Basketweaving"),
        CraftSupporting("Bookbinding"),
        CraftSupporting("Bowmaking"),
        CraftSupporting("Blacksmithing"),
        CraftCompetency("Calligraphy", "Calligrapher\'s Supplies"),
        CraftCompetency("Carpentry", "Carpenter\'s Tools"),
        CraftCompetency("Cobbling", "Cobbler\'s Tools"),
        CraftCompetency("Gemcutting", "Jeweler\'s Tools"),
        CraftCompetency("Leatherworking", "Leatherworker\'s Tools"),
        CraftSupporting("Locksmithing"),
        CraftCompetency("Painting", "Painter\'s Supplies"),
        CraftCompetency("Pottery", "Potter\'s Tools"),
        CraftSupporting("Sculpting"),
        CraftSupporting("Shipmaking"),
        CraftCompetency("Stonemasonry", "Mason\'s Tools"),
        CraftSupporting("Trapmaking"),
        CraftSupporting("Weaponsmithing"),
        CraftCompetency("Weaving", "Weaver\'s Tools"),

        Perform("Act"),
        Perform("Comedy"),
        Perform("Dance"),
        Perform("Keyboard Instruments"),
        Perform("Oratory"),
        Perform("Percussion Instruments"),
        Perform("String Instruments"),
        Perform("Wind Instruments"),
        Perform("Sing"),

        Profession("Apothecary"),
        Profession("Boater"),
        Profession("Bookkeeper"),
        Profession("Brewer"),
        Profession("Cook"),
        Profession("Driver"),
        Profession("Farmer"),
        Profession("Fisher"),
        Profession("Guide"),
        Profession("Herbalist"),
        Profession("Herder"),
        Profession("Hunter"),
        Profession("Innkeeper"),
        Profession("Lumberjack"),
        Profession("Miller"),
        Profession("Miner"),
        Profession("Porter"),
        Profession("Rancher"),
        Profession("Sailor"),
        Profession("Scribe"),
        Profession("Siege Engineer"),
        Profession("Stablehand"),
        Profession("Tanner"),
        Profession("Teamster"),
        Profession("Woodcutter")
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ReviewedSourceAliases =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["deception"] = ["Bluff", "Deception"],
            ["persuasion"] = ["Diplomacy", "Persuasion"],
            ["animal-handling"] = ["Handle Animal", "Animal Handling"],
            ["medicine"] = ["Heal", "Medicine"],
            ["intimidation"] = ["Intimidate", "Intimidation"],
            ["insight"] = ["Sense Motive", "Insight"],
            ["sleight-of-hand"] = ["Pick Pocket", "Sleight of Hand"],
            ["survival"] = ["Wilderness Lore", "Survival"],
            ["arcana"] = ["Knowledge (Arcana)", "Arcana"],
            ["history"] = ["Knowledge (History)", "History"],
            ["nature"] = ["Knowledge (Nature)", "Nature"],
            ["religion"] = ["Knowledge (Religion)", "Religion"],
            ["psionics"] = ["Knowledge (Psionics)", "Psionics"],
            ["the-planes"] = ["Knowledge (the planes)", "The Planes"],
            ["alchemy"] = ["Alchemy", "Craft (alchemy)", "Alchemist's Supplies"],
            ["calligraphy"] = ["Craft (calligraphy)", "Calligrapher's Supplies"],
            ["carpentry"] = ["Craft (carpentry)", "Carpenter's Tools"],
            ["cobbling"] = ["Craft (cobbling)", "Cobbler's Tools"],
            ["gemcutting"] = ["Craft (gemcutting)", "Jeweler's Tools"],
            ["leatherworking"] = ["Craft (leatherworking)", "Leatherworker's Tools"],
            ["painting"] = ["Craft (painting)", "Painter's Supplies"],
            ["pottery"] = ["Craft (pottery)", "Potter's Tools"],
            ["stonemasonry"] = ["Craft (stonemasonry)", "Mason's Tools"],
            ["weaving"] = ["Craft (weaving)", "Weaver's Tools"],
            ["brewing"] = ["Brewer's Supplies"],
            ["cartography"] = ["Cartographer's Tools"],
            ["cooking"] = ["Cook's Utensils"],
            ["glassblowing"] = ["Glassblower's Tools"],
            ["herbalism"] = ["Herbalism Kit"],
            ["navigation"] = ["Navigator's Tools"],
            ["poisoning"] = ["Poisoner's Kit"],
            ["smithing"] = ["Smith's Tools"],
            ["tinkering"] = ["Tinker's Tools"],
            ["woodcarving"] = ["Woodcarver's Tools"],
            ["disguise-kit"] = ["Disguise Kit"],
            ["thieves-tools"] = ["Thieves' Tools"],
            ["forgery"] = ["Forgery", "Forgery Kit"]
        };

    private static readonly IReadOnlyDictionary<string, UniversalCompetencyAlias> LegacyConceptAliases =
        new Dictionary<string, UniversalCompetencyAlias>(StringComparer.OrdinalIgnoreCase)
        {
            ["skill.bluff"] = new("deception", "Deception"),
            ["skill.diplomacy"] = new("persuasion", "Persuasion"),
            ["skill.handle-animal"] = new("animal-handling", "Animal Handling"),
            ["skill.heal"] = new("medicine", "Medicine"),
            ["skill.intimidate"] = new("intimidation", "Intimidation"),
            ["skill.sense-motive"] = new("insight", "Insight"),
            ["skill.pick-pocket"] = new("sleight-of-hand", "Sleight of Hand"),
            ["skill.wilderness-lore"] = new("survival", "Survival"),
            ["skill.alchemy"] = new("alchemy", "Alchemy"),
            ["skill.craft-alchemy"] = new("alchemy", "Alchemy"),
            ["tool.alchemists-supplies"] = new("alchemy", "Alchemy"),
            ["tool.calligraphers-supplies"] = new("calligraphy", "Calligraphy"),
            ["tool.carpenters-tools"] = new("carpentry", "Carpentry"),
            ["tool.cobblers-tools"] = new("cobbling", "Cobbling"),
            ["tool.jewelers-tools"] = new("gemcutting", "Gemcutting"),
            ["tool.leatherworkers-tools"] = new("leatherworking", "Leatherworking"),
            ["tool.painters-supplies"] = new("painting", "Painting"),
            ["tool.potters-tools"] = new("pottery", "Pottery"),
            ["tool.masons-tools"] = new("stonemasonry", "Stonemasonry"),
            ["tool.weavers-tools"] = new("weaving", "Weaving"),
            ["tool.brewers-supplies"] = new("brewing", "Brewing"),
            ["tool.cartographers-tools"] = new("cartography", "Cartography"),
            ["tool.cooks-utensils"] = new("cooking", "Cooking"),
            ["tool.glassblowers-tools"] = new("glassblowing", "Glassblowing"),
            ["tool.herbalism-kit"] = new("herbalism", "Herbalism"),
            ["tool.navigators-tools"] = new("navigation", "Navigation"),
            ["tool.poisoners-kit"] = new("poisoning", "Poisoning"),
            ["tool.smiths-tools"] = new("smithing", "Smithing"),
            ["tool.tinkers-tools"] = new("tinkering", "Tinkering"),
            ["tool.woodcarvers-tools"] = new("woodcarving", "Woodcarving"),
            ["tool.disguise-kit"] = new("disguise-kit", "Disguise Kit"),
            ["tool.thieves-tools"] = new("thieves-tools", "Thieves' Tools"),
            ["skill.forgery"] = new("forgery", "Forgery"),
            ["tool.forgery-kit"] = new("forgery", "Forgery")
        };

    public static IReadOnlyList<UniversalCompetencyDefinition> Families => FamilyDefinitions;
    public static IReadOnlyList<UniversalCompetencyDefinition> FamilyMembers => FamilyMemberDefinitions;
    public static IReadOnlyList<UniversalCompetencyDefinition> Catalog { get; } =
        FamilyDefinitions.Concat(FamilyMemberDefinitions).ToArray();

    public static UniversalCompetencyDefinition? FindByIdentityKey(string? identityKey)
    {
        if (string.IsNullOrWhiteSpace(identityKey))
        {
            return null;
        }

        var normalized = NormalizeIdentityKey(identityKey);
        return Catalog.FirstOrDefault(value =>
            string.Equals(value.IdentityKey, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static UniversalCompetencyDefinition? ResolveFamilyMember(
        string? familyName,
        string? specialty)
    {
        if (string.IsNullOrWhiteSpace(familyName) || string.IsNullOrWhiteSpace(specialty))
        {
            return null;
        }

        var normalizedFamily = familyName.Trim();
        var normalizedSpecialty = specialty.Trim();
        return FamilyMemberDefinitions.FirstOrDefault(value =>
            string.Equals(value.FamilyName, normalizedFamily, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(value.DisplayName, normalizedSpecialty, StringComparison.OrdinalIgnoreCase)
                || (value.SourceAliases ?? []).Any(alias =>
                    string.Equals(alias, normalizedSpecialty, StringComparison.OrdinalIgnoreCase))));
    }

    public static UniversalCompetencyAlias? ResolveLegacyConceptKey(string? conceptKey)
    {
        if (string.IsNullOrWhiteSpace(conceptKey))
        {
            return null;
        }

        var normalized = conceptKey.Trim();
        if (LegacyConceptAliases.TryGetValue(normalized, out var alias))
        {
            return alias;
        }

        const string knowledgePrefix = "skill.knowledge-";
        if (normalized.StartsWith(knowledgePrefix, StringComparison.OrdinalIgnoreCase)
            && normalized.Length > knowledgePrefix.Length)
        {
            var identityKey = NormalizeIdentityKey(normalized[knowledgePrefix.Length..]);
            return new UniversalCompetencyAlias(identityKey, Humanize(identityKey));
        }

        foreach (var family in FamilyDefinitions)
        {
            var prefix = $"skill.{family.IdentityKey}-";
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || normalized.Length <= prefix.Length)
            {
                continue;
            }

            var identityKey = NormalizeIdentityKey(normalized[prefix.Length..]);
            var member = FindByIdentityKey(identityKey);
            if (member is not null
                && string.Equals(
                    member.FamilyName,
                    family.DisplayName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new UniversalCompetencyAlias(member.IdentityKey, member.DisplayName);
            }
        }

        return null;
    }

    public static IReadOnlyList<string> SourceAliases(string identityKey)
    {
        var normalized = NormalizeIdentityKey(identityKey);
        var definitionAliases = FindByIdentityKey(normalized)?.SourceAliases ?? [];
        var reviewedAliases = ReviewedSourceAliases.TryGetValue(normalized, out var values)
            ? values
            : [];
        return definitionAliases
            .Concat(reviewedAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> CompatibilityConceptKeys(string identityKey)
    {
        var normalized = NormalizeIdentityKey(identityKey);
        var values = LegacyConceptAliases
            .Where(value => string.Equals(
                value.Value.IdentityKey,
                normalized,
                StringComparison.OrdinalIgnoreCase))
            .Select(value => value.Key)
            .ToList();

        var member = FindByIdentityKey(normalized);
        if (member?.FamilyName is not null)
        {
            var family = FamilyDefinitions.First(value =>
                string.Equals(
                    value.DisplayName,
                    member.FamilyName,
                    StringComparison.OrdinalIgnoreCase));
            values.Add($"skill.{family.IdentityKey}-{member.IdentityKey}");
        }

        return values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public static UniversalCompetencyMechanics? ResolveMechanics(string? identityKey)
    {
        var definition = FindByIdentityKey(identityKey);
        if (definition is null)
        {
            return null;
        }

        if (definition.Mechanics is not null)
        {
            return definition.Mechanics with
            {
                GoverningAbilityKey =
                    NormalizeAbilityKey(definition.Mechanics.GoverningAbilityKey)
                    ?? definition.Mechanics.GoverningAbilityKey
            };
        }

        if (string.IsNullOrWhiteSpace(definition.FamilyName))
        {
            return null;
        }

        var family = FamilyDefinitions.FirstOrDefault(value =>
            string.Equals(
                value.DisplayName,
                definition.FamilyName,
                StringComparison.OrdinalIgnoreCase));
        if (family?.Mechanics is null)
        {
            return null;
        }

        return family.Mechanics with
        {
            GoverningAbilityKey =
                NormalizeAbilityKey(family.Mechanics.GoverningAbilityKey)
                ?? family.Mechanics.GoverningAbilityKey
        };
    }

    public static string? NormalizeAbilityKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "str" or "strength" => "strength",
            "dex" or "dexterity" => "dexterity",
            "con" or "constitution" => "constitution",
            "int" or "intelligence" => "intelligence",
            "wis" or "wisdom" => "wisdom",
            "cha" or "charisma" => "charisma",
            var other => NormalizeIdentityKey(other)
        };
    }

    public static bool IsOrdinaryCharacterCompetencyIdentity(string? identityKey)
    {
        if (string.IsNullOrWhiteSpace(identityKey))
        {
            return false;
        }

        return !string.Equals(
            NormalizeIdentityKey(identityKey),
            "speak-language",
            StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeIdentityKey(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("competency.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["competency.".Length..];
        }

        var pieces = new List<string>();
        var current = new List<char>();
        foreach (var character in normalized.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Add(character);
                continue;
            }

            if (current.Count > 0)
            {
                pieces.Add(new string(current.ToArray()));
                current.Clear();
            }
        }
        if (current.Count > 0)
        {
            pieces.Add(new string(current.ToArray()));
        }

        return string.Join('-', pieces);
    }

    public static string Humanize(string identityKey) =>
        string.Join(
            ' ',
            NormalizeIdentityKey(identityKey)
                .Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => char.ToUpperInvariant(value[0]) + value[1..]));

    private static UniversalCompetencyDefinition Family(
        string key,
        string name,
        UniversalCompetencyMechanics mechanics,
        string presentationCategory = "skill") =>
        new(
            key,
            name,
            FamilyName: name,
            IsFamily: true,
            SourceAliases: [name],
            Mechanics: mechanics,
            PresentationCategory: presentationCategory);

    private static UniversalCompetencyMechanics RankedFamily(
        string governingAbilityKey,
        bool trainedOnly) =>
        new(
            NormalizeAbilityKey(governingAbilityKey) ?? governingAbilityKey,
            SupportsRanks: true,
            SupportsClassSkillState: true,
            SupportsTrainingState: true,
            TrainedOnly: trainedOnly,
            ArmorCheckPenaltyApplies: false,
            EvaluationProfileKey: "ranked-skill",
            EvaluationKind: CharacterMechanicEvaluationKinds.Sum,
            CanEvaluate: true);

    private static UniversalCompetencyDefinition CraftCompetency(
        string name,
        params string[] additionalAliases) =>
        Member("Craft", name, additionalAliases, presentationCategory: "competency");

    private static UniversalCompetencyDefinition CraftSupporting(
        string name,
        params string[] additionalAliases) =>
        Member("Craft", name, additionalAliases, presentationCategory: "supporting");

    private static UniversalCompetencyDefinition Perform(
        string name,
        params string[] additionalAliases) =>
        Member("Perform", name, additionalAliases);

    private static UniversalCompetencyDefinition Profession(
        string name,
        params string[] additionalAliases) =>
        Member("Profession", name, additionalAliases);

    private static UniversalCompetencyDefinition Member(
        string family,
        string name,
        IReadOnlyList<string> additionalAliases,
        string presentationCategory = "skill")
    {
        var aliases = new[]
            {
                name,
                $"{family} ({name.ToLowerInvariant()})"
            }
            .Concat(additionalAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new UniversalCompetencyDefinition(
            NormalizeIdentityKey(name),
            name,
            family,
            IsFamily: false,
            aliases,
            PresentationCategory: presentationCategory);
    }
}
