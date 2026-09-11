using System.Text.RegularExpressions;

namespace RulesCore.Application.Sources;

public static partial class LegacySrdDocumentInspector
{
    private static string InferEntityType(Uri uri, string name, int level, string body)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        var fileName = Path.GetFileName(path);
        var generic = IsGenericHeading(name);

        if (IsPsionicPowerDocument(uri) && level == 2 && !generic && LooksLikePowerBody(body))
        {
            return "power";
        }
        if (IsDivineAbilityDocument(uri) && level == 2 && !generic && LooksLikeDivineAbilityBody(body))
        {
            return "divineAbility";
        }
        if (IsSpellDescriptionDocument(uri)
            && level == 2
            && !generic
            && !name.StartsWith("Spells ", StringComparison.OrdinalIgnoreCase)
            && LooksLikeSpellBody(body))
        {
            return "spell";
        }
        if (IsFeatDocument(uri) && level >= 2 && !generic && LooksLikeFeatBody(body))
        {
            return "feat";
        }
        if (IsSkillDocument(uri) && level >= 2 && !generic && LooksLikeSkillHeading(name))
        {
            return "skill";
        }
        if (IsPrestigeClassDocument(uri) && level == 2 && !generic && LooksLikeClassBody(body))
        {
            return "prestigeClass";
        }
        if (IsNpcClassDocument(uri) && level == 2 && NpcClassNames.Contains(name))
        {
            return "npcClass";
        }
        if (IsClassDocument(uri) && level == 2 && IsKnownClassHeading(name))
        {
            return "class";
        }
        if (IsRaceDocument(uri) && level == 2 && CanonicalRaceNames.ContainsKey(name))
        {
            return "race";
        }
        if (IsDomainDocument(uri)
            && level == 2
            && name.EndsWith(" Domain", StringComparison.OrdinalIgnoreCase)
            && body.Contains("Granted Power", StringComparison.OrdinalIgnoreCase))
        {
            return "domain";
        }
        if (IsMonsterDocument(uri) && level >= 2 && !generic && LooksLikeMonsterBody(body))
        {
            return "monster";
        }
        if (IsItemDocument(uri) && level >= 2 && !generic && LooksLikePricedItemBlock(body))
        {
            return "item";
        }

        if (level == 1)
        {
            if (ThreeEClasses.Contains(fileName)) return "class";
            if (ThreeEPrestigeClasses.Contains(fileName)) return "prestigeClass";
            if (ThreeENpcClasses.Contains(fileName)) return "npcClass";
            if (ThreeERaces.Contains(fileName)) return "race";
        }

