using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

internal sealed record NativeLifeExperienceEquipmentBranch(string BenefitId, string OptionId);

/// <summary>
/// Independent cultural and experiential selections, not biological genes.
/// Selection keys refer to authored paths; a client never supplies grant
/// amounts, source occurrence receipts or completed inventory records.
/// </summary>
public sealed record NativeLifeExperienceProposal(
    string UpbringingId, string BackgroundId,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Selections = null,
    IReadOnlyDictionary<string, string>? StartingItemIds = null);

public sealed record NativeLifeExperienceSource(string SourceId, string OccurrenceId, bool Inherited);
public sealed record NativeLifeExperienceChoice(string Key, string Kind, int Count,
    IReadOnlyList<string> OptionIds, string? ContextConstraint = null);
public sealed record NativeLifeExperienceSelectedPath(string SourceId, string OccurrenceId,
    string BenefitId, string OptionId);
public sealed record NativeLifeExperienceGrant(string SourceId, string OccurrenceId,
    string BenefitId, string? OptionId, string Kind, string TargetId,
    int Ranks = 0, bool Proficient = false, JsonElement? AuthoredEffect = null,
    string? GrantPath = null, string? OriginalTargetId = null);
public sealed record NativeLifeExperienceItemIntent(string Key, string Name, int Quantity,
    IReadOnlyList<string> OrdinaryOptions, string? ExpectedEquipmentTypeId,
    bool RequiresMundane, bool RequiresInexpensive, string? SelectedItemId,
    string? VerifiedItemId = null);
public sealed record NativeLifeExperienceCurrency(string SourceId, string OccurrenceId,
    string Currency, int Quantity);

/// <summary>
/// The host verifies a real ordinary-inventory definition against the complete
/// source intent (including name/options and qualitative requirements). A
/// canonical base equipment type is not itself an ordinary inventory item.
/// </summary>
public sealed record NativeLifeExperienceItemProof(string ItemId, string? EquipmentTypeId,
    bool Mundane, bool Inexpensive);
public interface INativeLifeExperienceContextAuthority
{
    bool AuthorizesChoice(NativeLifeExperienceSource source,
        NativeLifeExperienceChoice choice, string optionId);
    NativeLifeExperienceItemProof? VerifyItem(NativeLifeExperienceSource source,
        NativeLifeExperienceItemIntent intent);
}

public sealed record NativeLifeExperienceAuthorization(
    IReadOnlyDictionary<string, string> SourceOccurrenceIds,
    INativeLifeExperienceContextAuthority ContextAuthority);

/// <summary>
/// Candidate outputs are never usable earned grants. The earned projection
/// is present only for a complete, independently authorized source/choice/item
/// resolution. Applying it with other character sources and committing the
/// resulting inventory and grants remain an atomic application operation.
/// </summary>
public sealed record NativeLifeExperienceEarnedProjection(
    IReadOnlyList<NativeLifeExperienceGrant> Grants,
    IReadOnlyList<NativeCompetencyTrainingGrant> CompetencyGrants,
    NativeEquipmentQualificationProjection Equipment,
    IReadOnlyList<NativeLifeExperienceItemIntent> StartingItems,
    IReadOnlyList<NativeLifeExperienceCurrency> Currency);

public sealed record NativeLifeExperienceResult(
    string Status, IReadOnlyList<NativeLifeExperienceSource> Sources,
    IReadOnlyList<NativeLifeExperienceChoice> Choices,
    IReadOnlyList<NativeLifeExperienceSelectedPath> SelectedPaths,
    IReadOnlyList<NativeLifeExperienceGrant> CandidateGrants,
    NativeEquipmentQualificationProjection? CandidateEquipment,
    IReadOnlyList<NativeLifeExperienceItemIntent> StartingItems,
    IReadOnlyList<NativeLifeExperienceCurrency> Currency,
    IReadOnlyList<NativeIssue> Issues,
    NativeLifeExperienceEarnedProjection? EarnedProjection);

/// <summary>
/// Source-local selection and grant derivation over the native snapshot. The
/// engine shares the existing equipment qualification evaluator and preserves
/// exact competency rank/proficiency occurrences for the common reducer.
/// It does not merge these grants into an existing character or infer effects
/// from narrative text.
/// </summary>
public sealed class NativeLifeExperienceResolver(NativeCatalogSnapshot catalog)
{
    public static bool ValidProposal(NativeLifeExperienceProposal? proposal) =>
        proposal is not null && ValidId(proposal.UpbringingId) && ValidId(proposal.BackgroundId) &&
        (proposal.Selections?.Count ?? 0) <= 100 &&
        (proposal.StartingItemIds?.Count ?? 0) <= 100 &&
        (proposal.Selections?.All(pair => ValidKey(pair.Key) && pair.Value is { Count: <= 32 } &&
            pair.Value.All(ValidId)) ?? true) &&
        (proposal.StartingItemIds?.All(pair => ValidKey(pair.Key) && ValidId(pair.Value)) ?? true);

