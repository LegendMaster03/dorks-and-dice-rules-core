# Species/Upbringing — unresolved material

Approved Dwarf, Halfling, Gnome and Elf rules are in `native/species/`, `native/upbringings/` and `native/rules/`. No parallel authoritative mechanics belong here.

## Initial scope
**Nine base Species:** Dwarf, Dragonborn, Elf, Gnome, Goliath, Halfling, Human, Orc and Tiefling. **Half-Elf and Half-Orc are excluded**: the later Mixed Species mechanic will address their concepts. Their historical SRD source candidates remain inert in `working/` as evidence, not approved Species definitions.

**Dragonborn first pass:** five Chromatic types (Black, Blue, Green, Red, White) and five Metallic types (Brass, Bronze, Copper, Gold, Silver). The Gem Dragonborn nonselectable family node is native; its five selectable leaf types and Gem-specific mechanics, along with exceptional forms, remain deferred.

## Remaining questions
- Species comparison: Dwarf/Halfling/Wood Elf speeds; Dwarf Darkvision; historical Elf Search; Low-Light Vision distribution; overall balance across the nine base Species.
- Goliath: a nonselectable shared Species and six selectable giant-ancestry Subspecies from SRD 5.2.1 are native as first-pass playtest content. Mountain Leap and Altitude Acclimation are additionally selected in native; remaining: native grapple/Powerful Build interaction, size/occupied-space review, comparative balance, unselected 5e legacy traits, level-1 merged Bigby's Strike modes and level-4 inherited advanced giant powers now in native (initial playtest selection); earlier minor passive secondary candidates remain in working only. One traditional General Goliath Upbringing and one separate Giant Foundling Upbringing are native. The previously invented six Giant Society Upbringings have been withdrawn; Giant Foundling's original Strike feat is omitted from its converted Upbringing and **excluded from the first-wave Feat catalog as non-SRD**; it is not queued for promotion. Gol-Kaa now has a native language definition. See `GOLIATH_SOURCE_REVIEW.md` and `GOLIATH_BIGBYS_SUBSPECIES_EXPANSION.md` for research and unsettled mechanics.
- Dwarf: maximum HP for NPCs/monsters; potential martial-focused Upbringing.
- Gnome: mechanical expression for general cultural knowledge; casting ability for Upbringing-granted Prestidigitation.
- Halfling: cultural Upbringing.
- Elf: final comparison of senses, movement and spells.
- All: ability scores, effect and qualification contracts, native Character projector and schema.
- Dragonborn: shared traits and Breath Weapon are native. The Chromatic family, 30-by-5-foot line breath, Chromatic Warding and Black/Blue/Green/Red/White individual adaptations are native. Metallic family and five Metallic leaves are now native, completing the ten-type Dragonborn Species/Subspecies mechanics pass. Remaining: comparative playtest balance and independent Dragonborn cultural Upbringings.
- Deferred: five Gem Dragonborn leaf Subspecies and their mechanics (the nonselectable Gem family node is native), exceptional Platinum/Ruby/composite forms, rites of rebirth, historical dragon families, Dragon Shaman ancestry-sharing feat, and Dragonfire Adept-derived feats or prestige classes.

## Upbringing defaults
Species/Subspecies declare `defaultUpbringing`, pointing to a stable Upbringing ID. An explicit player choice overrides any default; otherwise the most specific selected Subspecies/ancestor default wins. The chosen Upbringing inherits its own parents but consumes only one choice. If no default exists, leave the choice unresolved. Biological Species does not restrict selection by default.

Pinned source evidence remains in `data/reference/srd/`. When a source candidate is reconciled, move its authoritative definition to `native/`, remove it from `working/`, and update the SRD coverage manifest.
