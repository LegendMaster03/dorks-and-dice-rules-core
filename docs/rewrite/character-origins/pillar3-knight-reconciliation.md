# Pillar 3 — Independent Knight Background Reconciliation

**Status:** Source comparison and unapproved native content candidate. No Knight Background or native JSON is approved or authored here.
**Ruleset:** `first-party-001`  
**Working branch:** `feature/character-origins-pillar3-life-experiences`

## Established decisions

- Knight is a **new, independent Background**, not a variant or automatically inherited subclass of Noble or Soldier. The owner clarified that Soldier, rather than Knight, was the Background already designed in the rewrite.
- Aristocratic Upbringing, Noble Background, Soldier Background, and future Knight Background remain independently selectable; no rank, title, formal employment or upbringing mechanically grants the full package of another.
- A knight may be ennobled, serve in a military command, belong to a civic/religious/martial order, or act under a patron, but the game must not require all of these simultaneously.
- Prioritizing mechanical coherence and specific source concepts does not require reproducing 2014 PHB narrative wording. Most PHB Backgrounds, including Knight/Noble's source expression, are not part of SRD 5.1's licensed Background text.

## Published 5e comparative evidence

| 2014 source | Trained skills | Other learned grants | Recognized feature | Design takeaway |
| --- | --- | --- | --- | --- |
| **Noble: Knight** (2014 PHB) | History, Persuasion | One Gaming Set; one language | **Retainers**, in place of Noble's Position of Privilege; three noncombat attendants, commonly including a squire | Knightly household service and noncombat companions are the differentiator, but direct copy of Noble's training and noble-household prerequisites would undermine the desired independent Knight |
| **Knight of the Order** (*Sword Coast Adventurer's Guide*) | Persuasion; one of Arcana, History, Nature or Religion | One Gaming Set or musical instrument; one language | **Knightly Regard**, recognition from members and those who respect the order | Order identity need not imply inherited title or ordinary soldier command; learned expertise reflects the order's focus |
| **Soldier** (approved native) | Athletics, Intimidation | Gaming Set; Land Vehicles | **Military Rank**, conditional chain-of-command recognition and limited requisitions | Do not give Knight an automatic second copy of military authority |
| **Noble** (approved native) | History, Persuasion | Gaming Set; one language | **Position of Privilege**, contextual access from noble standing | Do not assume a knight has universal aristocratic standing |

Sources:
- Official D&D Beyond, 2014 Noble / Knight variant: https://www.dndbeyond.com/backgrounds/10-noble
- Official D&D Beyond source index for *Sword Coast Adventurer's Guide* Knight of the Order: https://www.dndbeyond.com/sources/dnd/scag
- Official D&D Beyond Background index: https://www.dndbeyond.com/backgrounds
- Repository-native Soldier and Noble records on the Pillar 3 branch.

## Initial identity proposal (not approved)

**Knight:** A person who has served in a recognized knightly capacity under an oath, commission, code, or title. This can represent a sworn protector, order member, mounted knight, wandering knight, civic champion, religious knight, or a knight whose rank was earned later in life. The role need not mean royal nobility, formal cleric spellcasting, a specific alignment, current military command, or ownership of a horse.

Distinguish practical service from **a merely aspirational character** who has not yet been trained, sworn or commissioned. The owner may separately decide whether an apprentice/squire can select Knight before a formal knighthood.

## Candidate learned benefits

**Skill proposal:** Persuasion plus **one skill of choice** from Athletics, Animal Handling, History, Religion, Ride, Arcana, or Nature. All are existing native skill IDs (`skill.persuasion`, `skill.athletics`, `skill.animal-handling`, `skill.history`, `skill.religion`, `skill.ride`, `skill.arcana`, `skill.nature`). Persuasion is present in both published Knight models; the second skill reflects service emphasis. This is a candidate, not a commitment that every knightly order teaches persuasion.

**Competency proposal:** Train with one Gaming Set **or** one type of musical instrument. Historical 2014 Knight allowed Gaming Set; Knight of the Order broadened this to an instrument alternative. Gaming Set is a category choice in native Backgrounds, while individual instrument choices and qualification execution remain to be defined. Do not invent a nonexistent `competency.musical-instrument` ID.

**Language proposal:** One language associated with the knight's order, patron, duties, training, or service, rather than one unrestricted added language required by arbitrary noble heritage. Common is possible where appropriate. This choice is learned through Background, not implicitly inherited from Aristocratic Upbringing.

**Exclude:** Background-derived martial armor/weapon proficiencies, free mounts, combat maneuvers, spellcasting, fixed 2024 ability-score increases or Origin feats merely for bearing the Knight title. Combat capabilities come from their own appropriate rules; do not make Knight a hidden Fighter or Paladin class.

## Defining feature: choose one primary model before approval

### Model A — Knightly Standing (organization/recognition)

Those familiar with the knight's commission or order acknowledge that standing. They may provide reasonable introductions, hospitality, advice, or access to relevant contacts, subject to local recognition and resources. This is **not** Noble's Position of Privilege or Soldier's Military Rank, and provides no universal command authority.

**Advantage:** Broadly fits knights without estates or households. **Risk:** An order-access benefit may feel mechanically too similar to existing Soldier, Acolyte, and Noble social features unless given a distinctive scope.

### Model B — Knightly Retinue (2014-derived, independent)

A knight can call upon the service of **up to three ordinary noncombat retainers** tied to a household, patron, order, or other established relationship. One could be a squire; others might act as messenger, caretaker, groom, or attendant. The players and GM establish how the attendants are actually obtained and whether they remain with an itinerant character. They perform appropriate ordinary duties but do not fight, take combat turns as followers, join dangerous expeditions, or provide an infinite supply of resources. Mistreatment or prolonged danger can end their service. Recognition of Knight status is narrative context, not a second unrestricted privilege.

**Advantage:** Closely preserves the historical Knight feature's mechanical distinction and avoids merely duplicating rank/privilege. **Risk:** Three persistent NPCs, even noncombatants, impose an unusual logistics and balance burden; a lone wandering knight without a patron may require a reasonable way to arrange this support.

### Model C — Both recognition and retainers

Grant order recognition plus guaranteed retinue. This is mechanically richer than Noble's or Soldier's single social feature; **not recommended** without a deliberate later balancing decision.

**Working recommendation:** Choose **Model B** as the core mechanical distinction, and keep organizational recognition contextual rather than stacking Model A as a separate feature. This preserves source mechanics without making Knight mechanically a Noble variant. However, review the three-retainer count and applicability before committing it as native content.

## Starting inventory proposal

**Distinct from Noble's inherited-wealth cues:** Emblem or document identifying knightly standing, service attire appropriate to the campaign, a personal token of patronage or service, a pouch and a modest amount of coin (e.g. **10 GP**, **not approved**). No guaranteed warhorse, armor set, long-lived retinue inventory, signet ring, land grant or hereditary pedigree. The 2014 Noble equipment is source history, not required to be inherited into Knight. Equipment would enter ordinary character inventory when native grants are executed.

## Test combinations

1. **Aristocratic + Knight:** The knight knows upper-class customs, without necessarily holding a Noble Background.
2. **Rural Community + Knight:** A character elevated to knighthood through service, with no aristocratic childhood.
3. **Giant Foundling + Knight:** A nontraditional Species/community origin with recognized knightly service; overlapping Intimidation does not arise automatically if Knight uses the proposed Persuasion skill.
4. **Knight versus Soldier:** An independent knight does not command soldiers from unrelated forces. A Soldier's rank does not automatically procure a squire.
5. **Knight versus Noble:** A knight can have retinue support without universal high-society access; a noble can have high-society access without automatic retainers.
6. **Knight without a horse:** The knight's Background remains usable where mounted service is not its tradition. Selecting Ride remains voluntary.
7. **Knight without active order support:** Discuss how previously earned recognition and attendants interact with excommunication, demobilization, loss of title, or distant regions without retroactively erasing trained skills.

## Approval boundary

No Knight JSON until its core feature is selected and the skill/competency/language grants and equipment package are approved. This remains a stand-alone Background; no Noble or Soldier inheritance, no mechanical variant system, and no new follower subsystem just to represent noncombat retainers.


## Approved Retainers feature direction — 2026-11-04

**Owner decision:** Adopt the **2014 5e Retainers model and its intentional ambiguity and limitations** as the core differentiating feature for the independent Knight Background. Do **not** design a detailed retainer-management subsystem to resolve matters that 5e leaves to the DM and table. This selects Model B above; Model A (Knightly Standing) is not a second separately granted feature.

**Approved conceptual mechanics:**

- A retinue of **three noncombat retainers**, including the 2014 Knight model of a squire and two ordinary attendants. These are supporting NPCs, **not extra player-controlled characters** or combatants.
- Retainers may perform appropriate ordinary tasks and errands. They **do not fight** for the character and **do not accompany the character into obviously dangerous areas**, including dungeons. They may leave if regularly endangered or abused.
- Their ordinary behavior, availability and portrayal are adjudicated by the DM, consistent with usual NPC handling.
- Keep 5e's **unspecified logistics unspecified**: no mandatory wages, daily cost, provisioning table, NPC character-sheet or initiative-tracking requirement; no automatic replacement or recruitment mechanic; no prescribed travel schedule, estate requirement, or detailed follower progression.
- Do not implicitly add noble status, a guaranteed mount, a second institutional-access power, or a separate combat companion grant. Knight remains a new independent Background rather than a Noble/Soldier variant.
- Author eventual native text in original wording rather than copying the non-SRD PHB passage.

**Still unapproved:** Exact Skill and Competency grants, language grant, starting equipment, the full final native text, and promotion to `native/backgrounds/knight.json`. Previous skill/competency/equipment proposals remain open for individual approval or revision.
