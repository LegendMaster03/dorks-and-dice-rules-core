# Authoritative native rules

Only selected, coherent, source-neutral Dorks & Dice rules belong here. A reviewed data-only rule is authoritative even before its C# executor exists. Pending integration does not approve unresolved dependent mechanics.

Selected Species and Ancestry definitions are in `species/`; approved independent cultural Upbringings are in `upbringings/`; Backgrounds are in `backgrounds/`. Files under `working/` are not a fallback for any runtime rule.

Species/Ancestry and Upbringing parentage use explicit stable IDs, not folder structure. A Species or Ancestry may suggest one `defaultUpbringing` stable ID; the nearest default in its selected Ancestry chain wins unless the player explicitly selects another. An Upbringing's parent chain contributes inherited benefits once but is not an additional selected Upbringing. Defaults do not impose Species restrictions. The approved data-only selection contract is `rules/species-subspecies-upbringing.json` (historical filename retained for references).

Not all Species require an Ancestry selection. Some, such as Dragonborn, require a specific playable leaf. Dragonborn Chromatic and Metallic families have five selectable leaves apiece; the shared family records are intentionally unselectable. The Gem family is a nonselectable placeholder with no approved playable leaves yet, and no Gem mechanics may be inferred from the placeholder. Cross-cultural Upbringings remain permissible. General Elf and General Gnome are explicitly **inheritance-only**, not directly selectable; their respective Ancestries suggest valid High/Wood/Drow and Forest/Rock cultural children, which inherit the general benefits once. Base Elf and Gnome Species have no `defaultUpbringing` because their former suggestions referred to now-nonselectable parent records.

The source SRD reference material stays in `data/reference/`, independent of authoritative native content. `data/reference/srd/native-coverage-manifest.json` records individual approved migrations. Provisional balance values within coherent native rules remain explicitly identified for later playtest adjustment.
