using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// Recognizes the authored fields of canonical biological characteristic
/// payloads. Choice, resource, constraint and gameplay projectors still
/// validate the interpretation and configured values they consume.
/// </summary>
internal static class NativeOriginBiologicalTraitShape
{
    public static bool IsRecognized(JsonElement trait)
    {
        if (trait.ValueKind != JsonValueKind.Object)
            return false;
        var hasMechanics = false;
        foreach (var property in trait.EnumerateObject())
        {
            if (property.Name is "id" or "name" or "description" or "reviewNotes")
                continue;
            if (!RecognizesRoot(property.Name, property.Value.ValueKind) ||
                !RecognizesChildren("/" + property.Name, property.Value))
                return false;
            hasMechanics = true;
        }
        return hasMechanics;
    }

    private static bool RecognizesChildren(string path, JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Any() && value.EnumerateObject().All(p =>
            RecognizesAt(path, p.Name) && RecognizesChildren(path + "/" + p.Name, p.Value)),
        JsonValueKind.Array => value.GetArrayLength() > 0 && value.EnumerateArray().All(item =>
            RecognizesChildren(path + "/*", item)),
        _ => true
    };

    // Object fields are scoped to their parent mechanic. A recognized field in
    // one mechanic supplies no meaning when used in an unrelated payload.
    private static bool RecognizesAt(string path, string field) => path switch
    {
        "/abilityChoice" => field is
            "from" or "perCharacter" or "when",
        "/hitDieHealing" => field is
            "spendHitDice" or "requiresAvailableHitDie" or "rollSpentHitDie" or
            "addAbilityModifier" or "minimumHitPointsRegained" or
            "extraHitDiceGranted",
        "/conditionalAdvantage" => field is
            "requires" or "appliesTo" or "resists" or "excludes" or "grantsImmunity",
        // A chosen one-roll advantage applies across attack rolls, checks,
        // and saves. It is distinct from persistent check-only advantage.
        "/rollAdvantage" => field is
            "eligibleRollTypes" or "declarationTiming" or "outcome" or
            "stacksWithExistingAdvantage" or "actionCost",
        // Infernal Flames of Phlegethos is one inherited characteristic
        // with two effects. Neither is an additional spell or use pool.
        "/fireSpellDamageReroll" => field is
            "source" or "damageType" or "triggerDieResult" or
            "rerollsPerDie" or "useNewResult",
        // Rest-limited healing requires actual delivered Necrotic damage;
        // its once-per-turn gate and PB-use budget remain a single gene.
        "/lifeSiphon" => field is
            "trigger" or "damageType" or "damageMeasured" or
            "requiresPositiveDamage" or "healing",
        "/lifeSiphon/healing" => field is
            "recipient" or "amount" or "maximum" or
            "obeysNormalHitPointMaximum",
        // Abyssal Fortitude is a level-derived maximum-HP bonus, rather
        // than the flat per-level HP growth of Dwarven Toughness.
        "/hitPointMaximumBonus" => field is
            "formula" or "levelBasis" or "rounding" or "minimum",
        // Potent Venom modifies effective resistance for this damage
        // instance only, never target immunity or another damage type.
        "/damageResistanceReduction" => field is
            "damageType" or "scope" or "timing" or
            "reductionPercentagePoints" or "minimumResistancePercent" or
            "preserveImmunityAtOrAbovePercent" or "stacksWithSelf" or
            "createsVulnerability",
        "/protectiveFlames" => field is
            "trigger" or "optional" or "endsAt" or "brightLightFeet" or
            "dimLightAdditionalFeet" or "harmsSelfOrPossessions" or "retaliation",
        "/protectiveFlames/retaliation" => field is
            "trigger" or "rangeFeet" or "damageDice" or "damageType" or
            "actionCost",
        // Compound inherited weapon techniques are one biological gene;
        // independent mode triggers/resources must not become extra grants.
        "/onWeaponHit" => field is
            "trigger" or "eligibleWeaponCategories" or "bonusDamageDice" or "uses",
        "/onWeaponHit/bonusDamageDice" => field is "count" or "dieFrom",
        "/onWeaponHit/uses" => field is "value" or "recharge",
        "/afterTraitUse" => field is
            "triggerTraitId" or "triggerTiming" or "activation" or
            "requiresManifestedTrait" or "extraUsesGranted",
        "/afterTraitUse/activation" => field is "action" or "grantsAction",
        "/reroll" => field is "roll" or "roller" or "useResult",
        "/suspendsTrait" => field is "id" or "until",
        "/animalCommunication" => field is
            "habitatParameter" or "eligibleAnimals" or "conveys" or "compelsCooperation",
        "/naturalCamouflage" => field is
            "biomeParameter" or "skill" or "advantageOnHideChecks" or
            "mayHideWhileLightlyObscuredByNaturalVegetation" or "grantsInvisibility",
        "/tracklessPassage" => field is
            "biomeParameter" or "targets" or "suppressesTracks" or
            "suppressesLingeringScentTrail" or "erasesExistingTracks" or
            "concealsCompanions" or "suppressesCurrentBodyScent" or
            "preventsVisualOrAuditoryDetection",
        "/activation" => field is
            "action" or "grantsAction" or "replaces" or "requiresSpaceFor" or "timing" or
            "trigger",
        "/advantageOnChecks" => field is
            "ability" or "checkType",
        "/alternateStrikeMode" => field is
            "applyConditionOnFailedSave" or "description" or "extraDamage" or
            "extraDamageAlreadyProvidedByParent" or "extraUsesGranted" or "id" or "name" or
            "onFailedSave" or "oncePerTurn" or "replaces" or "savingThrow" or
            "sharedUsesFromTrait" or "targetMaximumSize" or "trigger",
        "/alternateStrikeMode/extraDamage" => field is
            "damageTypeFrom" or "dice",
        "/alternateStrikeMode/onFailedSave" => field is
            "endsAt" or "speedSetFeet",
        "/alternateStrikeMode/savingThrow" or "/options/*/savingThrow" or
            "/startsTurnInside/savingThrow" => field is
            "ability" or "dc" or "onSuccess",
        "/applyConditionOnFailedSave" => field is
            "condition" or "endsAt",
        "/area" => field is
            "affectsAllies" or "blockedBy" or "center" or "lengthFeet" or "radiusFeet" or
            "shape" or "targets",
        "/attack" => field is
            "attackBonus" or "rangeFeet" or "type",
        "/bonusDamageDice" => field is
            "count" or "dieFrom",
        "/damage" => field is
            "add" or "damageType" or "dice" or "progression" or "typeFrom",
        "/damage/progression/*" => field is
            "characterLevel" or "dice",
        "/damageReduction" => field is
            "addAbilityModifier" or "appliesTo" or "dice",
        "/damageResistance" => field is
            "damageType" or "damageTypeFrom" or "duration" or "percent" or "poison" or "scope"
            or "stacking",
        "/damageResistanceOverride" => field is
            "damageTypeFrom" or "percent" or "stacking",
        "/duration" => field is
            "alsoEndsWhen" or "canEndEarly" or "endEarlyAction" or "endsAt" or "minutes",
        "/extraDamage" or "/retaliation" => field is
            "damageType" or "dice" or "target",
        "/flyingSpeed" => field is
            "equalTo",
        "/invisibility" => field is
            "alsoEndsImmediatelyAfter" or "endsAt" or "notGlobalCondition" or "onFailedSave"
            or "onlyAgainst",
        "/magicalSleep" => field is
            "immune" or "mayConsentToSpecificEffect",
        "/onFailedSave" => field is
            "attackRollMode" or "damage" or "direction" or "endsAt" or "forcedMovementFeet" or
            "speedEndsAt" or "speedSetFeet",
        "/onFailedSave/damage" => field is
            "add" or "damageType" or "dice",
        "/options/*" => field is
            "id" or "name" or "onFailure" or "savingThrow",
        "/options/*/onFailure" => field is
            "applyCondition" or "direction" or "endsAt" or "forcedMovement" or "pushFeet",
        "/originBindings" => field is
            "area" or "damageType" or "giantAncestryAbility" or "spellcastingAbility" or
            "subTrait",
        "/originBindings/area" or "/originBindings/damageType" or
            "/originBindings/subTrait" => field is "lookup" or "sourceField",
        "/originBindings/giantAncestryAbility" => field is
            "choiceRequirementId" or "lookup" or "mechanicalDefinitionId" or "scope",
        "/originBindings/spellcastingAbility" => field is
            "intrinsicSlot" or "lookup" or "scope",
        "/prevents" => field is
            "applyConditionFromTrigger" or "forcedMovementFromTrigger",
        "/savingThrow" => field is
            "ability" or "dc" or "onFailure" or "onSuccess",
        "/savingThrows" => field is
            "advantageAbilities" or "advantageAgainst" or "scope",
        "/sizeChange" => field is
            "actualSize" or "to",
        "/skillBonuses" => field is
            "skill.climb" or "skill.jump" or "skill.listen" or "skill.move-silently" or
            "skill.spot",
        "/skillCheckAdvantage" => field is
            "requiresTargetCanPerceivePresence" or "skill" or "when",
        "/slipperyIceSaveExemption" => field is
            "effect" or "savingThrow" or "trigger",
        "/speedReduction" => field is
            "endsAt" or "feet" or "target",
        "/startsTurnInside" => field is
            "onFailedSave" or "savingThrow" or "target",
        "/startsTurnInside/onFailedSave" => field is
            "endsAt" or "speedMultiplier",
        "/teleport" => field is
            "destination" or "maximumDistanceFeet" or "timing",
        "/temporaryHitPoints" => field is
            "value",
        "/uses" => field is
            "independentOf" or "recharge" or "resource" or "value",
        "/whileActive" => field is
            "attackRollsTargetingYou" or "damageResistance",
        "/whileActive/damageResistance/*" => field is
            "damageType" or "percent",
        _ => false
    };

