# Bounded package licensing review — 2026-11-25

Reviewed baseline 6a300933435c633b51f1d446fca09b9810dceb9e. This records evidence, not distribution authorization
or exhaustive source-fidelity certification.

| Package | Publisher license | Evidence / remaining limits |
| --- | --- | --- |
| first-party-001 | No package-wide CC declaration | Do not label the package Creative Commons; individual source obligations remain distinct. |
| dnd-5e | CC BY 4.0 | Official 403-page SRD 5.1 PDF; 401 gameplay page records, pinned SHA-256 2504d2a0abb0a4d491a939be4f17910a2dde0312570ab8d208080225ccf0a1f0. Manifest notice matches publisher legal page. |
| dnd-5-5e | CC BY 4.0 | Official 364-page SRD 5.2.1 PDF; 360 gameplay page records, pinned SHA-256 8974902d109d6e63672d7c490bde9ccf052410503d9cfa768237154fbc5e3d87. Manifest notice matches publisher legal page. |
| fate-condensed | CC BY 3.0 Unported | Publisher open-license product page and official Fate licensing guidance; pinned Markdown commit 93ed848e723f31b4994b466b7241011ba24be885, blob cc313c53ee75939e1f5c53f0f2ee59181e78ee00; importer preserves full source text and verifies blob. Structured derivative records have distinct provenance. |

Publisher evidence:
- https://www.dndbeyond.com/srd
- https://media.dndbeyond.com/compendium-images/srd/5.1/SRD_CC_v5.1.pdf (legal page)
- https://media.dndbeyond.com/compendium-images/srd/5.2/SRD_CC_v5.2.1.pdf (legal page)
- https://creativecommons.org/licenses/by/4.0/legalcode
- https://evilhat.com/product/fate-condensed/
- https://fate-srd.com/official-licensing-fate/cc
- https://creativecommons.org/licenses/by/3.0/

The D&D notices in the existing manifests match the required statements.
Neither PDF legal page specifies a separate copyright line; both request no
additional Wizards attribution. CC BY permits use without separate publisher
permission if its conditions are met. Retain source attribution, license links,
applicable notices and identify changes. Do not imply endorsement. The Fate
manifest retains the named creators; its original-source URLs include the
publisher site referenced in official guidance.

D&D import scope evidence: data/reference/srd/manifest.json explicitly limits
scope to the two SRDs. official-page-import.json pins both publisher PDFs;
structured-manifest.json pins CoolFireGiant/hewnhero-srd at
d06d1dadee357857767b1e4da985df6609509bcf and labels its license an upstream
claim requiring record verification. The expanded comparison pins 5etools-src
at 2512db75eb48ef06f6190b086a82430887906dca. Existing edition promotion
documents distinguish publisher-recovered and derived entries, source exclusions
and remaining body review. The native source fidelity second-pass audit
documents non-SRD lore and mechanics in the original structured corpus, some
corrected in native candidates, and unresolved third-party metadata.
Publisher-page extracts are within the stated SRD scope; structured identities
and verified corrections support intended scope, but independently authored
prose, sidecars, commercial-only content and additions are not automatically
covered by Wizards' grant. Do not certify that every promoted fragment is SRD
material based on tags or an upstream pack license claim. No new import,
exhaustive fidelity run, or distribution approval was performed. Unresolved
third-party rights must remain a publication-review gate. Existing source
evidence is sufficient for the bounded license identification and notice
contract, not a clean bill of rights for all structured data.

## Fate pinned-notice reconciliation

The actual preserved Markdown legal heading names Ed Turner, Leonard Balsera and Ryan Macklin in addition to other creators; current online licensing guidance names Lara Turner and omits two of those names. Retain the exact pinned attribution and copyright notice, all pinned credit paragraphs (including the Fateful Concepts acknowledgment), and the existing current-guidance credit as an additional acknowledgment. The original text and generated entity provenance remain unchanged; the manifest now supplies the full package notice independently. Publication review should reconcile the two source-specific attribution formulations if the publisher provides further guidance.
