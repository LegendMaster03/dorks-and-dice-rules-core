# Rules Core source

- `RulesCore.Content/` — validates the versioned native ruleset package and publishes an immutable catalog snapshot.
- `RulesCore.Mechanics/` — evaluates supported native character, condition, resource, harvesting, crafting, advancement, and equipment mechanics.
- `RulesCore.Host/` — .NET 10 headless ASP.NET Core host, authentication boundaries, native reference APIs, and mechanics endpoints.

The retired source-normalization and `/api/rules` service is not a second runtime. `PublicOnly` and `PrivateOnly` are separate configured API surfaces. The active ruleset lives under `data/rulesets/first-party-001/native/`; `working/` and `reference/` do not serve gameplay rules.

See [current architecture](../docs/architecture.md), [native mechanics API](../docs/native-mechanics-api.md), and the [repository README](../README.md) for validation and deployment.
