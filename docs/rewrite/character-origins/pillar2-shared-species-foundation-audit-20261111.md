# Pillar 2 — shared Species foundation and distinct Ancestry evidence

**Date:** 2026-11-11  
**Status:** Read-only, source-neutral design audit. Not an approved mixed-Ancestry algorithm, a source authorization, a completed mixed-Species compatibility classifier, or a balancing model.

**Tool:** `tools/character_origins_shared_foundations.py`  
**Tests:** `tests/test_character_origins_pillar2_shared_foundations.py`

## Goal

The user identified an important asymmetry: **mixed Ancestries within one Species** normally share an authored Species foundation, while **mixed Species** may require reconciling multiple foundations. This audit establishes a **representation** that can describe that difference without enforcing a new composition policy.

It builds on the **existing** source lineage, bound compound traits and `characteristic_candidate_pool`. It does not reimplement those systems or add Species-specific dispatch.

## What the new read-only projection records

- **Contributor lineages:** Each directly selected standard source, its root Species, and all intervening family/Ancestry nodes, preserving the distinct contributor occurrence identity.
- **Species foundation index:** A Species root appears once with the contributor IDs sharing it. In a mixed-Species example, multiple roots remain separately visible.
- **Unique source-node index:** The same authored Species or family definition appears once even when several contributors share it; `contributedBy` preserves all source occurrences.
- **Distinct configured candidate offers:** Equal complete source-qualified characteristic offers are grouped once, with all contributor provenances. Distinct values or trait configurations remain **separate** candidate offers, never stacked or automatically joined.
- **Existing review flags and deferred evidence:** Intrinsic collisions, unmodeled source fields and symbolic-parameter problems remain exactly visible; the tool does not resolve or discard them.
- **Explicit status:** No characteristics are granted, and compatibility and balance are `not-evaluated`. `same-species-shared-foundation` means only that direct contributors have the same root source; it is **not** a declaration that all Ancestry differences are compatible.

In particular, grouping one base Species definition does **not** silently give a character all the shared Species abilities. A common authored source is an *eligible* source, not an automatically manifested character package.

## Representative examples

| Direct contributors | Foundation evidence | What remains distinct or unresolved |
| --- | --- | --- |
| Red + Bronze Dragonborn | One `species.dragonborn` node, cited by both contributors | Chromatic and Metallic family nodes, both leaf nodes; Fire and Lightning trait parameter offers remain distinct; category/material inheritance and trait interaction remain undecided |
| Red + Red Dragonborn | One base, family and leaf node each, all cited by both contributor occurrences | One complete Fire/Line Breath Weapon candidate with two provenances; still **not two grants** |
| High Elf + Wood Elf | One `species.elf` root | Ancestor and descendant walking-speed candidates (30 and 35 feet) remain a conflict requiring policy; no speed addition or automatic override |
| Human + High Elf | Two Species roots, separately preserved | Human and Fey creature-type options and other characteristics need reconciliation; **not** rejected automatically |
| Orc + Cloud Goliath | Two roots | Same local trait ID `powerful-build` has distinct source-qualified identities and definitions; neither collapses into the other |
| Red + synthetic new-category Dragonborn leaf | One Dragonborn root, two authored family lineages | New category's full Breath Weapon area binds through the existing source-neutral resolver; unmodeled `skinMaterial` remains deferred, not invented as a supported mechanical grant |

## Mixed Species and limits