    public NativeLifeExperienceResult Resolve(NativeLifeExperienceProposal proposal,
        NativeLifeExperienceAuthorization? authorization = null)
    {
        var sources = new List<NativeLifeExperienceSource>();
        var choices = new List<NativeLifeExperienceChoice>();
        var selectedPaths = new List<NativeLifeExperienceSelectedPath>();
        var grants = new List<NativeLifeExperienceGrant>();
        var items = new List<NativeLifeExperienceItemIntent>();
        var currency = new List<NativeLifeExperienceCurrency>();
        var issues = new List<NativeIssue>();
        var seenChoices = new HashSet<string>(StringComparer.Ordinal);
        var seenItems = new HashSet<string>(StringComparer.Ordinal);
        var definitions = new List<NativeRuleEntity>();
        var activeEquipment = new Dictionary<string, IReadOnlyList<NativeLifeExperienceEquipmentBranch>>(StringComparer.Ordinal);
        var equipmentSources = new List<NativeEquipmentQualificationSource>();
        NativeEquipmentQualificationProjection? equipment = null;
        var invalid = false;
        var unavailable = false;
        var needsChoices = false;
        void Invalid(string code, string path, string detail)
        { invalid = true; issues.Add(new(code, path, detail)); }
        void Unavailable(string code, string path, string detail)
        { unavailable = true; issues.Add(new(code, path, detail)); }

        NativeLifeExperienceResult Finish()
        {
            var status = invalid ? "invalid" : needsChoices ? "needs-choices" :
                unavailable ? "unavailable" : "resolved";
            // No partial earned projection. Stateless calculation also never
            // authorizes acquisition, even when every choice is decidable.
            var earned = status == "resolved" && authorization is not null && equipment?.Status == "resolved"
                ? new NativeLifeExperienceEarnedProjection(grants.ToArray(),
                    grants.Where(grant => grant.Kind == "competency-training")
                        .Select(grant => new NativeCompetencyTrainingGrant(grant.SourceId,
                            CompetencyOccurrence(grant), grant.TargetId, grant.Ranks, grant.Proficient)).ToArray(),
                    equipment, items.ToArray(), currency.ToArray()) : null;
            return new(status, sources.ToArray(), choices.ToArray(), selectedPaths.ToArray(),
                invalid ? [] : grants.ToArray(), invalid ? null : equipment,
                invalid ? [] : items.ToArray(), invalid ? [] : currency.ToArray(),
                issues.ToArray(), earned);
        }

        if (!ValidProposal(proposal))
        {
            Invalid("invalid-life-experience-proposal", "proposal", "Bounded native selections are required.");
            return Finish();
        }

        // Only the selected Upbringing's actual cultural parent chain is
        // traversed. A Species default is never a second source.
        AddChain(proposal.UpbringingId, "upbringing");
        AddChain(proposal.BackgroundId, "background");
        if (invalid) return Finish();

        if (authorization is not null &&
            (authorization.ContextAuthority is null || authorization.SourceOccurrenceIds is null ||
             authorization.SourceOccurrenceIds.Count != definitions.Count ||
             definitions.Any(entity => !authorization.SourceOccurrenceIds.TryGetValue(entity.Id, out var occurrence) ||
                 !ValidId(occurrence) || occurrence.Contains('/')) ||
             authorization.SourceOccurrenceIds.Values.Distinct(StringComparer.Ordinal).Count() != definitions.Count))
        {
            Invalid("life-experience-source-authority-invalid", "sources",
                "Every selected and inherited source requires one independently verified occurrence.");
            return Finish();
        }

        foreach (var entity in definitions)
        {
            var source = new NativeLifeExperienceSource(entity.Id,
                authorization?.SourceOccurrenceIds[entity.Id] ?? entity.Id,
                entity.Id != proposal.UpbringingId && entity.Id != proposal.BackgroundId);
            sources.Add(source);
            var branches = new List<NativeLifeExperienceEquipmentBranch>();
            var seenBenefits = new HashSet<string>(StringComparer.Ordinal);
            foreach (var benefit in Array(entity.Definition, "benefits"))
            {
                var benefitId = Text(benefit, "id");
                if (!ValidPart(benefitId) || !seenBenefits.Add(benefitId!))
                {
                    Unavailable("life-experience-benefit-invalid", entity.Id, "Native benefit IDs must be unique.");
                    continue;
                }
                var path = entity.Id + "/" + benefitId;
                if (Text(benefit, "grantType") == "benefit-choice")
                {
                    var options = Array(benefit, "options");
                    var optionIds = options.Select(option => Text(option, "id") ?? "").ToArray();
                    if (optionIds.Any(id => !ValidPart(id)) ||
                        optionIds.Distinct(StringComparer.Ordinal).Count() != optionIds.Length ||
                        Number(benefit, "choiceCount") != 1)
                    {
                        Unavailable("life-experience-option-definition-invalid", path,
                            "A supported benefit choice must declare exactly one distinct option.");
                        continue;
                    }
                    var choice = Select(new(path, "benefit-option", 1, optionIds));
                    if (choice is not { Count: 1 }) continue;
                    var optionId = choice[0];
                    var option = options.Single(x => Text(x, "id") == optionId);
                    selectedPaths.Add(new(source.SourceId, source.OccurrenceId, benefitId!, optionId));
                    if (option.TryGetProperty("equipmentProficiencyGrants", out _))
                        branches.Add(new(benefitId!, optionId));
                    var children = Array(option, "benefits");
                    for (var i = 0; i < children.Length; i++)
                    {
                        var childPath = path + "/" + optionId +
                            (children.Length == 1 ? "" : "/" + i);
                        Benefit(source, benefitId!, optionId, childPath, children[i]);
                    }
                }
                else Benefit(source, benefitId!, null, path, benefit);
            }
            if (entity.Definition.TryGetProperty("equipmentProficiencyGrants", out _) || branches.Count != 0)
            {
                equipmentSources.Add(new(entity.Id, source.OccurrenceId));
                activeEquipment.Add(source.OccurrenceId, branches);
            }
        }

        // First ask the common resolver for its exact active choice keys and
        // native equipment membership. Then translate user keys to verified
        // occurrences. No unselected nested equipment block reaches it.
        var equipmentResolver = new NativeEquipmentQualifications(catalog);
        var discovery = equipmentResolver.ResolveLifeExperiences(
            new(catalog.RulesetId, catalog.Fingerprint, equipmentSources), activeEquipment);
        var selectedEquipment = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var choice in discovery.RequiredChoices)
        {
            var separator = choice.ChoiceKey.IndexOf('/');
            var source = sources.Single(s => s.OccurrenceId == choice.ChoiceKey[..separator]);
            var key = source.SourceId + "/equipment/" + choice.ChoiceKey[(separator + 1)..];
            var selection = Select(new(key, "equipment-type", choice.Count,
                choice.EligibleQualificationIds));
            if (selection is not null) selectedEquipment.Add(choice.ChoiceKey, selection);
        }
        equipment = equipmentResolver.ResolveLifeExperiences(
            new(catalog.RulesetId, catalog.Fingerprint, equipmentSources, selectedEquipment), activeEquipment);
        foreach (var issue in equipment.Issues)
            Unavailable(issue.Code, issue.Path, issue.Detail);
        if (equipment.Status == "needs-choices") needsChoices = true;

