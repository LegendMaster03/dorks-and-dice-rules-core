# Pillar 3 — Hermit: Seclusion, Upbringing and Discovery Reconciliation

**Status:** 2014 source comparison and design proposals only. The Hermit Background and any Isolated-Raised Upbringing are **not approved**; no native JSON, C# or consumer changes are authorized by this document.

**Ruleset:** `first-party-001`  
**Development branch:** `feature/character-origins-pillar3-life-experiences`

## 2014 historical baseline

The 2014 *Player's Handbook* Hermit describes living apart from ordinary social life, sometimes in a sheltered community such as a monastery, sometimes alone, for a significant **formative period**. This need not mean childhood. The source allows reasons including spiritual study, exile, artistic work, a retreat after a life-changing event, care of an old site, or communing with nature. None is a compulsory biography.

| Component | 2014 Hermit |
| --- | --- |
| Skills | Medicine and Religion |
| Tool training | Herbalism Kit |
| Language | One of choice |
| Feature | Discovery |
| Equipment | Scroll case containing notes/studies or prayers, winter blanket, common clothes, physical Herbalism Kit, and **5 gp** |

The *Discovery* feature specifies that the seclusion produced a consequential revelation, such as previously lost lore, an unusual place, an insight concerning supernatural or natural forces, or compromising information. Its nature and campaign significance are determined **with the DM**. The source does **not** define a repeatable numerical bonus, unrestricted ability to reveal new facts, a routine success on investigation checks, or automatic ownership of a powerful relic.

Historical sources (comparison material, not native expressions):
- 2014 PHB Hermit feature and equipment transcriptions: https://dnd5e.wikidot.com/background:hermit and https://www.dungeonheaven.com/backgrounds-5e/hermit
- 2014 PHB Hermit brief pointer: https://dnd-wiki.org/wiki/Hermit_(5e)

Use newly written Dorks & Dice prose for all native mechanics. Avoid source prose and narrative-table copying. No 2024 feats, Ability increases or automatic class benefits follow from this review.

## Classification: Background and formative Upbringing are different

**Recommendation: Hermit remains an independent Background**, representing a period of sustained seclusion, withdrawal or sheltered retreat and the experiences or consequential findings that came from it. It can occur early or late in a character's life; a purely chronological boundary between Upbringing and Background is neither necessary nor approved.

A hypothetical **Isolated-Raised Upbringing** would capture a formative household/community environment: protected study, remote upbringing, limited social contact, small-scale communal routines, or being educated away from common settlements. That is neither the same as voluntarily withdrawing later nor automatically the source of a major discovery.

The split is based on **the role of the experience**, not its exact age. A childhood period could have both formative influence and a consequential personal discovery, but choosing one option must not automatically grant the other or duplicate its Skill/language grants. A Hermit could have any Species or Upbringing, including Street-Raised, Aristocratic or Wilderness-Raised. A character with an Isolated-Raised Upbringing could later be a Soldier, Sage, or another Background rather than a Hermit.

**No forced history:** Hermit does not require being a religious ascetic, an orphan, a wilderness survivor, a magician, socially inept, impoverished, involuntarily exiled, or permanently opposed to society. A monastic retreat can be communal without being solitary. Isolation does not imply supernatural revelation, deprivation of all languages, or mental illness.

## Skill grant alternatives

**Problem with fixed Medicine + Religion:** These are coherent for a healer who prayed and studied but not necessary for an isolated writer, natural philosopher, historian, caregiver, exiled politician, or researcher of physical phenomena. The source's pairing is a reflection of a common 2014 Hermit archetype, not a causal consequence of all seclusion.

Three candidate models:

| Model | Skills | Other grant | Pros / concerns |
| --- | --- | --- | --- |
| **A — strict 2014** | Fixed Medicine and Religion | Herbalism + one language | Straightforward source fidelity but assumes an ascetic healer with botanical training |
| **B — flexible with a study/practice grant** | Choose two from an appropriate pool | Choose **either** Herbalism training **or** one learned language | Closer in overall grant count, but still forces everyone to be either a herbalist or student of a whole additional language |
| **C — origin-relevant Skills only** (**recommended**) | Choose two distinct from an appropriate pool | **No automatically granted Competency, extra language or vehicle qualification** | No mandatory vocation, religion or language acquisition; fewer grants than the 2014 source, which requires later comparative balance review |

**Preferred Skill pool for B or C:** Choose **two distinct** from:
- Medicine — aid and treatment of a secluded community or personal research;
- Religion — contemplation or devotional study;
- Nature — natural observations or scholarship;
- Survival — living alone outdoors where it was part of the experience;
- Arcana — study of magical phenomena or lore;
- History — historical research or stewardship of a site;
- Investigation — analysis of written materials, events or evidence;
- Insight — contemplation of behavior, self and small-group relationships.

