# Pillar 3 — Charlatan Background Reconciliation

**Status:** Charlatan approved and authored as an independent native Background. Earlier proposals and pending boundaries below are retained as design history; the approved allocation is documented in the final section.

**Ruleset:** `first-party-001`  
**Development branch:** `feature/character-origins-pillar3-life-experiences`

## Historical 2014 baseline

The 2014 *Player's Handbook* Charlatan describes sustained practice with impersonation, deception, confidence schemes and related misrepresentation. Its training, unlike Folk Hero's historical rural vocation package, follows fairly directly from its defining acquired experience.

| Component | 2014 Charlatan |
| --- | --- |
| Skills | Deception, Sleight of Hand |
| Tools | Disguise Kit and Forgery Kit |
| Languages | None |
| Feature | False Identity |
| Equipment | Fine clothes, Disguise Kit, **one** choice of con-related props, pouch with **15 gp** |

The con-prop choices are ten small stoppered bottles of colored liquid, weighted dice, marked playing cards, or a signet ring attributed to a fictional duke. They are ordinary objects, not magical items or proof of genuine noble status. **The published starting equipment includes a Disguise Kit, but not a separate Forgery Kit**, despite proficiency with both.

**False Identity** provides an *already established second identity* backed by paperwork, people who know the persona, and disguise arrangements, plus an ability to imitate documents (including formal papers and personal letters) after seeing an appropriate example of the document type or handwriting.

Source comparisons:
- Official D&D Beyond Background index, identifying legacy **Charlatan** and **False Identity**: https://www.dndbeyond.com/backgrounds
- Secondary historical mechanical reference (2014 PHB, not 2024): https://dnd5e.wikidot.com/background:charlatan
- Secondary independent entry listing the 2014 grants: https://onyx5e.com/en/backgrounds/charlatan/
- 2014 Basic Rules for general Background customization/context, not a complete Charlatan entry: https://www.dndbeyond.com/sources/dnd/basic-rules-2014/personality-and-background

Write independently authored native feature and equipment descriptions. Do not copy the PHB's extended narrative or suggested-characteristics tables.

**2024 contrast, not proposed here:** The newer Charlatan uses a different feat/ability model and has a Forgery Kit but not the complete 2014 Disguise Kit plus Forgery Kit training package. This reconciliation follows the 2014 source selected for Pillar 3; do not import a `Skilled` Origin feat or fixed ability increases.

## Identity and Pillar 3 separation

**Proposed independent Background identity:** You have substantial experience maintaining deceptive personas, misrepresenting goods or claims, performing confidence schemes, or using documents and disguises to present a believable story to other people. The Background records acquired methods and established social arrangements. It does not require a particular childhood, culture, Species, poverty, criminal organization, ongoing unlawful activity, compulsory cruelty, or a specific alignment. A character might now have abandoned that work.

**Upbringing:** There is **no required Charlatan Upbringing**, inherited parentage, or culturally prescribed deception grant. A Street-Raised, Aristocratic, Wilderness-Raised or other formative Upbringing can be combined with Charlatan where the story supports both. Street-Raised is not fully approved; selecting Charlatan does not approve or import that candidate.

**Other Backgrounds:**

| Background | Distinctive selected-native feature | Charlatan distinction |
| --- | --- | --- |
| `background.criminal` | **Criminal Contact** sends messages via an established underworld relationship | False Identity does not create a criminal communication route or unlimited access to unlawful networks |
| `background.spy` | **Intelligence Sources** can supply leads through a trusted intermediary | The alias acquaintances need not be informants, intelligence assets or even allies |
| `background.noble` | **Position of Privilege** represents socially recognized actual standing | A convincing fake noble signet or persona does not grant the legal rank or general privileges of real nobility |
| `background.folk-hero` | **Community Refuge** through recognition and sympathy | A Charlatan's identity has a social footprint but does not automatically secure shelter or trust from people who discover the deception |

A Spy may maintain an alias by ordinary play, and other characters may use tools or deceive others. The differentiator is that Charlatan **begins with an established second persona as a granted Background benefit**, rather than needing to build one entirely during play.

## Recommended native package — first pass, pending approval

### Skills: preserve the original fixed pair

- **Deception**: `skill.deception`, fixed training.
- **Sleight of Hand**: `skill.sleight-of-hand`, fixed training.

Both exist in `native/skills.json`. Deception covers convincing verbal or presented falsehoods and can be relevant to wearing a disguise convincingly; Sleight of Hand supports concealed transfers, subtle manipulation and similar techniques. These are not synonymous with each other.

**Why not offer two choices from a broader eight-Skill pool?** Charlatan, unlike Folk Hero, names a specific practiced tradecraft. The source-fixed skills represent that identity coherently and give a useful comparison against the deliberately flexible Spy and Folk Hero packages. If a future performer-oriented or court-infiltration character needs a different package, do not automatically redefine every Charlatan as any possible social specialist.

