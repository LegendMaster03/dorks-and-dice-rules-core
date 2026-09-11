using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RulesCore.Application.Sources;

public static class LegacySrdDocumentInspector
{
    private static readonly Regex MarkdownHeading = new(
        @"(?m)^(?<marks>#{1,6})[ \t]+(?<name>[^\r\n]+?)[ \t]*#*[ \t]*(?:\r?\n|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlHeading = new(
        @"<h(?<level>[1-6])\b[^>]*>(?<name>.*?)</h\k<level>\s*>",
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

    private static readonly HashSet<string> GenericHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "overview", "general", "description", "descriptions", "combat", "class skills", "class features",
        "prerequisites", "prerequisite", "requirements", "types of feats", "feat descriptions",
        "using skills", "skill checks", "skills summary", "special", "special abilities", "special qualities",
        "creating magic items", "magic items", "spells", "monsters", "races", "classes", "feats", "skills"
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

        var prefix = indexUri.AbsolutePath.EndsWith('/', StringComparison.Ordinal)
            ? indexUri.AbsolutePath
            : indexUri.AbsolutePath[..(indexUri.AbsolutePath.LastIndexOf('/') + 1)];

        return HtmlHref.Matches(html ?? string.Empty)
            .Select(match => match.Groups["double"].Success
                ? match.Groups["double"].Value
                : match.Groups["single"].Success
                    ? match.Groups["single"].Value
                    : match.Groups["bare"].Value)
            .Select(WebUtility.HtmlDecode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => !value.StartsWith('#')
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
    }

    private static List<LegacyEntity> ParseMarkdown(string markdown, Uri uri, string sourceCode)
    {
        var matches = MarkdownHeading.Matches(markdown).Cast<Match>().ToArray();
        var entities = new List<LegacyEntity>();
        for (var index = 0; index < matches.Length; index++)
        {
            var match = matches[index];
            var level = match.Groups["marks"].Value.Length;
            var originalHeading = match.Groups["name"].Value.Trim();
            var name = CleanHeadingName(originalHeading);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var end = markdown.Length;
            for (var next = index + 1; next < matches.Length; next++)
            {
                if (matches[next].Groups["marks"].Value.Length <= level)
                {
                    end = matches[next].Index;
                    break;
                }
            }

            var body = NormalizeText(markdown[match.Index + match.Length..end]);
            entities.Add(CreateEntity(
                InferEntityType(uri, name, level),
                name,
                originalHeading,
                sourceCode,
                uri,
                level,
                index,
                body));
        }

        return entities;
    }

    private static List<LegacyEntity> ParseHtml(string html, Uri uri, string sourceCode)
    {
        var matches = HtmlHeading.Matches(html).Cast<Match>().ToArray();
        var entities = new List<LegacyEntity>();
        for (var index = 0; index < matches.Length; index++)
        {
            var match = matches[index];
            var level = int.Parse(match.Groups["level"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var originalHeading = HtmlToText(match.Groups["name"].Value);
            var name = CleanHeadingName(originalHeading);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var end = html.Length;
            for (var next = index + 1; next < matches.Length; next++)
            {
                var nextLevel = int.Parse(
                    matches[next].Groups["level"].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                if (nextLevel <= level)
                {
                    end = matches[next].Index;
                    break;
                }
            }

            entities.Add(CreateEntity(
                InferEntityType(uri, name, level),
                name,
                originalHeading,
                sourceCode,
                uri,
                level,
                index,
                HtmlToText(html[match.Index + match.Length..end])));
        }

        if (IsMonsterDocument(uri))
        {
            AddMonsterStatBlocks(entities, html, uri, sourceCode);
        }

        return entities;
    }

    private static void AddMonsterStatBlocks(
        ICollection<LegacyEntity> entities,
        string html,
        Uri uri,
        string sourceCode)
    {
        var text = HtmlToText(html);
        var matches = MonsterStatBlock.Matches(text).Cast<Match>().ToArray();
        var existing = entities
            .Where(value => value.EntityType == "monster")
            .Select(value => value.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < matches.Length; index++)
        {
            var name = NormalizeText(matches[index].Groups["name"].Value);
            if (string.IsNullOrWhiteSpace(name) || !existing.Add(name))
            {
                continue;
            }
            var end = index + 1 < matches.Length ? matches[index + 1].Index : text.Length;
            var body = NormalizeText(text[matches[index].Index..end]);
            entities.Add(CreateEntity(
                "monster",
                name,
                name,
                sourceCode,
                uri,
                0,
                10000 + index,
                body));
        }
    }

    private static LegacyEntity CreateEntity(
        string entityType,
        string name,
        string originalHeading,
        string sourceCode,
        Uri uri,
        int headingLevel,
        int ordinal,
        string body)
    {
        var identityMaterial = $"{uri.AbsoluteUri}\n{headingLevel}\n{ordinal}\n{name}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityMaterial)))
            .ToLowerInvariant();
        return new LegacyEntity(
            entityType,
            name,
            sourceCode,
            $"legacy-{hash[..24]}",
            uri.AbsoluteUri,
            headingLevel,
            originalHeading,
            body);
    }

    private static string InferEntityType(Uri uri, string name, int level)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        var fileName = Path.GetFileName(path);
        var generic = IsGenericHeading(name);

        if (path.Contains("spell", StringComparison.Ordinal)
            && level >= 2
            && !generic
            && !name.StartsWith("Spells ", StringComparison.OrdinalIgnoreCase))
        {
            return "spell";
        }
        if (path.Contains("feat", StringComparison.Ordinal) && level >= 2 && !generic)
        {
            return "feat";
        }
        if (path.Contains("skill", StringComparison.Ordinal) && level >= 3 && !generic)
        {
            return "skill";
        }
        if (path.Contains("prestige", StringComparison.Ordinal) && level == 2 && !generic)
        {
            return "prestigeClass";
        }
        if ((path.Contains("npc-class", StringComparison.Ordinal)
                || path.Contains("npc_classes", StringComparison.Ordinal))
            && level == 2
            && !generic)
        {
            return "npcClass";
        }
        if (path.Contains("class", StringComparison.Ordinal) && level == 2 && !generic)
        {
            return "class";
        }
        if (path.Contains("race", StringComparison.Ordinal) && level == 2 && !generic)
        {
            return "race";
        }
        if (IsMonsterDocument(uri) && level == 2 && !generic)
        {
            return "monster";
        }
        if ((path.Contains("magic-items", StringComparison.Ordinal)
                || path.Contains("magic_items", StringComparison.Ordinal))
            && level >= 2
            && !generic)
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

    private static bool IsMonsterDocument(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("/monsters", StringComparison.Ordinal)
            || path.Contains("monster_", StringComparison.Ordinal)
            || path.Contains("monsters_", StringComparison.Ordinal);
    }

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
            || normalized.StartsWith("Skills ", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanHeadingName(string value)
    {
        var decoded = WebUtility.HtmlDecode(HtmlTag.Replace(value, string.Empty));
        decoded = Regex.Replace(decoded, @"\s*[\[(](?:General|Metamagic|Item Creation|Fighter|Epic|Psionic)[^\])]*[\])]\s*$", string.Empty, RegexOptions.IgnoreCase);
        decoded = decoded.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Trim(' ', '\t', '#', '*', '_');
        return NormalizeText(decoded);
    }

    private static string HtmlToText(string html)
    {
        var withoutScript = ScriptOrStyle.Replace(html ?? string.Empty, string.Empty);
        var withLines = HtmlBlockBoundary.Replace(withoutScript, "\n");
        var withoutTags = HtmlTag.Replace(withLines, string.Empty);
        return NormalizeText(WebUtility.HtmlDecode(withoutTags));
    }

    private static string NormalizeText(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        normalized = Whitespace.Replace(normalized, " ");
        normalized = string.Join(
            "\n",
            normalized.Split('\n').Select(line => line.TrimEnd()));
        normalized = BlankLines.Replace(normalized, "\n\n");
        return normalized.Trim();
    }

    private static string FileNameToDisplayName(Uri uri)
    {
        var fileName = Path.GetFileNameWithoutExtension(uri.AbsolutePath)
            .Replace('_', ' ')
            .Replace('-', ' ');
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(fileName);
    }

    private static string Serialize(IReadOnlyCollection<LegacyEntity> entities)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var group in entities
                         .GroupBy(value => value.EntityType, StringComparer.Ordinal)
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(group.Key);
                writer.WriteStartArray();
                foreach (var entity in group)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", entity.Name);
                    writer.WriteString("source", entity.Source);
                    writer.WriteString("uniqueId", entity.UniqueId);
                    writer.WriteString("documentUri", entity.DocumentUri);
                    writer.WriteNumber("headingLevel", entity.HeadingLevel);
                    writer.WriteString("originalHeading", entity.OriginalHeading);
                    writer.WriteString("body", entity.Body);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed record LegacyEntity(
        string EntityType,
        string Name,
        string Source,
        string UniqueId,
        string DocumentUri,
        int HeadingLevel,
        string OriginalHeading,
        string Body);
}
