# Biological Origination

This directory contains the biological method's canonical gene definitions and
conventional Species/Ancestry presets.

- `genes/` contains reusable biological genes. Each gene retains its stable
  `origin-trait` ID and kind so that the universal Origin engine can interpret
  its mechanic and the Species/Ancestry `traitId` references without another
  trait schema.
- `species/base/` contains shared Species foundations.
- `species/ancestry/` contains Ancestry presets and nested family records.
  Explicit `parent` IDs, not the directory hierarchy, determine ancestry.

Gene classification follows the canonical definition's native catalog source
location under `origination/biological/genes/`. The former
`inheritanceMethods: ["biological"]` whitelist belonged to the initial
six-gene proof of concept and is not the biological model.

A gene's location makes it an eligible building block for the biological
origination method when the contributor and method are valid. It does **not**
automatically manifest that gene, permit unrelated parameters to be combined,
or waive authored choices, dependencies, resource relationships, conflicts,
source restrictions, or table authority. Independently meaningful gene-level
behavior must still be authored. Mixed descendants transmit only verified
current manifested contributions, not arbitrary historical ancestor traits.

All Species, Ancestry, and canonical gene stable IDs and occurrence indices are
preserved by the directory organization; the common C# Origin engine remains
authoritative. Construct Assembly and other origination methods retain their
own building-block catalog organization.