No automatically added Persuasion, Insight, Performance, Disguise Skill or separately ranked Forgery Skill outside the grant's reviewed competency facet.

### Competencies: preserve both 2014 tool proficiencies, but honor native identities

- **Disguise Kit**: `competency.disguise-kit`, fixed training.
- **Forgery**: `competency.forgery`, fixed training.

Both IDs are verified in `native/competencies.json`. The Disguise Kit is a standalone tool-proficiency Competency; it does not automatically train `skill.disguise`. The Forgery Competency already includes **both a Forgery skill facet and Forgery Kit proficiency**, per the established universal training identity. This can be stronger than the 2014 Forgery Kit-only grant. Preserve the existing semantic identity; do **not** invent a second narrower `competency.forgery-kit` merely to match a source line. Flag unequal-facet balance for the corpus audit.

**Training is distinct from equipment:** The starting set includes the Disguise Kit. It does not automatically supply a Forgery Kit as a separate piece of inventory unless some other rule or purchase provides it. False Identity includes supporting documents in the fiction; it should not silently create unlimited independent tool kits, counterfeit goods or monetary value.

### Language: no extra Background language

The 2014 package grants **none**. Neither lying convincingly nor forging paperwork necessarily teaches a complete additional language. Existing learned language and literacy capabilities follow their ordinary separate sources. Forging a sample does not grant understanding of any unknown language's meaning.

### False Identity: proposed original feature wording

> **False Identity.** You have established one alternate persona supported by plausible documents, suitable disguise arrangements, and acquaintances who recognize you by that identity. In circumstances where the persona's history and evidence are credible, you can present yourself as that person without first having to build the identity from nothing. You also know how to imitate official or personal documents and a person's writing when you have examined a relevant example and have suitable materials and opportunity to work. The persona does not grant the real legal privileges of an impersonated office or rank. Suspicions, verification, scrutiny and the consequences of exposure remain part of play; neither the identity nor a forged document guarantees that every observer will accept it.

**Functional scope for owner review:**

1. **An actual pre-established cover:** One alternate identity has an existing history, some documentation, disguise preparation and acquaintanceship. It is not just a player's chosen pseudonym. The DM and player determine its scale and where it is recognized; without supporting context it is not a universal second citizenship or a record in every city.
2. **Document imitation is meaningful:** Having a model enables the character to attempt to reproduce the document type or writing using available means; there is no arbitrary prerequisite to first learn a special new rule. However, ability to manufacture an imitation and successfully deceive an examiner are different questions. Detection, security, hostile scrutiny and counterfeit quality follow ordinary checks/adjudication, not automatic validity.
3. **Not unlimited personas or items:** Starting with one established identity does not grant limitless fully documented cover personas or instantly generated passports, seals, credentials, disguises, contacts or forgery supplies. Ordinary future identity creation can be attempted through play.
4. **Potential exposure:** If the cover is exposed or supporting relationships change, its usefulness may decline without erasing acquired Deception, Sleight of Hand, Disguise Kit or Forgery training. Do not introduce a special per-persona durability or universal reputation subsystem.
5. **No implied institutional access:** A pretend noble or official might be believed where the fiction justifies it, but never gains actual institutional authority, immunity or property rights by automatic Background grant.
6. **No independent contact network:** The acquaintances who know the persona are part of the cover's credibility, not Criminal Contact or Spy Intelligence Sources. They need not be willing helpers, insiders or unlimited messengers.
7. **No compulsory active crimes:** The character may currently use a lawfully adopted pseudonym, stage persona, abandoned fraudulent identity or other setting-appropriate deception history. The Background does not dictate morality or present-day occupation.

**Key decision point:** The published feature's permission to forge from an example must not be reduced to a mere generic forgery tool proficiency, particularly now that `competency.forgery` itself includes skill and tool facets. Preserve the **pre-existing supported cover** and sample-based document-imitation entitlement, while leaving deception under scrutiny subject to actual play.

### Ordinary starting possessions: preserve the 2014 package

- **Fine clothes** — one set.
- **Disguise Kit** — one physical kit, separate from competency training.
- **Choose one con-prop package:**
  - Ten stoppered small bottles containing colored liquid; **or**
  - A set of weighted dice; **or**
  - A deck of marked playing cards; **or**
  - A signet ring claiming association with a fictional noble title.
- **Pouch containing 15 gp**.

All items are ordinary inventory. The bottles are props, not alchemical potions; the gaming props grant neither gaming-set training nor winning odds by fiat; the fictional ring does not give noble status. The source's four very different con-prop types may have different prices/weights; preserve their identity but do not invent a money-equivalent exchange rate or a new fraud-economy mechanic.

