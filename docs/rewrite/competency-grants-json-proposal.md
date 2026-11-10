# Native Competency Grants — JSON authoring proposal

**Status:** Proposed source-content representation, not an approved schema or a live converter. This document introduces no new origin allocation, proficiency, rank, milestone, feature, or gameplay change. Existing character-projection/grant logic remains as described in [Competency Baseline v1](competency-baseline-v1.md).

## Purpose and boundaries

Native Competencies remain **single learned capabilities**. Each character's state has one rank total and one binary proficiency value per competency. Equipment possession and tool qualifications are separate. A native Competency does **not** contain a Craft Skill plus a tool proficiency.

Authoring sources need to state the precise training effect of an origin, class, feat, training activity, or feature. A named source-edition tool proficiency must not silently convert to an arbitrary combination of rank and proficiency. This proposal supplies the missing **declarative content shape**. It does not replace the authoritative acquisition/selection/entitlement machinery.

The grant shape can be attached to a source benefit, class feature, or other actual rule concept under its existing authoring structure. The examples below are illustrative stand-alone grant objects, not a requirement to add a global nested competency-profile container.

## Competency training grant

~~~json
{
  "id": "smithing-training",
  "kind": "competency-training",
  "target": "competency.smithing",
  "ranks": 1,
  "proficient": false
}
~~~

- **id:** stable identifier unique among the enclosing feature's grants. This is the definition's grant key, not a character-specific acquisition or purchase ID.
- **kind:** `competency-training` marks the payload as a grant of training to one native competency.
- **target:** exactly one resolved native `competency.*` ID; alternative to `choice`.
- **ranks:** nonnegative integer *increment*, not an absolute rank target. Zero is permitted.
- **proficient:** whether the grant sets the *same competency's* binary proficiency true. It is independent of rank acquisition. Neither field automatically grants the other.
- At least one of `ranks > 0` and `proficient = true` is required. Do not encode a no-op.

This is a source definition. When a player legitimately acquires it, the authorization layer creates a validated occurrence. Different authorized occurrences add ranks and OR proficiency. Reapplying the same occurrence must not award ranks twice.

## Choice grant

~~~json
{
  "id": "crafting-choice",
  "kind": "competency-training",
  "choice": {
    "count": 1,
    "from": [
      "competency.smithing",
      "competency.brewing",
      "competency.stonemasonry"
    ],
    "distinct": true
  },
  "ranks": 1,
  "proficient": true
}
~~~

Here the character chooses exactly one target from the listed native Competency IDs. **The rank and proficiency effects apply to the selected target, not to every offered target.** The `distinct` property requires selections *within this grant* to differ when `count > 1`. It imposes no general prohibition on learning the same Competency from other earned sources.

A choice is unresolved until the character selects a legal option and the acquisition service records it. It must not be silently defaulted to the first option. A source with differently valued outcomes should encode separate choices or features, not assume a uniform grant that does not represent its rules.

This proposed shape is intentionally minimal: it does not decide source-specific eligibility predicates, rank budgets, retraining, rank caps, repeatable-feat eligibility, or the treatment of genuinely ambiguous source text. Those validations remain with the corresponding rule concepts.

## Acquisition and provenance

The static source provides the enclosing rule-concept identity, its local grant key, eligible targets, and the grant payload. Runtime state must additionally preserve:

1. Which origin, class feature, feat purchase, training event, or other authorized entitlement was actually acquired.
2. A **unique occurrence ID**, distinguishing separate legitimate purchases or level events from retries of the same event.
3. The concrete selected Competency ID for a choice grant (and, for multi-select grants, the choice slot).
4. The grant's resolved rank increment and proficiency flag and the ruleset/revision necessary to interpret it.

Neither a client-supplied SourceId nor the presence of a grant definition in JSON proves ownership. Authorization precedes the post-validation reducer. If a selection cannot be validated, the grant remains unresolved and no partial award is made. A source with several awards needs clear atomicity policy; do not silently accept a partially invalid origin.

Repeatable-feat purchases may supply distinct authorized occurrences even when their source ID is identical. Multiple distinct rank increments can add to the same competency. A repeated proficiency-only grant never creates a rank or grants a substitute choice without an explicit source rule.

## Equipment proficiency is not competency proficiency

