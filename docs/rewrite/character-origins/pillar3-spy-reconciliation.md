# Pillar 3 — Spy Background reconciliation

**Status:** Approved and authored as independent native Background. The proposal and unapproved sections below are historical analysis; the final approved allocation is recorded at the end.

**Ruleset:** `first-party-001`  
**Branch:** `feature/character-origins-pillar3-life-experiences`

## Sources and existing native boundary

- Official 2014 Basic Rules, Criminal and Variant Criminal: Spy: https://www.dndbeyond.com/sources/dnd/basic-rules-2014/personality-and-background
- D&D Beyond legacy Criminal / Spy record: https://www.dndbeyond.com/backgrounds/8-criminal-spy
- D&D Beyond 2014 Charlatan, Faction Agent, and Dimir Operative index: https://www.dndbeyond.com/backgrounds
- Approved native `background.criminal`, `background.sage`, `background.knight`, `background.noble`, and `background.acolyte` on this branch.
- The 2014 Basic Rules website publishes the source for comparison. It is **not evidence that the entire PHB Spy expression is part of SRD 5.1's Creative Commons grant**. Independently author native rule text.

2014 **Spy** is only a narrative variant of **Criminal**. It retains Criminal's Deception and Stealth training, one Gaming Set and Thieves' Tools, Criminal Contact, and the starting equipment including 15 gp. It adds no independent mechanical feature. The source explicitly allows espionage for an authority or for other interests.

**Project owner decision already established:** Backgrounds with significantly different professional or thematic experiences should not be treated as inherited variants simply because their historical 2014 entry reused a package. **Spy will be an independent Background**, with its own appropriate training and feature; the existing Criminal contact mechanics must not be inherited automatically.

## Identity: proposal

A Spy has gained meaningful experience collecting, verifying, concealing, obtaining, or communicating information covertly. The work may be for a state, organization, individual, informal association, or the character's own purposes. It may be lawful or unlawful; it does not require an underworld history, government employment, current allegiances, a permanent employer, or a specific Species or Upbringing.

Do not equate Spy with Assassin, Thief, Charlatan, or an all-purpose investigator. Intelligence work may involve infiltration, surveillance, human sources, covert observation, clandestine communication or analytical assessment. The Background should not grant automatic disguise success, surveillance immunity, infiltration access, or omniscience.

## Proposed learned grants

### Skills: two choices from six

- `skill.deception` — misdirection and operational secrecy
- `skill.stealth` — concealed movement and observation
- `skill.investigation` — assembling and examining evidence
- `skill.insight` — assessing motives and informants
- `skill.perception` — observation and reconnaissance
- `skill.persuasion` — elicitation and recruitment of sources

**Recommendation:** Choose **two distinct** skills from this pool. Unlike the 2014 source, do not mandate Deception and Stealth; that would exclude plausible intelligence specialists such as discreet observers, analysts and source handlers. This is a deliberate project design proposal, not an asserted original 2014 rule. An alternative would fix Investigation and offer one remaining choice; this gives a more rigid mechanical identity but is less faithful to the diversity of espionage work.

### Competency: one choice

Choose one existing native Competency from:

- `competency.disguise-kit`
- `competency.forgery`
- `competency.thieves-tools`
- `competency.calligraphy`

This represents cover, credentials, entry methods or document handling. **Calligraphy is not intrinsically cryptography**, and selecting it does not grant a universal cipher-breaking ability. The choice should not assume criminal conduct or automatically include the item itself.

### Language: one additional learned choice (proposed)

Learn one language used in the character's previous intelligence activity, sources, contacts, communications, or operational assignment. Common may qualify if relevant. This is one learned Background language, not a requirement that every spy operated abroad or an automatic grant of a secret language.

**Power comparison:** Two skills, one competency, one language, and one feature versus Criminal's two skills, two tool/category proficiencies, and one feature. The more flexible Spy skill pool and information-bearing feature may be stronger situationally; evaluate that difference in the corpus audit rather than assuming numerical parity.

## Defining feature alternatives — approval needed

### Model A: Intelligence Sources (recommended)

> **Intelligence Sources.** You maintain at least one established source or intermediary connected to your past intelligence work. Through the methods you learned, you know how to communicate discreetly with that contact and request leads concerning people, places, or events within their sphere of knowledge. They may share information they possess or identify another potential source, subject to their circumstances and willingness. This feature provides no automatic secrets, immediate responses, safe houses, guaranteed cooperation, or universal access to intelligence organizations.

This is not Criminal Contact with a different label. **Criminal Contact** guarantees a familiar liaison for conveying messages to an underworld network, including over long distances. **Intelligence Sources** is oriented toward gathering leads and evaluating possible sources, with neither an all-distance courier guarantee nor an obligatory criminal organization. It provides a bounded informational opportunity, not a result. As with Sage's Researcher, the GM resolves whether relevant information actually exists and is accessible.

**Concern:** A lone intelligence operative without enduring contacts may require an established past intermediary. The feature is a Background entitlement, but its meaning should not demand active employment or an intact spy agency. Test whether any contacts plausibly persist after disavowal or retirement.

### Model B: Operational Tradecraft (alternative)

> **Operational Tradecraft.** You recognize and understand common practices for covert communication and the protection of information, including arranged exchanges, signal conventions, and discreet meeting procedures. When in an environment where these methods can reasonably be used, you can identify an appropriate way to establish a covert exchange or determine whether an observed exchange appears deliberately concealed. This does not automatically reveal messages, defeat codes, evade surveillance, or establish an unknown contact.

