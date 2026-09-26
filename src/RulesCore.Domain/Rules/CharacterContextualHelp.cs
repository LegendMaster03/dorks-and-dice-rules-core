namespace RulesCore.Domain.Rules;

public static class CharacterHelpProminence
{
    public const string Standard = "standard";
    public const string Prominent = "prominent";

    public static IReadOnlyList<string> All { get; } = [Standard, Prominent];
}

public sealed record CharacterContextualHelpDefinition(
    string TopicKey,
    string DisplayName,
    string ShortText,
    string? FullText,
    string Prominence);

/// <summary>
/// Player-facing semantic help owned by Rules Core. Entries are keyed by stable
/// mechanic or semantic topic identities rather than consumer labels.
/// </summary>
public static class KnownCharacterContextualHelp
{
    private static readonly IReadOnlyDictionary<string, CharacterContextualHelpDefinition> Definitions =
        new Dictionary<string, CharacterContextualHelpDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["defense.ac.total"] = Standard(
                "defense.ac.total",
                "Armor Class",
                "Armor Class is the defense an attack normally needs to meet or exceed to hit you."),
            ["health.maximum-hp"] = Standard(
                "health.maximum-hp",
                "Hit Points",
                "Hit Points measure how much damage you can take before the rules for reaching 0 HP apply."),
            ["combat.initiative"] = Standard(
                "combat.initiative",
                "Initiative",
                "Initiative determines when you act in combat or another ordered encounter."),
            ["proficiency.standard"] = Standard(
                "proficiency.standard",
                "Proficiency Bonus",
                "Proficiency Bonus is added when the effective rules say your training or proficiency applies."),
            ["defense.ac.touch"] = Prominent(
                "defense.ac.touch",
                "Touch Armor Class",
                "Touch Armor Class is used when a rule says an attack only needs to make physical contact; armor and similar protection may not contribute normally.",
                "Use Touch Armor Class only when the source rule or normalized mechanic targets it. A magical attack or spell attack does not target Touch Armor Class merely because it is magical."),
            ["defense.ac.flat-footed"] = Prominent(
                "defense.ac.flat-footed",
                "Flat-Footed Armor Class",
                "Flat-Footed Armor Class is used when a rule says the defender is flat-footed or can not apply the relevant Dexterity-based defenses.",
                "Being flat-footed is not identical to every rule that denies a Dexterity bonus to Armor Class. Preserve the state and defensive consequence named by the source rule because exceptions can depend on that distinction."),
            ["combat.base-attack-bonus"] = Prominent(
                "combat.base-attack-bonus",
                "Base Attack Bonus",
                "Base Attack Bonus is the class-derived attack progression used by rules that call for BAB, before ability scores and other modifiers."),
            ["combat.grapple"] = Prominent(
                "combat.grapple",
                "Grapple Modifier",
                "Grapple Modifier is used by rules that resolve a 3.x-style grapple check, including the grapple-specific size modifier."),
            ["defense.damage-reduction"] = Prominent(
                "defense.damage-reduction",
                "Damage Reduction",
                "Damage Reduction reduces qualifying incoming damage according to the rule that grants it; the rule may also specify what bypasses it."),
            ["defense.spell-resistance"] = Prominent(
                "defense.spell-resistance",
                "Spell Resistance",
                "Spell Resistance is checked only when the spell or effect says that spell resistance applies."),
            ["resource.nonlethal-damage"] = Prominent(
                "resource.nonlethal-damage",
                "Nonlethal Damage",
                "Nonlethal damage is tracked separately when the effective rules distinguish damage intended to incapacitate without killing."),
            ["defense.miss-chance"] = Prominent(
                "defense.miss-chance",
                "Miss Chance",
                "Miss Chance is a separate avoidance check used only when the effective rule gives an attack or target a miss chance."),
            ["defense.concealment"] = Prominent(
                "defense.concealment",
                "Concealment",
                "Concealment represents obscured or uncertain targeting when the effective rule says concealment applies; use the consequence specified by that rule."),
            ["competency.armor-check-penalty"] = Prominent(
                "competency.armor-check-penalty",
                "Armor Check Penalty",
                "Armor Check Penalty is applied to a competency only when that competency's effective rules say the penalty applies."),
            ["competency.class-skill"] = Prominent(
                "competency.class-skill",
                "Class Skill",
                "Class Skill records whether the effective rules treat this competency as a class skill for the character."),
            ["competency.trained-only"] = Prominent(
                "competency.trained-only",
                "Trained Only",
                "A Trained Only competency can be attempted only when the effective rules say the character has the required training.")
        };

    public static IReadOnlyCollection<CharacterContextualHelpDefinition> All =>
        Definitions.Values.ToArray();

    public static CharacterContextualHelpDefinition? FindByTopicKey(string? topicKey)
    {
        if (string.IsNullOrWhiteSpace(topicKey))
        {
            return null;
        }

        var key = topicKey.Trim();
        if (Definitions.TryGetValue(key, out var exact))
        {
            return exact;
        }

        if (key.StartsWith("ability.", StringComparison.OrdinalIgnoreCase)
            && key.EndsWith(".score", StringComparison.OrdinalIgnoreCase))
        {
            return Standard(
                key,
                "Ability Score",
                "An ability score represents a core character capability and is the basis for its corresponding ability modifier.");
        }

        if (key.StartsWith("ability.", StringComparison.OrdinalIgnoreCase)
            && key.EndsWith(".modifier", StringComparison.OrdinalIgnoreCase))
        {
            return Standard(
                key,
                "Ability Modifier",
                "An ability modifier is the adjustment derived from an ability score and applied when a rule calls for that ability.");
        }

        if (key.StartsWith("save.", StringComparison.OrdinalIgnoreCase))
        {
            return Standard(
                key,
                "Saving Throw",
                "A saving throw is a defensive check used when a rule calls for that specific save.");
        }

        return null;
    }

    private static CharacterContextualHelpDefinition Standard(
        string topicKey,
        string displayName,
        string shortText,
        string? fullText = null) =>
        new(topicKey, displayName, shortText, fullText, CharacterHelpProminence.Standard);

    private static CharacterContextualHelpDefinition Prominent(
        string topicKey,
        string displayName,
        string shortText,
        string? fullText = null) =>
        new(topicKey, displayName, shortText, fullText, CharacterHelpProminence.Prominent);
}

public sealed record CharacterAttackResolutionDefinition(
    string? TargetDefenseKey,
    string RollMode,
    IReadOnlyList<string> TargetStateKeys);

/// <summary>
/// Normalizes explicit attack-resolution context without inferring equivalence between
/// target defense, d20 selection mode, or target state.
/// </summary>
public static class CharacterAttackResolutionSemantics
{
    public static CharacterAttackResolutionDefinition Create(
        string? targetDefenseKey,
        string? rollMode,
        IReadOnlyList<string>? targetStateKeys)
    {
        var defense = string.IsNullOrWhiteSpace(targetDefenseKey)
            ? null
            : targetDefenseKey.Trim();
        var mode = string.IsNullOrWhiteSpace(rollMode)
            ? CharacterMechanicRollModes.Normal
            : CharacterMechanicRollModes.Normalize(rollMode);
        var states = (targetStateKeys ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CharacterAttackResolutionDefinition(defense, mode, states);
    }
}
