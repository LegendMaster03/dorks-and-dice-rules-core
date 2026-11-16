# Pillar 2 — Authenticated Mixed-Descendant Transmission

**Date:** November 15, 2026

**Repository:** `LegendMaster03/dorks-and-dice-rules-core`

**Branch:** `feature/pillar2-power-evaluation-prototype-20261114`

**Draft PR:** [#157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157)

**Status:** Implementation and tests on draft branch. Do not merge or deploy without owner approval.

## What was already implemented

Prior to this increment the universal Deconstruct → Combine → Reconcile → Manifest → Reconstruct process supported direct Species, player-selected native mixed biological donors, bounded native eligibility, Red/Bronze Breath Weapon recombination, source-bound Goliath dependencies, Drow linked choices and level-gated Darkvision, typed sense/resistance overlap and a *read-only* allocation preview. Native biological eligibility included three physical foundation slots and six explicitly reviewed traits. `ManifestedProfile` contributors could be deconstructed into unreviewed candidates, but no native rule authenticated or admitted those offers.

## What this increment changes

1. **Trusted source boundary:** `INativeOriginManifestedContributorAuthority.ResolveCurrent(source, trustedScope)` may retrieve a completed `NativeOriginProfile` with an authoritative revision. `NativeOriginManifestedContributorVerifier` checks the requested source ID, optional requested revision, nonempty returned revision, completed authorization status and grant provenance before substituting the returned **Underlying** configuration. When a resolver is supplied and verification fails, it deliberately clears the unverified snapshot and reports `manifested-contributor-unverified`. The legacy unverified nested snapshot remains unreviewed without a resolver, and may still be separately subject to existing explicitly authenticated table decisions; it is not first-party eligibility.
2. **First-party transmission from verified state:** `NativeOriginBiologicalTransmissionEvaluator` recognizes only verified manifested contributors with a biological-parent role, one effective creature type whose native defaults support biological origination, and a grant whose original native characteristic is eligible under the existing biological policy. Mere historical genealogy or the ancestor's old Species packages do not provide offers. Other origination methods are untouched.
3. **Complete chosen manifestation:** The descendant selects each transmitted candidate. Inherited configured values and parameters are offered as already manifested by the verified parent. Resolved persistent choices are carried through, with no automatic reopening of unmanifested alternatives; the normal authorization, selection, dependency and reconciliation gates still run. Existing runtime resource links are never blindly imported into the descendant.
4. **Lineage and revision:** `NativeOriginEffectiveGrant.InheritedFrom` records zero or more immediate verified contributors, each with its local contributor ID, verified parent profile ID/revision, original source grant ID, prior source candidate list, prior per-parameter sources, and historical shared-resource owner links. This is audit metadata, not another grant. `NativeOriginReconciler` aggregates this metadata for all actual contributing candidates in consolidated or recombined grants. The descendant's own `SourceCandidateIds` and `ParameterSources` point to the descendant's candidates. Earlier generations can be traced through their stored parent revisions; the structure does not embed an unbounded ancestry tree.
5. **Defensive serialization checks:** `NativeOriginContractValidator.ReviewProfile` rejects malformed, empty, duplicate, or source-inconsistent lineage records when present. Validation alone does not verify account/campaign permissions or that a stored parent snapshot is authoritative.

## Temporary test-only self-override

`NativeOriginAuthenticatedDescendantTests.LocalSelfOverride` is an in-process test implementation of the authority interface. It supplies a known profile for the `campaign/local-test` scope and a deterministic revision. This permits meaningful tests without changing the site. It is not a browser-supplied `isOwner` flag or a global bypass. Production integration must authenticate the current saved underlying record, actual owner/campaign scope, and revision before providing this interface to the construction engine.

## Covered regression scenarios

- Mixed Human/Elf parent passes only its current underlying physical foundation, Low-Light Vision and Trance, despite caller-supplied histories, independent grants and active-form fakes.
- Unverified nested snapshots do not become first-party eligibility; stale revision, wrong profile ID and wrong trusted scope do not create offers.
- A configured Red/Bronze Dragonborn Fire/Cone Breath Weapon is inherited as **one** configured trait with one resource pool, its original authoring field sources retained as prior-generation evidence; unchosen alternatives and Draconic Resistance are not added.
- Two verified previously mixed parents contributing the same canonical vision grant consolidate to a single effective grant with both parent lineages.
- A second descendant generation accepts the already manifested configuration and retains the immediately preceding verified parent reference, without recovering unmanifested ancestors.
- A verified underlying Construct type without biological method availability cannot automatically transmit biological first-party grants.
- Serialized lineage tampering is rejected; valid JSON round trips preserve lineage.

Tests: `tests/RulesCore.Mechanics.Tests/NativeOriginAuthenticatedDescendantTests.cs`, along with existing construction/biological catalog tests.

## Remaining work and decisions

- **Production trust adapter:** implement an authenticated server-side lookup with current profile revision and stored completed state; deliberately deferred during self-override testing. Do not expose a caller-controlled bypass or trust arbitrary client-provided profiles.
- **Canonical trait coverage:** resolve actual native mechanics and source-bound relationships for Human skill/rank/feat choices, Elf/Tiefling spells, Dwarf HP progression, Goliath upgrades, movement modes, persistent choices and other still-unreviewed traits before first-party opt-in. Avoid forcing unrelated progression bundles into one inheritance grant.
- **Complete integration:** validate downstream Character Sheet, level-based consumption, action/resource execution, hit points, skill/competency choice handling and innate spell use. Current projector coverage is bounded; the site has not been modified.
- **Allocation:** the existing read-only load preview remains advisory. A meaningful biological capacity/bundle admission policy is not approved; do not silently adopt numerical thresholds, use the research evaluator as a mandatory gate or count extra parents as extra capacity.
- **Other originations:** construct assembly, plant reproduction, spawning, magical creation and supernatural reconstruction retain their existing independent procedure boundaries. This increment does not generalize biological transmission to them.

## Reproduction and CI

From repository root:

```bash
dotnet test tests/RulesCore.Mechanics.Tests/RulesCore.Mechanics.Tests.csproj -c Release --filter FullyQualifiedName~NativeOriginAuthenticatedDescendantTests
dotnet test RulesCore.slnx -c Release
python3 -m unittest discover -s tests -p 'test_*.py'
```

Also require both workflows: `Validate Rules Core Rewrite Structure` and `Rules Core Phase 3B Public Native API CI`. Their successful results prove the tested native subsystem, not completed game balance or an integrated production website. Consult draft PR #157 and its current head for final test status.

This record exists to permit incremental development to resume without reconstructing the decisions from PR discussion. Preserve the test-only self-override until site integration work is separately authorized.
