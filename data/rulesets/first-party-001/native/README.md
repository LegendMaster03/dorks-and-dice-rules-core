# Authoritative native rules

Only selected, coherent, source-neutral Dorks & Dice rules belong here. A reviewed data-only rule is authoritative even before its C# executor exists. Pending integration does not approve unresolved dependent mechanics.

Selected Species/Subspecies definitions are in `species/`; approved independent cultural Upbringings are in `upbringings/`. Files under `working/` are not a fallback for any runtime rule.

Species/Subspecies and Upbringing parentage is by explicit stable ID, not folder structure. A Species/Subspecies may supply one `defaultUpbringing` stable ID; the nearest default in its ancestor chain wins unless the player explicitly selects another. Upbringing parentage is independent, and a default never restricts selection. Not all Species require a Subspecies; some, such as Dragonborn, require a particular playable leaf. The Dragonborn family hierarchy is selected structurally; the Chromatic family and five Chromatic leaves now have authoritative native definitions, while Metallic and Gem mechanics remain unresolved/deferred.

The source SRD reference material stays in `data/reference/`, independent of authoritative native content. `data/reference/srd/native-coverage-manifest.json` records individual approved migrations. Provisional balance values within coherent native rules remain explicitly identified for later playtest adjustment.