        // Skill duplicates are distinct earned occurrences, not extra ranks.
        // Reserve all original targets first; assign the replacement to the
        // lexically later source path so replay/input order never changes it.
        // The character controller must still combine other earned sources.
        var skillTargets = grants.Where(g => g.Kind == "skill-training")
            .Select(g => g.TargetId).ToHashSet(StringComparer.Ordinal);
        var duplicateSkills = grants.Select((grant, index) => (grant, index))
            .Where(x => x.grant.Kind == "skill-training")
            .GroupBy(x => x.grant.TargetId, StringComparer.Ordinal)
            .SelectMany(group => group.OrderBy(x => x.grant.GrantPath, StringComparer.Ordinal).Skip(1))
            .OrderBy(x => x.grant.GrantPath, StringComparer.Ordinal).ToArray();
        foreach (var (grant, index) in duplicateSkills)
        {
            var eligible = All("skill").Where(entity => Eligible(entity.Id, "skill") &&
                !skillTargets.Contains(entity.Id)).Select(entity => entity.Id).ToArray();
            var choice = new NativeLifeExperienceChoice(grant.GrantPath + "/duplicate-skill",
                "duplicate-skill", 1, eligible);
            var alternative = Select(choice);
            if (alternative is { Count: 1 })
            {
                skillTargets.Add(alternative[0]);
                grants[index] = grant with { TargetId = alternative[0], OriginalTargetId = grant.TargetId };
            }
        }
        // Do not invent a tool-kind membership policy for duplicate tools.
        // They stay separate from Competencies and retain source provenance.
        foreach (var qualification in equipment.Qualifications.Where(q =>
            q.EquipmentCategory == "tool" && q.Provenance.Count > 1))
            Unavailable("duplicate-tool-alternative-required", qualification.QualificationId,
                "Independent duplicate tool proficiency needs an authorized same-kind alternative.");

