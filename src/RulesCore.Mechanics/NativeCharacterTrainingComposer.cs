using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Independently earned Skill proficiency, not a rank allocation. Occurrence
/// identity must be stable and distinct across all character sources.
/// </summary>
public sealed record NativeCharacterSkillProficiencyGrant(
    string SourceId, string OccurrenceId, string SkillId);

public sealed record NativeCharacterSkillReplacement(
    string OccurrenceId, string OriginalSkillId, string ReplacementSkillId);

public sealed record NativeCharacterSkillDuplicateChoice(
    string OccurrenceId, string OriginalSkillId, IReadOnlyList<string> EligibleSkillIds);

public sealed record NativeCharacterTrainingComposition(
    string Status, IReadOnlyList<NativeCharacterSkillProficiencyGrant> SkillGrants,
    IReadOnlyList<NativeCharacterSkillReplacement> SkillReplacements,
    IReadOnlyList<NativeCharacterSkillDuplicateChoice> RequiredSkillChoices,
    NativeCompetencyGrantResult? Competencies,
    NativeCharacterProjection? Projection, IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// Combine already-authorized class, inherent, acquired and Life Experience
/// training occurrences. The primary request carries independently verified
/// allocated Skill ranks and ordinary check inputs, not previously aggregated
/// proficiency or Competency ranks. No source may be counted twice.
/// </summary>
public static class NativeCharacterTrainingComposer
{
    public static NativeCharacterTrainingComposition Compose(
        NativeCatalogSnapshot catalog, NativeCharacterRequest primary,
        NativeLifeExperienceEarnedProjection life,
        IReadOnlyList<NativeCharacterSkillProficiencyGrant> otherSkills,
        IReadOnlyList<NativeCompetencyTrainingGrant> otherCompetencies,
        IReadOnlyDictionary<string, string> skillAlternatives,
        IReadOnlyDictionary<string, string> competencyAlternatives,
        NativeCompetencyDuplicateProficiencyPolicy duplicatePolicy,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<NativeIssue>();
        NativeCharacterTrainingComposition Invalid(string code, string detail) =>
            new("invalid", [], [], [], null, null, [new(code, "training", detail)]);
        bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 512;
        bool SelectableSkill(string id) => catalog.TryGetById(id, out var entity) &&
            entity?.Category == "skill" &&
            !entity.Definition.TryGetProperty("childSkills", out _) &&
            (!entity.Definition.TryGetProperty("selectable", out var value) ||
                value.ValueKind != System.Text.Json.JsonValueKind.False);

        if (primary is null || life?.Grants is null || life.CompetencyGrants is null ||
            otherSkills is null || otherCompetencies is null ||
            skillAlternatives is null || competencyAlternatives is null ||
            otherSkills.Count + life.Grants.Count > 500 ||
            otherCompetencies.Count + life.CompetencyGrants.Count > 500 ||
            skillAlternatives.Count > 500 || competencyAlternatives.Count > 500 ||
            primary.Skills?.Any(p => p.Value is null || p.Value.Proficient) == true ||
            primary.Competencies?.Any(p => p.Value is null || p.Value.Proficient || p.Value.Ranks != 0) == true)
            return Invalid("training-composition-input-invalid",
                "Use bounded source occurrences and unaggregated primary training inputs.");

        // The source-local Life Experiences resolver has already applied its
        // authorized internal duplicate selections. Do not re-award its old
        // original targets. Include target ID for multi-target benefit paths.
        var skills = otherSkills.Concat(life.Grants.Where(g => g.Kind == "skill-training")
            .Select(g => new NativeCharacterSkillProficiencyGrant(g.SourceId,
                "life:" + g.OccurrenceId + ":" + g.GrantPath + ":" + (g.OriginalTargetId ?? g.TargetId),
                g.TargetId))).ToArray();
        var occurrences = new HashSet<string>(StringComparer.Ordinal);
        if (skills.Any(g => g is null || !ValidId(g.SourceId) || !ValidId(g.OccurrenceId) ||
            !ValidId(g.SkillId) || !SelectableSkill(g.SkillId) || !occurrences.Add(g.OccurrenceId)))
            return Invalid("training-skill-occurrence-invalid",
                "Each distinct earned Skill occurrence must target a selectable native Skill.");

        var occupied = skills.Select(g => g.SkillId).ToHashSet(StringComparer.Ordinal);
        var replacements = new List<NativeCharacterSkillReplacement>();
        var required = new List<NativeCharacterSkillDuplicateChoice>();
        var usedAlternatives = new HashSet<string>(StringComparer.Ordinal);
        var selected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in skills.GroupBy(g => g.SkillId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var entries = group.OrderBy(g => g.OccurrenceId, StringComparer.Ordinal).ToArray();
            var supplied = entries.Where(g => skillAlternatives.ContainsKey(g.OccurrenceId)).ToArray();
            if (supplied.Length > entries.Length - 1)
                return Invalid("training-skill-alternative-excess", "One proficiency must retain each original Skill.");
            foreach (var grant in supplied)
            {
                var alternative = skillAlternatives[grant.OccurrenceId];
                if (!ValidId(alternative) || !SelectableSkill(alternative) || !occupied.Add(alternative))
                    return Invalid("training-skill-alternative-invalid",
                        "A duplicate proficiency alternative must be a distinct unproficient native Skill.");
                usedAlternatives.Add(grant.OccurrenceId);
                selected.Add(grant.OccurrenceId, alternative);
                replacements.Add(new(grant.OccurrenceId, grant.SkillId, alternative));
            }
            // Discovery is deterministic, but the owning application can
            // choose which actual duplicate occurrence receives the alternative.
            foreach (var grant in entries.Where(g => !selected.ContainsKey(g.OccurrenceId)).Skip(1))
                required.Add(new(grant.OccurrenceId, grant.SkillId, []));
        }
        if (usedAlternatives.Count != skillAlternatives.Count)
            return Invalid("training-skill-alternative-unknown", "An alternative references an unknown occurrence.");
        if (required.Count > 0)
        {
            var eligible = catalog.Search(null, "skill", 0, 1000).Items
                .Select(entity => entity.Id).Where(id => SelectableSkill(id) && !occupied.Contains(id))
                .Order(StringComparer.Ordinal).ToArray();
            return new("needs-choices", [], replacements, required.Select(choice =>
                choice with { EligibleSkillIds = eligible }).ToArray(), null, null, []);
        }

        // Reuse the common Competency reducer, including rank vs breadth
        // duplicate policies. The host authenticates cross-source alternatives.
        var competency = otherCompetencies.Concat(life.CompetencyGrants).ToArray();
        if (competency.Any(g => g is null || g.DuplicateProficiencyAlternativeCompetencyId is not null) ||
            competencyAlternatives.Keys.Any(key => !competency.Any(g => g.OccurrenceId == key)))
            return Invalid("training-competency-alternative-invalid",
                "Supply duplicate Competency choices once, against earned occurrences.");
        var combined = NativeCompetencyTraining.CombineValidated(catalog,
            competency.Select(g => g with
            {
                DuplicateProficiencyAlternativeCompetencyId = competencyAlternatives.GetValueOrDefault(g.OccurrenceId)
            }).ToArray(), duplicatePolicy);
        if (combined.Status != "resolved")
            return new(combined.Status, [], replacements, [], combined, null, combined.Issues);

        var skillMap = (primary.Skills ?? new Dictionary<string, NativeSkillTraining>())
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var id in skills.Select(g => selected.GetValueOrDefault(g.OccurrenceId) ?? g.SkillId))
            skillMap[id] = (skillMap.GetValueOrDefault(id) ?? new()) with { Proficient = true };
        var competencyMap = (primary.Competencies ?? new Dictionary<string, NativeSkillTraining>())
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var (id, training) in combined.Training)
            competencyMap[id] = (competencyMap.GetValueOrDefault(id) ?? new()) with
            { Ranks = training.Ranks, Proficient = training.Proficient };

        var projection = new NativeCharacterProjector(catalog).Project(primary with
        { Skills = skillMap, Competencies = competencyMap }, cancellationToken);
        // The existing kernel still rejects a Skill with simultaneous rank
        // and proficiency facets. Do not override that unresolved policy here.
        return new(projection.Status == "invalid" ? "invalid" : "resolved", skills,
            replacements, [], combined, projection, projection.Issues);
    }
}
