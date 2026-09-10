namespace RulesCore.Domain.Sources;

/// <summary>
/// Canonical D&D rules-edition identities used by Rules Core. Publication years are
/// release metadata, not edition identities.
/// </summary>
public enum DndEdition
{
    FirstEdition = 1,
    SecondEdition = 2,
    ThirdEdition = 3,
    ThirdPointFiveEdition = 35,
    FourthEdition = 4,
    FifthEdition = 5,
    FifthPointFiveEdition = 55
}

public static class DndEditionCatalog
{
    public static readonly IReadOnlyList<string> CanonicalLabels =
        ["1e", "2e", "3e", "3.5e", "4e", "5e", "5.5e"];

    /// <summary>
    /// Legacy/import aliases remain accepted so older source datasets do not need to
    /// be rewritten before import. In particular, the former Wizards year labels
    /// normalize to the current 5e / 5.5e terminology.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> LegacyImportAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["5e 2014"] = "5e",
            ["5e-2014"] = "5e",
            ["5e (2014)"] = "5e",
            ["2014"] = "5e",
            ["2014 rules"] = "5e",
            ["D&D 2014"] = "5e",
            ["5e 2024"] = "5.5e",
            ["5e-2024"] = "5.5e",
            ["5e (2024)"] = "5.5e",
            ["2024"] = "5.5e",
            ["2024 rules"] = "5.5e",
            ["D&D 2024"] = "5.5e",
            ["One D&D"] = "5.5e"
        };

    public static string GetCanonicalLabel(DndEdition edition) => edition switch
    {
        DndEdition.FirstEdition => "1e",
        DndEdition.SecondEdition => "2e",
        DndEdition.ThirdEdition => "3e",
        DndEdition.ThirdPointFiveEdition => "3.5e",
        DndEdition.FourthEdition => "4e",
        DndEdition.FifthEdition => "5e",
        DndEdition.FifthPointFiveEdition => "5.5e",
        _ => throw new ArgumentOutOfRangeException(nameof(edition))
    };

    public static string? NormalizeImportLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TryParse(value, out var edition))
        {
            return GetCanonicalLabel(edition);
        }

        throw new ArgumentException(
            $"Unrecognized D&D edition label '{value}'. Supported canonical labels are {string.Join(", ", CanonicalLabels)}.",
            nameof(value));
    }

    public static bool TryParse(string? value, out DndEdition edition)
    {
        edition = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var token = NormalizeAliasToken(value);
        return token switch
        {
            "1e" or "1st" or "firstedition" or "dnd1e" or "adnd1e" => Assign(DndEdition.FirstEdition, out edition),
            "2e" or "2nd" or "secondedition" or "dnd2e" or "adnd2e" => Assign(DndEdition.SecondEdition, out edition),
            "3e" or "30e" or "3rd" or "thirdedition" or "dnd3e" => Assign(DndEdition.ThirdEdition, out edition),
            "35e" or "35" or "thirdpointfiveedition" or "dnd35e" => Assign(DndEdition.ThirdPointFiveEdition, out edition),
            "4e" or "4th" or "fourthedition" or "dnd4e" => Assign(DndEdition.FourthEdition, out edition),
            "5e" or "5th" or "fifthedition" or "dnd5e" or "5e2014" or "2014" or "2014rules" or "dnd2014" => Assign(DndEdition.FifthEdition, out edition),
            "55e" or "55" or "fifthpointfiveedition" or "dnd55e" or "5e2024" or "2024" or "2024rules" or "dnd2024" or "onedd" => Assign(DndEdition.FifthPointFiveEdition, out edition),
            _ => false
        };
    }

    private static bool Assign(DndEdition value, out DndEdition edition)
    {
        edition = value;
        return true;
    }

    private static string NormalizeAliasToken(string value)
    {
        var buffer = new char[value.Length];
        var length = 0;
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = char.ToLowerInvariant(character);
            }
        }
        return new string(buffer, 0, length);
    }
}

public static class SourceReleaseKinds
{
    public const string Published = "published";
    public const string Playtest = "playtest";
    public const string Preview = "preview";
    public const string Errata = "errata";
    public const string Srd = "srd";
    public const string ThirdParty = "third-party";
    public const string Other = "other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Published, Playtest, Preview, Errata, Srd, ThirdParty, Other],
        StringComparer.Ordinal);

    public static string? NormalizeImportLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
        normalized = normalized switch
        {
            "thirdparty" => ThirdParty,
            "system-reference-document" => Srd,
            _ => normalized
        };

        if (!All.Contains(normalized))
        {
            throw new ArgumentException(
                $"Unrecognized source release kind '{value}'. Supported values are {string.Join(", ", All.OrderBy(item => item, StringComparer.Ordinal))}.",
                nameof(value));
        }
        return normalized;
    }
}
