# CC SRD baselines — staged import

The authoritative source documents are **SRD 5.1 (2014-era fifth edition)** and **SRD 5.2.1 (2024-era revision)**, under **Creative Commons Attribution 4.0**. See [`manifest.json`](manifest.json) for the original document links and required attribution statements.

**Import status: NOT COMPLETE.** The checked-in `skills.index.json` files are discovery-only skill-name/default-Ability indexes derived from the upstream 5etools skills catalog's `srd` and `srd52` flags. They **are not** copies of, or a substitute for, the official SRDs, are **not authoritative native rules**, and should be checked against the official documents before use. The whole 5etools corpus is **not** automatically CC-licensed.

When implementing the importer, ingest the two exact official artifacts as separately fingerprinted, immutable source representations; extract and validate native-source entities with location/edition lineage; track modifications and precise attribution. Then convert applicable, verified content to the native ontology independently for each approved subsystem. Do not copy legacy source representations into `data/native/` as though they were native mechanics.

The third input to the native baseline is already-reviewed Dorks & Dice decisions from the previous Rules Core implementation and the rewrite documentation, not additional SRD publications.