    private static bool RecognizesRoot(string field, JsonValueKind value) => field switch
    {
        "allowedMaterials" or "canBreathe" or "disallowedMaterials" or "doesNotChange" or
            "doesNotGrant" or "ignoresDifficultTerrainFrom" or "options" or
            "requiresSharedContact" or "standingJumpAsRunning" or "terrain" => value is
            JsonValueKind.Array,
        "additionalSavingThrow" or "atWill" or "canHover" or
            "doesNotGrantConditionImmunity" or "doesNotNegateOtherHazards" or
            "duplicateStrikeBenefitGranted" or "endsOnLanding" or "extraUsesGranted" or
            "grantsFlight" or "grantsHover" or "grantsPermanentFlight" or
            "grantsSkillProficiency" or "grantsSwimSpeed" or "grantsThoughtReading" or
            "grantsWaterBreathing" or "guaranteesDeception" or
            "ignoreIcySurfaceAbilityChecks" or "ignoresRunningApproachRequirement" or
            "leavesPermanentTraversableTunnel" or "maintainsAcclimationAtLowerElevations"
            or "naturallyAcclimated" or "negatesSuffocation" or
            "negatesVisibilityRestrictions" or "obeysNormalSkillRankLimits" or
            "oncePerShortRest" or "oncePerTurn" or "oneWay" or "perHitDie" or
            "preventsFallingDamageOnLanding" or "protectsOtherCreatures" or
            "requiresCriticalHit" or "requiresRecipientLanguage" or
            "requiresSharedLanguage" or "requiresSight" or "sleepRequired" or "slotCasting"
            or "visualEffect" => value is
            JsonValueKind.True or JsonValueKind.False,
        "altitudeLimitFeet" => value is
            JsonValueKind.Null,
        "addToExistingDarkvisionFeet" or "additionalRanksAtCharacterLevel1" or
            "additionalRanksPerSubsequentCharacterLevel" or "choiceCount" or
            "durationMinutes" or "effectiveSizeCategoriesHigherForCarryingCapacity" or
            "effectiveSizeCategoriesHigherForPushingDraggingLifting" or
            "fallingSpeedFeetPerRound" or "freeCastSpellLevel" or "hitPointsInstead" or
            "hitPointsPerCharacterLevel" or "includesElevationAboveFeet" or "level" or
            "longRestHours" or "rangeFeet" or "requiredCharacterLevel" or "speedFeet" or
            "targetCount" or "walkingSpeedBonusFeet" => value is
            JsonValueKind.Number,
        "freeUses" => value is
            JsonValueKind.Number or JsonValueKind.String,
        "abilityChoice" or "advantageOnChecks" or "alternateStrikeMode" or "area" or
            "hitDieHealing" or "hitPointMaximumBonus" or
            "conditionalAdvantage" or "rollAdvantage" or
            "fireSpellDamageReroll" or "protectiveFlames" or "lifeSiphon" or
            "onWeaponHit" or "afterTraitUse" or
            "attack" or "bonusDamageDice" or "damage" or "damageReduction" or
            "damageResistance" or "damageResistanceOverride" or
            "damageResistanceReduction" or "extraDamage" or
            "flyingSpeed" or "invisibility" or "magicalSleep" or "onFailedSave" or
            "originBindings" or "prevents" or "retaliation" or "savingThrow" or
            "savingThrows" or "sizeChange" or "skillBonuses" or "skillCheckAdvantage" or
            "animalCommunication" or "naturalCamouflage" or "tracklessPassage" or
            "reroll" or "suspendsTrait" or
            "slipperyIceSaveExemption" or "speedReduction" or "startsTurnInside" or
            "teleport" or "temporaryHitPoints" or "uses" or "whileActive" => value is
            JsonValueKind.Object,
        "activation" or "applyConditionOnFailedSave" or "duration" => value is
            JsonValueKind.Object or JsonValueKind.String,
        "advantageOnAbilityChecksToEndCondition" or "allocation" or "apparentOrigin" or
            "applyCondition" or "areaFrom" or "attackModifier" or "choiceTiming" or
            "chooseFrom" or "communicationMode" or "condition" or
            "coversStrikeOfTheGiantsMode" or "end" or "extraHealing" or "featCategory" or
            "grantType" or "ignores" or "independentUsesFrom" or "manifestation" or
            "movementCost" or "movementMode" or "recharge" or "replacement" or
            "requiresContact" or "requiresItem" or "rule" or "savingThrowDC" or "sense" or
            "sharedUsesFromTrait" or "soundSource" or "spell" or "spellList" or
            "spellcastingAbilityFrom" or "stacking" or "targetMaximumSize" or "targets" or
            "trigger" or "when" => value is
            JsonValueKind.String,
        _ => false
    };
}
