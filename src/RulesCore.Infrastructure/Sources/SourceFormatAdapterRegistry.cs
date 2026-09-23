using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RulesCore.Infrastructure.Sources;

public sealed class SourceFormatAdapterRegistry(IEnumerable<ISourceFormatAdapter> adapters)
    : ISourceFormatAdapterRegistry
{
    private readonly ISourceFormatAdapter[] adapters = adapters.ToArray();

    public bool IsCandidateFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && adapters.Any(adapter => adapter.IsCandidate(fileName, ReadOnlySpan<byte>.Empty));

    public NormalizedSourceRepresentation? TryRead(SourceRepresentationArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        foreach (var adapter in adapters)
        {
            if (!adapter.IsCandidate(artifact.FileName, artifact.Content))
            {
                continue;
            }

            var result = adapter.TryRead(artifact);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }

    public IReadOnlyList<NormalizedSourceRepresentation> TryReadMany(
        IReadOnlyList<SourceRepresentationArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        if (artifacts.Count == 0)
        {
            return [];
        }

        var resolved = new Dictionary<string, NormalizedSourceRepresentation>(StringComparer.Ordinal);
        foreach (var adapter in adapters)
        {
            var candidates = artifacts
                .Where(artifact => !resolved.ContainsKey(ArtifactKey(artifact)))
                .Where(artifact => adapter.IsCandidate(artifact.FileName, artifact.Content))
                .ToArray();
            if (candidates.Length == 0)
            {
                continue;
            }

            if (adapter is ISourceFormatBatchAdapter batchAdapter)
            {
                foreach (var representation in batchAdapter.TryReadMany(candidates))
                {
                    resolved[ArtifactKey(representation.Artifact)] = representation;
                }
                continue;
            }

            foreach (var artifact in candidates)
            {
                var representation = adapter.TryRead(artifact);
                if (representation is not null)
                {
                    resolved[ArtifactKey(artifact)] = representation;
                }
            }
        }

        return artifacts
            .Select(artifact => resolved.GetValueOrDefault(ArtifactKey(artifact)))
            .Where(representation => representation is not null)
            .Cast<NormalizedSourceRepresentation>()
            .ToArray();
    }

    private static string ArtifactKey(SourceRepresentationArtifact artifact) =>
        $"{artifact.OriginIdentity}\n{artifact.FileName}";
}
