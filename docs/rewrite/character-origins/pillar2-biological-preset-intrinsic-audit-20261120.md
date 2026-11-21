# Pillar 2 — Biological gene catalog and preset-field boundary

**Date:** 2026-11-20
**Scope:** Biological Origination only
**Status:** Flat gene storage implemented; intrinsic-to-gene conversion is a design follow-up, not part of this file relocation.

## Implemented storage model

The authoritative biological catalog contains 87 distinct canonical `origin-trait`
genes, representing 95 original trait occurrences in nine Species and 34
Ancestry records. The earlier 36 files grouped 89 definitions by the original Species or
Ancestry source; this pass consolidated two mechanically equivalent pairs.

Each canonical gene now lives in its own file directly under
`native/origination/biological/genes/`, named by its source-neutral mechanical ID:
`genes/trait.breath-weapon.json`,
`genes/trait.stone-strike.json`, and
`genes/trait.low-light-vision.json` are examples. Prior source-prefixed IDs are retained only in the offline migration map,
not as active gene aliases or inheritance constraints.

Species and Ancestry records already reference canonical genes using ordered
`{"traitId":"trait.…"}` presets; the inherited `parent` graph supplies shared
foundation records. Gene identities and preset `traitId` references are now converted together;
source occurrence indices and parent relationships are unchanged. Existing projectors and the universal Origin construction
pipeline still consume the same mechanical definitions.

## What is not yet represented by a gene

**The presets are not yet literally just lists of genes.** The audit of all
43 authoritative Species/Ancestry records identified these executable or
contextual fields outside the canonical gene list:

| Field family | Current examples | Boundary before conversion |
| --- | --- | --- |
| Foundation | `creatureType` on all nine Species; `size` or `sizeChoice`; `walkingSpeedFeet` | These are intrinsic identity/physical slots. Direct and mixed Origins need explicit effective values, provenance and conflict resolution, not merely an unordered set of grants. |
| Other senses and movement | Dwarf/Drow `darkvisionFeet`; Wood Elf speed; Red/Copper climbing, White swimming, Brass burrowing | Preserve numeric intrinsic semantics, movement modes, valid prerequisites and the Drow source-linked drawback. Do not assume every field is an independent gene. |
| Gene configuration | `damageType` on Dragonborn leaf presets, `breathWeaponArea` on Chromatic/Metallic/Gem family presets, `spellcastingAbilityChoice` on Elf/Gnome/Tiefling ancestries | These may become preset-supplied values of a reusable gene or configured group. Moving them without a source-aware parameter contract would break native leaf/family binding. |
| Organism needs and other physical properties | Tiefling `needs` (breathing, food, sleep) | The generic physical-property representation must remain compatible with mixed Origins and other origination methods. |
| Authored relationships | Drow `originLinkedSelections` linking source Darkvision to Sunlight Sensitivity | The relationship is an enforceable authored prerequisite; it is not an automatic gene grant or a rule for all Darkvision. |
| Preset and narrative metadata | `id`, `kind`, `name`, `parent`, `selectable`, `acquisitionMethods`, `ancestrySelection`, `ancestryTheme`, `defaultUpbringing`, review notes | Identity, family grouping, access gates, cultural suggestions and descriptive metadata are not biological genes and should not be forced into the gene catalog. |

The present deconstructor, selection validator, intrinsic and sense projectors,
dependent binders and Character Sheet consumers rely on several of these
specific field shapes. Their removal is not a pure content refactor.

## Gene parameter bindings that still use preset evidence

The 87-gene catalog contains 31 definitions with canonical `originBindings`:

- **Dragonborn (3):** damage type uses `leaf` binding, and ordinary Breath
  Weapon's complete area uses `nearest` family/leaf binding. Existing
  `originComposition` permits contributor-supported independent selection of
  damage type and *whole* area for ordinary Breath Weapon; it does not imply
  universal cross-gene alignment, independent resistance, or unrelated warding.
- **Elf (9):** Drow, High and Wood Elf innate spells use the selected
  `spellcastingAbility` intrinsic from the correct source.
- **Goliath (9):** Ancestry attacks and abilities use the manifested,
  contributor-scoped Ancestral Aptitude choice. They do not acquire
  another contributor's choice merely because the IDs match.
- **Tiefling (10):** Innate spells use source-scoped
  `spellcastingAbility`, and shared Otherworldly Presence uses the verified
  descendant-scoped choice.

The existing binder is not merely a legacy directory lookup. It enforces
contributor identity, native source/family evidence and actual manifestation.
A later schema that replaces `leaf`, `nearest`, `source` or `descendant`
with configured-gene references must preserve those checks.

## Recommended next implementation boundary

1. Specify one reusable **configured intrinsic/gene instance** representation,
   retaining identity of the originating preset, contributor occurrence and
   the exact source of each parameter. Do not replace meaningful creature
   identity with arbitrary trait keywords.
2. Make a preset an explicit ordered set of gene configurations **plus**
   non-gene identity, parentage, availability and contextual metadata. Define
   how shared Species foundations combine with Ancestry overrides without
   duplicating genes or silently inheriting unselected effects.
3. Migrate existing intrinsic fields and their readers in a coordinated
   general-purpose pass, preserving direct character creation, mixed and
   nested origins, requirements, transformations, mechanical projections,
   serialization and round trips.
4. Express source-linked gene relationships through the existing universal
   constraint/binding contracts where possible. Add general semantics only
   where the current contracts are insufficient. Do not author blanket links,
   new gene equivalences, latent inheritance, or quantitative costs by inference.

**Decision boundary:** This audit establishes the remaining gap but does not
approve a new gene or intrinsic schema, a migration of creature-type foundations,
or an interpretation of which physical characteristics are independently
transmissible. Those need explicit design treatment rather than silently
changing gameplay during the catalog relocation.

Construct Assembly and other origination methods are outside this pass.

## Persisted profile compatibility

Old canonical IDs are not runtime aliases. The migration manifest under
`data/migrations/biological-gene-ids-20261121.json` maps 89 former IDs to
87 current definitions. Apply `tools/migrate_biological_gene_ids.py` explicitly
to saved Origin JSON; do not silently rewrite historical contributor IDs,
source occurrence paths, or table authorization evidence.
