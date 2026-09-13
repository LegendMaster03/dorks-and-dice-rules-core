using RulesCore.Application.Sources;

namespace RulesCore.Tests;

public sealed class CanonicalSourceIdentityTests
{
    [Fact]
    public void SemanticFingerprintIgnoresTopLevelSourceProvenance()
    {
        const string first = """
            {
              "name": "Fireball",
              "source": "PHB",
              "page": 241,
              "level": 3,
              "school": "V",
              "entries": ["A bright streak flashes..."]
            }
            """;
        const string second = """
            {
              "page": 999,
              "source": "PDF-IMPORT",
              "school": "V",
              "entries": ["A bright streak flashes..."],
              "level": 3,
              "name": "Fireball"
            }
            """;

        Assert.Equal(
            CanonicalSourceIdentity.SemanticFingerprint(first),
            CanonicalSourceIdentity.SemanticFingerprint(second));
    }

    [Fact]
    public void SemanticFingerprintChangesWhenRuleBearingContentChanges()
    {
        const string first = """
            { "name": "Fireball", "source": "PHB", "level": 3, "damage": "8d6" }
            """;
        const string second = """
            { "name": "Fireball", "source": "PHB", "level": 3, "damage": "10d6" }
            """;

        Assert.NotEqual(
            CanonicalSourceIdentity.SemanticFingerprint(first),
            CanonicalSourceIdentity.SemanticFingerprint(second));
    }

    [Fact]
    public void BibliographicFingerprintIsRepresentationIndependent()
    {
        var structured = new CanonicalPublicationEvidence(
            "Player's Handbook",
            "Wizards of the Coast",
            "5e",
            new DateOnly(2014, 8, 19));
        var extractedPdf = new CanonicalPublicationEvidence(
            " PLAYER'S HANDBOOK ",
            "WIZARDS OF THE COAST",
            "5e",
            new DateOnly(2014, 8, 19));

        Assert.Equal(
            CanonicalSourceIdentity.BibliographicFingerprint(structured),
            CanonicalSourceIdentity.BibliographicFingerprint(extractedPdf));
    }

    [Theory]
    [InlineData("spell", "Fireball", "spell|fireball")]
    [InlineData("Monster", "Adult Red Dragon", "monster|adult-red-dragon")]
    public void OccurrenceKeysDoNotEncodeImportFormat(
        string entityType,
        string name,
        string expected)
    {
        Assert.Equal(expected, CanonicalSourceIdentity.OccurrenceKey(entityType, name));
    }
}
