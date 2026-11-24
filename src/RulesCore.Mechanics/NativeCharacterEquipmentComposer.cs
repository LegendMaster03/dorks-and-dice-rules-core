using RulesCore.Content;

namespace RulesCore.Mechanics;

public sealed record NativeCharacterEquipmentDuplicateChoice(
    NativeEquipmentQualificationProvenance Provenance, string OriginalQualificationId,
    IReadOnlyList<string> EligibleQualificationIds);

public sealed record NativeCharacterEquipmentComposition(
    string Status, NativeEquipmentQualificationProjection? Equipment,
    IReadOnlyList<NativeLifeExperienceEquipmentReplacement> Replacements,
    IReadOnlyList<NativeCharacterEquipmentDuplicateChoice> RequiredChoices,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// Combine native-qualified occurrences, not item ownership or proficiency
/// assertions. Only exact duplicate specific-tool grants offer a replacement;
/// overlapping groups, weapon proficiencies and armor remain binary coverage.
/// </summary>
public static class NativeCharacterEquipmentComposer
{
    public static NativeCharacterEquipmentComposition Compose(
        NativeCatalogSnapshot catalog, NativeEquipmentQualificationProjection life,
        NativeEquipmentQualificationRequest otherSources,
        IReadOnlyList<NativeLifeExperienceEquipmentReplacement> alternatives,
        IReadOnlyList<NativeLifeExperienceEquipmentReplacement>? lifeReplacements = null)
    {
        NativeCharacterEquipmentComposition Reject(string code, string detail) =>
            new("invalid", null, [], [], [new(code, "equipment", detail)]);
        if (life?.Status != "resolved" || life.RulesetId != catalog.RulesetId ||
            life.Fingerprint != catalog.Fingerprint || otherSources is null ||
            alternatives is null || alternatives.Count > 100)
            return Reject("character-equipment-input-invalid", "Complete native source projections and bounded choices are required.");

        var resolver = new NativeEquipmentQualifications(catalog);
        var other = resolver.Resolve(otherSources);
        if (other.Status != "resolved")
            return new(other.Status, other, [], [], other.Issues);

        // A counted choice may grant several different targets under the
        // same authored grant ID. Distinguish its targets while rejecting
        // a replayed source occurrence across independently earned inputs.
        var lifeEntries = life.Qualifications
            .SelectMany(q => q.Provenance.Select(p => (Qualification: q, Provenance: p))).ToArray();
        var otherEntries = other.Qualifications
            .SelectMany(q => q.Provenance.Select(p => (Qualification: q, Provenance: p))).ToArray();
        var entries = lifeEntries.Concat(otherEntries).ToArray();
        var lifeOccurrences = lifeEntries.Select(x => x.Provenance.SourceOccurrenceId)
            .ToHashSet(StringComparer.Ordinal);
        if (entries.Length > 1500 ||
            otherSources.Sources.Any(source => lifeOccurrences.Contains(source.OccurrenceId)) ||
            entries.GroupBy(x => x.Provenance.SourceOccurrenceId, StringComparer.Ordinal)
                .Any(g => g.Select(x => x.Provenance.SourceId).Distinct(StringComparer.Ordinal).Count() != 1) ||
            entries.Select(x => (x.Provenance.SourceOccurrenceId, x.Provenance.GrantId,
                x.Qualification.QualificationId)).Distinct().Count() != entries.Length)
            return Reject("character-equipment-occurrence-duplicate",
                "An equipment entitlement occurrence cannot be counted twice.");
        if (alternatives.Any(a => a is null || a.Provenance is null) ||
            alternatives.Select(a => (a.Provenance, a.OriginalQualificationId)).Distinct().Count() != alternatives.Count)
            return Reject("character-equipment-alternative-invalid", "One alternate selection per duplicate target is permitted.");

        // Source-local replacements are already reflected in the earned
        // qualifications. Retain their provenance history without applying
        // them again or offering their retired targets as new grants.
        var prior = lifeReplacements ?? [];
        if (prior.Count > 100 || prior.Any(r => r is null || r.Provenance is null ||
            r.OriginalQualificationId == r.ReplacementQualificationId ||
            !lifeEntries.Any(x => x.Provenance == r.Provenance &&
                x.Qualification.QualificationId == r.ReplacementQualificationId)) ||
            prior.Select(r => (r.Provenance, r.OriginalQualificationId)).Distinct().Count() != prior.Count)
            return Reject("character-equipment-history-invalid", "Source-local replacement history must match earned targets.");

        var qualifications = entries.GroupBy(x => x.Qualification.QualificationId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => new NativeEquipmentQualification(g.Key,
                g.First().Qualification.EquipmentCategory, g.Select(x => x.Provenance)
                    .OrderBy(p => p.SourceOccurrenceId, StringComparer.Ordinal)
                    .ThenBy(p => p.GrantId, StringComparer.Ordinal).ToArray()), StringComparer.Ordinal);
        var baseline = life with { Qualifications = qualifications.Values.ToArray() };
        var originalTools = qualifications.Values.Where(q => q.EquipmentCategory == "tool" &&
            catalog.TryGetById(q.QualificationId, out var type) && type?.Category == "equipment-type").ToArray();

        var used = new HashSet<(NativeEquipmentQualificationProvenance Provenance, string Target)>();
        var replacements = new List<NativeLifeExperienceEquipmentReplacement>(prior);
        var required = new List<NativeCharacterEquipmentDuplicateChoice>();
        var occupied = new HashSet<string>(StringComparer.Ordinal);
        bool IsUncoveredTool(string id) =>
            catalog.TryGetById(id, out var entity) && entity?.Category == "equipment-type" &&
            entity.Definition.TryGetProperty("equipmentCategory", out var category) &&
            category.ValueKind == System.Text.Json.JsonValueKind.String && category.GetString() == "tool" &&
            resolver.Covers(baseline, id).Proficient == false && !occupied.Contains(id);
        foreach (var original in originalTools.OrderBy(q => q.QualificationId, StringComparer.Ordinal))
        {
            var selected = alternatives.Where(a => a.OriginalQualificationId == original.QualificationId &&
                original.Provenance.Contains(a.Provenance)).ToArray();
            if (selected.Length >= original.Provenance.Count)
                return Reject("character-equipment-alternative-excess",
                    "One source must retain the original specific-tool proficiency.");
            foreach (var replacement in selected)
            {
                var id = replacement.ReplacementQualificationId;
                if (string.IsNullOrWhiteSpace(id) || !IsUncoveredTool(id) || !occupied.Add(id) ||
                    !used.Add((replacement.Provenance, original.QualificationId)))
                    return Reject("character-equipment-alternative-ineligible",
                        "A replacement must be a distinct native specific tool not already covered.");
                qualifications.Add(id, new(id, "tool", [replacement.Provenance]));
                replacements.Add(replacement);
            }
            var remaining = original.Provenance.Where(p => !used.Contains((p, original.QualificationId))).ToArray();
            qualifications[original.QualificationId] = original with { Provenance = remaining };
            foreach (var duplicate in remaining.Skip(1))
                required.Add(new(duplicate, original.QualificationId, []));
        }
        if (used.Count != alternatives.Count)
            return Reject("character-equipment-alternative-unknown", "A replacement must identify an exact duplicate specific-tool grant.");
        if (required.Count != 0)
        {
            var eligible = new List<string>();
            for (var offset = 0; ; offset += 100)
            {
                var page = catalog.Search(category: "equipment-type", offset: offset, limit: 100);
                eligible.AddRange(page.Items.Select(x => x.Id).Where(IsUncoveredTool));
                if (offset + page.Items.Length >= page.Total) break;
            }
            var choices = eligible.Order(StringComparer.Ordinal).ToArray();
            return new("needs-choices", null, replacements,
                required.Select(c => c with { EligibleQualificationIds = choices }).ToArray(), []);
        }
        return new("resolved", baseline with
        {
            Qualifications = qualifications.Values.OrderBy(q => q.QualificationId, StringComparer.Ordinal).ToArray()
        }, replacements, [], []);
    }
}
