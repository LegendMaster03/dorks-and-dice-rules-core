# Biological Origination

This method owns one canonical gene catalog and the Species/Ancestry presets
that refer to it:

- `genes/trait.<mechanical-gene-id>.json`: one canonical `origin-trait` definition
  per JSON file, directly under `genes/`. The filename is the definition's
  stable ID plus `.json`, not an Ancestry-based category or an eligibility gate.
  Active IDs name the mechanic, not its original Species or Ancestry. The
  conversion from the prior canonical IDs is recorded only in the explicit
  profile migration manifest outside the native catalog.
- `species/base/`: shared Species presets and foundations.
- `species/ancestry/`: Ancestry presets, including intermediate family
  presets. Explicit `parent` IDs establish their grouping and inheritance,
  not physical file or gene paths.

A preset's ordered `traitId` entries reference the same independent gene
definitions available to the Origin engine. Other currently authored preset
fields (for example, creature type, size, movement, source-provided damage
type and casting choice) have not all been modeled as canonical genes yet.
Those fields are part of the existing executable Source/Ancestry contract and
must not be dropped or silently turned into unrelated free genes. See
`docs/rewrite/character-origins/pillar2-biological-preset-intrinsic-audit-20261120.md`
for the separate migration analysis.

Gene classification follows a canonical definition's trusted source location
under `native/origination/biological/genes/`. A gene being there makes it an
available biological building block **when a valid contributor and origination
method provide the offer**. It does not automatically manifest the gene, make
every parameter independent, or waive authored source bindings, dependencies,
resource relationships, conflicts, and table authority. Previously manifested
contributors offer only verified current characteristics, not every gene of
historical ancestors.

The `inheritanceMethods: ["biological"]` six-gene proof of concept was
retired earlier. The loader still uses the universal `origin-trait` kind;
there is no second gene definition engine. The identity normalization replaces old gene IDs with 87 active,
source-neutral canonical IDs, including two consolidated equivalent genes.
Preset references and internal dependency pointers now use those IDs. It
preserves source occurrence positions, Ancestry links and mechanical effects.

Construct Assembly and other origination methods have separate catalogs and
are unaffected by the biological layout.

Saved Origin profiles carrying previous mechanical definition IDs require an
explicit migration using `tools/migrate_biological_gene_ids.py` and the
`data/migrations/biological-gene-ids-20261121.json` map; migration never
changes contributor provenance or source occurrence paths automatically.
