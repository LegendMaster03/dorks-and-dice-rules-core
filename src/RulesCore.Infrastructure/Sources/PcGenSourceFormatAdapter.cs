using System.Globalization;
using System.Text;
using System.Text.Json;
using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

public sealed class PcGenSourceFormatAdapter : ISourceFormatBatchAdapter
{
    public const string Format = "pcgen-data";

    private static readonly IReadOnlyDictionary<string, string?> ReferenceKinds =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ABILITY"] = "ability",
            ["ARMORPROF"] = "armor-proficiency",
            ["CLASS"] = null,
            ["COMPANIONMOD"] = null,
            ["DEITY"] = "deity",
            ["DOMAIN"] = "domain",
            ["EQUIPMENT"] = "item",
            ["EQUIPMOD"] = null,
            ["FEAT"] = "feat",
            ["KIT"] = null,
            ["LANGUAGE"] = "language",
            ["RACE"] = "race",
            ["SHIELDPROF"] = "shield-proficiency",
            ["SKILL"] = "skill",
            ["SPELL"] = "spell",
            ["TEMPLATE"] = "template",
            ["WEAPONPROF"] = "weapon-proficiency"
        };

    public string FormatKey => Format;

    public bool IsCandidate(string? fileName, ReadOnlySpan<byte> content)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        return string.Equals(extension, ".pcc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".lst", StringComparison.OrdinalIgnoreCase);
    }

    public NormalizedSourceRepresentation? TryRead(SourceRepresentationArtifact artifact) =>
        TryReadMany([artifact]).SingleOrDefault();

    public IReadOnlyList<NormalizedSourceRepresentation> TryReadMany(
        IReadOnlyList<SourceRepresentationArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        var candidates = artifacts
            .Where(artifact => IsCandidate(artifact.FileName, artifact.Content))
            .Select(artifact => new ArtifactView(artifact, ArtifactPath(artifact), DecodeText(artifact.Content)))
            .ToArray();
        if (candidates.Length == 0) return [];

        var campaigns = candidates
            .Where(value => string.Equals(Path.GetExtension(value.Path), ".pcc", StringComparison.OrdinalIgnoreCase))
            .Select(ParseCampaign)
            .Where(value => value is not null)
            .Cast<CampaignDescriptor>()
            .ToArray();
        var references = campaigns
            .SelectMany(value => value.References)
            .ToArray();

        var results = new List<NormalizedSourceRepresentation>(candidates.Length);
        foreach (var candidate in candidates)
        {
            if (string.Equals(Path.GetExtension(candidate.Path), ".pcc", StringComparison.OrdinalIgnoreCase))
            {
                var campaign = campaigns.SingleOrDefault(value =>
                    string.Equals(value.Path, candidate.Path, StringComparison.OrdinalIgnoreCase));
                if (campaign is not null)
                {
                    results.Add(BuildCampaignRepresentation(campaign));
                }
                continue;
            }

            var representation = BuildListRepresentation(
                candidate,
                references.Where(value =>
                    string.Equals(value.TargetPath, candidate.Path, StringComparison.OrdinalIgnoreCase)).ToArray());
            if (representation is not null)
            {
                results.Add(representation);
            }
        }
        return results;
    }

    private static CampaignDescriptor? ParseCampaign(ArtifactView artifact)
    {
        var fields = ReadFields(artifact.Text);
        var hasPcGenEvidence = fields.Any(value =>
            string.Equals(value.Tag, "CAMPAIGN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.Tag, "GAMEMODE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.Tag, "SOURCELONG", StringComparison.OrdinalIgnoreCase)
            || ReferenceKinds.ContainsKey(value.Tag));
        if (!hasPcGenEvidence) return null;

        var sourceLong = LastValue(fields, "SOURCELONG");
        var sourceShort = LastValue(fields, "SOURCESHORT");
        var campaignName = LastValue(fields, "CAMPAIGN");
        var campaignKey = LastValue(fields, "KEY");
        var publisher = LastValue(fields, "PUBNAMELONG");
        var gameMode = LastValue(fields, "GAMEMODE");
        var sourceDateRaw = LastValue(fields, "SOURCEDATE");
        var displayName = FirstNonBlank(sourceLong, campaignName, campaignKey, Path.GetFileNameWithoutExtension(artifact.Path))!;
        var publicationLocalKey = sourceLong is null && sourceShort is null
            ? null
            : BuildPublicationLocalKey("pcc", artifact.Path, sourceShort, sourceLong);

        var references = new List<PccReference>();
        foreach (var field in fields.Where(value => ReferenceKinds.ContainsKey(value.Tag)))
        {
            var rawTarget = ReferenceTarget(field.Value);
            if (string.IsNullOrWhiteSpace(rawTarget)) continue;
            var targetPath = ResolveReferencePath(artifact.Path, rawTarget);
            if (targetPath is null) continue;
            references.Add(new PccReference(
                targetPath,
                field.Tag.ToUpperInvariant(),
                ReferenceKinds[field.Tag],
                CarriesPublicationContext(artifact.Path, rawTarget, targetPath)));
        }

        var descriptor = new CampaignDescriptor(
            artifact,
            displayName,
            sourceLong,
            sourceShort,
            publisher,
            MapGameEdition(gameMode),
            gameMode,
            ParseExactDate(sourceDateRaw),
            sourceDateRaw,
            campaignKey,
            publicationLocalKey,
            references);
        foreach (var reference in references)
        {
            reference.Campaign = descriptor;
        }
        return descriptor;
    }

    private static NormalizedSourceRepresentation BuildCampaignRepresentation(CampaignDescriptor campaign)
    {
        var fields = ReadFields(campaign.Artifact.Text);
        var rawJson = JsonSerializer.Serialize(new
        {
            format = Format,
            kind = "campaign",
            path = campaign.Path,
            fields = fields.Select(value => new
            {
                value.LineNumber,
                value.Tag,
                value.Value,
                value.Raw
            })
        });
        var record = new NormalizedSourceRecord(
            "pcgen-campaign",
            Truncate(campaign.DisplayName, 300),
            campaign.SourceCode,
            $"pcgen|campaign|{CanonicalSourceIdentity.Fingerprint(campaign.Path)[..24]}",
            rawJson,
            NativeIdentityJson: JsonSerializer.Serialize(new
            {
                path = campaign.Path,
                campaignKey = campaign.CampaignKey
            }));

        var publications = campaign.PublicationLocalKey is null
            ? Array.Empty<NormalizedSourcePublication>()
            : [ToPublication(campaign.ToPublicationContext())];
        return new NormalizedSourceRepresentation(
            Format,
            campaign.Artifact.Artifact,
            [record],
            publications,
            JsonSerializer.Serialize(new
            {
                schemaFamily = "pcgen-pcc",
                path = campaign.Path,
                gameMode = campaign.NativeGameMode,
                sourceLong = campaign.SourceLong,
                sourceShort = campaign.SourceCode,
                publisher = campaign.Publisher,
                sourceDate = campaign.SourceDateRaw,
                referenceCount = campaign.References.Count,
                semantics = "campaign-metadata-and-references-preserved"
            }));
    }

    private static NormalizedSourceRepresentation? BuildListRepresentation(
        ArtifactView artifact,
        IReadOnlyList<PccReference> references)
    {
        var fields = ReadFields(artifact.Text);
        var embedded = ReadEmbeddedSourceMetadata(fields);
        var entityType = ResolveEntityType(artifact.Path, references);
        var publication = ResolvePublication(artifact.Path, references, embedded);
        var hasPcGenEvidence = references.Count > 0
            || entityType is not null
            || embedded.HasEvidence
            || fields.Any(value => IsLikelyPcGenDataTag(value.Tag));
        if (!hasPcGenEvidence) return null;

        var records = BuildListRecords(artifact, entityType, publication);
        if (records.Count == 0)
        {
            records =
            [
                new NormalizedSourceRecord(
                    "pcgen-file",
                    Truncate(Path.GetFileName(artifact.Path), 300),
                    publication?.SourceCode,
                    $"pcgen|file|{CanonicalSourceIdentity.Fingerprint(artifact.Path)[..24]}",
                    JsonSerializer.Serialize(new
                    {
                        format = Format,
                        kind = "metadata-only",
                        path = artifact.Path
                    }),
                    NativeIdentityJson: JsonSerializer.Serialize(new { path = artifact.Path }))
            ];
        }

        var publications = publication is null
            ? Array.Empty<NormalizedSourcePublication>()
            : [ToPublication(publication)];
        return new NormalizedSourceRepresentation(
            Format,
            artifact.Artifact,
            records,
            publications,
            JsonSerializer.Serialize(new
            {
                schemaFamily = "pcgen-lst",
                path = artifact.Path,
                inferredEntityType = entityType,
                referenceCount = references.Count,
                publicationResolution = publication is null ? "unresolved" : "resolved",
                embeddedSourceLong = embedded.SourceLong,
                embeddedSourceShort = embedded.SourceShort,
                embeddedSourceDate = embedded.SourceDateRaw,
                embeddedSourceMetadataAmbiguous = embedded.Ambiguous,
                semantics = "supported-single-line-records-with-lossless-unsupported-fragments"
            }));
    }

    private static List<NormalizedSourceRecord> BuildListRecords(
        ArtifactView artifact,
        string? entityType,
        PublicationContext? publication)
    {
        var records = new List<NormalizedSourceRecord>();
        var duplicateOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lines = SplitLines(artifact.Text);
        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            var lineNumber = index + 1;
            if (string.IsNullOrWhiteSpace(rawLine) || rawLine.TrimStart().StartsWith('#')) continue;
            if (IsSourceMetadataLine(rawLine)) continue;

            var fields = rawLine.Split('\t');
            var firstToken = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(firstToken))
            {
                records.Add(FragmentRecord(artifact.Path, lineNumber, rawLine, publication?.SourceCode, "continuation"));
                continue;
            }

            if (TryReadOperation(firstToken, out var operationKind, out var target, out var copyName))
            {
                records.Add(OperationRecord(
                    artifact.Path,
                    lineNumber,
                    rawLine,
                    publication?.SourceCode,
                    operationKind,
                    target,
                    copyName));
                continue;
            }

            if (entityType is null)
            {
                records.Add(FragmentRecord(artifact.Path, lineNumber, rawLine, publication?.SourceCode, "unsupported-family"));
                continue;
            }

            var name = ObjectName(firstToken);
            if (string.IsNullOrWhiteSpace(name))
            {
                records.Add(FragmentRecord(artifact.Path, lineNumber, rawLine, publication?.SourceCode, "unresolved-name"));
                continue;
            }

            var segments = ReadSegments(fields.Skip(1));
            var explicitKey = segments
                .LastOrDefault(value => string.Equals(value.Tag, "KEY", StringComparison.OrdinalIgnoreCase))?.Value;
            var identityName = FirstNonBlank(explicitKey, name)!;
            var ordinalKey = $"{entityType}\n{identityName}";
            var ordinal = duplicateOrdinals.GetValueOrDefault(ordinalKey) + 1;
            duplicateOrdinals[ordinalKey] = ordinal;

            var rawJson = JsonSerializer.Serialize(new
            {
                format = Format,
                kind = "record",
                path = artifact.Path,
                lineNumber,
                rawLine,
                name,
                entityType,
                segments = segments.Select(value => new
                {
                    value.Index,
                    value.Tag,
                    value.Value,
                    value.Raw
                })
            });
            var semanticSegments = segments
                .Where(value => !value.Tag.StartsWith("SOURCE", StringComparison.OrdinalIgnoreCase))
                .Select(value => new { value.Tag, value.Value })
                .OrderBy(value => value.Tag, StringComparer.Ordinal)
                .ThenBy(value => value.Value, StringComparer.Ordinal)
                .ToArray();
            var semanticJson = JsonSerializer.Serialize(new
            {
                name,
                entityType,
                segments = semanticSegments
            });
            var normalizedIdentity = CanonicalSourceIdentity.NormalizeIdentityPart(identityName);
            if (string.IsNullOrWhiteSpace(normalizedIdentity)) normalizedIdentity = "unnamed";
            if (normalizedIdentity.Length > 180) normalizedIdentity = normalizedIdentity[..180];

            records.Add(new NormalizedSourceRecord(
                entityType,
                Truncate(name, 300),
                publication?.SourceCode,
                $"pcgen|{entityType}|{CanonicalSourceIdentity.Fingerprint(artifact.Path)[..20]}|{normalizedIdentity}|{ordinal}",
                rawJson,
                LocatorKey: BuildLocator(artifact.Path, lineNumber),
                PublicationLocalKey: publication?.LocalKey,
                NativeIdentityJson: JsonSerializer.Serialize(new
                {
                    path = artifact.Path,
                    key = identityName,
                    ordinal
                }),
                SemanticJson: semanticJson));
        }
        return records;
    }

    private static NormalizedSourceRecord OperationRecord(
        string path,
        int lineNumber,
        string rawLine,
        string? sourceCode,
        string operationKind,
        string target,
        string? copyName)
    {
        var display = copyName is null ? target : $"{target} -> {copyName}";
        return new NormalizedSourceRecord(
            "pcgen-operation",
            Truncate($"{operationKind}: {display}", 300),
            sourceCode,
            $"pcgen|operation|{CanonicalSourceIdentity.Fingerprint($"{path}\n{rawLine}")[..32]}",
            JsonSerializer.Serialize(new
            {
                format = Format,
                kind = "operation",
                path,
                lineNumber,
                rawLine,
                operation = operationKind,
                target,
                copyName
            }),
            LocatorKey: BuildLocator(path, lineNumber),
            NativeIdentityJson: JsonSerializer.Serialize(new { path, operation = operationKind, target, copyName }));
    }

    private static NormalizedSourceRecord FragmentRecord(
        string path,
        int lineNumber,
        string rawLine,
        string? sourceCode,
        string reason) =>
        new(
            "pcgen-fragment",
            Truncate($"PCGen fragment line {lineNumber}", 300),
            sourceCode,
            $"pcgen|fragment|{CanonicalSourceIdentity.Fingerprint($"{path}\n{rawLine}")[..32]}",
            JsonSerializer.Serialize(new
            {
                format = Format,
                kind = "fragment",
                path,
                lineNumber,
                rawLine,
                reason
            }),
            LocatorKey: BuildLocator(path, lineNumber),
            NativeIdentityJson: JsonSerializer.Serialize(new { path, lineNumber, reason }));

    private static PublicationContext? ResolvePublication(
        string listPath,
        IReadOnlyList<PccReference> references,
        EmbeddedSourceMetadata embedded)
    {
        if (embedded.Ambiguous) return null;

        var candidates = references
            .Where(value => value.CarriesPublicationContext
                && value.Campaign?.PublicationLocalKey is not null)
            .Select(value => value.Campaign!.ToPublicationContext())
            .ToArray();

        if (embedded.HasEvidence)
        {
            var compatible = candidates.Where(value => Compatible(value, embedded)).ToArray();
            var baseContext = MergeCompatible(compatible);
            return new PublicationContext(
                baseContext?.LocalKey
                    ?? BuildPublicationLocalKey("lst", listPath, embedded.SourceShort, embedded.SourceLong),
                FirstNonBlank(embedded.SourceLong, baseContext?.DisplayName, Path.GetFileNameWithoutExtension(listPath))!,
                baseContext?.Publisher,
                baseContext?.GameEdition ?? InferEditionFromPath(listPath),
                embedded.PublicationDate ?? baseContext?.PublicationDate,
                FirstNonBlank(embedded.SourceShort, baseContext?.SourceCode));
        }

        return MergeCompatible(candidates);
    }

    private static PublicationContext? MergeCompatible(IReadOnlyList<PublicationContext> candidates)
    {
        if (candidates.Count == 0) return null;
        var first = candidates[0];
        if (candidates.Skip(1).Any(value => !Compatible(first, value))) return null;
        return new PublicationContext(
            first.LocalKey,
            candidates.Select(value => value.DisplayName).First(value => !string.IsNullOrWhiteSpace(value)),
            candidates.Select(value => value.Publisher).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            candidates.Select(value => value.GameEdition).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            candidates.Select(value => value.PublicationDate).FirstOrDefault(value => value.HasValue),
            candidates.Select(value => value.SourceCode).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static bool Compatible(PublicationContext left, PublicationContext right) =>
        CompatibleValue(left.DisplayName, right.DisplayName)
        && CompatibleValue(left.Publisher, right.Publisher)
        && CompatibleValue(left.GameEdition, right.GameEdition)
        && CompatibleValue(left.SourceCode, right.SourceCode)
        && (!left.PublicationDate.HasValue
            || !right.PublicationDate.HasValue
            || left.PublicationDate == right.PublicationDate);

    private static bool Compatible(PublicationContext candidate, EmbeddedSourceMetadata embedded) =>
        CompatibleValue(candidate.DisplayName, embedded.SourceLong)
        && CompatibleValue(candidate.SourceCode, embedded.SourceShort)
        && (!candidate.PublicationDate.HasValue
            || !embedded.PublicationDate.HasValue
            || candidate.PublicationDate == embedded.PublicationDate);

    private static bool CompatibleValue(string? left, string? right) =>
        string.IsNullOrWhiteSpace(left)
        || string.IsNullOrWhiteSpace(right)
        || string.Equals(
            CanonicalSourceIdentity.NormalizeIdentityPart(left),
            CanonicalSourceIdentity.NormalizeIdentityPart(right),
            StringComparison.Ordinal);

    private static string? ResolveEntityType(string path, IReadOnlyList<PccReference> references)
    {
        if (references.Count > 0)
        {
            var mapped = references.Select(value => value.EntityType).ToArray();
            if (mapped.All(value => value is not null))
            {
                var distinct = mapped.Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (distinct.Length == 1) return distinct[0];
            }
            return null;
        }
        return InferEntityTypeFromFileName(path);
    }

    private static string? InferEntityTypeFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        if (name.Contains("spell", StringComparison.Ordinal)) return "spell";
        if (name.Contains("feat", StringComparison.Ordinal)) return "feat";
        if (name.Contains("skill", StringComparison.Ordinal)) return "skill";
        if (name.Contains("race", StringComparison.Ordinal)) return "race";
        if (name.Contains("abilit", StringComparison.Ordinal)) return "ability";
        if (name.Contains("language", StringComparison.Ordinal)) return "language";
        if (name.Contains("deit", StringComparison.Ordinal)) return "deity";
        if (name.Contains("domain", StringComparison.Ordinal)) return "domain";
        if (name.Contains("template", StringComparison.Ordinal)) return "template";
        if (name.Contains("equip", StringComparison.Ordinal)) return "item";
        return null;
    }

    private static EmbeddedSourceMetadata ReadEmbeddedSourceMetadata(IReadOnlyList<TaggedField> fields)
    {
        var longValues = Values(fields, "SOURCELONG");
        var shortValues = Values(fields, "SOURCESHORT");
        var dateValues = Values(fields, "SOURCEDATE");
        var webValues = Values(fields, "SOURCEWEB");
        var ambiguous = longValues.Count > 1
            || shortValues.Count > 1
            || dateValues.Count > 1
            || webValues.Count > 1;
        var sourceLong = SingleOrNull(longValues);
        var sourceShort = SingleOrNull(shortValues);
        var dateRaw = SingleOrNull(dateValues);
        var sourceWeb = SingleOrNull(webValues);
        return new EmbeddedSourceMetadata(
            sourceLong,
            sourceShort,
            dateRaw,
            ParseExactDate(dateRaw),
            sourceWeb,
            ambiguous);
    }

    private static string? SingleOrNull(IReadOnlyList<string> values) =>
        values.Count == 1 ? values[0] : null;

    private static IReadOnlyList<string> Values(IReadOnlyList<TaggedField> fields, string tag) =>
        fields
            .Where(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase))
            .Select(value => value.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<TaggedField> ReadFields(string text)
    {
        var result = new List<TaggedField>();
        var lines = SplitLines(text);
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var rawLine = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(rawLine) || rawLine.TrimStart().StartsWith('#')) continue;
            foreach (var rawField in rawLine.Split('\t'))
            {
                var field = rawField.Trim();
                if (string.IsNullOrWhiteSpace(field)) continue;
                var colon = field.IndexOf(':');
                if (colon <= 0) continue;
                result.Add(new TaggedField(
                    lineIndex + 1,
                    field[..colon].Trim(),
                    field[(colon + 1)..].Trim(),
                    field));
            }
        }
        return result;
    }

    private static IReadOnlyList<Segment> ReadSegments(IEnumerable<string> rawSegments)
    {
        var result = new List<Segment>();
        var index = 0;
        foreach (var rawSegment in rawSegments)
        {
            var raw = rawSegment.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                index++;
                continue;
            }
            var colon = raw.IndexOf(':');
            var tag = colon > 0 ? raw[..colon].Trim() : string.Empty;
            var value = colon > 0 ? raw[(colon + 1)..].Trim() : raw;
            result.Add(new Segment(index++, tag, value, raw));
        }
        return result;
    }

    private static bool IsSourceMetadataLine(string rawLine)
    {
        var fields = rawLine.Split('\t')
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (fields.Length == 0) return false;
        var recognized = 0;
        foreach (var field in fields)
        {
            var colon = field.IndexOf(':');
            if (colon <= 0) return false;
            var tag = field[..colon].Trim();
            if (string.Equals(tag, "SOURCELONG", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, "SOURCESHORT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, "SOURCEDATE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, "SOURCEWEB", StringComparison.OrdinalIgnoreCase))
            {
                recognized++;
                continue;
            }
            return false;
        }
        return recognized > 0;
    }

    private static bool IsLikelyPcGenDataTag(string tag) =>
        tag.StartsWith("SOURCE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(tag, "TYPE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(tag, "DESC", StringComparison.OrdinalIgnoreCase)
        || string.Equals(tag, "KEY", StringComparison.OrdinalIgnoreCase)
        || string.Equals(tag, "CLASSES", StringComparison.OrdinalIgnoreCase)
        || string.Equals(tag, "PRE", StringComparison.OrdinalIgnoreCase)
        || tag.StartsWith("PRE", StringComparison.OrdinalIgnoreCase)
        || tag.StartsWith("BONUS", StringComparison.OrdinalIgnoreCase);

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

    private static string ObjectName(string firstToken)
    {
        var token = firstToken.Trim();
        if (token.StartsWith("CATEGORY=", StringComparison.OrdinalIgnoreCase))
        {
            var separator = token.LastIndexOf('|');
            if (separator >= 0 && separator + 1 < token.Length)
            {
                return token[(separator + 1)..].Trim();
            }
        }
        return token;
    }

    private static string? ReferenceTarget(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var separator = value.IndexOf('|');
        var target = (separator >= 0 ? value[..separator] : value).Trim();
        return string.IsNullOrWhiteSpace(target) ? null : target;
    }

    private static string? ResolveReferencePath(string campaignPath, string rawTarget)
    {
        var normalizedTarget = rawTarget.Replace('\\', '/').Trim();
        if (normalizedTarget.StartsWith("@/", StringComparison.Ordinal))
        {
            return NormalizePath(normalizedTarget[2..]);
        }
        if (normalizedTarget.StartsWith('@'))
        {
            normalizedTarget = normalizedTarget[1..].TrimStart('/');
        }
        var directory = PathDirectory(campaignPath);
        return NormalizePath(string.IsNullOrEmpty(directory)
            ? normalizedTarget
            : $"{directory}/{normalizedTarget}");
    }

    private static bool CarriesPublicationContext(string campaignPath, string rawTarget, string targetPath)
    {
        if (rawTarget.TrimStart().StartsWith('@')) return false;
        var directory = PathDirectory(campaignPath);
        return string.IsNullOrEmpty(directory)
            || string.Equals(targetPath, directory, StringComparison.OrdinalIgnoreCase)
            || targetPath.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ArtifactPath(SourceRepresentationArtifact artifact)
    {
        var marker = artifact.OriginIdentity.LastIndexOf('#');
        if (marker >= 0 && marker + 1 < artifact.OriginIdentity.Length)
        {
            var fromOrigin = Uri.UnescapeDataString(artifact.OriginIdentity[(marker + 1)..]);
            var normalized = NormalizePath(fromOrigin);
            if (!string.IsNullOrWhiteSpace(normalized)) return normalized;
        }
        return NormalizePath(artifact.FileName);
    }

    private static string NormalizePath(string value)
    {
        var parts = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(part);
        }
        return string.Join('/', stack);
    }

    private static string PathDirectory(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator <= 0 ? string.Empty : path[..separator];
    }

    private static string BuildPublicationLocalKey(
        string kind,
        string path,
        string? sourceShort,
        string? sourceLong) =>
        $"pcgen-{kind}:{CanonicalSourceIdentity.Fingerprint($"{path}\n{sourceShort}\n{sourceLong}")[..28]}";

    private static string BuildLocator(string path, int lineNumber)
    {
        var locator = $"pcgen:{path}#line:{lineNumber}";
        return locator.Length <= 500
            ? locator
            : $"pcgen:{CanonicalSourceIdentity.Fingerprint(path)[..32]}#line:{lineNumber}";
    }

    private static string? InferEditionFromPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(value => string.Equals(value, "35e", StringComparison.OrdinalIgnoreCase))) return "3.5e";
        if (segments.Any(value => string.Equals(value, "3e", StringComparison.OrdinalIgnoreCase))) return "3e";
        return null;
    }

    private static string? MapGameEdition(string? gameMode)
    {
        if (string.IsNullOrWhiteSpace(gameMode)) return null;
        return gameMode.Trim().ToLowerInvariant() switch
        {
            "3e" => "3e",
            "35e" => "3.5e",
            "5e" => "5e",
            _ => null
        };
    }

    private static DateOnly? ParseExactDate(string? value) =>
        DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;

    private static string DecodeText(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static string? LastValue(IReadOnlyList<TaggedField> fields, string tag) =>
        fields.LastOrDefault(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static NormalizedSourcePublication ToPublication(PublicationContext publication)
    {
        IReadOnlyDictionary<string, string>? identifiers = string.IsNullOrWhiteSpace(publication.SourceCode)
            ? null
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pcgen-source-short"] = publication.SourceCode
            };
        return new NormalizedSourcePublication(
            publication.LocalKey,
            publication.DisplayName,
            publication.Publisher,
            publication.GameEdition,
            publication.PublicationDate,
            identifiers);
    }

    private sealed record ArtifactView(SourceRepresentationArtifact Artifact, string Path, string Text);

    private sealed record TaggedField(int LineNumber, string Tag, string Value, string Raw);

    private sealed record Segment(int Index, string Tag, string Value, string Raw);

    private sealed class PccReference(
        string targetPath,
        string referenceTag,
        string? entityType,
        bool carriesPublicationContext)
    {
        public string TargetPath { get; } = targetPath;
        public string ReferenceTag { get; } = referenceTag;
        public string? EntityType { get; } = entityType;
        public bool CarriesPublicationContext { get; } = carriesPublicationContext;
        public CampaignDescriptor? Campaign { get; set; }
    }

    private sealed record CampaignDescriptor(
        ArtifactView Artifact,
        string DisplayName,
        string? SourceLong,
        string? SourceCode,
        string? Publisher,
        string? GameEdition,
        string? NativeGameMode,
        DateOnly? PublicationDate,
        string? SourceDateRaw,
        string? CampaignKey,
        string? PublicationLocalKey,
        IReadOnlyList<PccReference> References)
    {
        public string Path => Artifact.Path;

        public PublicationContext ToPublicationContext()
        {
            if (PublicationLocalKey is null)
            {
                throw new InvalidOperationException("Campaign does not contain publication evidence.");
            }
            return new PublicationContext(
                PublicationLocalKey,
                DisplayName,
                Publisher,
                GameEdition,
                PublicationDate,
                SourceCode);
        }
    }

    private sealed record PublicationContext(
        string LocalKey,
        string DisplayName,
        string? Publisher,
        string? GameEdition,
        DateOnly? PublicationDate,
        string? SourceCode);

    private sealed record EmbeddedSourceMetadata(
        string? SourceLong,
        string? SourceShort,
        string? SourceDateRaw,
        DateOnly? PublicationDate,
        string? SourceWeb,
        bool Ambiguous)
    {
        public bool HasEvidence => !string.IsNullOrWhiteSpace(SourceLong)
            || !string.IsNullOrWhiteSpace(SourceShort)
            || !string.IsNullOrWhiteSpace(SourceDateRaw)
            || !string.IsNullOrWhiteSpace(SourceWeb);
    }
}
