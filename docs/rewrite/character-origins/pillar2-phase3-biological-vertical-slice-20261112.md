# Pillar 2 Phase 3 — Biological mixed-Origin vertical slice

**Date:** 2026-11-12
**Branch:** `feature/pillar2-phase3-biological-origin-20261112`
**Base:** `feature/pillar2-characteristic-inventory-20261111`
**Scope:** C# native construction pipeline and selected-native content. No API or Character Sheet changes.

## Implemented behavior

- The same `NativeOriginSourceDeconstructor` now traces native Ancestry contributors through their authored parent Ancestries to Species. Each input retains its contributor ID and each shared family trait retains the actual source occurrence. A historical source on a previously manifested contributor does not become an offer.
- The source's explicit `originBindings` definitions associate mechanically relevant trait parameters with an authored selected descendant field (`leaf`) or nearest suitable ancestor (`nearest`). This binds Dragonborn resistance, ordinary Breath Weapon damage and whole area, and Chromatic Warding without creating a Species-pair dispatcher. Source trait definitions remain immutable, while the evaluated grant carries actual values in `Parameters`.
- `NativeOriginDependentBinder` resolves explicit `manifested-intrinsic` bindings **after reconciliation**. The selected High Elf or Wood Elf spellcasting-ability characteristic must remain effective and belong to the trait's original Ancestry occurrence. An incomplete or ambiguous binding excludes the dependent trait and reports an issue; independently granted characteristics survive.
- Elf magical-sleep immunity now has a structured native field preserving both immunity and the explicit consent exception. The full combat/spell effect remains downstream execution, not a construction-phase operation.
- The existing `INativeOriginAuthority` is still required for mixed-contributor selection and scoped overrides. Native parentage and a matching trait label alone never confer transmission eligibility.
- Tests exercise real Human, Elf, High Elf, Wood Elf, Red and Bronze Dragonborn content, exact-source duplicate handling and nested profile provenance.

## Approved scope versus unresolved first-party policy

**Previously approved and implemented more completely**

1. Standard Species/Ancestry traits are selected from the native source chain, with independently selected Upbringings kept outside biological grants.
2. Dragonborn ordinary Breath Weapon independently carries a damage type and a *whole* area parameter; resistance and warding retain their separately authored type references and fixed operational constraints.
3. The currently manifested traits of a mixed contributor may be offered to offspring; history alone is not transmission eligibility.
4. DM decisions require trusted, exact scope/target/authority verification and do not modify native definitions.

**Normalized authored semantics, not new balance policy**

- Explicit `originBindings` encode values already referenced by the approved native traits. They do not authorize mixing unrelated traits or automatically select a preferred damage type.
- High/Wood Elf spellcasting traits now explicitly depend on the same source's chosen spellcasting ability. The source has always offered a finite ability choice; the binder does not create an additional ability.
- The magical-sleep structure preserves the existing Elf exception for consent.

**Policy decisions still requiring approval**

1. **Direct intrinsic replacement:** Wood Elf's authored walking speed is 35 feet while the Elf parent is 30 feet. The pipeline retains both and requires a supported relation or a verified DM decision. Proposed default: a directly selected descendant's explicitly authored intrinsic value replaces the corresponding ancestor value *for that direct lineage only*. Do not apply this rule automatically to mixed biological inputs.
2. **Mixed characteristic transmission:** No general first-party quota, eligibility rule, inheritance ratio or species-classification outcome has yet been approved. A mixed Human–Elf fixture proves that a table can construct a coherent effective configuration through verified per-candidate choices and a verified custom configuration identity. This is **table-authorized**, not a default unrestricted Human–Elf recipe.
3. **Cross-Ancestry spellcasting:** High Elf and Wood Elf spell traits keep their distinct source-bound ability choices. Whether an individual with both may select one shared spellcasting ability, retain two or substitute one source for the other still needs a rule or a scoped DM outcome.
4. **Cross-contributor equivalence and shared resource usage:** Duplicate *identical authored occurrences* consolidate evidence. Different occurrences, even with similar names, remain separate unless a verified relationship justifies combining or replacing them.
5. **Dragonborn mixing:** Source-derived Fire/Lightning and line/cone offers are preserved. A first-party rule for whole-profile mixed Dragonborn type/resistance/warding consistency has not yet been approved; no new coupling policy is inferred.
6. **Latent traits and temporary forms:** A prior unmanifested characteristic is not offered merely because it appeared in an ancestor's history. The rule for reproduction during a temporary transformation remains unresolved; underlying and effective state remain separate.

## Verification and acceptance

The Phase 3 regression tests are in `NativeOriginConstructionTests.cs` and exercise the real C# evaluator. The tests use `TestAuthority` to supply exact scoped DM approvals and mock open-catalog eligibility proofs. They are **not** evidence that the live Feat and Skill eligibility adapters exist. Missing trusted choices must remain incomplete/unresolved.

Target matrix:

| Case | Expected evaluation boundary |
| --- | --- |
| Standard Human | Authorized only when Small/Medium, Skillful, and Versatile choices have trusted eligible selections; otherwise incomplete |
| Standard High Elf | Authorized with a validated ability and cantrip selection; dependent spells inherit the authored source ability |
| Standard Wood Elf | Native speed conflict unresolved without policy; scoped DM relation can select the authored 35-foot value |
| Human + Elf | Selected coherent test profile authorized by verified table decisions; no automatic full Species stacking |
| High Elf + Wood Elf | Shared Elf source occurrence can be consolidated; independent Ancestry spell choices are not arbitrarily merged |
| Red/Bronze direct | Native lineage binds source damage type and complete Breath Weapon area; respects distinct families/adaptations |
| Identical contributors | One effective grant, multiple exact source-candidate IDs |
| Nested previously mixed | Only current grants offered, all original provenance retained, history not restored |
| Unverified or missing authorization | No silently effective grant |
| Active temporary form | Existing distinction between authoritative baseline and temporary effective form preserved |

Build and test verification is reported by CI for the pull request. The document does not claim that local .NET tests ran in an environment without the .NET SDK.

## Architectural boundary and Phase 4 readiness

These changes are source-neutral additions to the existing five-stage engine; they do not create a biological-only constructor. The engine can continue to represent nonparent inputs, unspecified contributor counts and complete replacement versus temporary effective forms.

Before Phase 4 can demonstrate *authorized* Plant, Construct, Fiend or Rite behaviors, the selected process-specific source eligibility/characteristic contracts and actual reconstruction transitions need their own rules. In particular, `Rite of Rebirth` must replace underlying inherent grants without restoring superseded history or deleting independent entitlements. The current Phase 3 change does not execute transformations.

Do not equate a verified table-specific mixed profile with adoption of the unapproved first-party defaults above. Do not merge this branch into `main` or begin Phase 4 as part of this work.
