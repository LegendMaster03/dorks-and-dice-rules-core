# Documentation map

The **authoritative rewrite design** is in [`rewrite/`](rewrite/). These files contain the current native-first foundation and subsystem decisions.

The remaining top-level documentation records behavior and design of the previous Rules Core implementation. It is retained for reference while the replacement is built; **it is not a mandate to preserve the .NET code, database schema, old HTTP endpoints, or legacy runtime architecture**.

The prior implementation remains available on the repository's `main` branch until the reviewed rewrite replaces it. Any migration of existing user-authored state, restricted source material, or published rules requires an explicit plan and authorization before execution.

Rules Core is a headless service. Rules Wiki owns the separately hosted human-facing presentation. This branch does not introduce a new runtime or enable access to the hidden service.
