# Tool Host integration

Rules Core is a backend delegation target. It continues to consume the standard
Tool Host authentication ticket and introspection path, validates the target-scoped
context, and derives user/global/campaign authority exactly as before the split.

Rules Core is not the browser Tool. Human-facing links emitted by Rules Core use
`toolSlug: rules-wiki` plus the stable tool-relative route. Rules Wiki owns the
Embedded Module lifecycle and converts that route into browser state.

Production registration of Rules Core is blocked until the Site supports an enabled,
health-checked, authenticatable, delegatable service registration that is not
navigable and does not require a public UI slug. Do not emulate this with a hidden
normal Tool registration.