For reference, a distinct binary qualification might eventually be authored as:

~~~json
{
  "id": "tool-qualification",
  "kind": "equipment-proficiency",
  "target": {
    "category": "tool",
    "id": "equipment.thieves-tools"
  }
}
~~~

This is **illustrative vocabulary only**; `equipment.thieves-tools` is not asserted to be an existing canonical equipment ID. A fully reviewed equipment qualification schema belongs to the shared weapon/armor/shield/tool proficiency reconciliation, not to the Competency rank system. Such a grant has no `ranks` field.

Thieves' Tools are the established example: they do not map automatically to Locksmithing or a new Trap Disarming competency. `associatedTools` in a competency definition conveys tool applicability, not possession, tool qualification, or another check bonus.

## Concrete mapping questions exposed by current content

| Current source record | Existing content | Follow-up needed before editing its effect |
| --- | --- | --- |
| Native Dwarf General Upbringing, benefit `crafting-tradition` | `chooseOne` of Smithing, Brewing, Stonemasonry; no explicit rank/proficiency payload | Determine whether the choice grants ranks, proficiency, or both; only then translate into a typed choice grant |
| Working Acolyte Background candidate (2024-style) | Calligrapher's Supplies under source-shaped `toolProficiencies` | Explicitly approve its native Calligraphy rank/proficiency payload during Background reconciliation; a tool name alone does not select the payload |
| Working Criminal Background (2024-style) | Thieves' Tools under source-shaped `toolProficiencies` | Convert under the **equipment qualification** contract, not as a Competency rank grant |
| Classes and repeatable feats | Feature acquisitions and choices at specific character levels/purchases | Record earned feature instance and selected target before creating an occurrence; do not read a free-standing feat definition as an automatic character entitlement |

The examples above do not approve changes to these source benefits. In particular, the Dwarf option's current descriptive wording does not determine an integer rank increase or proficiency flag.

## Prerequisite representation

A future prerequisite may reference one competency and require independently a minimum rank and/or proficiency:

~~~json
{
  "competencyId": "competency.smithing",
  "minimumRanks": 3,
  "requiresProficiency": true
}
~~~

The rank value **3 is purely illustrative** and does not define a Journeyman, Expert, or Master threshold. Prerequisite expressions are distinct from grants; the implementation's typed prerequisite contract, source entitlement, and feature evaluation determine actual authorization. Rank-only characters may satisfy a rank requirement even if they are not proficient, unless that prerequisite explicitly demands proficiency.

## Native catalog naming and progression notes

The current native Competency definitions call the independent numeric dimension `profile.level` (`"progressive"`, with `levelScale: "pending-reconciliation"`), whereas the grant contract and current projector call character-state increments `ranks`. **These must designate one dimension, not two.** Before finalizing the schema, select canonical authoring terminology and a migration boundary. No mass rename is authorized by this proposal.

Automatic Journeyman/Expert/Master abilities remain a separate benefits pass: they must reference defined procedures and rank thresholds, and they do not award tool possession, recipe knowledge, enchantment, spellcasting, or item success by implication. Smithing, Cooking and Alchemy are reference cases for that later pass, not special variants of the grant mechanism.

## Proposed validation cases (content contract only)

- A fixed rank-only grant adds ranks without proficiency; a proficiency-only grant adds proficiency without ranks.
- A grant of both produces one rank increment and one proficiency flag, never a second tool bonus.
- One choice selects exactly its declared count of available native Competencies; no choice leaves the grant unresolved.
- A character can legitimately choose the same Competency through two different sources and accumulate ranks.
- The same earned occurrence replayed twice is rejected or idempotently ignored **before** reduction, not counted as a new rank source.
- Two distinct purchases of an eligible repeatable feat have distinct occurrence IDs and may stack ranks.
- Invalid target identities, negative/fractional rank amounts, duplicate static keys, duplicate resolved occurrences, and ineligible choices fail validation.
- A source-shaped grant of Thieves' Tools does not create `competency.locksmithing` ranks.
- A milestone prerequisite does not itself grant a rank, a feat, or an ability.

**Next design decision:** agree on the final fixed/choice JSON vocabulary and the meaning of a normal native training grant, then reconcile actual Background/Upbringing/Class benefits individually. Run schema and integration tests only after field names and conversion rules are approved.
