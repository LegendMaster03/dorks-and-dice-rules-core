using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// Versioned first-party trial valuation of the native biological gene catalog.
/// This is an opt-in host-owned policy, not a grant of biological permission or
/// an automatic alteration of historical characters. Revise prices for
/// playtesting by creating a new policy revision rather than mutating saves.
/// </summary>
public static class NativeOriginFirstPartyBiologicalPricing
{
    public const string Revision = "biological-pricing-20261123-v7";
    public const int TrialAllowance = 18;

    /// <summary>
    /// The allowance is a host configuration. Changing it does not modify the
    /// prices or relax native compatibility and provenance requirements.
    /// </summary>
    public static NativeOriginAllocationPolicy Create(int allowance = TrialAllowance)
    {
        var prices = new List<NativeOriginAllocationPrice>
        {
            new("creature-type", 0, ["intrinsic:creatureType"]),
            new("size", 0, ["intrinsic:size"]),
            // Ordinary authored exceptions (including 35 ft) are free. An
            // unknown enhanced intrinsic speed is *not* a free fallback.
            new("ordinary-walking-speed", 0, ["intrinsic:walkingSpeedFeet"],
                Configurations: [
                    new("20-feet", 0, 10, [
                        EqualsValue("intrinsic:walkingSpeedFeet", "value", 20)
                    ]),
                    new("25-feet", 0, 10, [
                        EqualsValue("intrinsic:walkingSpeedFeet", "value", 25)
                    ]),
                    new("30-feet", 0, 10, [
                        EqualsValue("intrinsic:walkingSpeedFeet", "value", 30)
                    ]),
                    new("35-feet", 0, 10, [
                        EqualsValue("intrinsic:walkingSpeedFeet", "value", 35)
                    ]),
                    new("40-feet", 0, 10, [
                        EqualsValue("intrinsic:walkingSpeedFeet", "value", 40)
                    ]),
                    new("45-feet", 0, 10, [
                        EqualsValue("intrinsic:walkingSpeedFeet", "value", 45)
                    ])
                ]),
            new("source-bound-spellcasting-ability", 1,
                ["intrinsic:spellcastingAbility"]),
            new("innate-darkvision", 3, ["intrinsic:darkvisionFeet"],
                Configurations:
                [
                    new("60-feet", 3, 10, [
                        EqualsValue("intrinsic:darkvisionFeet", "value", 60)
                    ]),
                    // The derived sunlight limitation is included in this
                    // price, never a separate positive charge or refund.
                    new("120-feet-with-sunlight-drawback", 4, 10, [
                        EqualsValue("intrinsic:darkvisionFeet", "value", 120)
                    ])
                ]),
            new("derived-sunlight-sensitivity", 0,
                ["trait:trait.sunlight-sensitivity"])
        };

        // Every gene below prices one complete effective trait, including
        // deferred level unlocks, rather than paying once per donor offer.
        foreach (var (id, load) in OrdinaryGenes)
            prices.Add(new("gene:" + id, load, ["trait:" + id]));

        foreach (var (id, spellLevel, modifier) in InnateSpells)
            prices.Add(new("spell:" + id, SpellLevelCost(spellLevel) + modifier,
                ["trait:" + id]));

        // These grant only the indicated damage resistance. Other complete
        // composites, such as Poison Resilience, have their own fixed price.
        foreach (var id in ResistanceGenes)
            prices.Add(Resistance(id));

        return new(Revision, "rule.creature-origination", "biological",
            allowance, prices);
    }

    /// <summary>
    /// Cantrip = 1, 1st-level = 2, ... 9th-level = 10. A native trait's
    /// "level" is a character unlock level, NOT its spell level. The table
    /// below records the spell level (or free upcast level) independently.
    /// </summary>
    public static int SpellLevelCost(int spellLevel) =>
        spellLevel is >= 0 and <= 9
            ? spellLevel + 1
            : throw new ArgumentOutOfRangeException(nameof(spellLevel));

    private static NativeOriginAllocationPriceCondition EqualsValue(
        string member, string path, object value) =>
        new(member, path, NativeOriginAllocationComparison.Equals,
            JsonSerializer.SerializeToElement(value));

    private static NativeOriginAllocationPrice Resistance(string id)
    {
        var member = "trait:" + id;
        return new("resistance:" + id, 3, [member],
            Configurations:
            [
                new("25-percent", 2, 10, [
                    EqualsValue(member, "value/damageResistance/percent", 25)
                ]),
                new("50-percent", 3, 10, [
                    EqualsValue(member, "value/damageResistance/percent", 50)
                ]),
                new("75-percent", 4, 10, [
                    EqualsValue(member, "value/damageResistance/percent", 75)
                ]),
                new("100-percent", 5, 10, [
                    EqualsValue(member, "value/damageResistance/percent", 100)
                ])
            ]);
    }

