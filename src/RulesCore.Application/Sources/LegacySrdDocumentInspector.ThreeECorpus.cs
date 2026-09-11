namespace RulesCore.Application.Sources;

public static partial class LegacySrdDocumentInspector
{
    public const string ReviewedThreeEIndexUri = "https://www.dragon.ee/30srd/";

    public static readonly IReadOnlyList<string> ReviewedThreeEDocumentNames =
    [
        "ability_scores.htm",
        "adept.htm",
        "ageffect.htm",
        "alignment.htm",
        "animal_companion.htm",
        "arcane_archer.htm",
        "arcane_spells.htm",
        "aristocrat.htm",
        "armor_class.htm",
        "assassin.htm",
        "attack_modifiers.htm",
        "barbarian.htm",
        "bard.htm",
        "bardspells.htm",
        "basics.htm",
        "blackguard.htm",
        "carrying.htm",
        "classes_overview.htm",
        "cleric.htm",
        "clericdomains.htm",
        "clericspells.htm",
        "combat_actions.htm",
        "combat_basics.htm",
        "commoner.htm",
        "condition_summary.htm",
        "coverconceal.htm",
        "creating_magic_items.htm",
        "cursed_items.htm",
        "damageobj.htm",
        "death_dying_healing.htm",
        "divine_spells.htm",
        "druid.htm",
        "druidspells.htm",
        "dwarf.htm",
        "dwarven_defender.htm",
        "elf.htm",
        "encounters.htm",
        "environment.htm",
        "equipment.htm",
        "equipment_armor.htm",
        "equipment_misc.htm",
        "equipment_wpns.htm",
        "expert.htm",
        "familiars.htm",
        "feats.htm",
        "feats_overview.htm",
        "fighter.htm",
        "gnome.htm",
        "halfelf.htm",
        "halfling.htm",
        "halforc.htm",
        "hazards_obstacles.htm",
        "hit_points.htm",
        "home.htm",
        "human.htm",
        "inflicting_damage.htm",
        "intelligent_items.htm",
        "loremaster.htm",
        "magic_armor.htm",
        "magic_artifacts.htm",
        "magic_items_overview.htm",
        "magic_overview.htm",
        "magic_weapons.htm",
        "monk.htm",
        "monster_overview.htm",
        "monsters_a.html",
        "monsters_animal.html",
        "monsters_b.html",
        "monsters_c.html",
        "monsters_d.html",
        "monsters_dragon.html",
        "monsters_e.html",
        "monsters_f.html",
        "monsters_g.html",
        "monsters_h.html",
        "monsters_i.html",
        "monsters_j.html",
        "monsters_k.html",
        "monsters_l.html",
        "monsters_m.html",
        "monsters_n.html",
        "monsters_o.html",
        "monsters_p.html",
        "monsters_r.html",
        "monsters_s.html",
        "monsters_t.html",
        "monsters_templates.htm",
        "monsters_u.html",
        "monsters_v.html",
        "monsters_w.html",
        "monsters_x.html",
        "monsters_y.html",
        "monsters_z.html",
        "movement.htm",
        "overviewnpcs.htm",
        "paladin.htm",
        "paladinspells.htm",
        "potions.htm",
        "prestige_overview.htm",
        "racelang.htm",
        "ranger.htm",
        "rangerspells.htm",
        "rings.htm",
        "rods.htm",
        "rogue.htm",
        "saving_throws.htm",
        "schools_of_magic.htm",
        "scrolls.htm",
        "shadowdancer.htm",
        "skills.htm",
        "skills_overview.htm",
        "sorcerer.htm",
        "sorwizspells.htm",
        "special_abilities.htm",
        "special_abilities_overview.htm",
        "spellsa.htm",
        "spellsb.htm",
        "spellsc.htm",
        "spellsd.htm",
        "spellse.htm",
        "spellsf.htm",
        "spellsg.htm",
        "spellsh.htm",
        "spellsi.htm",
        "spellsjkl.htm",
        "spellsm.htm",
        "spellsno.htm",
        "spellsp.htm",
        "spellsqr.htm",
        "spellss.htm",
        "spellst.htm",
        "spellsuvwxyz.htm",
        "staves.htm",
        "turn_and_rebuke_undead.htm",
        "vision.htm",
        "wands.htm",
        "warrior.htm",
        "wizard.htm",
        "wondrous_items.htm"
    ];

    private static IReadOnlyList<Uri> EnforceReviewedThreeECorpus(
        Uri indexUri,
        IReadOnlyList<Uri> references)
    {
        if (!string.Equals(
                indexUri.AbsoluteUri,
                ReviewedThreeEIndexUri,
                StringComparison.OrdinalIgnoreCase))
        {
            return references;
        }

        var actualByName = references
            .GroupBy(value => Path.GetFileName(value.AbsolutePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var expected = ReviewedThreeEDocumentNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = expected
            .Where(value => !actualByName.ContainsKey(value))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var unexpected = actualByName.Keys
            .Where(value => !expected.Contains(value))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missing.Length > 0 || unexpected.Length > 0)
        {
            var details = new List<string>();
            if (missing.Length > 0)
            {
                details.Add($"missing: {string.Join(", ", missing)}");
            }
            if (unexpected.Length > 0)
            {
                details.Add($"unexpected: {string.Join(", ", unexpected)}");
            }
            throw new InvalidDataException(
                "The Dragon.ee 3e SRD representation no longer matches the reviewed corpus manifest ("
                + string.Join("; ", details)
                + "). Review the representation against the archived SRD 3.0 authority before changing the manifest.");
        }

        return ReviewedThreeEDocumentNames
            .Select(name => actualByName[name])
            .ToArray();
    }
}
