# Dorks & Dice — Rules Core

Rules Core is the headless native rules-content and mechanics service for the
Dorks & Dice tabletop system. Rules Wiki is a separate user-facing application:
https://github.com/LegendMaster03/dorks-and-dice-rules-wiki.

## Architecture and development status

`main` contains the native catalog, private Wiki reference API, and native-first
consumer/mechanics contracts. The first-party ruleset remains under active
revision: implemented mechanics are available through those contracts, while
unsupported behavior must be reported as unresolved rather than guessed.
The retired legacy `/api/rules` service is not a second active runtime.

The initial ruleset identifier is first-party-001. The approved content source
is exclusively data/rulesets/first-party-001/native/. Content in working/ is
inert and never a fallback. Reference evidence and conversions are not loaded
as native rules. Controlled promotion into native/ establishes approval; the
runtime has no per-rule publication allowlist or approval detector.

The native package is an evolving collection of source-neutral JSON definitions
organized beneath `native/`. Its contents and counts should be inspected in the
repository rather than treated as a fixed phase milestone. Earlier phase
migration evidence is preserved separately. Native data updates do not require
recompilation of RulesCore.Content or RulesCore.Host.

## Manual deployment

Open **GitHub Actions → Deploy native Rules Core → Run workflow** on `main`.
The dialog has a single **What should be deployed?** dropdown:

- **Content update** (default): layer the latest `native/` JSON and package manifest onto
  the existing validated Rules Core image, without rebuilding C# or changing the
  runtime. It requires both Core instances to run the same trusted image and no
  Core executable/Docker configuration changes since that image's commit.
  If either condition fails, select **Full deploy**.
- **Full deploy**: rebuild the Rules Core application and its native content
  from the latest `main` commit.

Both choices automatically select the current `main` SHA, require a successful
`main` CI run for that exact commit, independently calculate the native catalog
fingerprint, and check the deployed image, both ingress fingerprints, readiness,
network restrictions and public-distribution denial. There are no SHA or review
reference text inputs. The GitHub actor and run identify the manual authorization.

Merging a PR does not deploy. A failed replacement is withdrawn, but the
previous image is not automatically restored. Both actions deploy **Rules Core
only**, not the other Dorks & Dice services.

## Catalog and host

RulesCore.Content is independent of ASP.NET Core and PostgreSQL. It validates
the manifest and native JSON package, rejects inconsistent content, and builds
a complete immutable snapshot with stable identity lookup, category indexes,
case-insensitive searches, deterministic pagination, and SHA-256 fingerprint.
The ASP.NET Core Minimal API host initializes this snapshot through dependency
injection.

GET /health reports process liveness and remains HTTP 200 when content is
unavailable. GET /ready is HTTP 200 only with a validated catalog, otherwise
HTTP 503 with a safe reason. PublicOnly maps the distribution-gated /api/rulesets reference API, the
Site-authenticated /api/consumer/v1/rulesets reference API, and authenticated
/api/mechanics/v1 evaluation. PrivateOnly maps authenticated
/api/private/rulesets and /api/private/mechanics/v1. Neither surface exposes
legacy /api/rules. The current first-party native package has no anonymous
public-distribution authorization.

Run from the repository root:

    dotnet restore RulesCore.slnx
    dotnet build RulesCore.slnx -c Release --no-restore
    dotnet test RulesCore.slnx -c Release --no-build
    python3 -m unittest discover -s tests -p 'test_*.py'
    python3 tools/validate_ruleset_split.py
    dotnet run --project src/RulesCore.Host

For an alternate working directory, configure Rulesets__ContentRoot to the
absolute path of data/rulesets. Docker embeds the manifest and native content,
but excludes working drafts and conversion/reference data.

See [the documentation index](docs/README.md) for the current contract map,
[architecture](docs/architecture.md) for content and API authority,
[native mechanics](docs/native-mechanics-api.md) for supported calculations,
and [private tunnel deployment](docs/private-tool-tunnel-deployment.md) for
network isolation. [Native rules design](docs/rewrite/native-rules-design-specification.md)
is a living game-design specification; a proposal in it is not necessarily an
implemented mechanic.
