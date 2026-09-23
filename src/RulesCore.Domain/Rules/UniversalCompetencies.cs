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
    UniversalCompetencyMechanics? Mechanics = null)
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
        Family("craft", "Craft", RankedFamily("intelligence", trainedOnly: false)),
        Family("perform", "Perform", RankedFamily("charisma", trainedOnly: false)),
        Family("profession", "Profession", RankedFamily("wisdom", trainedOnly: true))
    ];

    private static readonly IReadOnlyList<UniversalCompetencyDefinition> FamilyMemberDefinitions =
    [
        Craft("Alchemy", "Craft (alchemy)", "Alchemist's Supplies"),
        Craft("Armorsmithing"),
        Craft("Basketweaving"),
        Craft("Bookbinding"),
        Craft("Bowmaking"),
        Craft("Blacksmithing"),
        Craft("Calligraphy"),
        Craft("Carpentry"),
        Craft("Cobbling"),
        Craft("Gemcutting"),
        Craft("Leatherworking"),
        Craft("Locksmithing"),
        Craft("Painting"),
        Craft("Pottery"),
        Craft("Sculpting"),
        Craft("Shipmaking"),
        Craft("Stonemasonry"),
        Craft("Trapmaking"),
        Craft("Weaponsmithing"),
        Craft("Weaving"),

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
        UniversalCompetencyMechanics mechanics) =>
        new(
            key,
            name,
            FamilyName: name,
            IsFamily: true,
            SourceAliases: [name],
            Mechanics: mechanics);

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

    private static UniversalCompetencyDefinition Craft(
        string name,
        params string[] additionalAliases) =>
        Member("Craft", name, additionalAliases);

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
        IReadOnlyList<string> additionalAliases)
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
            aliases);
    }
}