The feature's supporting false-identity documents and disguise arrangements should remain meaningfully available to the character as part of the feature's established fictional circumstances, but do not automatically duplicate the Disguise Kit item or represent unpriced unlimited evidence as salable inventory. Ordinary item identity/materialization remains deferred to shared inventory integration.

### Optional schemes and character history

Possible practiced schemes: confidence pitches, false investments, forged references, altered documents, impersonation, rigged games, stage cons or fraudulent sales. These are optional prompts, not mechanical subtypes or a fixed favored-scheme roll. A Charlatan need not have mastered every scam.

## Corpus-wide comparison questions

1. **Forgery identity:** `competency.forgery` has a skill facet and a tool facet; compare strength against Criminal (Thieves' Tools), Spy (choice among four Competencies), and Guild Artisan (variable multi-facet crafting Competency).
2. **False Identity value:** A developed alternate persona may be considerably more useful in intrigue than Ship's Passage or Explorer's Expedition Survey; value depends on campaign, settings and how the DM recognizes supporting acquaintances.
3. **Equipment:** Fine clothes and a Disguise Kit plus con props and 15 gp are materially richer than some approved Background packages; compare inventory economics later without arbitrary immediate cuts.
4. **Upbringing overlaps:** Street-Raised may grant Sleight of Hand or Disguise Kit/Thieves' Tools (selection counts unresolved); Aristocratic may grant social training and access to institutions. Do not make duplicate-training replacement rules locally.
5. **False papers versus actual rank:** False Identity does not confer Noble's Position of Privilege, Knight's Retainers, military command, legal authority, or official court recognition.
6. **No extra class powers:** No Roguish Expertise, forged magic, free spells, 2024 Skilled feat, unlimited disguise effectiveness, or automatic Deception success.

## Approval boundary

**Recommended for owner approval:** Independent `background.charlatan` with fixed **Deception and Sleight of Hand**, fixed **Disguise Kit and Forgery** competencies, **no language**, the bounded yet meaningful **False Identity** feature (one existing documented cover plus sample-based document imitation), and source-shaped physical starting possessions including **15 gp**.

**Unresolved details to decide or revise:** Whether the skill pair should remain fixed, whether the two native Competencies are justified despite Forgery's additional native skill facet, and whether the social cover wording adequately preserves the 2014 feature without overstating universal identity recognition.

No native `data/rulesets/first-party-001/native/backgrounds/charlatan.json` is selected or authorized by this document. Native false-identity feature execution, inventory materialization, duplicate training and comparative balance are deferred. No runtime or Character Sheet code is to be changed by this source review.


## Approved native Charlatan allocation — 2026-11-04

**Owner-approved:** Preserve the proposed independent 2014-based Charlatan package with fixed **Deception** and **Sleight of Hand**, both **Disguise Kit** and **Forgery** Competencies, **no additional language**, one established **False Identity** and sample-based document imitation, and the 2014-style ordinary equipment with **15 gp**.

**Authored native content:** `data/rulesets/first-party-001/native/backgrounds/charlatan.json` (`background.charlatan`, `selected-native`, independently selectable).

- **Training:** `skill.deception`, `skill.sleight-of-hand`, `competency.disguise-kit`, and `competency.forgery` are fixed grants. Native Forgery retains its approved skill and tool facets; it is not split into a fictional narrower Forgery Kit Competency. The separate Disguise Skill is not automatically trained.
- **Feature:** False Identity establishes **one credible alternate persona** supported by documents, suitable disguise arrangements and acquaintances familiar with that persona. It also enables imitation of document types and handwriting after examining relevant examples with suitable materials and opportunity. It does not guarantee acceptance of forged documents, confer actual noble/institutional authority, or provide Criminal Contact/Spy Intelligence Sources.
- **Ordinary starting inventory:** Fine clothes, one physical Disguise Kit, **one mutually exclusive con-prop package** from ten colored-liquid bottles, weighted dice, marked playing cards or a fictional-noble signet ring, a pouch and **15 gp**. No physical Forgery Kit is automatically granted. False-identity supporting records are part of the feature's established circumstances and are not unlimited salable inventory.
- **Independence:** No mandatory illegal activity, childhood, criminal organization, Species, social class or Upbringing. No 2024 Origin feat or fixed ability increases.
- **Deferred:** Comparative strength of `competency.forgery` multi-facet grant, feature recognition/scaling under different campaigns, identity/document scrutiny, item identity/materialization, and overlap with Street-Raised or Aristocratic Upbringing.

**This approved allocation supersedes the earlier pending approval boundary and unapproved language in this document.** Runtime grant projection and consumer behavior were not added.