        return "rule";
    }

    private static string NormalizeEntityName(string entityType, string name)
    {
        if (string.Equals(entityType, "skill", StringComparison.Ordinal))
        {
            return NormalizeText(SkillAbilitySuffix.Replace(name, string.Empty));
        }
        if (string.Equals(entityType, "race", StringComparison.Ordinal)
            && CanonicalRaceNames.TryGetValue(name, out var canonicalRaceName))
        {
            return canonicalRaceName;
        }
        return name;
    }

    private static bool IsKnownClassHeading(string name)
    {
        if (CoreClassNames.Contains(name) || PsionicClassNames.Contains(name))
        {
            return true;
        }
        return name.StartsWith("Epic ", StringComparison.OrdinalIgnoreCase)
            && CoreClassNames.Contains(name[5..].Trim());
    }

    private static bool IsClassDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("character-classes", StringComparison.Ordinal)
            || path.Contains("psionic-classes", StringComparison.Ordinal)
            || path.Contains("epic-classes", StringComparison.Ordinal);
    }

    private static bool IsNpcClassDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("npc-class", StringComparison.Ordinal)
            || path.Contains("npc_classes", StringComparison.Ordinal);
    }

    private static bool IsPrestigeClassDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("prestige", StringComparison.Ordinal)
            && path.Contains("class", StringComparison.Ordinal);
    }

    private static bool IsRaceDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("race", StringComparison.Ordinal);
    }

    private static bool IsSkillDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("skill", StringComparison.Ordinal);
    }

    private static bool IsFeatDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("feat", StringComparison.Ordinal)
            && !path.Contains("divine-abilities-and-feats", StringComparison.Ordinal);
    }

    private static bool IsDivineAbilityDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("divine-abilities-and-feats", StringComparison.Ordinal);
    }

    private static bool IsPsionicPowerDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("psionic-powers-", StringComparison.Ordinal)
            && !path.Contains("list", StringComparison.Ordinal)
            && !path.Contains("overview", StringComparison.Ordinal);
    }

    private static bool IsSpellDescriptionDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        var fileName = Path.GetFileName(path);
        if (path.Contains("/spells/spells-", StringComparison.Ordinal)
            || path.Contains("epic-spells", StringComparison.Ordinal)
            || path.Contains("psionic-spells", StringComparison.Ordinal))
        {
            return true;
        }
        return fileName.StartsWith("spells", StringComparison.Ordinal)
            && (fileName.EndsWith(".htm", StringComparison.Ordinal)
                || fileName.EndsWith(".html", StringComparison.Ordinal));
    }

    private static bool IsDomainDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("domains-and-spells", StringComparison.Ordinal)
            || path.Contains("clericdomains", StringComparison.Ordinal);
    }

    private static bool IsMonsterDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("/monsters", StringComparison.Ordinal)
            || path.Contains("monster_", StringComparison.Ordinal)
            || path.Contains("monsters_", StringComparison.Ordinal)
            || path.Contains("-monsters", StringComparison.Ordinal);
    }

    private static bool IsItemDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        var fileName = Path.GetFileName(path);
        return path.Contains("/magic-items/", StringComparison.Ordinal)
            || path.Contains("magic-items", StringComparison.Ordinal)
            || path.Contains("magic_items", StringComparison.Ordinal)
            || path.Contains("psionic-items", StringComparison.Ordinal)
            || path.Contains("epic-magic-items", StringComparison.Ordinal)
            || fileName is "magic_armor.htm" or "magic_artifacts.htm" or "cursed_items.htm"
                or "intelligent_items.htm" or "potions.htm" or "rings.htm" or "rods.htm"
                or "scrolls.htm" or "staves.htm" or "wands.htm" or "magic_weapons.htm"
                or "wondrous_items.htm";
    }

    private static bool LooksLikeSkillHeading(string name) =>
        SkillAbilitySuffix.IsMatch(name);

    private static bool LooksLikeClassBody(string body) =>
        body.Contains("Hit Die:", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeFeatBody(string body) =>
        body.Contains("Benefit:", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSpellBody(string body) =>
        body.Contains("Level:", StringComparison.OrdinalIgnoreCase)
        && (body.Contains("Components:", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Casting Time:", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Range:", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikePowerBody(string body) =>
        body.Contains("Level:", StringComparison.OrdinalIgnoreCase)
        && body.Contains("Power Points:", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeMonsterBody(string body) =>
        body.Contains("Hit Dice:", StringComparison.OrdinalIgnoreCase)
        && (body.Contains("Challenge Rating:", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Armor Class:", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeDivineAbilityBody(string body) =>
        body.Contains("**Benefit:**", StringComparison.OrdinalIgnoreCase)
        || body.Contains("**Prerequisite", StringComparison.OrdinalIgnoreCase)
        || body.Contains("**Suggested Portfolio Elements:**", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSpecificItemLabel(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= 140
        && !GenericItemLabels.Contains(name)
        && !IsGenericHeading(name);

    private static bool LooksLikePricedItemBlock(string body) =>
        Regex.IsMatch(
            body,
            @"\bMarket Price\s*:?\s*(?!Modifier\b)(?!\+)\d",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        || Regex.IsMatch(
            body,
            @"\bPrice\s*:?\s*(?!\+)\d",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        || Regex.IsMatch(
            body,
            @"\bCost to Create\s*:?\s*\d",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool IsGenericHeading(string name)
    {
        var normalized = name.Trim();
        if (GenericHeadings.Contains(normalized))
        {
            return true;
        }
        return normalized.StartsWith("Table:", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Spells (", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Monsters (", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Classes ", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Skills ", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(" Domain Spells", StringComparison.OrdinalIgnoreCase);
    }

}