These are verified existing IDs: `skill.medicine`, `skill.religion`, `skill.nature`, `skill.survival`, `skill.arcana`, `skill.history`, `skill.investigation`, `skill.insight`. **The original Medicine + Religion pair remains selectable.** The two-distinct-Skill executor follows the established `background.folk-hero` / `background.spy` content shape; implementation is deferred. Skills learned from existing Upbringing may overlap; general duplicate training semantics apply later.

**Why recommend C:** The same principle motivated the approved Folk Hero departure from source-mandated rural trades: meaningful life experience need not require an unrelated vocational training grant. A deeply isolated astronomer need not know herbal remedies, a solitary caregiver may never have learned a second language, and an exiled noble need not practice theology. Adding a forced optional benefit solely to maintain original source grant counts conceals the design problem rather than solving it.

If the owner prefers closer 2014 fidelity, **B** is a viable alternate proposal: one `benefit-choice` between `competency.herbalism` and one language learned through the Hermit's study or sheltered community. The native `competency.herbalism` is verified as a tool-proficiency identity for Herbalism Kit; it is distinct from `skill.profession-herbalist` and does not automatically train that independently ranked Profession specialty. This is not the current preferred selection and needs explicit approval.

## Discovery — native feature candidates

### Defining the entitlement

A feature that merely says "the player and GM may create a plot hook" may amount to ordinary cooperative backstory creation, not a usable mechanical entitlement. Conversely, automatically revealing a setting-defining hidden truth would force unapproved lore on the GM and might overwhelm the campaign.

The source's most defensible gameplay function is **the character begins with one real, nontrivial discovery made during seclusion**, established jointly with the GM, rather than merely having a question or a rumor that may turn out to be wrong. It can have consequences in play but need not rewrite the world or be relevant to every session.

**Preferred feature name:** Discovery (retained from the source because it accurately describes the entitlement).

**Proposed original Dorks & Dice wording:**

> **Discovery.** During your time apart from ordinary society, you uncovered one meaningful truth about a person, place, event, practice, or aspect of the world that was not generally understood in the circles you knew. Work with the GM to establish what you discovered, how you came to know it, and why it may matter. That established discovery is genuine within the agreed circumstances, although your understanding of its causes, reach, and implications may be incomplete. When you apply the discovery during play, you know the established information without needing to rediscover it through an ordinary knowledge check. The discovery does not grant an unapproved spell, item, ability, access right, guaranteed solution to a future challenge, or a continuing power to demand hidden facts. The GM determines undiscovered details and how the world responds to the information.

**Operational notes:**
1. **One established fact, not an arbitrary secret on demand.** The chosen truth should have a clear scope and sufficient specificity to create a meaningful opportunity or choice. "I have a mysterious feeling" alone is not a Discovery. "I learned where the old boundary stones mark an abandoned access route to a familiar site" is a possible scoped example if the GM agrees that it exists.
2. **Agreed truth versus inferences.** The core agreed fact is real when established; the character's theory about why it is true may be mistaken. The feature does not promise infallible understanding or future immutability when the world changes.
3. **Does not compel main-plot rewrite.** The player proposes a discovery and the GM works to integrate or bound it. It may be personal, local, historical, natural, religious or arcane. No default requirement that it affect gods, planes, a campaign villain or an entire kingdom.
4. **Not an unlimited information service.** After the fact is set, repeated requests about the same discovery do not create more unknown information. Ordinary Investigation, Arcana, Nature, History and other checks still resolve new questions.
5. **Timing:** Establish the fact during character creation if workable. If the campaign setting is not sufficiently defined yet, the player and GM can mark the discovery as pending and select its actual content at the first meaningful opportunity **before using the feature**. This is not license to retroactively declare a guaranteed solution after seeing a scene.
6. **No mandatory physical relic.** A discovery may concern an object, but the feature does not automatically grant its possession or access. Any actual item enters ordinary inventory by its own explicit grant.
7. **No personality or age gate.** A younger character or a character formed in a community retreat may select Hermit if the sustained experience and discovery are meaningful; they need not have lived alone as an adult.

