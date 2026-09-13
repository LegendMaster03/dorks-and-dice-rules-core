# Source identity conventions

Rules Core separates D&D edition identity, publication/release identity, imported representation identity, and Rules Layer concept identity. These conventions should be followed before bulk source ingestion so durable source keys do not encode accidental assumptions.

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

## Representation provenance and canonical identity

Two distinct identity systems coexist deliberately.

The existing Source Layer hierarchy records the imported **representation** and its access boundary:

`SourcePackage -> SourceWork -> SourceEdition -> SourceEntity -> SourceEntityRevision`

A package is still the distribution/access boundary. Account-added packages remain separate even when two users independently supply the same publication. The package-owned work/release hierarchy therefore preserves the provenance of what was actually imported and from where.

Above those representation-specific records, Rules Core now maintains format-independent identity:

`CanonicalPublication -> CanonicalSourceOccurrence`

A representation-specific `SourceEntity` can be associated with a canonical occurrence. Canonical identity records contain descriptive identity metadata, aliases, locators, confidence, and fingerprints; they do not contain a globally readable replacement for the restricted source document and do not grant source access.

This distinction allows, for example, a 5e.tools representation of the *Player's Handbook* and a later compatible PDF-derived representation of that same book to remain separate imports while being recognized as representations of the same canonical publication and source occurrences.

## Source package, work, edition/release, entity, and revision

`SourcePackage` is the distribution/access boundary. It carries provider, license, and public/restricted access metadata. A package may contain one or more works. An account-specific import package can coexist with another package representing the same real-world publication.

`SourceWork` is the publication identity **within that imported representation**: a book, article, SRD, third-party publication, or comparable source work as supplied by the package. Do not use one giant work merely because several publications share a brand such as Unearthed Arcana. Cross-package sameness is represented by canonical publication identity rather than by forcing unrelated packages to share one SourceWork row.

`SourceEdition` is a particular release/version of a representation-specific work. It can record a canonical D&D game edition, release kind, and publication date independently from its release key/display name. An errata-integrated release, revised printing, or separately versioned release of the same publication may therefore remain under one work without becoming the same source entity revision as another publication.

`SourceEntity` is one rule-bearing item as it appears in one imported source release.

`SourceEntityRevision` is an immutable revision of that same representation-specific source identity in Rules Core. It is used when the representation of the same source entity changes, such as an upstream correction or changed reimport. It is **not** the mechanism for representing a later UA article, later book, or later D&D edition.

`CanonicalPublication` identifies the real publication independently of import package or file format.

`CanonicalSourceOccurrence` identifies one rule-bearing occurrence within a canonical publication. It is still distinct from a Dorks & Dice `RuleConcept`: the same conceptual rule may occur in several publications and editions.

## Canonical publication matching

Format adapters emit evidence rather than choosing database IDs directly. Rules Core resolves that evidence conservatively in this order:

1. a strong format/provider alias, such as an exact 5e.tools source code already associated with a canonical publication;
2. an exact normalized bibliographic fingerprint derived from publication title, publisher, game edition, and publication date, when it is unambiguous;
3. exact overlap among already known rule-bearing occurrence fingerprints, requiring at least three matching occurrences and a unique best publication;
4. otherwise a new canonical publication is created rather than guessing.

The alias is evidence from a representation, not the canonical identity itself. This prevents the database from making 5e.tools-specific identifiers the permanent cross-format key.

## Canonical source-occurrence matching

Within a known canonical publication, entity type plus normalized name supplies the normal occurrence identity. Representation-specific metadata such as source code, page field encoding, import IDs, and JSON property ordering does not define the canonical occurrence.

Adapters can also supply a semantic fingerprint that excludes top-level display/provenance markers such as name, source code, page, edition flags, and representation IDs. When an adapter can not reproduce the same display name but produces an exact semantic fingerprint, Rules Core can associate it with an existing occurrence only when that fingerprint resolves uniquely within the same publication and entity type. A locator such as a page can further constrain the match.

Ambiguous semantic matches do not collapse occurrences automatically. Approximate OCR/text similarity is not treated as proof of identity.

## Future PDF adapter

PDF support does not require changing Add Source or the canonical identity model. A future PDF adapter should perform this pipeline:

`uploaded artifact -> publication evidence -> extracted source occurrences -> canonical association`

For a digitally generated PDF, publication evidence may include title, publisher, ISBN/product identifiers, copyright/publication date, edition, printing/revision markers, and page coordinates. Extracted rules then produce the same format-neutral occurrence evidence used by structured importers.

For a scanned PDF, OCR can provide candidate text but introduces uncertainty. Exact bibliographic identifiers or exact structured occurrence fingerprints may still permit an automatic association. Fuzzy/OCR-only matches should remain review candidates rather than silently merging source identity.

The PDF adapter's responsibility ends at source identity and extraction. It does not decide that two occurrences from different publications are the same Dorks & Dice rule concept; that remains the Rules Layer normalization/adjudication workflow.

## Access boundary

Canonicalization must never broaden source access. Source grants continue to be evaluated against the representation-specific `SourcePackage`. Two representations can point to the same canonical publication/occurrence while remaining independently restricted.

A user who supplied representation A does not gain access to representation B merely because both are recognized as the same publication. Likewise, another user does not gain content access merely because Rules Core already knows the canonical publication metadata or fingerprints.

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

These are examples of representation-specific key shape rather than mandatory cross-format names. Canonical publication identity is resolved separately. The important constraints are stable identity, explicit provenance, separation between imported representation and real publication identity, and separation between source history and Rules Layer adjudication.
