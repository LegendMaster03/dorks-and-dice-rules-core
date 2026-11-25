# Expanded 5e.tools upstream comparison

Status: diagnostic comparison; no additional native promotion or licensing determination.

The existing corpus is pinned to CoolFireGiant/hewnhero-srd at
`d06d1dadee357857767b1e4da985df6609509bcf`. Its reviewed projections and publisher
corrections remain authoritative within each native package. Compare against
5etools-mirror-3/5etools-src v2.41.3 at `2512db75eb48ef06f6190b086a82430887906dca` (2026-11-23).

The upstream Git tree contains 544 files under data/ (63 root, 104 adventure,
216 bestiary, 68 book, 49 class, 11 generated, 33 spells). Unlike the existing
corpus, the upstream uses publication identities such as PHB/XPHB and Boolean
or string srd/srd52 flags. String flags can identify alternative SRD names.
Magic variant flags are commonly under inherits. Untagged records, embedded
definitions, and metadata require separate classification; a flag is not
publisher-source or license verification.

## Initial live comparison

| Source category | Flagged | Identity matches | Unmatched |
|---|---:|---:|---:|
| Spells (5.1 / 5.2.1) | 319 / 339 | 319 / 339 | 0 / 0 |
| Skills | 36 | 36 | 0 |
| Senses | 8 | 8 | 0 |
| Actions | 37 | 36 | 1 |
| Variant rules | 116 | 115 | 1 |
| Fighter classes/subclasses/features | 66 | 66 | 0 |
| Magic variants | 92 | 63 | 29 |

These are identity comparisons against the existing pinned corpus, not a
certification of native coverage. Use an Object already has a corrected
legacy native identity. Reach is now recovered as glossary.reach from the upstream record at data/variantrules.json#/variantrule/179. Its complete body agrees with official SRD 5.2.1 p.186; the existing publisher recovery generator checks the exact right-column section. The expanded source pointer is tracked separately from original corpus pointers and validated by the comparison audit. The
variant candidates require comparison against existing concrete and template
item definitions rather than automatic import. The source tree and selected
file comparisons were inspected via GitHub; no local upstream checkout was
available in the chat environment.

## Reproduce complete diagnostic inventory

Obtain a clean checkout of the exact upstream commit outside the authoritative
data tree. For example, from the Rules Core root:

```sh
git clone --no-checkout --filter=blob:none https://github.com/5etools-mirror-3/5etools-src.git build/5etools-upstream
git -C build/5etools-upstream sparse-checkout set data
git -C build/5etools-upstream checkout --detach 2512db75eb48ef06f6190b086a82430887906dca
python3 tools/audit_srd_upstream.py --checkout build/5etools-upstream --output build/srd-review/upstream-comparison.json
```

The diagnostic reads the tracked data tree, validates the exact checkout
commit, reuses the existing pinned corpus and native promotion report, and
records candidate source pointers, contextual identities, ambiguous matches,
native name hints, and field-name differences. Unhandled nested flags are
counted separately. The report contains no upstream entry bodies or artwork.
It is not a new importer or a completeness claim. Resolve remaining
discrepancies against official publications before changing generators.

The SRD Edition Review Catalogs workflow runs this comparison against the
pinned data checkout and includes the report in its existing artifact. No
upstream scripts are executed; the checkout is not a runtime data source.
