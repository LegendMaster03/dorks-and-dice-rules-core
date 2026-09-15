namespace RulesCore.Application.Sources;

public static class FiveEToolsSchemaContract
{
    private static readonly HashSet<string> GeneratedAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "bookref-quick.json",
        "gendata-spell-source-lookup.json"
    };

    public static bool IsUnderDataTree(string? filePath) =>
        TryGetDataRelativePath(filePath, out _);

    public static bool IsValidatedSiteDataFile(string? filePath) =>
        GetSiteSchemaId(filePath) is not null;

    public static string? GetSiteSchemaId(string? filePath)
    {
        if (!TryGetDataRelativePath(filePath, out var relative)
            || !relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Foundry companions attach VTT configuration to definitions in other
        // files. Their class/spell/etc. arrays are partial overlays, not native
        // entities or revisions (for example, class/foundry.json omits edition).
        var fileName = Path.GetFileName(relative);
        if (string.Equals(fileName, "foundry.json", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("foundry-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (relative.StartsWith("generated/", StringComparison.OrdinalIgnoreCase)
            && !GeneratedAllowlist.Contains(Path.GetFileName(relative)))
        {
            return null;
        }

        if (relative.StartsWith("adventure/", StringComparison.OrdinalIgnoreCase))
        {
            return "adventure/adventure.json";
        }
        if (relative.StartsWith("book/", StringComparison.OrdinalIgnoreCase))
        {
            return "book/book.json";
        }
        if (relative.StartsWith("bestiary/bestiary-", StringComparison.OrdinalIgnoreCase))
        {
            return "bestiary/bestiary.json";
        }
        if (relative.StartsWith("bestiary/fluff-bestiary-", StringComparison.OrdinalIgnoreCase))
        {
            return "bestiary/fluff-bestiary.json";
        }
        if (relative.StartsWith("class/class-", StringComparison.OrdinalIgnoreCase))
        {
            return "class/class.json";
        }
        if (relative.StartsWith("class/fluff-class-", StringComparison.OrdinalIgnoreCase))
        {
            return "class/fluff-class.json";
        }
        if (relative.StartsWith("spells/spells-", StringComparison.OrdinalIgnoreCase))
        {
            return "spells/spells.json";
        }
        if (relative.StartsWith("spells/fluff-spells-", StringComparison.OrdinalIgnoreCase))
        {
            return "spells/fluff-spells.json";
        }

        return relative;
    }

    public static bool TryGetCorpusBodyIdentity(
        string? filePath,
        out string corpusType,
        out string corpusId)
    {
        corpusType = string.Empty;
        corpusId = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var schemaId = GetSiteSchemaId(filePath);
        var prefix = schemaId switch
        {
            "book/book.json" => "book-",
            "adventure/adventure.json" => "adventure-",
            _ => null
        };
        if (prefix is null)
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || fileName.Length <= prefix.Length)
        {
            return false;
        }

        corpusType = prefix[..^1];
        corpusId = fileName[prefix.Length..];
        return !string.IsNullOrWhiteSpace(corpusId);
    }

    private static bool TryGetDataRelativePath(string? filePath, out string relative)
    {
        relative = string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var normalized = filePath.Trim().Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
        {
            relative = normalized["data/".Length..];
            return relative.Length > 0;
        }

        var marker = normalized.IndexOf("/data/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return false;
        }

        relative = normalized[(marker + "/data/".Length)..];
        return relative.Length > 0;
    }
}
