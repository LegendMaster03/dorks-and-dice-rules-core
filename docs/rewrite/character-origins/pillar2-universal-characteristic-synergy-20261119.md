# Pillar 2 — Universal catalog-authored characteristic synergy

**Branch:** `feature/pillar2-universal-characteristic-synergy-20261119`  
**Baseline:** `e4f5a279d786ef56063364d62284da8c897ea80a` (PR #157)  
**Scope:** Shared Rules Core characteristic interactions; no first-party gameplay traits or procedure-specific acquisition rules.

## Reused foundation and gap

The merged construction engine already deconstructs native Species and Ancestry sources, verifies actual manifestations and scoped table decisions, reconciles collisions, binds dependent choices and resources, and closes effective prerequisites. Constraints already support `Requires`, `Exclusion`, `SelectionCardinality` and `LinkedParameters` with exact native or authenticated policy evidence. Those relationships are **not** replaced.

Before this increment, no catalog-authored interaction could **mint a new canonical effective characteristic from multiple already effective grants**. Proposed relationships and the presence of selectable offers were insufficient to do so.

## Native authored interaction grammar

A native entity with `kind: "rule"` may declare `originSynergies`. Authenticated table decisions may provide individual definitions using the same interaction shape. A synthetic example, **not approved gameplay content**:

```json
{
  "id": "rule.example",
  "kind": "rule",
  "name": "Example",
  "originSynergies": [
    {
      "id": "stabilized-locomotion",
      "resultTraitId": "trait.example.stability",
      "inputs": [
        {
          "anyOf": [
            { "mechanicalDefinitionId": "trait.example.mechanical-actuator" },
            { "mechanicalDefinitionId": "trait.example.alchemical-actuator" }
          ],
          "count": 1
        },
        {
          "anyOf": [{ "mechanicalDefinitionId": "trait.example.stabilizer" }],
          "count": 1
        }
      ]
    }
  ]
}
```

`resultTraitId` must point to a native `origin-trait` with a canonical `trait` object. That object supplies the exact effective value; the client does not submit the result payload. All `inputs` groups must be satisfied; each `anyOf` is a set of explicitly authored alternatives, and `count` requires distinct effective grant instances. A single grant can not satisfy two slots. Supported selector fields are `mechanicalDefinitionId`, `intrinsicSlotId`, `sourceId`, `parameterKey` and `expectedValue`, using the existing selector contract. Proposal-local `candidateId` selectors are intentionally not permitted in catalog-wide authoring.

Only the implemented interaction, input-group and selector fields are executable. Unrecognized fields (for example, a proposed `requiresPoweredMode` operating condition or selector negation) are rejected with `synergy-authoring-invalid`, not silently ignored. Optional interaction metadata is limited to `name`, `description` and `reviewNotes`. Supported input-group fields are `anyOf` and `count`.

A synergy requires at least two total inputs. The currently supported bound is 16 total input slots and at most 16 alternatives per group. No implicit equivalence is inferred from trait names, descriptions, materials, Species, or the origination method. Multiple donor source occurrences can satisfy distinct slots if their effective grants remain distinct after reconciliation. An identical occurrence consolidated into one grant counts once.

## Evaluation and lifecycle

1. The existing pipeline resolves all player selections, authorization, collisions, constraints, and dependent bindings to a final effective set.
2. The synergy evaluator reads **native catalog definitions and verified table definitions**, never unverified caller-provided interaction claims. Only already effective grants may satisfy input selectors.
3. A deterministic, distinct-input matching pass selects contributing grants. Each derived grant retains its existing `NativeOriginEffectiveGrant` shape, canonical trait value, native rule authorization basis, and complete `NativeOriginSynergyEvidence` with input grant, manifestation, characteristic identity and source candidate IDs.
4. Derived characteristics can satisfy inputs to later synergies. An interaction issues at most one result; identical result trait definitions are not automatically duplicated. If multiple *eligible* interactions independently produce the same canonical trait, evaluation returns `synergy-result-ambiguous` and grants neither result rather than silently selecting whichever rule sorts first. This also applies if the conflict emerges only after a later transitive pass: the evaluator rebuilds the closure without the disputed outcome, retracting any dependent synergies while retaining unrelated results. Authors can express intentional alternatives within one `anyOf` group. The grant and manifestation identifiers are stable hashes of the authoring rule and interaction identifiers. Rule graph keys use length-prefixed owner IDs so delimiter-containing IDs do not collide.
5. Removal or suppression of an input on a subsequent construction reevaluation prevents the dependent result. Transitive dependencies are consequently removed. No stored result is blindly carried forward.
6. Detected authored interaction cycles fail closed with `synergy-cycle-unresolved` when relevant inputs are present. The evaluator never recursively mints unlimited outputs.
7. Profile review replays native authored synergies from direct effective grants and compares each expected derived grant with its complete serialized record. For verified table decisions, the trusted construction engine passes its authenticated subset to the same replay; an unauthenticated structural review never independently trusts table-supplied definitions. Forged or omitted derived grants are invalid.

The resulting grant is stored alongside other effective inherent grants in `NativeOriginProfile.Underlying`. It is not an independent entitlement, procedural power budget, second mechanical grant class, or directly selectable candidate. This deliberately avoids fake manifestation records. To protect that identity boundary, caller-supplied manifestation IDs beginning with `synergy:` are invalid. Native rule and table interaction IDs may contain ordinary separators such as colons but may not contain CR/LF delimiters, which would make the stable hashed identity ambiguous.

## Boundaries

This increment supports **additional, fixed canonical Origin traits**. Results with unresolved `originBindings`, `requiredCharacterLevel`, creation choice menus (including `chooseFrom`, `choiceCount`, `abilityChoice`, and `grantType: "feat-choice"`) or resource dependencies (`uses`, `sharedUsesFromTrait`, `resourcePool`) and descriptive-only traits without a normalized mechanic are rejected rather than silently activating incomplete mechanics. It does not yet implement in-place alterations of existing grant values, dynamic operating-condition evaluation, level-gated benefits, choice-driven synergy results, arbitrary parameter rewriting, or resource-pool creation from a synergy. These require additional explicit authoring and trusted binding semantics rather than inference.

Native catalog-defined and **authenticated table-authored** synergies use the same matching and grant construction path. A table ruling must have `Target.Kind = "origin-synergy"`, `Target.Id` equal to the authored interaction's `id`, and a complete `EffectiveOutcome` object using the same `id`, `resultTraitId` and `inputs` grammar shown above. The existing `INativeOriginAuthority` must validate that decision against the request's trusted scope (or an expressly accepted proposal scope) before the evaluator receives it. The resulting effective grant has `NativeOriginSourceKind.Custom`, a stable source identifier `table-decision:<decision-id>`, a `DmAuthorized` basis referencing that exact decision, and verifiable contributing input evidence. Duplicate table interaction targets, unverified decision claims, and mismatched target IDs fail closed. This does not change campaign authentication policy.

A standalone public `NativeOriginContractValidator.ReviewProfile` does not have authentication context and therefore can not independently authorize or replay table-authored synergies. The trusted construction pipeline uses its internal verified-decision replay to check their exact grant records. External readers must not treat structural profile data as a substitute for host authorization.

No first-party Species, Construct Assembly component, biological transmission policy, numerical characteristic cost, or transformation mechanic has been added. Construct Assembly and Biological Reproduction can author their own acquisition procedures and compatible native interaction definitions without changing this evaluator.

Consumers that expose canonical Origin grants can read these results through the existing profile structure. Projector-specific recognition of rule-owned synthetic trait occurrences still requires targeted compatibility checks; this increment does not silently make every trait value executable by every specialized projector.

## Acceptance verification

The synthetic xUnit fixture in `NativeOriginSynergyTests.cs` tests: two-input construct-style stability, wings plus musculature, offered-but-unmanifested traits, three-input composition, explicitly authored alternative actuators, transitive effects, effective input removal, duplicate input prevention, cycle fail-closure, forged native candidates, forged derived provenance, deterministic reconstruction, JSON round-trip, verified table-authored rules, mismatched and forged table claims, conflicting table rule targets, rejection of choice- or resource-dependent outputs, fail-closed unknown rule and selector fields, two distinct offers reconciled into one effective input, order-independent proof replay, ambiguous duplicate outputs, collision-safe table interaction IDs, reserved generated manifestation IDs, and rejection of newline-delimited hash collisions, delayed conflicts and transitive retractions, malformed host authority records, and descriptive-only output traits.

Relevant commands:

```bash
dotnet test tests/RulesCore.Mechanics.Tests/RulesCore.Mechanics.Tests.csproj -c Release --filter FullyQualifiedName~NativeOriginSynergyTests
dotnet test RulesCore.slnx -c Release
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

Do not equate an ordinary synthetic test passing with gameplay balance, general Construct Assembly implementation, or approval of first-party synergies.