        foreach (var source in sources)
        {
            var entity = definitions.Single(x => x.Id == source.SourceId);
            if (!entity.Definition.TryGetProperty("startingEquipment", out var package)) continue;
            if (package.ValueKind != JsonValueKind.Object)
            {
                Unavailable("starting-equipment-invalid", source.SourceId, "Native starting equipment must be an object.");
                continue;
            }
            var authoredItems = Array(package, "items");
            for (var index = 0; index < authoredItems.Length; index++)
            {
                var raw = authoredItems[index];
                var key = source.SourceId + "/item/" + index;
                var name = Text(raw, "name");
                var quantity = Number(raw, "quantity");
                if (string.IsNullOrWhiteSpace(name) || quantity is null or < 1 or > 10000)
                {
                    Unavailable("starting-item-invalid", key, "Native starting item lacks a valid description or quantity.");
                    continue;
                }
                var itemRule = raw;
                var ordinaryOptions = Strings(raw, "options");
                if (Text(raw, "sourceBenefitChoiceId") is string outer)
                {
                    var path = selectedPaths.SingleOrDefault(x => x.SourceId == source.SourceId &&
                        x.BenefitId == outer);
                    if (path is null) { needsChoices = true; continue; }
                    var variants = Array(raw, "conditionalItemChoices").Where(x =>
                        Text(x, "whenOptionId") == path.OptionId).ToArray();
                    if (variants.Length != 1)
                    {
                        Unavailable("starting-item-branch-invalid", key, "Native item branch does not match selected training.");
                        continue;
                    }
                    itemRule = variants[0];
                    ordinaryOptions = Strings(itemRule, "ordinaryItemOptions");
                }
                string? expectedType = null;
                if (Text(itemRule, "equipmentTypeChoiceId") is string typeChoice)
                {
                    if (!selectedEquipment.TryGetValue(source.OccurrenceId + "/" + typeChoice, out var selection) ||
                        selection.Count != 1)
                    { needsChoices = true; continue; }
                    expectedType = selection[0];
                }
                var requirements = itemRule.TryGetProperty("itemRequirements", out var req) ? req : default;
                seenItems.Add(key);
                var submitted = proposal.StartingItemIds?.GetValueOrDefault(key);
                // All currently authored Background starting possessions are
                // ordinary items. A verified application may resolve a fixed
                // item without making the player manually choose its ID.
                var intent = new NativeLifeExperienceItemIntent(key, name!, quantity.Value,
                    ordinaryOptions, expectedType, true,
                    Flag(requirements, "inexpensive"), submitted);
                var proof = authorization?.ContextAuthority.VerifyItem(source, intent);
                if (proof is null && submitted is null && ordinaryOptions.Length > 1)
                {
                    needsChoices = true;
                    issues.Add(new("starting-item-selection-required", key,
                        "Select one permitted ordinary item."));
                }
                else if (proof is null || !ValidId(proof.ItemId) ||
                    (submitted is not null && proof.ItemId != submitted) ||
                    (expectedType is not null && proof.EquipmentTypeId != expectedType) ||
                    (intent.RequiresMundane && !proof.Mundane) ||
                    (intent.RequiresInexpensive && !proof.Inexpensive))
                    Unavailable("starting-item-unverified", key,
                        "An independent inventory authority must verify the exact ordinary item and any training/type binding.");
                else intent = intent with { VerifiedItemId = proof.ItemId };
                items.Add(intent);
            }
            if (package.TryGetProperty("currency", out var money))
            {
                if (money.ValueKind != JsonValueKind.Object)
                    Unavailable("starting-currency-invalid", source.SourceId, "Native currency must be a keyed quantity.");
                else foreach (var entry in money.EnumerateObject())
                    if (entry.Value.ValueKind == JsonValueKind.Number &&
                        entry.Value.TryGetInt32(out var quantity) && quantity >= 0)
                        currency.Add(new(source.SourceId, source.OccurrenceId, entry.Name, quantity));
                    else Unavailable("starting-currency-invalid", source.SourceId, "Native currency quantity is invalid.");
            }
        }

