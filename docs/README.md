# Rules Core documentation map

Rules Core's **native content and mechanics implementation is active on `main`**. The retired resolved-rule service and its `/api/rules` routes are not parallel runtimes. Source-neutral rules are loaded only from `data/rulesets/first-party-001/native/`; `working/`, `reference/`, and conversion material are not a runtime fallback.

## Current implementation and operations

- [Root README](../README.md) — current catalog layout, development commands, separate ingresses, and manual deployment procedure.
- [Authenticated native consumer catalog](native-authenticated-consumer-catalog.md) — ordinary signed-in catalog access and ruleset fingerprints, which identify a version rather than grant access.
- [Native catalog implementation](../src/RulesCore.Host/NativePublicEndpoints.cs) — anonymous distribution-gated `/api/rulesets` and authenticated `/api/consumer/v1/rulesets` reads.
- [Private reference implementation](../src/RulesCore.Host/NativeReferenceEndpoints.cs) — `/api/private/rulesets` reads for the pair-specific Rules Wiki private tunnel.
- [Native mechanics implementation](../src/RulesCore.Host/NativeMechanicsEndpoints.cs) — authenticated `/api/mechanics/v1` operations on the shared ingress, and `/api/private/mechanics/v1` on the private ingress.
- [API surface configuration](../src/RulesCore.Host/RulesCoreSettings.cs) — only `PublicOnly` (default) and `PrivateOnly` are valid.

Rules Wiki is a separately deployed frontend and communicates with the private Rules Core ingress over the source/target-specific private tunnel. The Site supplies the identity and ticket-exchange control plane. Other authorized tools consume the appropriate native consumer/mechanics contract. Native content can evolve without recompiling the host, subject to validation and the deployment workflow.

## Historical design records

- [API boundaries](api-boundaries.md) records both enduring security principles and a **retired resolved-rule API migration design**. Its `Compatibility`, `Combined`, `/api/rules`, and older source/authoring endpoints are **not current implementation instructions**.
- [Rewrite plans](rewrite/) preserve earlier requirements, proposed structures, reconciliation decisions, and phase checkpoints. Historical claims that the rewrite is unmerged or waiting to replace `main` have been superseded.
- Other top-level documents may describe the previous source-normalization, publication, persistence, or Rules Lawyer runtime. Consult the current native code and tests before applying them as present-day contracts.

Do not restore an old endpoint or schema merely because it appears in historical documentation. Any change to restricted content, authored decisions, distribution policy, or actual persisted data requires its own technical and authorization review; this documentation map does not approve migration or deployment.