**Examples for adjudication:**
- An herbal researcher discovers an unusual plant's genuine local property. This is not automatic potion creation or Botanical/Herbalism training.
- An archivist in retreat learns a factual, overlooked connection between two historical events. That does not reveal all relevant secrets or identities.
- A caretaker knows a previously overlooked route into an old ruin because they personally surveyed it during seclusion. It provides fixed prior knowledge, not Explorer's reusable `Expedition Survey` capability.
- A contemplative scholar knows a true detail of an obscure religious practice. This is not Acolyte's institutional `Shelter of the Faithful`.
- A person exiled for political reasons knows a significant factual inconsistency in an accusation. This does not automatically secure acquittal, witnesses or legal authority.

### Neighboring selected-native Background distinctions

| Neighbor | Existing feature | Hermit distinction |
| --- | --- | --- |
| `background.sage` | **Researcher:** indicates where unknown lore might be found | **Discovery:** one concrete piece of information **already known**, not a locator for future research |
| `background.explorer` | **Expedition Survey:** bounded actionable conclusions from presently available field evidence, plus personal route recall | **Discovery:** a **fixed prior finding** learned during seclusion; not an ongoing survey information entitlement |
| `background.acolyte` | **Shelter of the Faithful:** conditional help from a faith institution | Hermit needs no religious institution, temple access or implied spells |
| `background.wilderness-raised` | (Not a Background: `upbringing.wilderness-raised`) six-person Wilderness Provisioning | Hermit is not automatically skilled at wilderness resource gathering |
| `background.folk-hero` | **Community Refuge:** sympathy and hospitality from recognized deeds | Hermit receives no automatic loyal supporters, public fame or shelter |

**Residual risk:** Discovery remains partly GM-authored and may have extremely different campaign usefulness depending on content. That is a known feature-value risk, not a reason to secretly substitute a permanent +2 Knowledge bonus or automatic new lore on demand. The scope should be confirmed in playtest.

## Starting equipment alternatives

**2014 source package (for comparison only):** Scroll case of papers, winter blanket, common clothes, physical Herbalism Kit, **5 gp**. Being granted an item and being proficient with it are separate.

**Preferred adaptable ordinary inventory if C is approved:**
- One **scroll case or notebook of personal notes**, studies, observations, drawings or reflections from seclusion; the content does not itself grant unlimited unknown information.
- One **winter blanket** or comparable modest bedding item.
- One set of **common or practical clothing**.
- One **ordinary personal item used during seclusion** appropriate to the actual experience, selected with the GM, without guaranteed resale value, tool proficiency or special effects. A Herbalism Kit is possible only if separately agreed as suitable and reasonably valued, not an automatic default.
- **5 gp**.

**Equipment concern:** A full Herbalism Kit may have greater material value than a small keepsake. This alternative deliberately does not guarantee a free kit of any particular cost. All possessions are ordinary character inventory and await item-identity/materialization support.

## Unapproved formative follow-up: Isolated-Raised Upbringing

A future `upbringing.isolated-raised` (working ID only) might represent an unusually secluded formative household, educational institution, remote community, or limited-contact lifestyle. It **must not** enforce neglect, trauma, silent childhood, low social skills, poverty or fixed ethnicity. It needs its own language and learned-training balance review.

It **must not** automatically include Discovery. A childhood in seclusion need not produce secret knowledge, and Hermit does not automatically include an Isolated-Raised package. Do not infer that remote wilderness living is the same thing: `upbringing.wilderness-raised` already covers **natural resource provisioning** and practical environmental learning, whereas an isolated scholar could live in a well-supplied tower.

No native Upbringing or new general social-isolation subsystem is authorized by this proposal.

## Owner decision needed

**Recommended first-pass independent Hermit Background (Model C):**
- **Two distinct chosen Skills** from Medicine, Religion, Nature, Survival, Arcana, History, Investigation, Insight.
- **No automatically granted herbalism training, extra language, vehicle qualification or other vocational grant.**
- **Discovery:** one meaningful, genuine, GM-agreed fact originating from sustained seclusion, already known when established, with no repeatable secret-finding or world-rewriting authority.
- **Ordinary starting items:** notes in scroll case/notebook, winter blanket/bedding, practical clothes, one ordinary personal item appropriate to seclusion without guaranteed monetary value, **5 gp**.
- No required class, religion, monastic order, specific age, Species, Upbringing or alignment; no automatic future Isolated-Raised Upbringing.

**Alternative requiring owner choice:** Model B retains a single choice between Herbalism Competency and an additional language and can be paired with the same flexible Skills and feature. Model A is exact 2014 training but imposes a monastic healer archetype.

**Approval boundary:** The comparison and recommendation are **not** approval of `background.hermit`. No native record should be created before explicit owner approval. Discovery's information binding and future choice projection require common consumer integration; no separate secret-reveal subsystem is introduced.
