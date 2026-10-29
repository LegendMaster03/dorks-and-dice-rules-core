# Warlock 3.5e prestige material — source-to-native classification

**Status:** owner-approved classification distinction (2026-10-24): multiclass prestige is always prestige; single-class prestige is valid but must be evaluated case by case. The two single-class Warlock examples are unclassified candidates, not final rulings. Mechanics, entitlement, and non-SRD integration remain deferred; the SRD-only Warlock slice is unchanged.

## Governing classification rule (owner decision)

**Multi-class prestige is always prestige.** If a specialization substantively combines advancement or prerequisite capabilities from more than one base class, its native category is **prestige class**.

**Single-class prestige is also valid, but not automatic.** A specialization based on only one class is **not required** to become a subclass or modular specialization. Evaluate the individual design to decide whether an independently leveled prestige class, a subclass, or (where supported) a part/bundle progression and guided preset best preserves the mechanical intent.

- **Multiple classes:** always native prestige. Preserve prerequisites and relationships among the separate base-class features/progression tracks.
- **One class:** compare independent advancement, depth and rigidity of progression, opportunity costs, entry requirements, and the benefits or drawbacks of integrating it into the base class's specialization model. Keeping a native prestige progression is a legitimate outcome.
- **Source versus native identity:** retain the original 3.5e `prestigeClass` record and provenance, regardless of the native decision. Do not silently relabel or overwrite the original.
- **No automatic conversion:** a historical name or label alone never completes the review; a single-class source prestige may remain prestige, or may be represented through a class-specific development system after owner review.

This rule definitively classifies **cross-class specializations** as prestige but leaves each **single-class** historical prestige's native classification pending individual assessment.

## Case-by-case review questions

1. Does the specialization depend on and advance more than one base class? If yes, it **must** remain prestige.
2. If single-class, does it behave like an independently trained or separately leveled career with meaningful entry requirements and trade-offs, or like a specialization of abilities already owned by the class?
3. Would splitting its progression into parts and a guided preset faithfully preserve its leveling, feature access, and opportunity cost? Would doing so improve optional player complexity, or create misleading freedom?
4. Which effects are coherent independent parts, upgrades or inseparable bundles? Which existing class resources and prerequisites must they use?
5. Would either implementation duplicate, bypass or erase existing entitlements and source behavior?
6. Does source-to-native reconciliation require substantive changes? Record those decisions separately from classification.

## Four historical cases — mandatory multi-class outcomes, single-class pending

| 3.5e source prestige class | Native classification | Why | Work still to do |
| --- | --- | --- | --- |
| **Hellfire Warlock** (*Fiendish Codex II*, 3 levels) | **Pending case-by-case decision: prestige or Warlock specialization** | Primarily develops Warlock's invocation and Eldritch Blast capabilities, but its separate entry requirements, costs and progression may justify retaining a prestige class | Evaluate independent progression and meaningful hellfire drawbacks, along with whether parts/preset preserve the original cost |
| **Enlightened Spirit** (*Complete Mage*, 10 levels) | **Pending case-by-case decision: prestige or Warlock specialization** | Develops supernatural Warlock-related powers with an interdependent multi-level progression | Evaluate whether the long cohesive advancement and distinct benefits are better expressed through a separate prestige track or Warlock bundle/preset |
| **Eldritch Disciple** (*Complete Mage*, 10 levels) | **Prestige class — classification established** | Combines Warlock invocation progression with divine spellcasting from another class | Preserve dual-class prerequisites and class-specific progression |
| **Eldritch Theurge** (*Complete Mage*, 10 levels) | **Prestige class — classification established** | Combines Warlock invocation progression with arcane spellcasting from another class | Preserve independent caster and invocation advancement |

These are **classification decisions, not approvals to implement non-SRD mechanics**. Only the multi-class examples are conclusively categorized; the single-class examples remain open.


## Why this matters to the active vertical slice

- The approved implementation remains **SRD 5.1 + SRD 5.2.1** full Band 1 Warlock including Fiend, Pacts and invocations, plus prior approved historical 3.5e *base-class* feature rulings.
- The four non-SRD 3.5e prestige classes are **reference-only compatibility stress cases**, useful for verifying the model is not incompatible with future thematic paths or multiclass progression.
- Do **not** add Hellfire or Enlightened Spirit playable features, 3.5e heritage feat catalogs, or 3.5e invocation prerequisites to the active content inventory now.
- Once the SRD-based Warlock model is tested, **review Hellfire Warlock and Enlightened Spirit individually** for prestige-versus-specialization placement, then integrate content only after separate approval. Eldritch Disciple and Eldritch Theurge are definitively prestige classes; their detailed progression still requires separate design.

## Historical reference sources (research)

- *Fiendish Codex II*, Hellfire Warlock: https://srd.dndtools.org/srd/classes/prestigeFc2/hellfireWarlock.html
- *Complete Mage*, Enlightened Spirit: https://srd.dndtools.org/srd/classes/prestigeCmage/enlightenedSpirit.html
- *Complete Mage*, Eldritch Disciple: https://srd.dndtools.org/srd/classes/prestigeCmage/eldritchDisciple.html
- *Complete Mage*, Eldritch Theurge: https://srd.dndtools.org/srd/classes/prestigeCmage/eldritchTheurge.html
- Repo’s structural distinction: `docs/mechanical-relationships.md`; native Band levels: `docs/rewrite/native-rules-design-specification.md`.
