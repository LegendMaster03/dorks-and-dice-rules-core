using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A verified check context from the character/check-owning application. The
/// ordinary total already contains the ordinary proficiency contribution.
/// This is not a second general Skill calculator or a persisted training patch.
/// </summary>
public sealed record NativeScopedSkillCheckInput(
    string UpbringingId, IReadOnlyDictionary<string, string> SourceOccurrenceIds,
    string SkillId, string Ability, IReadOnlyList<string> ActivityScopeIds,
    int ProficiencyBonus, int OrdinaryProficiencyContribution, int OrdinaryTotal,
    bool NormallyProficient, bool CompetingProficiencyReplacement = false);

public sealed record NativeScopedSkillEffect(string SourceId, string OccurrenceId,
    string BenefitId, string Kind, int Amount);

public sealed record NativeScopedSkillCheckResult(string Status, int? Total,
    int? ProficiencyContribution, int? FlatBonus, bool? ConsideredProficientOnCheck,
    IReadOnlyList<NativeScopedSkillEffect> AppliedEffects, IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// Applies the explicitly authored Upbringing check-local effects to an
/// existing ordinary check result. The host must independently establish
/// the acquired source, actual task scope, ability and ordinary contribution.
/// </summary>
public sealed class NativeScopedSkillCheckResolver(NativeCatalogSnapshot catalog)
{
    public NativeScopedSkillCheckResult Resolve(NativeScopedSkillCheckInput input)
    {
        NativeScopedSkillCheckResult Fail(string code, string detail, string status = "unresolved") =>
            new(status, null, null, null, null, [], [new(code, "check", detail)]);

        if (input is null || input.SourceOccurrenceIds is null ||
            string.IsNullOrWhiteSpace(input.SkillId) || input.SkillId.Length > 160 ||
            !catalog.TryGetById(input.SkillId, out var skill) || skill?.Category != "skill" ||
            input.Ability is not ("strength" or "dexterity" or "constitution" or
                "intelligence" or "wisdom" or "charisma") ||
            input.ActivityScopeIds is null || input.ActivityScopeIds.Count > 16 ||
            input.ActivityScopeIds.Any(scope => string.IsNullOrWhiteSpace(scope) || scope.Length > 160) ||
            input.ActivityScopeIds.Distinct(StringComparer.Ordinal).Count() != input.ActivityScopeIds.Count ||
            input.ProficiencyBonus is < 0 or > 1000000 ||
            input.OrdinaryProficiencyContribution is < 0 or > 1000000 ||
            input.OrdinaryTotal is < -1000000 or > 1000000)
            return Fail("invalid-scoped-check", "A bounded, verified ordinary Skill check is required.", "invalid");

        var chain = NativeLifeExperienceSourceChain.Resolve(catalog, input.UpbringingId,
            "upbringing", out var issue);
        if (chain is null) return new("invalid", null, null, null, null, [], [issue!]);
        if (input.SourceOccurrenceIds.Count != chain.Count ||
            chain.Any(source => !input.SourceOccurrenceIds.TryGetValue(source.Id, out var occurrence) ||
                string.IsNullOrWhiteSpace(occurrence) || occurrence.Length > 160 || occurrence.Contains('/')) ||
            input.SourceOccurrenceIds.Values.Distinct(StringComparer.Ordinal).Count() != chain.Count)
            return Fail("unverified-scoped-check-source",
                "The exact current effective Upbringing chain and unique occurrences must be verified.", "invalid");

        var effects = new List<NativeScopedSkillEffect>();
        int? replacement = null;
        var flat = 0;
        var consideredProficient = input.NormallyProficient;
        foreach (var source in chain)
        {
            if (!source.Definition.TryGetProperty("benefits", out var benefits) ||
                benefits.ValueKind != JsonValueKind.Array)
                return Fail("invalid-scoped-benefits", "A native Upbringing benefit list is unavailable.");
            foreach (var benefit in benefits.EnumerateArray())
            {
                if (Text(benefit, "skill") != input.SkillId ||
                    !benefit.TryGetProperty("scope", out var scope)) continue;
                var requiredAbility = Text(benefit, "ability");
                if (requiredAbility is not null && requiredAbility != input.Ability) continue;
                // Match a verified activity, not the creature's mere location.
                string[] scopes;
                if (scope.ValueKind == JsonValueKind.String)
                    scopes = [scope.GetString()!];
                else if (scope.ValueKind == JsonValueKind.Array &&
                    scope.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String))
                    scopes = scope.EnumerateArray().Select(item => item.GetString()!).ToArray();
                else return Fail("invalid-scoped-benefit", "The authored task scope is unavailable.");
                if (!scopes.Any(id => input.ActivityScopeIds.Contains(id, StringComparer.Ordinal))) continue;
                var benefitId = Text(benefit, "id");
                if (string.IsNullOrWhiteSpace(benefitId))
                    return Fail("invalid-scoped-benefit", "The applicable benefit has no stable source identity.");
                var occurrence = input.SourceOccurrenceIds[source.Id];

                if (benefit.TryGetProperty("proficiencyBonusMultiplier", out var multiplier))
                {
                    // The two approved History effects are self-contained
                    // replacements, including when the check is untrained.
                    // Other multiplier/composition grammars are not inferred.
                    if (multiplier.ValueKind != JsonValueKind.Number ||
                        !multiplier.TryGetInt32(out var factor) || factor != 2 ||
                        Text(benefit, "proficiencyBonusApplication") != "replace-normal-proficiency-contribution" ||
                        !Flag(benefit, "appliesWithoutSkillProficiency") ||
                        benefit.TryGetProperty("bonus", out _))
                        return Fail("unsupported-scoped-replacement", "The authored replacement policy is unsupported.");
                    if (replacement is not null || input.CompetingProficiencyReplacement)
                        return Fail("scoped-replacement-overlap",
                            "Composition of multiple active proficiency replacements is not yet established.");
                    replacement = checked(input.ProficiencyBonus * factor);
                    consideredProficient |= Flag(benefit, "consideredProficientOnQualifyingCheck");
                    effects.Add(new(source.Id, occurrence, benefitId,
                        "replace-proficiency-contribution", replacement.Value));
                }
                else if (benefit.TryGetProperty("bonus", out var amount))
                {
                    if (amount.ValueKind != JsonValueKind.Number || !amount.TryGetInt32(out var bonus) ||
                        bonus != 2 || !Flag(benefit, "stacksWithTraining"))
                        return Fail("unsupported-scoped-flat-bonus", "The authored flat check modifier is unsupported.");
                    // The two current Survival effects belong to alternative
                    // selectable Upbringings; do not invent a new stacking rule.
                    if (flat != 0)
                        return Fail("scoped-flat-overlap", "Composition of multiple scoped flat modifiers is unresolved.");
                    flat = bonus;
                    effects.Add(new(source.Id, occurrence, benefitId, "flat-bonus", bonus));
                }
                else return Fail("unsupported-scoped-benefit", "No executable check contribution is authored.");
            }
        }
        var contribution = replacement ?? input.OrdinaryProficiencyContribution;
        var total = checked(input.OrdinaryTotal - input.OrdinaryProficiencyContribution + contribution + flat);
        return new("resolved", total, contribution, flat, consideredProficient,
            effects.ToArray(), []);
    }

    private static string? Text(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool Flag(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.True;
}
