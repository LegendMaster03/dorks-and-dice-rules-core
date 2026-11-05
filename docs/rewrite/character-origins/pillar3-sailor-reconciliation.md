# Pillar 3 — Sailor Background Reconciliation

**Status:** Source-reviewed design proposal; **not approved for native promotion**. This document does not authorize a selectable Sailor or Pirate native record.

**Ruleset:** `first-party-001`  
**Development branch:** `feature/character-origins-pillar3-life-experiences`

## Purpose and prior design boundaries

Reconcile the **2014 5e Sailor** as an independent professional Background for maritime or other substantial shipboard experience. Retain the project's separation of formative **Upbringing** from acquired **Background** and do not treat the 2014 Pirate variant as an automatic inherited package.

Existing architectural and design decisions that affect Sailor:

- `background.soldier` represents Land Vehicles training using `grantType: qualification-training`, `qualificationCategory: vehicle`, and `vehicleType: land`, because a verified generic native vehicle Competency does not exist. Sailor should mirror that representation for **water** vehicles rather than inventing a fake Competency ID or granting Navigation twice.
- `competency.navigation` is an established native Competency with a **Navigator's Tools** proficiency facet. It is a different benefit from the ability to operate Water Vehicles.
- `skill.athletics` and `skill.perception` already exist and are composite Skills. Selecting either does **not automatically train its independently ranked child Skills**: Athletics covers Climb, Jump and Swim; Perception covers Listen and Spot. This is an existing native skill-system distinction, not an invented Sailor restriction.
- Ship's Passage should be a bounded service/network feature in the same general weight class as other 2014 social/exploration Background features, not a ship-ownership or fleet-summoning power.
- No additional background languages, origin feats, or ability-score adjustments should be assumed because other Backgrounds sometimes grant them. The 2014 Sailor intentionally grants **no language**.

## Historical 2014 baseline

| Component | 2014 Sailor |
| --- | --- |
| Skills | Athletics, Perception |
| Tools | Navigator's Tools |
| Vehicle proficiency | Water Vehicles |
| Language | None |
| Feature | Ship's Passage |
| Equipment | Belaying pin (usable as a club), 50 feet of silk rope, lucky charm or ordinary trinket, common clothes, pouch containing 10 gp |

The original **Ship's Passage** functionally lets a former sailor arrange a voyage on a friendly or connected sailing ship for themselves and their traveling companions without paying a normal passage fare. There is **no guaranteed route, departure date, or travel time**, and the passengers are expected to assist with suitable shipboard duties. The GM determines what journey can actually be arranged.

The 2014 **Pirate variant** retains Sailor's skills, tools and vehicle training but substitutes **Bad Reputation** for Ship's Passage. In the published version, people may be too intimidated by the pirate's reputation to report some minor misbehavior. This is **not the Sailor feature** and is not an instruction to treat all sailors as pirates.

The 2024 Sailor source uses a different skill/feat model (including Acrobatics and Tavern Brawler). This reconciliation uses **2014**, matching the project-selected approach for the other Backgrounds.

