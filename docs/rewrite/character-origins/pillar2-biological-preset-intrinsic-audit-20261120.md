# Pillar 2 — Biological gene catalog and preset-field boundary

**Date:** 2026-11-20
**Scope:** Biological Origination only
**Status:** Flat gene storage implemented; intrinsic-to-gene conversion is a design follow-up, not part of this file relocation.

## Implemented storage model

The authoritative biological catalog contains 89 distinct canonical `origin-trait`
genes, representing 95 original trait occurrences in nine Species and 34
Ancestry records. The old 36 files were grouped by the original Species or
Ancestry source, sometimes containing several canonical definitions.

Each canonical gene now lives in its own file directly under
`native/origination/biological/genes/`, named by its unchanged stable ID:
`genes/trait.dragonborn.breath-weapon.json`,
`genes/trait.goliath.stone.stone-strike.json`, and
`genes/trait.shared.low-light-vision.json` are examples. A source name inside
an existing stable ID is historical identity/provenance, **not** a folder,
automatic affinity rule, or permission to inherit exclusively from that
Species or Ancestry.

Species and Ancestry records already reference canonical genes using ordered
`{"traitId":"trait.…"}` presets; the inherited `parent` graph supplies shared
foundation records. Nothing in this storage pass changes canonical gene IDs,
trait payloads, source `traitId` arrays, source occurrence indices, or parent
relationships. Existing projectors and the universal Origin construction
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

The 89-gene audit identified 31 definitions with canonical `originBindings`:

- **Dragonborn (3):** damage type uses `leaf` binding, and ordinary Breath
  Weapon's complete area uses `nearest` family/leaf binding. Existing
  `originComposition` permits contributor-supported independent selection of
  damage type and *whole* area for ordinary Breath Weapon; it does not imply
  universal cross-gene alignment, independent resistance, or unrelated warding.
- **Elf (9):** Drow, High and Wood Elf innate spells use the selected
  `spellcastingAbility` intrinsic from the correct source.
- **Goliath (9):** Ancestry attacks and abilities use the manifested,
  contributor-scoped Giant Ancestry Aptitude choice. They do not acquire
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
