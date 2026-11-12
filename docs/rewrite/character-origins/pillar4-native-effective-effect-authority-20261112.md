# Pillar 4 — native effect definition and source authority

**Date:** 2026-11-12  
**Branch:** `feature/pillar4-catalog-effect-authority-20261112`  
**Parent:** `feature/pillar4-effective-projection-bridge-20261112` (draft PR #148, itself stacked on draft PR #146)  
**Status:** Reusable catalog-backed authorization component. No approved native supernatural progressions or host authentication adapter are introduced.

## What was missing

The previous `NativeEffectiveCharacterEffects` evaluator intentionally delegated to `INativeEffectiveEffectAuthority`. Its synthetic tests supplied an authorization stub that checked little more than a rule name. That is insufficient for production: a client could claim a recognized rule but supply a different source grant, target, operation or numerical value.

The new `NativeCatalogEffectiveEffectAuthority` implements that interface using exact, selected-native rule definitions **and** an independent trusted host proof. It refuses to treat a serialized Origin status, grant record or DM claim as self-authorizing.

## Native definition shape

For a selected native entity with `kind: "rule"`, an optional array `effectiveEffectDefinitions` can specify *individual executable definitions* to which a trusted earned source may refer. This is a **proposed opt-in authoring shape**, not a retroactive grant of existing creature abilities:

```json
{
  "id": "rule.example-effects",
  "kind": "rule",
  "name": "Illustrative effect definitions",
  "effectiveEffectDefinitions": [
    {
      "id": "example-strength-bonus",
      "sourceId": "feature.example-source",
      "sourceOccurrencePath": "/traits/example",
      "sourceMechanicalDefinitionId": "mechanic.example",
      "target": "ability-score",
      "targetId": "strength",
      "operation": "add",
      "numericValue": 2,
      "requiredFormId": null
    }
  ]
}
```

The example above illustrates field contracts only; no such first-party source, rule, power, or numeric bonus is added by this phase. Catalog loading retains the rule definition, while the effect authorizer executes **only exact values** and **only with a verified earned grant occurrence**. The source entity itself must exist in the same selected catalog.

The existing `NativeEffectiveEffectDirective` gains optional `DefinitionId`. This identifies the reusable authored effect definition while `Id` remains the **specific effect occurrence**. A native directive is accepted only when all of the following agree:

- selected native rule ID and category; declared effect definition ID must identify exactly one definition;
- exact source entity ID, source occurrence path, and source mechanical definition ID on the *earned grant*;
- exact operation (`add`, `replace`, `suspend`), target category and target ID;
- exact numerical value (or explicit null), and exact form scope (or explicit null);
- actual source entitlement separately verified by the authenticated host; independently earned grant applicability remains separately checked.

No source is inferred from a name, CR/monster record, inherited condition label, or similarly titled feature. The evaluator continues to withhold competing target directives and unsupported operations instead of implicitly stacking them.

## Scoped DM override

The universal DM-override principle is preserved. An **authenticated and scoped** `NativeTableRuleDecision` can authorize a precisely specified alternate effect even where a native definition does not exist or differs. For a directive, the decision target is `effective-effect` and the target ID is the directive's unique occurrence ID. Its effective outcome must contain **exactly** eight values:

```json
{
  "sourceGrantId": "grant-instance-id",
  "ruleId": "rule.dm-custom",
  "target": "ability-score",
  "targetId": "strength",
  "operation": "add",
  "numericValue": 2,
  "requiredFormId": null,
  "definitionId": null
}
```

DM authority is not a bypass of grant ownership or application authentication. The source grant must still be earned and host verified; a *new* grant must first be awarded through an authorized acquisition/construction path. A claim with a different campaign scope, target, operation, numeric value, or untrusted actor is refused.

An **active form** currently needs a separate authenticated `effective-form` decision matching its form identity, source, activation evidence, and persistent influence ID, in addition to host verification. This is **not** automatic form-transition execution, which remains with the later Pillar 2/4 construction work.

## Host-trust responsibility

`INativeEffectiveCatalogTrust` is an application-authentication **boundary**, not a working production host implementation. A production adapter must verify that the Origin result genuinely came from the permitted construction pipeline and belongs to the authorized character, that every grant is actually owned and earned, that applicability and form activations are authorized, and that the DM actor has permission for the exact campaign and target. The tests use fixture-specific trust implementations and cannot provide these production guarantees.

This module is intentionally not wired to public endpoints or Character Sheet. There is no permissive fallback when native data or trusted host proofs are absent.

## Tests and remaining integration

Synthetic catalog tests exercise native exact matching, wrong rule/definition/value/operation/target/format/source identity, unearned grants, rejected host trust, forged Origin claims, explicit DM scope/content verification, inactive form rules and form authority. No native first-party content was changed.

Next work: create a server-hosted trusted-state adapter and a real end-to-end test with `NativeOriginConstructionEngine` and an actual earned grant. Only then connect the catalog authorizer and projection bridge to the API. Further changes to effect operation vocabulary and the four supernatural options remain separate design/approval tasks.

**No changes to `main` or the parent feature branches.**