    // The complete percentage must be verified from the effective grant;
    // symbolic donor identity alone must not select a cheaper resistance.
    private static readonly string[] ResistanceGenes =
    [
        "trait.damage-type-resistance",
        "trait.born-of-flame",
        "trait.born-of-ice"
    ];

    // Other native genes retain the WorkChat fine-scale trial estimate except
    // for the explicitly revised full-ability costs (Versatile, Breath Weapon
    // and Cloudy Escape). Missing new genes intentionally remain unpriced.
    private static readonly (string Id, int Load)[] OrdinaryGenes =
    [
        ("trait.adaptable-learning", 6),
        ("trait.adrenaline-rush", 4),
        ("trait.altitude-acclimation", 1),
        ("trait.amphibious", 2),
        ("trait.ancestral-aptitude", 1),
        ("trait.brave", 1),
        ("trait.bountiful-luck", 3),
        ("trait.breath-weapon", 3),
        ("trait.bulwark", 3),
        ("trait.cavernous-sight", 2),
        ("trait.cloud-strike", 2),
        ("trait.clouds-jaunt", 3),
        ("trait.cloudy-escape", 4),
        ("trait.coldwater-swimmer", 2),
        ("trait.control-breath-weapon", 1),
        ("trait.displaced-voice", 1),
        ("trait.dwarven-fortitude", 4),
        ("trait.fey-ancestry", 1),
        ("trait.fires-burn", 3),
        ("trait.frigid-retaliation", 4),
        ("trait.frosts-chill", 3),
        ("trait.hills-tumble", 5),
        ("trait.human-determination", 4),
        ("trait.icewalker", 1),
        ("trait.iron-stomach", 1),
        ("trait.keen-hearing", 1),
        ("trait.keen-senses", 2),
        ("trait.large-form", 4),
        ("trait.larger-creature-passage", 1),
        ("trait.level-based-vitality", 3),
        ("trait.low-light-vision", 2),
        ("trait.luck-reroll", 2),
        ("trait.maelstrom-aura", 5),
        ("trait.magical-cunning", 3),
        ("trait.magical-sleep", 1),
        ("trait.menacing-presence", 1),
        ("trait.mountain-leap", 1),
        ("trait.natural-agility", 2),
        ("trait.natural-camouflage", 3),
        ("trait.natural-climber", 2),
        ("trait.orcish-fury", 4),
        ("trait.naturally-stealthy", 2),
        ("trait.poison-resilience", 4),
        ("trait.powerful-build", 1),
        ("trait.psionic-mind", 1),
        ("trait.relentless-endurance", 4),
        ("trait.sand-burrower", 2),
        ("trait.savage-attacks", 1),
        ("trait.searing-ignition", 4),
        ("trait.second-chance", 4),
        ("trait.skillful", 1),
        ("trait.soft-landing", 1),
        ("trait.spectral-flight", 3),
        ("trait.stability", 1),
        ("trait.stone-strike", 1),
        ("trait.stone-throw", 3),
        ("trait.stonebound-tremorsense", 3),
        ("trait.stones-endurance", 4),
        ("trait.storm-strike", 2),
        ("trait.storms-thunder", 2),
        ("trait.temporary-damage-immunity", 1),
        ("trait.trackless-passage", 1),
        ("trait.trance", 1),
        ("trait.versatile", 6),
    ];

    // Base cost depends on spell level, never individual spell utility.
    // Modifier 1: PB free uses of Speak with Animals, or replaceable
    // Wizard cantrip selection each Long Rest. Hellish Rebuke is granted
    // as a free SECOND-level casting, hence spellLevel 2 here.
    private static readonly (string Id, int SpellLevel, int Modifier)[] InnateSpells =
    [
        ("trait.animal-speech", 1, 1),
        ("trait.arcane-cantrip", 0, 1),
        ("trait.chill-touch", 0, 0),
        ("trait.dancing-lights", 0, 0),
        ("trait.darkness", 2, 0),
        ("trait.detect-magic", 1, 0),
        ("trait.druidcraft", 0, 0),
        ("trait.faerie-fire", 1, 0),
        ("trait.false-life", 1, 0),
        ("trait.fire-bolt", 0, 0),
        ("trait.forest-illusion", 0, 0),
        ("trait.hellish-rebuke", 2, 0),
        ("trait.hold-person", 2, 0),
        ("trait.longstrider", 1, 0),
        ("trait.misty-step", 2, 0),
        ("trait.otherworldly-presence", 0, 0),
        ("trait.pass-without-trace", 2, 0),
        ("trait.poison-spray", 0, 0),
        ("trait.ray-of-enfeeblement", 2, 0),
        ("trait.ray-of-sickness", 1, 0),
    ];
}
