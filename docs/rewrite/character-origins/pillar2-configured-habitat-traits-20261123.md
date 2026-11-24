# Pillar 2 — configured habitat traits (2026-11-23)

Status: approved design increment; provisional trial prices. The branch does not change main.

## Native mechanics

- `trait.natural-camouflage` gives advantage on Hide checks in the configured biome and allows hiding while lightly obscured by natural vegetation there. It does not grant invisibility.
- `trait.trackless-passage` prevents the character personally leaving tracks or lingering scent trails while moving in the configured biome. It does not erase existing traces, protect companions, suppress present body scent, or prevent other forms of detection.
- Both Forest Gnome and Wood Elf reference the exact same two canonical genes. Each supplies the source-owned `environmentSubTrait: { "biome": "woodland" }`. The existing `originBindings` procedure carries that sub-trait as a `subTrait` parameter; no biome-specific gene variants or automatic extra expressed instances are introduced.
- Wood Elf retains the separate full `trait.pass-without-trace` spell. Its group and out-of-biome applications remain distinct from passive Trackless Passage.
- `trait.animal-speech` remains a single canonical gene. Forest Gnome supplies `animalCommunicationSubTrait: { "animalHabitat": "woodland" }` to restrict the eligible native animals. The current PB/Long Rest free uses and optional spell-slot casting are *retained*. Changing the resource model to continuous basic communication requires separate design approval. An unrelated class or item source of Speak with Animals is not restricted by this biological trait.
- Provisional prices: Natural Camouflage 3, Trackless Passage 1. Forest Gnome becomes 14 nominal points (previously 10), Wood Elf becomes 18 (previously 14). Animal Speech stays at 3 pending a dedicated pricing review. Neither total proves gameplay parity.

## Contract boundary

Source sub-traits are values of a canonical gene, not separately inheritable genes or additional trait grants. The effective value travels through the existing trusted source binding and inheritance parameters. Creation-time validation and catalog shape checks recognize the two new typed mechanics, but the combat/stealth/tracking gameplay execution and broader biome taxonomy are not implemented here.

Do not use this increment to expand the number of simultaneous expressed variants of the same canonical gene. Additional biome combinations require an independent expression-policy decision.
