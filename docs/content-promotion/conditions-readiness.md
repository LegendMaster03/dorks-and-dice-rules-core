# Conditions — native read-only Wiki readiness (Wave 1)

**Ruleset:** `first-party-001`  
**Authority:** `data/rulesets/first-party-001/native/`  
**Status:** All **30 selected conditions** are coherent native data and ready for authenticated, read-only Wiki display. This is **not** a statement that all condition effects execute in Rules Core.

## Existing canonical content

- `native/conditions.json`: **29** `condition` entries and **one** `leveled-condition` entry (Exhaustion).
- `native/rules/conditions.json`: `rule.conditions`, chapter prose and all 30 `conditionIds` for navigation.
- `native/defenses.json`: `rule.defenses`, normal/Touch/Flat-Footed AC rules, condition-specific defensive operations and stacking constraints.
- `native/held-pinned.json`: `rule.pin-and-held`, related grapple/held mechanics and source identity.
- `data/reference/srd/` and `data/reference/3x/`: historical source and reconciliation evidence, outside runtime authority.

**Canonical identities (30):** Blinded, Charmed, Cowering, Dazed, Dazzled, Deafened, Entangled, Exhaustion, Fascinated, Fatigued, Flat-Footed, Frightened, Grappled, Held, Helpless, Incapacitated, Invisible, Nauseated, Panicked, Paralyzed, Petrified, Pinned, Poisoned, Prone, Restrained, Shaken, Sickened, Stunned, Unconscious, Wet.

## Scope of this content pass

Five existing conditions (Deafened, Frightened, Grappled, Poisoned and Restrained) previously provided only structured `effects`. Their added `description` strings now summarize exactly those selected mechanics for a Wiki overview; no existing effect, numeric rule, ending trigger or condition inheritance is changed. The Conditions chapter now links by stable native identity rather than obsolete `data/native/` paths.

All 30 descriptions, IDs, inheritance/requires-condition links and the chapter's index are regression-checked by `tests/test_conditions_wiki_readiness.py`. Existing detailed regression suites remain authoritative for particular mechanics. Do not create source-edition versions of an already selected condition.

## Wiki presentation contract

The Wiki can navigate from `rule.conditions.conditionIds[]` to the same catalog's condition entities. Show `name`, `description`, effect details, `appliesConditions`, `requiresConditions`, `endsWhen`, and the six cumulative Exhaustion levels with appropriate resource/action terminology. Do not merge condition identities, infer execution from declarative records, calculate AC by double-applying inherited penalties, or silently hide conditional effect predicates (for example the attacker's relationship to the grappler).

- **Incapacitated:** prevents actions and ends Concentration; it does not independently remove movement, speech, or awareness. **Dazed** remains different.
- **Stunned:** half Speed; not automatically Helpless. **Petrified:** awareness may remain; it does not automatically apply Unconscious.
- **Held:** Helpless but not automatically Incapacitated; purely mental actions remain possible when otherwise permitted. **Pinned:** requires an independent Grappled instance by its pinner and does not itself imply Helpless.
- **Frightened, Shaken, Panicked, Cowering:** distinct fear states; do not automatically chain escalating fear tiers. Panicked may conditionally cause Cowering where escape is impossible.
- **Fatigued:** distinct from six-tier **Exhaustion**. **Wet:** its approved thresholds and removal conditions remain unchanged.

## Pending issues that do not block reference publication

1. **Runtime effect adjudication:** automatic condition evaluation, nested inheritance source tracking, defense calculations and encounter action handling belong to the Rules Core Code Rewrite project. Display must not advertise execution.
2. **Fear stacking:** overlapping fear penalties and 3.x automatic fear escalation still require separate reconciliations; no implicit escalation.
3. **Historical fatigue/exhaustion:** 3.x Exhausted and repeated-Fatigued escalation do not automatically map to native Exhaustion levels.
4. **Specific action and spell procedures:** Entangled spell interruption, Nauseated move-equivalent activity, Pinned initiation/escape action costs and other historical source procedures require shared action/spell reconciliation.
5. **Future identities:** Confused, Staggered, Turned and death/dying-related candidates are not selected by this pass. Absence is intentional, not a reason to withhold the 30 settled entries.

**Release boundary:** Native content changes require a fresh loader-computed catalog fingerprint and reviewed release SHA. No additional runtime root, fallback, per-rule publication gate, merge or deployment is authorized here.