**Advantage:** A solitary Spy without an agency or enduring network can use learned methods.  
**Concern:** Mostly interpretive; weak if every relevant task requires ordinary skill checks without a concrete benefit. It may also be less distinctive from skills such as Investigation and Perception.

### Deferred option: Constructed cover identity

A durable false persona may fit individual spies, but it strongly overlaps the original Charlatan's False Identity and the Dimir Operative's analogous feature. **Do not award a full alternate identity automatically before reconciling Charlatan** or silently bundle it with Model A.

**Recommendation:** Adopt **Model A, Intelligence Sources**, without a separate Operational Tradecraft feature. Describe tradecraft as part of the character's skill and contact narrative. If persistent contacts prove too restrictive, return to Model B rather than creating variants or stacked benefits.

## Proposed starting equipment

- Plain or work-appropriate clothing suitable for the character's cover or previous role.
- A small notebook or set of notes relevant to a past assignment (the contents may be coded but are not necessarily useful secrets).
- Ink and writing instrument.
- A pouch containing **10 gp**.

No crowbar, Gaming Set or stolen goods are mandatory. Neither a forged identity nor a master key nor a guaranteed disguise kit is supplied automatically by this package. The chosen competency represents training, not necessarily ownership of its tools. Starting items will be ordinary inventory when item materialization is available.

**10 gp is a candidate**, intentionally below the 2014 Criminal's 15 gp equipment package; its fairness and exact gear value remain open for review.

## Existing Criminal cleanup when Spy is approved

Current `native/backgrounds/criminal.json` contains an **optional Spy narrative variant** and includes an `intelligence operative` as a possible Criminal description. Those should be reviewed once an independent Spy is approved:

1. Remove the formal `suggestedVariants` Spy entry so that the content catalog does not present Spy as both a narrative Criminal variant and an independent Background.
2. Revise the Criminal description and suggested-characteristics text that imply ordinary espionage is necessarily criminal. A Spy who violated the law can still reasonably choose Criminal; the two concepts can overlap in fiction but remain different choices.
3. Retain approved Criminal skills, Thieves' Tools, Gaming Set, Criminal Contact, and equipment. This cleanup must not silently change its previously approved mechanics.

Do **not** change Criminal or add Spy native JSON until the Spy grants and feature are approved.

## Review scenarios

1. **Lawful intelligence officer:** Can select Spy without a criminal history, government employment being a permitted but not mandatory example.
2. **Freelance informant or surveillance specialist:** Can choose Spy without an agency or aristocratic upbringing.
3. **Criminal informant:** May identify with either Background according to defining experiences, with no assumed inheritance.
4. **Aristocratic + Spy:** Social education is distinct from learned intelligence work.
5. **Street-Raised + Spy:** Potential Stealth or Deception overlap awaits general duplicate-grant handling.
6. **Spy versus Sage:** Intelligence Sources obtains possible personal leads; Researcher identifies knowledge repositories or experts, not automatically contacts or covert cooperation.
7. **Spy versus Charlatan:** An established operative does not automatically own a mechanically guaranteed second identity.

## Approval boundary

The proposed two-of-six Skill selection, one-of-four Competency choice, one service-linked language, feature choice (Model A or B), and 10 gp equipment package are **not yet approved**. Author a standalone `native/backgrounds/spy.json` and clean up Criminal's old variant reference only after owner approval. No consumer C# or API work is authorized by this design document.


## Approved native Spy Background — 2026-11-04

**Owner approval:** The prior proposed package using Model A, **Intelligence Sources**, is approved as an independent native Background. The native selected record is `data/rulesets/first-party-001/native/backgrounds/spy.json` (`background.spy`, `selected-native`).

**Final approved grants:**

- **Skills:** Choose **two distinct** Skills from Deception, Stealth, Investigation, Insight, Perception, and Persuasion (`skill.deception`, `skill.stealth`, `skill.investigation`, `skill.insight`, `skill.perception`, `skill.persuasion`).
- **Competency:** Choose **one** of Disguise Kit, Forgery, Thieves' Tools, or Calligraphy (`competency.disguise-kit`, `competency.forgery`, `competency.thieves-tools`, `competency.calligraphy`). Training does not automatically supply the equipment.
- **Language:** Learn one language associated with the character's intelligence work, contacts, sources, or communications. Common can qualify where appropriate.
- **Distinctive feature:** **Intelligence Sources**, an existing source or intermediary the character can discreetly contact for possible leads in their area of knowledge, without guaranteed secrets, safe houses, access, instant answers, or cooperation. No additional Operational Tradecraft or False Identity feature is granted.
- **Equipment:** Work-appropriate or plain clothing, notebook or notes, ink, writing instrument, pouch, and **10 gp**. These become ordinary inventory; notes confer no guaranteed useful secrets or identity.
- **Identity:** The character's covert work need not have been unlawful, governmental, or tied to any present employer, Species, or Upbringing. Spy is not a subclass, variant, or inherited package of Criminal.
- **Integration pending:** The two-from-six native choice form (`chooseFrom` with `choiceCount: 2` and `distinctChoices: true`), competency choices, item materialization, and duplicate-grant handling await their general integration and corpus balance work.

**Criminal cleanup completed:** `data/rulesets/first-party-001/native/backgrounds/criminal.json` no longer embeds the Spy `suggestedVariants` entry or presents general intelligence work as an inherently criminal alternative. Criminal retains its approved five benefits and unchanged starting equipment. A Spy who actually participated in crime may still choose Criminal instead where that better describes the character's defining experience.

This section supersedes all earlier proposal, alternative, and approval-boundary language in this document. No special espionage subsystem or native Background inheritance was introduced.
