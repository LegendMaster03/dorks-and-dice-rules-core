# Character Advancement Eligibility

Rules Core exposes candidate acquisition eligibility through the stable public consumer API. This contract is intended for Character Sheet and other tools that operate only on the effective Dorks & Dice ruleset.

Consumers do not inspect source-native rule documents, edition identifiers, or private Rules Core APIs to determine advancement requirements.

## Endpoints

Global effective ruleset:

`POST /api/rules/character-advancement/eligibility`

Campaign-effective ruleset:

`POST /api/campaigns/{campaignId}/rules/character-advancement/eligibility`

The campaign endpoint requires authority to read that Campaign's effective rules.

## Request

The request identifies one candidate concept and supplies Character state using the existing `CharacterRulesProjectionRequest` contract:

- `candidateConceptKey` — effective Subclass or Prestige Class concept to evaluate;
- `character` — current Character facts used by normal Character mechanics projection;
- `parentAdvancementOccurrenceKey` — optional Class occurrence identity used when evaluating a Subclass against a specific Class occurrence.

Using the existing Character projection request avoids a second or divergent Character-state contract.

## Response

The response reports:

- effective scope (`global` or `campaign`);
- candidate key, display name, and kind;
- `eligible`, `ineligible`, or `unresolved` state;
- nullable Boolean eligibility;
- Subclass parent-Class requirement when applicable;
- effective prerequisite requirements;
- candidate-related blocking conflicts.

`unresolved` means Rules Core can not determine eligibility from the currently resolved effective rules and supplied Character state. Consumers must not interpret unresolved eligibility as either allowed or forbidden.

## Subclass

Subclass eligibility is evaluated against the effective `parent-class` relationship and the acquisition level resolved inside Rules Core. The Character may identify a specific parent Class occurrence. This matters when a Character has more than one occurrence that could otherwise match the same parent concept.

The current resolver prefers normalized acquisition metadata when available and otherwise derives the threshold from the effective Subclass progression. This derivation remains internal to Rules Core.

Character Sheet should use the ordinary public rules catalog to discover effective Subclasses and their `parent-class` relationships, then use the eligibility endpoint to evaluate a candidate for the current Character and scope.

## Prestige Class

Prestige Class eligibility uses the same effective prerequisite model as Character mechanics projection. Currently executable prerequisite kinds include:

- ability score;
- competency/skill ranks;
- Class level;
- simple named feat possession.

Existing grouped prerequisite semantics are preserved, including `N of M` groups.

Simple PCGen `PREFEAT` evidence that has not yet moved into the general source translator is normalized internally by the eligibility service into ordinary `feat` prerequisite entries. It is never exposed to public consumers as PCGen syntax. Complex feat selectors or source expressions that have not been reviewed remain unresolved rather than being guessed.

This public support does not imply that every Prestige Class has complete effective prerequisite data. Character Sheet should not build the Prestige Class acquisition UI until the applicable Prestige Class data is ready.

## Scope

A consumer chooses the appropriate endpoint from its active rules scope:

- outside a Campaign: global Dorks & Dice ruleset;
- inside an explicit Campaign context: that Campaign's effective Dorks & Dice ruleset.

Character membership alone does not silently choose a Campaign rules scope.
