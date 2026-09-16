using System.Text.Json;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

/// <summary>
/// Full-corpus acceptance checks for the exact upstream trees users import through Rules Core.
/// The ordinary test suite skips these when the pinned corpora are not present. The dedicated
/// upstream-corpus validation workflow supplies those directories from reviewed upstream commits.
/// </summary>
public sealed class UpstreamCorpusAdapterAcceptanceTests
{
    [Fact]
    public void FiveEToolsDataTreeParsesWithoutNativeIdentityConflicts()
    {
        var root = Environment.GetEnvironmentVariable("RULESCORE_5ETOOLS_DATA_PATH");
        if (string.IsNullOrWhiteSpace(root)) return;

        var artifacts = ReadArtifacts(
            root,
            "data",
            path => string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase),
            "https://github.com/5etools-mirror-3/5etools-src/tree/main/data");
        Assert.NotEmpty(artifacts);

        var representations = new FiveEToolsSourceFormatAdapter().TryReadMany(artifacts);

        Assert.NotEmpty(representations);
        Assert.All(representations, representation => Assert.NotEmpty(representation.Records));
        AssertNativeIdentityConsistency(representations);
    }

    [Theory]
    [InlineData("RULESCORE_PCGEN_3E_PATH", "data/3e")]
    [InlineData("RULESCORE_PCGEN_35E_PATH", "data/35e")]
    public void PcGenEditionTreeParsesWithoutAmbiguousSequenceFailures(
        string environmentVariable,
        string repositoryPrefix)
    {
        var root = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(root)) return;

        var artifacts = ReadArtifacts(
            root,
            repositoryPrefix,
            path =>
            {
                var extension = Path.GetExtension(path);
                return string.Equals(extension, ".pcc", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".lst", StringComparison.OrdinalIgnoreCase);
            },
            $"https://github.com/PCGen/pcgen/tree/master/{repositoryPrefix}");
        Assert.NotEmpty(artifacts);

        var representations = new PcGenSourceFormatAdapter().TryReadMany(artifacts);

        Assert.NotEmpty(representations);
        Assert.All(representations, representation => Assert.NotEmpty(representation.Records));
        AssertNativeIdentityConsistency(representations);
    }

    private static SourceRepresentationArtifact[] ReadArtifacts(
        string root,
        string repositoryPrefix,
        Func<string, bool> predicate,
        string sourceRoot)
    {
        var fullRoot = Path.GetFullPath(root);
        Assert.True(Directory.Exists(fullRoot), $"Corpus directory '{fullRoot}' does not exist.");

        return Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
            .Where(predicate)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(fullRoot, path).Replace('\\', '/');
                var repositoryPath = $"{repositoryPrefix.TrimEnd('/')}/{relative}";
                return new SourceRepresentationArtifact(
                    repositoryPath,
                    File.ReadAllBytes(path),
                    $"acceptance:{sourceRoot}#{repositoryPath}",
                    SourceUri: $"{sourceRoot.TrimEnd('/')}/{Uri.EscapeDataString(relative).Replace("%2F", "/", StringComparison.OrdinalIgnoreCase)}");
            })
            .ToArray();
    }

    private static void AssertNativeIdentityConsistency(
        IReadOnlyList<NormalizedSourceRepresentation> representations)
    {
        var seen = new Dictionary<string, NormalizedSourceRecord>(StringComparer.Ordinal);
        var recordCount = 0;
        foreach (var representation in representations)
        {
            foreach (var record in representation.Records)
            {
                recordCount++;
                if (!seen.TryAdd(record.NativeKey, record))
                {
                    var previous = seen[record.NativeKey];
                    Assert.Equal(previous.EntityType, record.EntityType);
                    Assert.Equal(previous.Name, record.Name);
                    Assert.Equal(previous.SourceCode, record.SourceCode);
                    Assert.True(
                        JsonEquivalent(previous.NativeIdentityJson, record.NativeIdentityJson),
                        $"Native key '{record.NativeKey}' resolved to conflicting identity metadata.");
                }
            }
        }
        Assert.True(recordCount > 0, "The upstream corpus did not produce any normalized source records.");
    }

    private static bool JsonEquivalent(string left, string right)
    {
        using var leftDocument = JsonDocument.Parse(left);
        using var rightDocument = JsonDocument.Parse(right);
        return JsonElement.DeepEquals(leftDocument.RootElement, rightDocument.RootElement);
    }
}
