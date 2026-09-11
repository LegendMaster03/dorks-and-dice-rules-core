using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RulesCore.Application.Sources;

public static partial class LegacySrdDocumentInspector
{
    private static readonly Regex MarkdownHeading = new(
        @"(?m)^(?<marks>#{1,6})[ \t]+(?<name>[^\r\n]+?)[ \t]*#*[ \t]*(?:\r?\n|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MarkdownStrongLabel = new(
        @"(?m)^\*\*(?<name>[^*\r\n:]{2,140}):\*\*[ \t]*(?<inline>[^\r\n]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlHeading = new(
        @"<h(?<level>[1-6])\b[^>]*>(?<name>.*?)</h\k<level>\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlStrongLabel = new(
        @"<(?:b|strong)\b[^>]*>\s*(?<name>[^<>:]{2,140}):\s*</(?:b|strong)\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlHref = new(
        """\bhref\s*=\s*(?:"(?<double>[^"]+)"|'(?<single>[^']+)'|(?<bare>[^\s>]+))""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ScriptOrStyle = new(
        @"<(script|style)\b[^>]*>.*?</\1\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlTag = new(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex HtmlBlockBoundary = new(
        @"(?i)<\s*(?:br\s*/?|/p|/div|/li|/tr|/table|/h[1-6])\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Whitespace = new(
        @"[ \t\f\v]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BlankLines = new(
        @"(?:\r?\n){3,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MonsterStatBlock = new(
        @"(?m)^(?<name>[A-Z][A-Za-z0-9'’() ,+\-/:]{1,120})\r?\nSizeAndType:\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SkillAbilitySuffix = new(
        @"\s*\((?:Str|Dex|Con|Int|Wis|Cha|None)(?:\s*;[^)]*)?\)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> ThreeEClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "barbarian.htm", "bard.htm", "cleric.htm", "druid.htm", "fighter.htm", "monk.htm",
        "paladin.htm", "ranger.htm", "rogue.htm", "sorcerer.htm", "wizard.htm"
    };

    private static readonly HashSet<string> ThreeEPrestigeClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "arcane_archer.htm", "assassin.htm", "blackguard.htm", "dwarven_defender.htm",
        "loremaster.htm", "shadowdancer.htm"
    };

    private static readonly HashSet<string> ThreeENpcClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "adept.htm", "aristocrat.htm", "commoner.htm", "expert.htm", "warrior.htm"
    };

    private static readonly HashSet<string> ThreeERaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwarf.htm", "elf.htm", "gnome.htm", "halfelf.htm", "halforc.htm", "halfling.htm", "human.htm"
    };

    private static readonly HashSet<string> CoreClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Barbarian", "Bard", "Cleric", "Druid", "Fighter", "Monk", "Paladin", "Ranger",
        "Rogue", "Sorcerer", "Wizard"
    };

    private static readonly HashSet<string> PsionicClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Psion", "Psychic Warrior", "Soulknife", "Wilder"
    };

    private static readonly HashSet<string> NpcClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Adept", "Aristocrat", "Commoner", "Expert", "Warrior"
    };

    private static readonly IReadOnlyDictionary<string, string> CanonicalRaceNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Human"] = "Human",
            ["Humans"] = "Human",
            ["Dwarf"] = "Dwarf",
            ["Dwarves"] = "Dwarf",
            ["Elf"] = "Elf",
            ["Elves"] = "Elf",
            ["Gnome"] = "Gnome",
            ["Gnomes"] = "Gnome",
            ["Half-Elf"] = "Half-Elf",
            ["Half-Elves"] = "Half-Elf",
            ["Half-Orc"] = "Half-Orc",
            ["Half-Orcs"] = "Half-Orc",
            ["Halfling"] = "Halfling",
            ["Halflings"] = "Halfling",
            ["Dromite"] = "Dromite",
            ["Dromites"] = "Dromite",
            ["Elan"] = "Elan",
            ["Elans"] = "Elan",
            ["Half-Giant"] = "Half-Giant",
            ["Half-Giants"] = "Half-Giant",
            ["Maenad"] = "Maenad",
            ["Maenads"] = "Maenad",
            ["Xeph"] = "Xeph",
            ["Xephs"] = "Xeph",
            ["Duergar"] = "Duergar"
        };

    private static readonly HashSet<string> GenericHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "overview", "general", "description", "descriptions", "combat", "class skills", "class features",
        "prerequisites", "prerequisite", "requirements", "types of feats", "feat descriptions", "feat name",
        "using skills", "skill checks", "skills summary", "skill descriptions", "skill synergy", "special",
        "special abilities", "special qualities", "creating magic items", "magic items", "magic item descriptions",
        "psionic item descriptions", "using items", "intelligent items", "cursed items", "random psionic items",
        "saving throws against psionic item powers", "damaging psionic items", "repairing items",
        "charges and multiple uses", "magic items for psionic characters", "spells", "spell descriptions",
        "monsters", "races", "classes", "feats", "skills", "favored class", "race and languages",
        "small characters", "the power point reserve", "abilities and manifesters", "random starting gold",
        "psionic item creation feats", "metapsionic feats", "power descriptions", "powers", "domains and spells",
        "salient divine ability descriptions", "ability name", "definitions of terms", "acquiring epic feats",
        "types of epic feats", "divine feats", "wild feats", "epic psionic feats"
    };

    private static readonly HashSet<string> GenericItemLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Physical Description", "Activation", "Special Qualities", "Aura", "Caster Level", "Manifester Level",
        "Prerequisites", "Market Price", "Price", "Cost to Create", "Cost", "Weight", "Construction",
        "Strong", "Moderate", "Faint", "Random Generation", "Saving Throw", "Power Resistance"
    };

    public static string ConvertToCanonicalJson(
        string content,
        string documentUri,
        string sourceCode,
        out int entityCount)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidDataException("Legacy SRD document was empty.");
        }
        if (!Uri.TryCreate(documentUri, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Legacy SRD document URI must be absolute.", nameof(documentUri));
        }
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Legacy SRD source code can not be blank.", nameof(sourceCode));
        }

        var normalizedSource = sourceCode.Trim();
        var entities = uri.AbsolutePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? ParseMarkdown(content, uri, normalizedSource)
            : ParseHtml(content, uri, normalizedSource);

        if (entities.Count == 0)
        {
            var body = uri.AbsolutePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? NormalizeText(content)
                : HtmlToText(content);
            var fallbackName = FileNameToDisplayName(uri);
            entities.Add(CreateEntity(
                "rule",
                fallbackName,
                fallbackName,
                normalizedSource,
                uri,
                0,
                0,
                body));
        }

        entityCount = entities.Count;
        return Serialize(entities);
    }

    public static IReadOnlyList<Uri> ResolveHtmlIndexReferences(string html, Uri indexUri)
    {
        ArgumentNullException.ThrowIfNull(indexUri);
        if (!indexUri.IsAbsoluteUri)
        {
            throw new ArgumentException("HTML index URI must be absolute.", nameof(indexUri));
        }

        var prefix = indexUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            ? indexUri.AbsolutePath
            : indexUri.AbsolutePath[..(indexUri.AbsolutePath.LastIndexOf('/') + 1)];

        var references = HtmlHref.Matches(html ?? string.Empty)
            .Select(match => match.Groups["double"].Success
                ? match.Groups["double"].Value
                : match.Groups["single"].Success
                    ? match.Groups["single"].Value
                    : match.Groups["bare"].Value)
            .Select(WebUtility.HtmlDecode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Where(value => !value.StartsWith("#", StringComparison.Ordinal)
                && !value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                && !value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            .Select(value => Uri.TryCreate(indexUri, value, out var resolved) ? resolved : null)
            .Where(value => value is not null
                && string.Equals(value.Scheme, indexUri.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(value.Host, indexUri.Host, StringComparison.OrdinalIgnoreCase)
                && value.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)
                && (value.AbsolutePath.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
                    || value.AbsolutePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase)))
            .Select(value => value!)
            .DistinctBy(value => value.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return EnforceReviewedThreeECorpus(indexUri, references);
    }

}