This projection deliberately **does not** decide whether distinct Species roots are compatible. It does not claim all mixes are balanced, nor does it issue blanket prohibitions. As recorded in the [Pillar 2 scope](pillar2-heritage-manifestation-scope-20261111.md#mixed-species-limits-balance-expectations-and-table-authority), the eventual rules will support as many combinations as reasonably practical while acknowledging that some have no coherent or balanced **default** configuration. A table may explicitly adjudicate or customize those combinations under the standing system-wide table customization principle.

## Systematic same-Species coverage, without inventing pairwise policies

The companion `tools/character_origins_same_species_pair_audit.py` builds a **review matrix for all currently selectable same-Species pairs**, including matching sources. The matrix is generated from the current source catalog, so new Ancestries are included automatically without a hard-coded pair list. It does **not** generate or claim exhaustive cross-Species coverage.

Each pair reports shared source nodes, all generic source review flags, review flags **newly visible only when the two contributors are combined**, and a count of deferred/unmodeled source evidence. The audit distinguishes existing authored-source ambiguity (for example, Wood Elf's base 30 versus leaf 35-foot walk speed) from newly introduced two-contributor ambiguity. Neither category is assumed to be a proven mechanical incompatibility.

Every pair remains `defaultCompatibility: not-evaluated` and `balance: not-evaluated`. A pair with no detected review flags is **not certified compatible**; missing category/material rules, unknown trait semantics and balance questions may remain. The companion [synthetic declarative compatibility prototype](pillar2-declarative-compatibility-prototype-20261111.md) explores a source-neutral language for explicit requirements, exclusions and exception review, but is **not yet integrated with this pair matrix** and does not establish real Species constraints.

The matrix is a practical next step toward intentional same-Species coverage: when the owner approves formal compatibility policies for a formulaic Species, generated cases can verify their completeness. It is **not** appropriate to require a comparable exhaustive matrix across every pair of different Species; those should rely on generalized characteristic-conflict logic and selected curated exceptions.

```bash
python3 tools/character_origins_same_species_pair_audit.py \
  --species species.dragonborn
python3 -m unittest discover -s tests \
  -p 'test_character_origins_pillar2_same_species_pair_audit.py' -v
```

## Scope of this study

The first audit accepts **only directly selectable source contributors**. It explicitly rejects nested profile inputs to avoid falsely treating their historical roots as active manifested foundations. The existing `characteristic_candidate_pool` already has a separate **verified nested-profile** path and exports previously manifested characteristics, but the idea that *only* manifested traits transmit to descendants remains a provisional design hypothesis, not a finalized genetic rule.

A future extension should analyze verified nested contributors with a separate, explicit notion of historical lineage **versus actual manifested characteristics**. It must not reconstruct an unmanifested ancestral Species package merely because a nested contributor had that Species in its ancestry.

This is not a per-Species solver or a new Origin engine. It is evidence for a future **generalized normalization step** that may allow same-Species character creation to reuse a common foundation while sending only actual conflicting characteristics through compatibility review.

## Acceptance criteria and next decisions

1. **No source loss:** Every direct contributor's lineage maps to precisely the expected unique authored nodes, each with complete contributor occurrences.
2. **No duplicate grants:** Equal source offers can be grouped for inspection without granting the trait, multiplying an effect or losing provenance.
3. **No accidental value combination:** A configured Breath Weapon with Fire/Line is not implicitly equated to Lightning/Cone, even though the previously approved independent parameter policy can produce the crossed combinations through a separate **explicit selection**.
4. **No blanket Species verdict:** Different Species foundations remain separate and their characteristic collisions visible. Source-root difference alone can not prove impossibility or balance.
5. **No content-specific code paths:** Synthetic additional categories and leaves work without expanding hard-coded Dragonborn lists.
6. **No false nested inheritance:** Do not treat source roots in a nested profile's historical ancestry as its fully manifested mechanical foundation.

### CLI

```bash
python3 tools/character_origins_shared_foundations.py \
  --contributors ancestry.dragonborn.red ancestry.dragonborn.bronze

python3 tools/character_origins_shared_foundations.py \
  --contributors species.human ancestry.elf.high

python3 -m unittest discover -s tests \
  -p 'test_character_origins_pillar2_shared_foundations.py' -v
```

**Next substantive design step:** extend the verified nested-contributor evidence into a shared-foundation view without conflating historical ancestry with manifested characteristics. Only then design optional default groups or conflict resolution for different Species; this study does not justify adopting them.
