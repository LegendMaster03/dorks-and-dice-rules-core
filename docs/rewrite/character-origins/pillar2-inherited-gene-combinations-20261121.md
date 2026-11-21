# Pillar 2 — Selected inherited-gene combinations

**Branch:** `feature/pillar2-inherited-gene-combinations-20261121`  
**Baseline:** `9f50ab70a37be69a6748603d210fa9406ba7ea59`  
**Scope:** Generic expression from authenticated inherited instances using the existing universal synergy authoring and grant model. No playable first-party combination or point value is established.

## Inheritance and expression remain separate

Biological transmission selects *exact contributor-local candidate occurrences*. The construction evaluator finalizes the genotype independently of effective manifestations. The default limit remains two inherited instances per canonical kind and one effective expression per kind; authenticated capacity overrides remain supported. There is no separate latent-instance quota, dominance/recessiveness model, or random selection. Latent genes provide no ordinary mechanical benefit.

An inherited combination is **not automatic**. All of the following are required:

1. The interaction is authored in the native `originSynergies` collection, or carried by a scope-verified `origin-synergy` table decision.
2. The proposal has an explicit biological transmission plan containing the exact gene candidate IDs.
3. The player selects the interaction and names the exact input candidate IDs in `BiologicalTransmission.Combinations`.
4. The inheritance evaluator can authenticate and complete each instance, including any previous-generation configuration and lineage.
5. The selected input set meets all authored selector counts, parameter predicates, and the expression-capacity policy.
6. The canonical result is a fixed, mechanically defined `origin-trait`. It is issued once; source instances remain inherited but their ordinary expressions are retracted.

The combination grant retains the complete, verified `NativeOriginBiologicalGeneInstance` inputs in `Synergy.InheritedInputs`, including original occurrence, parameters, finite choices, and immediate verified parent provenance. Its source candidate IDs name the local inherited offers, not the older donor's raw grants. The shared effective-grant result can participate in ordinary downstream synergies, and dependency/resource closure is rerun after consumed grants are suppressed.

## Authoring a rule

The existing `originSynergies` grammar has an optional `inputEvidence` property:

- Absent or `"effective-grant"`: existing automatic effective-grant interaction behavior.
- `"inherited-gene"`: selected inherited-instance interaction; never activates merely because eligible offers or latent genes exist.

The following is a **synthetic illustration**, not approved playable content:

```json
{
  "id": "synthetic-double-shell",
  "inputEvidence": "inherited-gene",
  "resultTraitId": "trait.example-combined-shell",
  "inputs": [
    {
      "anyOf": [
        { "mechanicalDefinitionId": "trait.example-shell" }
      ],
      "count": 2
    }
  ]
}
```

`trait.example-combined-shell` must be an existing, catalog-backed fixed `origin-trait` with a normalized mechanical effect. The evaluator rejects display-only output, resource pools, selectable options, unbound parameters, and unknown fields. The rule does not define an armor formula or point cost. For table authoring, the authenticated decision target is `{ "kind": "origin-synergy", "id": "synthetic-double-shell" }`, and the effective outcome carries the same rule definition. The rule owner used when selecting that table interaction is `"table-decision:"` followed by its exact decision ID.

A selected biological transmission plan includes:

```json
{
  "candidateIds": ["gene-offer-a", "gene-offer-b"],
  "combinations": [
    {
      "ruleOwnerId": "rule.example",
      "interactionId": "synthetic-double-shell",
      "candidateIds": ["gene-offer-a", "gene-offer-b"]
    }
  ]
}
```

Actual candidate IDs come from discovery; these placeholders do not authorize any offer. Each input slot must match a distinct authenticated gene. `mechanicalDefinitionId`, `intrinsicSlotId`, `sourceId`, and `parameterKey` with `expectedValue` reuse the existing selector semantics. A configured gene is matched by its own instance parameters, not by combining fields from two separate instances. Source IDs and canonical definitions are not fungible.

## Replacement, capacity and conflict behavior

The construction pipeline completes the genotype, evaluates selected inherited expressions, removes the direct grants of consumed gene occurrences, and re-closes downstream dependencies. It then computes ordinary effective-grant synergies against that final effective set. The resulting combined phenotype is **one effective grant** even if both inputs were latent or one was expressed. It does not create two stacking modifiers, copy resources, or reauthorize independent latent abilities.

A missing authoring rule, mismatched parameter, forged candidate ID, unverified parent, repeated input, duplicate selected interaction, competing result, or incompatible expression capacity yields a construction issue rather than an upgrade. Competing combinations fail together rather than picking a winner based on ordering. Existing native direct Species selection and nonbiological methods do not use this extension.

The `NativeOriginBiologicalCombination` projection reports the single shared combined grant as an expression of its contributing inherited instances. It does not alter the stored genes. Profile review recomputes both inherited and ordinary synergy evidence rather than trusting serialized proof.

## Pricing and integration

The result enters the **existing complete effective-grant creation-allocation ledger** and is subject to the same host-configured budget gate as other expressed characteristics. No new number, special discount, or free combination is authorized by this track. The characteristic-pricing track must price the canonical result and configuration using its independent policy. An absent or incomplete required price schedule remains unresolved; the mere presence of genetic evidence adds no expenditure or benefit.

The Character Sheet and Rules Wiki need to expose the authored interaction only when the required inherited instances are actually available, accept the explicit combination selection, and present one resulting effective grant with its source-instance evidence. A combination result with configurable output choices or resource semantics requires separate design and authoring support; this implementation deliberately supports fixed canonical output only.

## Test scope

The focused integration tests use actual Orc transmitted Powerful Build instances and a synthetic authenticated rule whose fixed result is the existing Amphibious origin trait. They exercise both latent and expressed source cases, absent selection, unverified table authorization, nonexistent gene offers, configuration mismatch, result conflicts, and authenticated transmission from a mixed-heritage parent. Existing effective-grant synergy tests remain the compatibility baseline.

No Natural Armor mechanics, armor value, balance price, new Species ability, or parallel heredity engine is introduced.
