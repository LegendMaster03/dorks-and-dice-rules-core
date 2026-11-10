# Rules Core CI and evolving native content

The authoritative native JSON is under active rules design. Schema decisions should not become mandatory tests before the mechanics are approved, and a content revision should not require a new workflow.

## Automatic checks

| Workflow | Trigger | Scope |
| --- | --- | --- |
| `validate.yml` | Pull requests targeting `main`, pushes to `main` | Parse JSON under `data/`. Does not assert a particular competency schema, content inventory, or advancement model. |
| `phase3b-native-public-api-ci.yml` | Pull requests targeting `main`, pushes to `main` | Build and test the executable Rules Core application, including native loader, API and container behavior. Its filename and job identity remain stable because deployment checks refer to it. |

Neither workflow automatically runs on every design branch push. The integration workflow must be brought into alignment with an approved new content schema before that content is merged for deployment. A draft JSON file being syntactically valid does not establish runtime compatibility.

## Manual checks and operations

| Workflow | Purpose |
| --- | --- |
| `content-native-catalog-digest.yml` | On-demand native loader inventory and fingerprint diagnostic; content counts are informational, not a frozen inventory. |
| `rewrite-host-preflight.yml` | Read-only host inspection when troubleshooting deployment. |
| `deploy.yml` | Explicitly selected content-only update or full deployment from `main`; requires successful `phase3b-native-public-api-ci.yml` push validation for the exact release commit. |

The retired Phase 1/2/3A, old native-mechanics feature, and one-off Weretiger adaptation workflows are not needed as parallel validation jobs. Their useful runtime and content coverage belongs in the maintained integration suite when applicable.

## Design boundary

Competency definitions are **single capabilities with proficiency and level**, but the current field structure and progression mechanics remain provisional. During reconciliation, review JSON changes for readability, valid identities, and deliberate parent/child links without adding hard-coded schema tests or modifying the runtime to follow every exploratory edit. Once the design is approved, update the consumer and add tests for behavior that must remain stable.

The distinction is between experimentation on a branch and verifying deployable integration on `main`, not between tested code and untested production releases.
