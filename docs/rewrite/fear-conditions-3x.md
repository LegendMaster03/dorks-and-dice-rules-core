# D&D 3.0/3.5 fear-family conditions

**Stage:** Stacked on PR #100, which depends on PR #99. This pass adopts three additional condition identities: Shaken, Panicked, and Cowering. The catalog now contains **27** native conditions.

## Native conditions and Frightened preservation

| Condition | Native consequence |
| --- | --- |
| **Shaken** | −2 on attacks, saving throws, ability checks and skill checks. No compulsory flight. |
| **Frightened** | **Previously approved definition unchanged.** Disadvantage on ability checks and attacks while the fear source is visible; cannot willingly approach. Does not inherently force flight. |
| **Panicked** | Automatically releases held objects, flees as quickly as possible from fear and hazards, and may only move or use permitted escape options. −2 on saves, skill and ability checks. If no escape route is available, also becomes Cowering until flight is again possible. |
| **Cowering** | Cannot move or act; denies applicable positive Dexterity/dodge AC contributions and applies −2 AC. Not automatically Unconscious, Helpless, or concentration-ending Incapacitated. Does not inherently drop equipment. |

3.0 Cowering required item dropping, but the 3.5 version did not. 3.0 Panicked carried a 50% chance to drop held items, while 3.5 made the drop automatic. This pass adopts the respective 3.5 details without confusing the two triggers.

**Roll penalty nonstacking:** Shaken and Panicked can each impose a −2 fear penalty on a roll. Where the same check receives the same fear-family −2 through both conditions, apply that family penalty only once. This is distinct from independent non-fear modifiers. Existing Frightened's line-of-sight-dependent Disadvantage is deliberately unchanged; deciding whether Shaken should be replaced or coexist when Frightened applies belongs to a separate fear escalation ruling.

**Cornered Panicked:** Derived Cowering is conditional, not a permanent application. Ending Panicked, or opening a viable escape route, ends *that derived instance* of Cowering, not independently imposed Cowering.

## Documented but not adopted: cumulative fear escalation

The 3.5 SRD's Special Abilities rules elevate Shaken when a new fear effect is applied, and may elevate Frightened to Panicked. Automatically imposing that source behavior could change the existing native Frightened baseline and the semantics of independent durations, so it has **not been activated**. The comparison remains recorded in `data/native/fear.json` and `data/reference/3x/fear-conditions.json` for owner-level reconciliation.

## Design preservation and next steps

- Ordinary/Touch/Flat-Footed AC remain composable; Cowering removes positive Dexterity/dodge contributions and subtracts 2, not Helpless's effective Dexterity score of 0.
- The original Exhaustion, Stunned half-Speed, aware-Petrified, Wet, and prior condition definitions are unchanged.
- Source passages remain in historical reference materials and the original 5.x CC BY corpus remains untouched; 3.x transcriptions are distinct OGL provenance.
- The rewrite does not yet have an executable fear/condition interpreter; JSON plus regression tests document native behavior for its eventual implementation.

**Next candidates:** Fatigued/Exhausted interaction with six-tier Exhaustion, Pinned/Held and Grappled, Confused, Staggered, and Turned.

Historical references: [3.5 Condition Summary](https://www.d20srd.org/srd/conditionSummary.htm), [3.5 Fear Special Abilities](https://www.d20srd.org/srd/specialAbilities.htm), [3.0 Condition Summary transcription](https://opengamecontent.fandom.com/wiki/D20_System_v3.0_SRD/Condition_Summary).