        // A stale selection from a previously selected option is not an
        // entitlement. Keys for currently unresolved branches are not active.
        foreach (var key in proposal.Selections?.Keys ?? [])
            if (!seenChoices.Contains(key)) Invalid("unexpected-life-experience-choice", key,
                "Selection does not identify an active authored choice.");
        foreach (var key in proposal.StartingItemIds?.Keys ?? [])
            if (!seenItems.Contains(key)) Invalid("unexpected-starting-item-choice", key,
                "Item selection does not identify an active authored starting item.");

        return Finish();

        void AddChain(string id, string category)
        {
            var chain = NativeLifeExperienceSourceChain.Resolve(catalog, id, category, out var issue);
            if (chain is null)
                Invalid(issue!.Code, issue.Path, issue.Detail);
            else definitions.AddRange(chain);
        }

        IReadOnlyList<string>? Select(NativeLifeExperienceChoice choice, bool open = false)
        {
            if (!seenChoices.Add(choice.Key) || choice.Count is < 1 or > 32)
            {
                Unavailable("invalid-life-experience-choice-definition", choice.Key, "Native choice identity or count is invalid.");
                return null;
            }
            choices.Add(choice);
            if (proposal.Selections?.TryGetValue(choice.Key, out var selected) != true)
            { needsChoices = true; return null; }
            if (selected.Count != choice.Count || selected.Distinct(StringComparer.Ordinal).Count() != selected.Count ||
                (!open && selected.Any(x => !choice.OptionIds.Contains(x, StringComparer.Ordinal))))
            {
                Invalid("invalid-life-experience-selection", choice.Key, "Select the required number of distinct eligible options.");
                return null;
            }
            return selected;
        }

