using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RulesCore.Application.Sources;

public static partial class LegacySrdDocumentInspector
{
    private static string CleanHeadingName(string value)
    {
        var decoded = WebUtility.HtmlDecode(HtmlTag.Replace(value, string.Empty));
        decoded = Regex.Replace(
            decoded,
            @"\s*[\[(](?:General|Metamagic|Item Creation|Fighter|Epic|Psionic)[^\])]*[\])]\s*$",
            string.Empty,
            RegexOptions.IgnoreCase);
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
