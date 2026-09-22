using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Aggregates PCGen CLASS files into one source entity per class. PCGen permits the
/// CLASS header to be repeated and stores level progression on numeric lines, so a
/// line-at-a-time source model would split one class into unrelated fragments.
/// </summary>
internal static class PcGenClassSourceParser
{
    public static IReadOnlyList<NormalizedSourceRecord> Parse(
        string path,
        string text,
        string? sourceCode,
        string? publicationLocalKey)
    {
        var classes = new Dictionary<string, ClassAccumulator>(StringComparer.OrdinalIgnoreCase);
        var classOrder = new List<string>();
        var fragments = new List<NormalizedSourceRecord>();
        string? currentClass = null;
        var segmentIndex = 0;

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var rawLine = lines[lineIndex];
            var lineNumber = lineIndex + 1;
            if (string.IsNullOrWhiteSpace(rawLine) || rawLine.TrimStart().StartsWith('#'))
            {
                continue;
            }
            if (IsSourceMetadataLine(rawLine))
            {
                continue;
            }

            var fields = rawLine.Split('\t');
            var firstToken = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(firstToken))
            {
                fragments.Add(Fragment(path, lineNumber, rawLine, sourceCode, "class-continuation-without-leading-token"));
                continue;
            }

            if (TryReadOperation(firstToken, out var operationKind, out var target, out var copyName))
            {
                fragments.Add(Operation(path, lineNumber, rawLine, sourceCode, operationKind, target, copyName));
                continue;
            }

            int? level = null;
            string? className = null;
            if (firstToken.StartsWith("CLASS:", StringComparison.OrdinalIgnoreCase))
            {
                className = firstToken["CLASS:".Length..].Trim();
                if (string.IsNullOrWhiteSpace(className))
                {
                    fragments.Add(Fragment(path, lineNumber, rawLine, sourceCode, "class-name-missing"));
                    currentClass = null;
                    continue;
                }

                currentClass = className;
                if (!classes.ContainsKey(className))
                {
                    classes.Add(className, new ClassAccumulator(className, lineNumber));
                    classOrder.Add(className);
                }
            }
            else if (int.TryParse(firstToken, out var parsedLevel) && parsedLevel >= 0)
            {
                level = parsedLevel;
                if (currentClass is null || !classes.TryGetValue(currentClass, out _))
                {
                    fragments.Add(Fragment(path, lineNumber, rawLine, sourceCode, "class-level-without-class"));
                    continue;
                }
                className = currentClass;
            }
            else
            {
                fragments.Add(Fragment(path, lineNumber, rawLine, sourceCode, "unsupported-class-line"));
                continue;
            }

