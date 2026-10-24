# Wizard Band 1 — Specialization selection reconciliation

**Status (2026-10-24):** The **Wizard Specialization selection mechanism** is owner-approved and recorded declaratively. Wizard levels 1–20 as a whole **are now reconciled declaratively**; this document is not a claim that the Wizard is playable or that source candidates have been converted.

## Approved Wizard Specialization rule

At **Wizard class levels 5, 10, 15, and 20**, the Wizard gains **one additional selection** from a Wizard Specialization list. Eligible option families are **Metamagic and Item Creation**, reflecting the historical 3.5e Wizard bonus-feat categories. These four grants are **additional to**, not replacements for, the Wizard's other class features, general advancement feats, or Ability Score Improvements.

A Wizard may also **select one eligible option from that same list instead of a general feat** whenever the Wizard has an actual general-feat selection. This consumes the ordinary feat choice. If an Ability Score Improvement permits choosing a feat, the Wizard may use that feat choice; this does **not** change an Ability Score Improvement that does not permit feat substitution.

The list provides a **class selection interface to shared option definitions** rather than creating Wizard-specific copies of feats. General feats and other classes' legitimate access to those shared capabilities remain independent.

**Eligibility and repetition:** Apply all prerequisites of each option, including any level or spellcasting prerequisites. Multiple selections of the same option are allowed only when that option explicitly permits them. One selection grants one option, not an entire category.

**Metamagic separation:** This grant does not award Sorcery Points, the Sorcerer's Metamagic class feature, or free spell modification. Exact resource costs and any differences between historical feat-based Metamagic and the approved Sorcerer class feature are **deferred to shared Metamagic/feat reconciliation**. Reuse equivalent effect resolution where appropriate.

**Crafting:** Preserve historical Item Creation choices as eligible option families; their actual crafting rules, resource costs, and prerequisites require the general item-creation reconciliation.

**Spell Mastery:** Historical feat excluded; 2014 level-18 class feature retained.

## Source distinction

D&D 3.5e provided a Wizard bonus feat at levels **5, 10, 15, and 20**, selected from Metamagic, Item Creation, or Spell Mastery, subject to prerequisites, while ordinary advancement feats remained independent. The historical **level-1 Scribe Scroll automatic grant** was a *separate* Wizard feature. The owner has approved the four class selections and the optional general-feat substitution; **level-1 Scribe Scroll is now an approved automatic class grant**.

Source: [D&D 3.5e SRD — Sorcerers and Wizards](https://www.d20srd.org/srd/classes/sorcererWizard.htm).

## Native data and scope

- `data/native/classes/base/wizard.json`: `approvedNativeSelections.wizardSpecialization` records the grant levels, two acquisition pathways, restriction semantics, and explicit deferred mechanics.
- `data/reference/srd/class-reconciliation-decisions.json`: `class.wizard.specialization-selections` records owner approval.
- **Not yet executable:** Shared feat/crafting systems, runtime effects, subclasses and companion mechanics.
- The existing Wizard source candidates, reconciliation manifest statuses, and statistics are **unchanged**, because this partial decision does not finish any imported source record.

## Integration checkpoints for subsequent Wizard pass

1. Finish the Wizard 1–20 native base-class chassis, retaining approved shared spell-resource and preparation policies.
2. Expose specialization choice milestones at 5/10/15/20, including levels with other ordinary features.
3. Route ordinary feat eligibility to the same Wizard Specialization list by substitution, consuming a general-feat choice.
4. Reconcile underlying option catalog, prerequisites, effect costs, and feats through shared systems instead of implementing duplicate Wizard mechanics.
5. Keep Wizard familiar-path powers, subclasses, and prestige classes deferred to their separate passes.
