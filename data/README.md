# Repository-authored rules data

Unlike the previous implementation, root `data/` is for **version-controlled native rules and approved conversion/reference definitions**, inspired by 5etools' category organization but not its monolithic data files or frontend architecture.

- `native/`: native rule entities, organized by mechanics category and subtype
- `conversions/`: reviewed mappings from imported source formats/editions to native entities
- `schemas/`: data validation contracts
- `reference/`: only explicitly approved/licensed reference material

**Runtime data is not kept here.** Imported user materials, source grants, source blobs, uploads, caches, and database files belong in deployment-owned volumes outside the repository. No entitlement or license is inferred from the presence of a mapping.
