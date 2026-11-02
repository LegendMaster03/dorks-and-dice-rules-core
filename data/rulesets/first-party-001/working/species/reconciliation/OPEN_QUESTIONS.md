# Species/Upbringing — unresolved material

Approved Dwarf, Halfling, Gnome and Elf rules are in `native/species/`, `native/upbringings/` and `native/rules/`. No parallel authoritative mechanics belong here.

## Initial scope
**Nine base Species:** Dwarf, Dragonborn, Elf, Gnome, Goliath, Halfling, Human, Orc and Tiefling. **Half-Elf and Half-Orc are excluded**: the later Mixed Species mechanic will address their concepts. Their historical SRD source candidates remain inert in `working/` as evidence, not approved Species definitions.

**Dragonborn first pass:** five Chromatic types (Black, Blue, Green, Red, White) and five Metallic types (Brass, Bronze, Copper, Gold, Silver). Gem Dragonborn and exceptional forms are deferred, although the three-family inheritance structure remains an approved future-capable model.

## Remaining questions
- Species comparison: Dwarf/Halfling/Wood Elf speeds; Dwarf Darkvision; historical Elf Search; Low-Light Vision distribution; overall balance across the nine base Species.
- Dwarf: maximum HP for NPCs/monsters; potential martial-focused Upbringing.
- Gnome: mechanical expression for general cultural knowledge; casting ability for Upbringing-granted Prestidigitation.
- Halfling: cultural Upbringing.
- Elf: final comparison of senses, movement and spells.
- All: ability scores, effect and qualification contracts, native Character projector and schema.
- Dragonborn: common Breath Weapon action, uses, save and damage have been selected in native. Remaining: breath geometry, Chromatic/Metallic family features, ten individual-type adaptations, and independent cultural traditions.
- Deferred: Gem Dragonborn, exceptional Platinum/Ruby/composite forms, rites of rebirth, historical dragon families, Dragon Shaman ancestry-sharing feat, and Dragonfire Adept-derived feats or prestige classes.

## Upbringing defaults
Species/Subspecies declare `defaultUpbringing`, pointing to a stable Upbringing ID. An explicit player choice overrides any default; otherwise the most specific selected Subspecies/ancestor default wins. The chosen Upbringing inherits its own parents but consumes only one choice. If no default exists, leave the choice unresolved. Biological Species does not restrict selection by default.

Pinned source evidence remains in `data/reference/srd/`. When a source candidate is reconciled, move its authoritative definition to `native/`, remove it from `working/`, and update the SRD coverage manifest.