Source evidence:
- Official D&D Beyond Background index, showing both legacy Sailor (*Ship's Passage*, Athletics/Perception) and legacy Pirate (*Bad Reputation*, Athletics/Perception): https://www.dndbeyond.com/backgrounds
- Nonofficial 2014 mechanics summary: https://dnd5e.wikidot.com/background:sailor
- Further mechanical summary: https://onyx5e.com/en/backgrounds/sailor/

The PHB's expressive text is not assumed to be covered by the SRD license. Independently author native descriptions while retaining relevant gameplay methods and rules.

## Independent Sailor Background — initial proposal

### Identity

You have meaningful experience working or serving aboard vessels on seas, navigable rivers, lakes, or other waterways. You might have been a crew member, ship's cook, lookout, navigator, deckhand, ship's officer, naval sailor, passenger-turned-crew, or merchant-mariner. The Background does not require an ocean crossing, a military commission, present employment, a particular Species or Upbringing, ownership of a vessel, or a specific moral outlook.

A character raised aboard a ship may also qualify for a future maritime/itinerant Upbringing; that formative environment does not automatically grant Sailor's acquired professional feature, and Sailor does not require that childhood.

### Skills — preserve the 2014 source pair

- **Athletics:** `skill.athletics`
- **Perception:** `skill.perception`

**Recommendation:** Keep both as fixed training. Although specialized shipboard positions could plausibly use other Skills, the baseline pair is coherent and gives a useful contrast against other Backgrounds. Personal shipboard jobs remain narrative specialties rather than an obligatory subtype or additional grant.

**Composite Skill detail:** Athletics training does not grant separate Climb/Jump/Swim ranks or proficiency automatically; Perception training does not independently train Listen/Spot. Honor the approved common composite skill semantics.

### Navigation — selected native Competency

- Train `competency.navigation`, the existing native Competency corresponding to **Navigator's Tools**.
- This grant does not automatically include `competency.cartography`, `competency.shipmaking`, or a broader general piloting Competency. Navigation and vehicle operation remain separate.

### Water Vehicles — independently qualified vehicle training

- Train with **Water Vehicles** using the same structure already approved for Soldier's Land Vehicles:
  - `grantType: qualification-training`
  - `qualificationCategory: vehicle`
  - `vehicleType: water`
- This is **not** another Navigation grant and does not confer Land Vehicles, ownership of a ship, command authority over its crew, or automatic expertise in every specialist shipboard task.
- Consumer projection and vehicle-qualification resolution are deferred to the general native mechanics integration. Do not create a placeholder Competency just to represent vehicle qualification.

### Language — no Background language

**Proposal:** Keep the 2014 **zero-language grant**.

Sailors may learn foreign languages, local dialects or shipboard vocabulary through Upbringing, other learning, and individual character experiences, but neither maritime profession nor travel necessarily makes one additional full language inevitable. This also creates a genuine contrast with Sage, Guild Merchant, Noble, Acolyte, Knight, and Spy, which each have a learned-language benefit.

There is no requirement to normalize every Background to one language merely because many currently provide it.

### Ship's Passage — proposed original native wording

> **Ship's Passage.** Through your previous service and maritime relationships, you can usually arrange passage aboard a vessel for yourself and your traveling companions without paying the ordinary passenger fare. You may rely on a ship you know or on contacts among its crew or other seafarers. The route, departure, and duration depend on ships and journeys actually available; you do not choose the schedule or destination without regard to those circumstances. In exchange, you and your companions are expected to help with reasonable shipboard work during the voyage.

**Approved-source functional scope preserved; proposed native expression is unapproved:**

- **The entire adventuring group** may benefit, not just the Sailor alone.
- The primary benefit is **free passage**, not ownership of a vessel, guaranteed transport at any moment, financial support, unlimited cargo transport, immunity from travel hazards, passage into inaccessible territory, or authority over a captain.
- An available voyage may be indirect or delayed. No guaranteed route, timetable or arrival date.
- Cooperation with crew tasks is part of the arrangement. This does not silently grant weapon/armor proficiencies, automatic skilled labor on behalf of the party, or shipboard character class abilities.
- In circumstances where there are no usable vessels or relevant relationships, the GM adjudicates availability normally. Avoid overrestricting the feature by requiring a detailed contact list or proving that a specific captain owes a favor before it can function.

**Strength comparison:** This is a reasonably concrete transport benefit potentially valuable to several companions, but it is strongly campaign-dependent. Compare during the corpus review with Street-Raised's approved settlement navigation, Guild Merchant's mule/cart, and other travel benefits. Do not preemptively remove the source's free-passage entitlement to force numerical parity.

### Starting equipment — source-faithful proposal

- One **belaying pin**, treated as an ordinary club where relevant.
- **50 feet of silk rope**.
- One **lucky charm or equivalent ordinary trinket**, with no automatic supernatural luck modifier.
- **Common clothes**.
- A **pouch with 10 gp**.

All are ordinary inventory possessions; item IDs and materialization remain deferred to common character-equipment integration. The rope and club have meaningful gear value and should be reviewed along with other Background packages, not balanced solely by the 10 gp cash grant.

No vessel, shipbuilding supplies, navigator's tools, dock property, cargo, or sailor's wages are granted automatically by this proposed package. Professional **training** in Navigation is separate from **possessing** Navigator's Tools.

### Narrative specialties

Examples: Deckhand, Navigator, Cook, Lookout, Boatswain, Ship's Officer, Sailmaker, Quartermaster, River Pilot, Fisher, Merchant Mariner. These are optional experience prompts, not mandatory mechanical variants or individually approved new Backgrounds.

**Important:** Pirate history may still be part of a Sailor character's story where the character prefers Ship's Passage. This narrative overlap does not automatically grant Pirate's distinct Bad Reputation mechanics.

## Pirate — independent later reconciliation, not a Sailor variant

Consistent with the owner's objections to variants and the separately approved Knight, Spy and Guild Merchant:

- Treat **Pirate as a potential independent Background** rather than an inheriting Sailor subtype.
- The 2014 Bad Reputation feature is a distinct mechanical identity and merits its own evaluation instead of being an interchangeable Sailor-feature option.
- Do **not** silently inherit Sailor's Athletics, Perception, Navigation, water-vehicle qualification or equipment into Pirate before considering whether they suit its independent training. Some piracy is maritime, but the relationship does not compel a mechanical dependency.
- The published Bad Reputation effect is unusually broad when read literally (impunity for minor offenses anywhere). During Pirate reconciliation, decide whether recognition and intimidated witnesses, local consequences and enforcement context preserve the intended functionality without creating universal legal immunity. Do not rewrite or approve the feature here.
- No compulsory childhood under pirates, moral alignment or universal unlawful conduct inferred from the published Pirate narrative.
- A Pirate who earns noncombat work aboard vessels may have shipmates and voyage contacts in the story without automatically inheriting Sailor's free-passage feature.

Separate Pirate reconciliation may take place after the first corpus-wide review if we need only a representative set of Background mechanics.

## Cross-component and balance tests

1. **Sailor versus Guild Merchant:** The merchant's Navigator's Tools training is only one choice and does not confer Water Vehicles; Sailor receives both Navigation and Water Vehicles but no extra language, and gives conditional group passage rather than commercial association benefits.
2. **Sailor versus Soldier:** Both have a vehicle qualification, but for different modes. Military Rank does not confer access to a ship and Ship's Passage does not grant military command.
3. **Sailor versus Street-Raised Upbringing:** Both can improve travel within bounded environments, but one secures transport aboard vessels while the other accelerates accessible city routes; neither grants impossible routes.
4. **Sailor versus maritime formative Upbringing:** Growing up aboard a vessel may teach languages or customs independently; overlapping Navigation training awaits general duplicate-grant semantics.
5. **Sailor versus Knight:** A knight's noncombat retinue does not provide ocean passage, and crew who offer passage are not permanent retainers.
6. **Sailor versus future Outlander:** Shipboard wayfinding and wilderness travel should not imply one universal navigation procedure.
7. **Sailor versus Pirate:** A sailor can have worked on a pirate ship without automatically acquiring Bad Reputation, and a future Pirate should not inherit every Sailor benefit merely by source-edition naming.
8. **Compound Skills:** Athletics and Perception are composite; do not automatically grant the independently ranked child Skills or double rank bonuses.
9. **Equipment:** Compare the cost of 50 feet of silk rope and any implicit transport advantage with other starting sets; do not reinterpret ordinary belongings as an additional inventory system.

## Approval boundary

**Pending owner approval:** Fixed Athletics/Perception, Navigation Competency, Water Vehicles qualification, **no extra language**, independent Ship's Passage and its bounded group benefit, and the source-faithful ordinary equipment package with **10 gp**.

Only after approving those points may we create `data/rulesets/first-party-001/native/backgrounds/sailor.json`. **Pirate remains an independent design candidate**, not an approved or inherited variant; no native Pirate JSON is authorized. Native grant execution, vehicle category projection, identity resolution and common balance review remain deferred.
