using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RulesCore.Application.Sources;

public static partial class LegacySrdDocumentInspector
{
    private static List<LegacyEntity> ParseMarkdown(string markdown, Uri uri, string sourceCode)
    {
        var matches = MarkdownHeading.Matches(markdown).Cast<Match>().ToArray();
        var entities = new List<LegacyEntity>();
        for (var index = 0; index < matches.Length; index++)
        {
            var match = matches[index];
            var level = match.Groups["marks"].Value.Length;
            var originalHeading = match.Groups["name"].Value.Trim();
            var cleanedName = CleanHeadingName(originalHeading);
            if (string.IsNullOrWhiteSpace(cleanedName))
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

            var body = NormalizeText(markdown[(match.Index + match.Length)..end]);
            var classificationEnd = index + 1 < matches.Length ? matches[index + 1].Index : markdown.Length;
            var classificationBody = NormalizeText(markdown[(match.Index + match.Length)..classificationEnd]);
            var entityType = InferEntityType(uri, cleanedName, level, classificationBody);
            var name = NormalizeEntityName(entityType, cleanedName);
            entities.Add(CreateEntity(
                entityType,
                name,
                originalHeading,
                sourceCode,
                uri,
                level,
                index,
                body));
        }

        if (IsItemDocument(uri))
        {
            AddMarkdownItemBlocks(entities, markdown, uri, sourceCode);
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
            var cleanedName = CleanHeadingName(originalHeading);
            if (string.IsNullOrWhiteSpace(cleanedName))
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

            var body = HtmlToText(html[(match.Index + match.Length)..end]);
            var classificationEnd = index + 1 < matches.Length ? matches[index + 1].Index : html.Length;
            var classificationBody = HtmlToText(html[(match.Index + match.Length)..classificationEnd]);
            var entityType = InferEntityType(uri, cleanedName, level, classificationBody);
            entities.Add(CreateEntity(
                entityType,
                NormalizeEntityName(entityType, cleanedName),
                originalHeading,
                sourceCode,
                uri,
                level,
                index,
                body));
        }

        if (IsMonsterDocument(uri))
        {
            AddMonsterStatBlocks(entities, html, uri, sourceCode);
        }
        if (IsItemDocument(uri))
        {
            AddHtmlItemBlocks(entities, html, uri, sourceCode);
        }

        return entities;
    }

    private static void AddMarkdownItemBlocks(
        ICollection<LegacyEntity> entities,
        string markdown,
        Uri uri,
        string sourceCode)
    {
        var labels = MarkdownStrongLabel.Matches(markdown).Cast<Match>().ToArray();
        var headings = MarkdownHeading.Matches(markdown).Cast<Match>().ToArray();
        var existing = entities
            .Where(value => value.EntityType == "item")
            .Select(value => value.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < labels.Length; index++)
        {
            var originalName = NormalizeText(labels[index].Groups["name"].Value);
            var name = CleanHeadingName(originalName);
            if (!LooksLikeSpecificItemLabel(name) || existing.Contains(name))
            {
                continue;
            }

            var end = index + 1 < labels.Length ? labels[index + 1].Index : markdown.Length;
            var nextHeading = headings.FirstOrDefault(value => value.Index > labels[index].Index);
            if (nextHeading is not null && nextHeading.Index < end)
            {
                end = nextHeading.Index;
            }

            var body = NormalizeText(markdown[labels[index].Index..end]);
            if (!LooksLikePricedItemBlock(body) || !existing.Add(name))
            {
                continue;
            }

            entities.Add(CreateEntity(
                "item",
                name,
                originalName,
                sourceCode,
                uri,
                0,
                20000 + index,
                body));
        }
    }

    private static void AddHtmlItemBlocks(
        ICollection<LegacyEntity> entities,
        string html,
        Uri uri,
        string sourceCode)
    {
        var labels = HtmlStrongLabel.Matches(html).Cast<Match>().ToArray();
        var headings = HtmlHeading.Matches(html).Cast<Match>().ToArray();
        var existing = entities
            .Where(value => value.EntityType == "item")
            .Select(value => value.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < labels.Length; index++)
        {
            var originalName = HtmlToText(labels[index].Groups["name"].Value);
            var name = CleanHeadingName(originalName);
            if (!LooksLikeSpecificItemLabel(name) || existing.Contains(name))
            {
                continue;
            }

            var end = index + 1 < labels.Length ? labels[index + 1].Index : html.Length;
            var nextHeading = headings.FirstOrDefault(value => value.Index > labels[index].Index);
            if (nextHeading is not null && nextHeading.Index < end)
            {
                end = nextHeading.Index;
            }

            var body = HtmlToText(html[labels[index].Index..end]);
            if (!LooksLikePricedItemBlock(body) || !existing.Add(name))
            {
                continue;
            }

            entities.Add(CreateEntity(
                "item",
                name,
                originalName,
                sourceCode,
                uri,
                0,
                30000 + index,
                body));
        }
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

}
