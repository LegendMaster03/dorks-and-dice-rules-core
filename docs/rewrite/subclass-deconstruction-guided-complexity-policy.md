# Subclass deconstruction: class-specific development models and optional player complexity

**Status: owner-established architectural direction (2026-10-24), not an approved data schema, a completed converter, a balanced advancement budget, or approval to change all existing classes.**

**First proof of concept:** Warlock, Band 1 (levels 1–20). The conventional subclass mechanism and previously approved base-class reconciliations remain intact unless and until a replacement is separately validated and approved.

## Governing requirement

An alternative class-development system, such as the proposed modular Warlock, is a **deconstruction of what a subclass does**. It is **not merely a second way to label subclass features**, and it is **not a requirement that every class follow Warlock's four pillars**.

Each such system must:

1. **Represent imported and established subclasses as recognizable guided presets.** A player must be able to choose an existing subclass identity and follow a coherent advancement path without needing to master the underlying modular system. The preset is a composition of real features, constraints, and grants, not a parallel class implementation.
2. **Expose additional, meaningful customization to players who opt in.** The system deconstructs subclass packages into the smallest *coherently acquirable* parts, dependent upgrades, and necessary bundles. Where a part is valid as an independent choice, an advanced player can select it subject to prerequisites, entitlements, and class rules. This should make additional combinations and deeper specialization possible, including new native choices.
3. **Keep both modes in one authoritative ruleset.** Presets and manually selected parts use the same source-neutral native effects, eligibility checks, resource costs, and advancement ledger. Choosing advanced complexity must not silently provide additional grants; choosing guided progression must not impose unnecessary manual decisions.
4. **Provide a practical, repeatable subclass conversion path.** For each supported source subclass, map source feature progression and interactions to native part definitions, grant/budget rules, dependencies, and a guided preset. Record source/version provenance and explicitly reconcile incompatibilities; do not assume 2014 and 2024 subclasses have identical timing, effects, or resources. Conversion is a design requirement, not a claim that every subclass can be imported automatically or without adjudication.
5. **Allow domain-specific models.** Warlock's Patron/Origin, Pact specializations, invocations, and thematic paths provide a strong modular design. A future Druid alternative might achieve the same guided/custom outcome through an entirely different structure reflecting Wild Shape, nature circles, terrain-state dependencies, or companions. Its existence and concrete design are not currently approved.
6. **Preserve conventional subclasses when appropriate.** Most classes may benefit from the existing relatively rigid subclass progression. No class must replace that system merely because Warlock does. Shared category labels do not force equal structure, four mandatory slots, or equivalent character choices.

**The universal requirement is the outcome and Rules Core integration contract. The class-specific development structure is intentionally not universal.**

## What a "part" is, and what it is not

A **part** is an acquirable unit that remains mechanically coherent under the class's development model.

- An independent feature (such as a bonus skill entitlement) may be one part.
- A feature that upgrades a previous choice must have an explicit prerequisite or supersession relationship (such as Champion's improved-to-superior critical threshold). Those bonuses cannot stack by mere deconstruction.
- Several tightly coupled effects may remain a **bundle** or depend on one state owner (such as Circle of the Land's active terrain feeding spells, resistance, and sanctuary effects). Deconstruction does not require allowing players to buy mechanically invalid fragments.
- Parts may enhance pre-existing class actions or consume a parent-class resource; they do not automatically become freestanding actions or cross-class options.
- A preset may recommend or automatically resolve ordinary entitled choices within its specified policy. It may not simultaneously confer the subclass's full fixed package **and** charge a player for an equivalent second package.
- **Reusability of effect code** does not imply **portability of entitlement**. A Warlock, Bard, and Druid can share generic reaction/resource primitives without automatically becoming eligible for each other's class powers.

## Required layers and ownership

| Layer | Responsibility | May vary by class? |
| --- | --- | --- |
| Shared Rules Core | Effect execution, feature grants, prerequisites, resources, level/track evaluation, source mapping, effective-rule projection | Core APIs are shared |
| Conventional class development | Fixed base and subclass grants where that design is best | Yes; preserved as valid baseline |
| Specialized development model | Domain-specific relationships, part groupings, unlock rules, selection structure, mutual dependencies, and guided/custom building | **Yes**; Warlock is the first experiment |
| Native part definitions | Meaningful abilities, upgrades, bundles, selection prerequisites, resource contracts, provenance | Yes, with generic effect primitives underneath |
| Guided presets | Existing source-class/subclass-inspired character paths using the same underlying parts and grants | Yes; no duplicate entitlement pool |

Do **not** create a universal set of four required character-development systems (Origin, Discipline, Invocations, and Thematic Progression). Those concepts describe the **Warlock hypothesis**; another class may have one, several, or none of their equivalents. A generalized *mechanical contract* is sufficient for integration, while specialized models are free to implement class-appropriate architecture.

## Concrete example: Warlock versus a possible future Druid

**Warlock proposal:** Deconstruct Fiend and other Patron subclasses into source-specific abilities, meaningful powers and interlocking Pact/Invocation options. Reconstruct a traditional Fiend as a guided preset while offering qualified custom progression. Respect existing Band 1 historical Warlock rulings and don't conflate the healing and damage-resistance features both historically named Fiendish Resilience. The exact invocation count, Patron timing, Pact cost and pool structure remain **unresolved**.

**Druid illustration, not an approved alternative:** A Circle of the Land preset might preserve active-land state and its coupled upgrades. If later given a deeper player-choice model, it could organize coherent circle/terrain packages or nature specializations rather than copying Warlock's Patron/Invocation design. Already approved Druid base-class Wild Shape/companion-path mechanics remain unaffected by this thought experiment.

**Conventional subclass:** A class that benefits from predetermined feature acquisition can keep its fixed subclass structure. Its continued existence does not undermine the optional-complexity goal; the alternate systems should be adopted only where they are beneficial.

## Validation gate: Warlock vertical slice first

Before generalizing to any other full class implementation, prove:

1. **Guided construction:** import or translate a representative 2014 and 2024 Patron subclass into familiar playable **presets**, preserving coherent source intent and recording source-specific differences.
2. **Advanced construction:** the same native parts support an alternate qualifying build that meaningfully departs from the preset rather than merely rearranging labels.
3. **Equivalent entitlement:** compare ledger grants, selection budgets, prerequisites, resource costs and effects for guided versus manually configured equivalents; no duplicate free powers from the preset.
4. **Valid dependencies:** show a real prerequisite chain or composite power, and reject acquisition that would leave it invalid or duplicate an effect.
5. **Edition interoperability:** support the historical 3.5e at-will invocation identity along with modern Patron/Pact features without parallel edition-specific runtime engines. An original 3.5e Warlock need not be retroactively assigned a modern named Patron subclass to be valid.
6. **Mixed complexity:** guided and advanced characters work at the same table using one set of rules; a beginner is not asked to resolve a complete feature dependency graph.
7. **Failure and revision:** early tester feedback determines whether the Warlock model is retained, modified, or abandoned. Keep baseline classes and conventional subclasses available as the fallback; a failed Warlock experiment must not require rewriting the other classes.

**Out of scope:** migrating all class subclasses to parts, inventing a Druid-specific alternative now, finalizing Warlock's numerical advancement budget, generating the full 3.5e invocation catalog, Band 2 advancement, or implementing an unreviewed runtime schema.

See `docs/rewrite/character-development-subclass-parts-feasibility.md` for the six-class research and examples. Its finding that features may be independent, dependent, or bundled is **not** evidence that all classes should adopt the same decomposed subclass engine.
