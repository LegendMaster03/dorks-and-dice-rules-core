# Source identity conventions

Rules Core separates D&D edition identity from publication/release identity. These conventions should be followed before bulk source ingestion so durable source keys do not encode accidental assumptions.

## Canonical D&D edition labels

Rules Core uses the current Wizards/D&D Beyond labels in authoring and resolved provenance:

- `1e`
- `2e`
- `3e`
- `3.5e`
- `4e`
- `5e`
- `5.5e`

For current fifth-edition-family material, `5e` identifies the rules originally labeled by year as the 2014 rules, while `5.5e` identifies the updated rules originally labeled by year as the 2024 rules. Legacy import labels such as `5e-2014`, `5e 2014`, `2014`, `5e-2024`, `5e 2024`, and `2024` are accepted and normalized to the current canonical labels. Legacy aliases are import compatibility only; Rules Core should present the canonical labels after ingestion.

A publication year remains useful metadata. It is not itself the D&D edition identity.

## Package, work, edition/release, entity, and revision

`SourcePackage` is the distribution/access boundary. It carries provider, license, and public/restricted access metadata. A package may contain one or more works.

`SourceWork` is a publication identity: a book, article, SRD, third-party publication, or comparable source work. Do not use one giant work merely because several publications share a brand such as Unearthed Arcana.

`SourceEdition` is a particular release/version of a work. It can record a canonical D&D game edition, release kind, and publication date independently from its release key/display name. An errata-integrated release, revised printing, or separately versioned release of the same publication may therefore remain under one work without becoming the same source entity revision as another publication.

`SourceEntity` is one rule-bearing item as it appears in one source release.

`SourceEntityRevision` is an immutable revision of that same source identity in Rules Core. It is used when the representation of the same source entity changes, such as an upstream correction or changed reimport. It is **not** the mechanism for representing a later UA article, later book, or later D&D edition.

## Unearthed Arcana

Unearthed Arcana is source/publication provenance, not a D&D edition. It has appeared in different forms across D&D history. Do not infer `playtest` merely from the words `Unearthed Arcana`: some uses of the name are published books, while other uses are playtest/preview material.

Each actual publication/release receives its own normal source identity and explicit release metadata. Historical/developmental relationships among the contained rules are represented through source lineage.

## Stable key guidance

Keys should identify source objects, not encode a Dorks & Dice ruling. Prefer short stable source identifiers that can survive a later change in display wording.

Examples:

```text
package: wotc-srd
work:    srd-5-1
release: original

package: wotc-ua
work:    ua-example-article
release: 2017-04
```

These are examples of shape rather than mandatory names. The important constraints are stable identity, explicit provenance, and separation between source history and Rules Layer adjudication.
