using System.Reflection;
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
    private const int WebSourceDocumentLimit = 2000;
    private static readonly MethodInfo TranslateRecordMethod = RequireStaticMethod(
        "RulesCore.Infrastructure.Sources.RulesCoreContentTranslation",
        "TranslateRecord");
    private static readonly MethodInfo ApplyAliasPolicyMethod = RequireStaticMethod(
        "RulesCore.Infrastructure.Sources.TrustedCanonicalAliasPolicy",
        "Apply");

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
        Assert.True(
            artifacts.Length <= WebSourceDocumentLimit,
            $"The pinned 5e.tools data tree contains {artifacts.Length} candidate files, exceeding the current Web-source acquisition limit of {WebSourceDocumentLimit}.");

        var representations = new FiveEToolsSourceFormatAdapter().TryReadMany(artifacts);

        Assert.NotEmpty(representations);
        Assert.All(representations, representation => Assert.NotEmpty(representation.Records));
        var normalized = NormalizeForImport(representations, requireLosslessNativeContent: true);
        AssertNativeIdentityConsistency(normalized);
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
        Assert.True(
            artifacts.Length <= WebSourceDocumentLimit,
            $"The pinned {repositoryPrefix} tree contains {artifacts.Length} candidate files, exceeding the current Web-source acquisition limit of {WebSourceDocumentLimit}.");

        var representations = new PcGenSourceFormatAdapter().TryReadMany(artifacts);

        Assert.NotEmpty(representations);
        Assert.All(representations, representation => Assert.NotEmpty(representation.Records));
        var normalized = NormalizeForImport(representations, requireLosslessNativeContent: false);
        AssertNativeIdentityConsistency(normalized);
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

    private static IReadOnlyList<NormalizedSourceRecord> NormalizeForImport(
        IReadOnlyList<NormalizedSourceRepresentation> representations,
        bool requireLosslessNativeContent)
    {
        var normalized = new List<NormalizedSourceRecord>();
        foreach (var representation in representations)
        {
            foreach (var record in representation.Records)
            {
                try
                {
                    var translated = (NormalizedSourceRecord)(TranslateRecordMethod.Invoke(
                        null,
                        [representation, record])
                        ?? throw new InvalidOperationException("Translation returned null."));
                    var finalRecord = (NormalizedSourceRecord)(ApplyAliasPolicyMethod.Invoke(
                        null,
                        [representation, translated])
                        ?? throw new InvalidOperationException("Canonical alias policy returned null."));

                    if (requireLosslessNativeContent)
                    {
                        Assert.False(
                            string.IsNullOrWhiteSpace(finalRecord.ContentJson),
                            $"Native 5e.tools record '{record.NativeKey}' did not retain mechanical content.");
                        Assert.True(
                            JsonEquivalent(record.RawJson, finalRecord.ContentJson!),
                            $"Native 5e.tools record '{record.NativeKey}' was altered by translation.");
                    }

                    normalized.Add(finalRecord);
                }
                catch (TargetInvocationException exception)
                {
                    throw new InvalidOperationException(
                        $"Normalization failed for '{representation.Artifact.FileName}' record '{record.NativeKey}'.",
                        exception.InnerException ?? exception);
                }
            }
        }
        return normalized;
    }

    private static void AssertNativeIdentityConsistency(
        IReadOnlyList<NormalizedSourceRecord> records)
    {
        var seen = new Dictionary<string, NormalizedSourceRecord>(StringComparer.Ordinal);
        foreach (var record in records)
        {
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
        Assert.NotEmpty(records);
    }

    private static MethodInfo RequireStaticMethod(string typeName, string methodName)
    {
        var type = typeof(PcGenSourceFormatAdapter).Assembly.GetType(typeName, throwOnError: true)
            ?? throw new InvalidOperationException($"Type '{typeName}' was not found.");
        return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method '{typeName}.{methodName}' was not found.");
    }

    private static bool JsonEquivalent(string left, string right)
    {
        using var leftDocument = JsonDocument.Parse(left);
        using var rightDocument = JsonDocument.Parse(right);
        return JsonElement.DeepEquals(leftDocument.RootElement, rightDocument.RootElement);
    }
}
