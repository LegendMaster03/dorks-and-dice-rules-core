# Pillar 2 — Canonical Damage Resistance consolidation

**Status:** Native content integration on the Pillar 2 branch; no first-party price or expression-cap change.

The Dragonborn and the Infernal, Abyssal, and Chthonic Tiefling ancestries now share a single canonical `trait.damage-type-resistance` gene. The native resistance is 50% in each of these sources. A source-qualified `originBindings.damageType` lookup resolves the damage type through the verified selected lineage: Dragonborn from the selected leaf and Tiefling from the explicit fire, poison or necrotic damage type of its selected Ancestry.

The original Dragonborn symbolic `damageTypeFrom` token remains unchanged for compatibility with older binding research and native projections. The executable source-neutral `nearest` binding governs the actual damage type. A successful binding must be traced to real, verified source data; no resistance type is implied by the player's Species display name alone.

**Compound abilities are not collapsed.** Dwarf `trait.poison-resilience` grants saving-throw advantage against poison as well as poison resistance. Goliath `trait.born-of-flame` starts at level four. These are complete traits with consequences beyond a simple, always-on damage-type resistance. They retain their distinct identities, and genuine overlapping damage types remain subject to explicit reconciliation.

**Inheritance and balance:** A Fire variant and a Necrotic variant are two source-qualified occurrences of the **same** biological gene kind. Their inherited instances and configurations retain provenance. The present maximum of two inherited instances and one expressed instance remains unchanged until the balance and capacity discussion is resolved. The allocation ledger already prices separately authorized effective instances and supports optional escalating multiplicity charges; those charges do not themselves grant extra expression permission. No first-party costs or allowances are implied.

**Migration:** Former source-neutral Fire, Poison and Necrotic Resistance IDs are accepted as migration aliases. Identifier replacement alone does not produce the correct `damageType` parameter for a saved profile. The exact original native source must be authenticated and the character re-evaluated before its mechanical effects are accepted.

**Follow-up:** Assess whether percentage variants and any additional expressed instances should be allowed and at what cost. The only supported first-party percentage supplied by this consolidated native trait today is 50.
