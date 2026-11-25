# Structured bestiary promotion

The native foreign packages now contain all 323 pinned SRD 5.1 and 334 pinned SRD 5.2.1 top-level monster definitions, plus the modern Avatar of Death and Giant Fly recovered from the official publisher document (336 modern records). This is complete pinned top-level inventory coverage, not independently certified official bestiary completeness.

The batch adds 321 old and 238 modern definitions. Previously promoted records are retained unchanged. Run `node tools/promote_srd_bestiary.mjs --check` to reproduce the selected source projection and diagnostic reports. The SRD Edition Review Catalogs workflow independently checks the source pack hashes and native explicit reference integrity.

The generator copies stat-block fields, including original nested action and spellcasting markup, armor class, ability scores, movement, CR, conditional gear and summon scaling fields. It does not flatten special HP or ability formulas. It corrects the Lich's old Acid Arrow reference (official SRD 5.1 p. 326) and the two modern black dragons' Acid Arrow references (S52-015, pp. 264–265). The modern Avatar remains the previous publisher-backed definition. The equipment pass recovered the embedded modern Giant Fly (publisher p. 222), without inventing an attack or ordinary XP reward. No first-party data or behavior is changed.

All new records have a publisher name locator on the candidate page or an adjacent page, accounting for known shared headings and alternate names. Name occurrence is weaker than a verified stat-block heading and does not establish full paragraph, table, or numeric fidelity. Source candidate page hints and publisher locator status are recorded separately. The exact source occurrence pointers remain available for review.

The diagnostic reports `data/reference/srd/bestiary-promotion-51.json` and `bestiary-promotion-52.json` enumerate all retained/imported IDs, three corrected records, and pending markup targets. The 45 old and two modern previously unresolved inline item tag occurrences are now linked to unique edition-local concrete equipment identities. The original markup remains intact. Resolved local spell, condition, skill, and creature tags and publisher pages are linked. The existing animal and spell markup still requires comprehensive reconciliation.

Source-generated _versions arrays (15 old and four modern base records) are separately inventoried, not blindly materialized as official entities. External media, fluff, derived metadata, and non-verified variant expansions are excluded. In particular, summoning stat blocks retain source formulas and conditional actions, but variant expansion and official spell-specific details require review. Lair/regional effects and broader source coverage still require reconciliation.

The import is not authorization to publish. No production approval, downstream consumer acceptance, or full system mechanic implementation is implied.