        void Benefit(NativeLifeExperienceSource source, string benefitId, string? optionId,
            string path, JsonElement benefit)
        {
            if (benefit.ValueKind != JsonValueKind.Object)
            { Unavailable("invalid-life-experience-benefit", path, "Native benefit must be an object."); return; }
            var kind = Text(benefit, "grantType");
            var constraint = Text(benefit, "choiceConstraint");
            void Grant(string domain, string target, int ranks = 0, bool proficient = false,
                JsonElement? effect = null) =>
                grants.Add(new(source.SourceId, source.OccurrenceId, benefitId, optionId,
                    domain, target, ranks, proficient, effect, path));
            if (kind is "skill-training" or "competency-training")
            {
                var domain = kind == "skill-training" ? "skill" : "competency";
                var ids = Strings(benefit, "chooseOne").Concat(Strings(benefit, "chooseFrom")).ToArray();
                if (Text(benefit, "choiceCategoryId") is string categoryId)
                {
                    if (!catalog.TryGetById(categoryId, out var category) ||
                        category?.Category != "competency-category")
                    { Unavailable("life-experience-category-unavailable", path, "Native choice category is unavailable."); return; }
                    ids = Strings(category.Definition, "competencyIds");
                }
                IReadOnlyList<string>? targets;
                if (ids.Length > 0)
                {
                    var eligible = ids.Where(id => Eligible(id, domain)).Distinct(StringComparer.Ordinal).ToArray();
                    var choice = new NativeLifeExperienceChoice(path, domain, Number(benefit, "choiceCount") ?? 0,
                        eligible, constraint);
                    targets = Select(choice);
                    if (targets is not null && constraint is not null &&
                        targets.Any(id => authorization?.ContextAuthority.AuthorizesChoice(source, choice, id) != true))
                    { Unavailable("contextual-choice-unverified", path, "Source-specific eligibility needs independent authority."); return; }
                }
                else targets = Text(benefit, domain) is string fixedId ? [fixedId] : null;
                if (targets is null) return;
                var ranks = 0;
                var proficient = true;
                if (domain == "competency")
                {
                    if (!benefit.TryGetProperty("competencyTraining", out var training) ||
                        training.ValueKind != JsonValueKind.Object ||
                        Number(training, "ranks") is not int rank || rank < 0 ||
                        !training.TryGetProperty("proficient", out var prof) ||
                        prof.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                        (rank == 0 && prof.ValueKind == JsonValueKind.False))
                    { Unavailable("competency-grant-unavailable", path, "Native ranks and proficiency must be explicitly authored."); return; }
                    ranks = rank;
                    proficient = prof.GetBoolean();
                }
                foreach (var target in targets)
                    if (Eligible(target, domain)) Grant(kind, target, ranks, proficient);
                    else Unavailable("life-experience-target-unavailable", path, "Native training target is unavailable.");
                return;
            }
            if (Text(benefit, "language") is string language)
            { Grant("language", language); return; }
            if (Number(benefit, "extraLanguages") is int count)
            {
                // The current native language catalog is incomplete. Open
                // language identity and the source-specific context must be
                // verified by the authoritative application, not guessed.
                var choice = new NativeLifeExperienceChoice(path, "language", count,
                    All("language").Select(x => x.Id).ToArray(), constraint);
                var selected = Select(choice, open: true);
                if (selected is not null)
                    foreach (var target in selected)
                        if (authorization?.ContextAuthority.AuthorizesChoice(source, choice, target) == true)
                            Grant("language", target);
                        else Unavailable("language-choice-unverified", path,
                            "A learned language requires a verified identity and applicable source context.");
                return;
            }
            if (kind == "qualification-training")
            {
                if (Text(benefit, "qualificationCategory") == "vehicle")
                {
                    Grant("vehicle-qualification", Text(benefit, "vehicleType") ?? "", effect: benefit);
                    Unavailable("vehicle-qualification-execution-unavailable", path,
                        "Vehicle operator training remains separate from equipment proficiency.");
                }
                // Other equipment training is interpreted exactly once from
                // its root or selected nested equipmentProficiencyGrants.
                return;
            }
            if (Text(benefit, "skill") is string scopedSkill && benefit.TryGetProperty("scope", out _))
            {
                if (Eligible(scopedSkill, "skill"))
                    Grant("scoped-skill-effect", scopedSkill, effect: benefit);
                else Unavailable("scoped-skill-unavailable", path, "Scoped Skill identity is unavailable.");
                return;
            }
            if (Text(benefit, "spell") is string spell)
            {
                var selected = Select(new(path, "spellcasting-ability", 1, Strings(benefit, "spellcastingAbilityChoice")));
                if (selected is { Count: 1 })
                    Grant("learned-spell-reference", spell, effect: JsonSerializer.SerializeToElement(
                        new { spellcastingAbility = selected[0], authoredBenefit = benefit }));
                Unavailable("learned-spell-execution-unavailable", path,
                    "The learned spell reference is not a verified native spell grant.");
                return;
            }
            // Preserve conditional/manual features without turning their prose
            // into an unconditional numeric effect or extra inventory grant.
            Grant("manual-feature", benefitId, effect: benefit);
        }
    }

    // The shared competency reducer requires a unique grant occurrence,
    // not the enclosing source occurrence reused by two different benefits.
    // Hash a structured identity; retain its human-readable provenance in Grants.
    private static string CompetencyOccurrence(NativeLifeExperienceGrant grant) =>
        "life-experience:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(new { grant.SourceId, grant.OccurrenceId,
                grant.GrantPath, grant.TargetId }))).ToLowerInvariant();

    private bool Eligible(string id, string category) =>
        catalog.TryGetById(id, out var entity) && entity?.Category == category &&
        (!entity.Definition.TryGetProperty("selectable", out var flag) || flag.ValueKind != JsonValueKind.False);

    private IEnumerable<NativeRuleEntity> All(string category)
    {
        for (var offset = 0; ; offset += 100)
        {
            var page = catalog.Search(category: category, offset: offset, limit: 100);
            foreach (var item in page.Items) yield return item;
            if (offset + page.Items.Length >= page.Total) yield break;
        }
    }

    private static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 160 && value == value.Trim();
    private static bool ValidPart(string? value) => ValidId(value) && !value!.Contains('/');
    private static bool ValidKey(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 500 && value == value.Trim();
    private static string? Text(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var item) &&
        item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    private static int? Number(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var item) &&
        item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value) ? value : null;
    private static bool Flag(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var item) &&
        item.ValueKind == JsonValueKind.True;
    private static JsonElement[] Array(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var item) &&
        item.ValueKind == JsonValueKind.Array ? item.EnumerateArray().ToArray() : [];
    private static string[] Strings(JsonElement obj, string key) =>
        Array(obj, key).Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!).ToArray();
}