            var accumulator = classes[className!];
            accumulator.RawLines.Add(new ClassLine(lineNumber, level, rawLine));
            foreach (var rawSegment in fields.Skip(1))
            {
                var raw = rawSegment.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var colon = raw.IndexOf(':');
                var tag = colon > 0 ? raw[..colon].Trim() : string.Empty;
                var value = colon > 0 ? raw[(colon + 1)..].Trim() : raw;
                accumulator.Segments.Add(new ClassSegment(
                    segmentIndex++,
                    lineNumber,
                    level,
                    tag,
                    value,
                    raw));
            }
        }

        var result = new List<NormalizedSourceRecord>(classOrder.Count + fragments.Count);
        foreach (var name in classOrder)
        {
            var value = classes[name];
            var normalizedIdentity = CanonicalSourceIdentity.NormalizeIdentityPart(name);
            if (string.IsNullOrWhiteSpace(normalizedIdentity))
            {
                normalizedIdentity = "unnamed";
            }
            if (normalizedIdentity.Length > 180)
            {
                normalizedIdentity = normalizedIdentity[..180];
            }

            var rawJson = JsonSerializer.Serialize(new
            {
                format = PcGenSourceFormatAdapter.Format,
                kind = "class-record",
                path,
                name,
                entityType = "class",
                lines = value.RawLines.Select(line => new
                {
                    line.LineNumber,
                    line.Level,
                    line.Raw
                }),
                segments = value.Segments.Select(segment => new
                {
                    segment.Index,
                    segment.LineNumber,
                    segment.Level,
                    segment.Tag,
                    segment.Value,
                    segment.Raw
                })
            });

            var semanticJson = JsonSerializer.Serialize(new
            {
                name,
                entityType = "class",
                segments = value.Segments
                    .Where(segment => !segment.Tag.StartsWith("SOURCE", StringComparison.OrdinalIgnoreCase))
                    .Select(segment => new
                    {
                        segment.Level,
                        segment.Tag,
                        segment.Value
                    })
            });

            result.Add(new NormalizedSourceRecord(
                "class",
                Truncate(name, 300),
                sourceCode,
                $"pcgen|class|{CanonicalSourceIdentity.Fingerprint(path)[..20]}|{normalizedIdentity}|1",
                rawJson,
                LocatorKey: BuildLocator(path, value.FirstLineNumber),
                PublicationLocalKey: publicationLocalKey,
                NativeIdentityJson: JsonSerializer.Serialize(new
                {
                    path,
                    key = name
                }),
                SemanticJson: semanticJson));
        }

        result.AddRange(fragments);
        return result;
    }

    private static bool IsSourceMetadataLine(string rawLine)
    {
        var fields = rawLine.Split('\t')
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (fields.Length == 0)
        {
            return false;
        }

        foreach (var field in fields)
        {
            var colon = field.IndexOf(':');
            if (colon <= 0)
            {
                return false;
            }
            var tag = field[..colon].Trim();
            if (!tag.StartsWith("SOURCE", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryReadOperation(
        string firstToken,
        out string operationKind,
        out string target,
        out string? copyName)
    {
        var copyMarker = firstToken.IndexOf(".COPY=", StringComparison.OrdinalIgnoreCase);
        if (copyMarker > 0)
        {
            operationKind = "copy";
            target = firstToken[..copyMarker].Trim();
            copyName = firstToken[(copyMarker + ".COPY=".Length)..].Trim();
            return true;
        }

        var modMarker = firstToken.IndexOf(".MOD", StringComparison.OrdinalIgnoreCase);
        if (modMarker > 0 && modMarker + ".MOD".Length == firstToken.Length)
        {
            operationKind = "modify";
            target = firstToken[..modMarker].Trim();
            copyName = null;
            return true;
        }

        var forgetMarker = firstToken.IndexOf(".FORGET", StringComparison.OrdinalIgnoreCase);
        if (forgetMarker > 0 && forgetMarker + ".FORGET".Length == firstToken.Length)
        {
            operationKind = "forget";
            target = firstToken[..forgetMarker].Trim();
            copyName = null;
            return true;
        }

        operationKind = string.Empty;
        target = string.Empty;
        copyName = null;
        return false;
    }

    private static NormalizedSourceRecord Operation(
        string path,
        int lineNumber,
        string rawLine,
        string? sourceCode,
        string operationKind,
        string target,
        string? copyName) =>
        new(
            "pcgen-operation",
            Truncate($"{operationKind}: {(copyName is null ? target : $"{target} -> {copyName}")}", 300),
            sourceCode,
            $"pcgen|operation|{CanonicalSourceIdentity.Fingerprint($"{path}\n{lineNumber}\n{rawLine}")[..32]}",
            JsonSerializer.Serialize(new
            {
                format = PcGenSourceFormatAdapter.Format,
                kind = "operation",
                path,
                lineNumber,
                rawLine,
                operation = operationKind,
                target,
                copyName
            }),
            LocatorKey: BuildLocator(path, lineNumber),
            NativeIdentityJson: JsonSerializer.Serialize(new
            {
                path,
                lineNumber,
                operation = operationKind,
                target,
                copyName
            }));

    private static NormalizedSourceRecord Fragment(
        string path,
        int lineNumber,
        string rawLine,
        string? sourceCode,
        string reason) =>
        new(
            "pcgen-fragment",
            Truncate($"PCGen class fragment line {lineNumber}", 300),
            sourceCode,
            $"pcgen|fragment|{CanonicalSourceIdentity.Fingerprint($"{path}\n{lineNumber}\n{rawLine}")[..32]}",
            JsonSerializer.Serialize(new
            {
                format = PcGenSourceFormatAdapter.Format,
                kind = "fragment",
                path,
                lineNumber,
                rawLine,
                reason
            }),
            LocatorKey: BuildLocator(path, lineNumber),
            NativeIdentityJson: JsonSerializer.Serialize(new { path, lineNumber, reason }));

    private static string BuildLocator(string path, int lineNumber)
    {
        var locator = $"pcgen:{path}#line:{lineNumber}";
        return locator.Length <= 500
            ? locator
            : $"pcgen:{CanonicalSourceIdentity.Fingerprint(path)[..32]}#line:{lineNumber}";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed class ClassAccumulator(string name, int firstLineNumber)
    {
        public string Name { get; } = name;
        public int FirstLineNumber { get; } = firstLineNumber;
        public List<ClassLine> RawLines { get; } = [];
        public List<ClassSegment> Segments { get; } = [];
    }

    private sealed record ClassLine(int LineNumber, int? Level, string Raw);
    private sealed record ClassSegment(
        int Index,
        int LineNumber,
        int? Level,
        string Tag,
        string Value,
        string Raw);
}
