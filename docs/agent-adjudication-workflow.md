# Agent-assisted adjudication workflow

Rules Core exposes a structured adjudication queue for authenticated global Rules Lawyers. The queue is designed for both humans and an external Rules Lawyer agent. Rules Core does not host or invoke an LLM. It remains the authority for source access, deterministic compatibility policy, decision validation, provenance, and publication.

## Responsibility boundary

Rules Core owns:

- immutable Source Layer entities and revisions;
- canonical source recognition and Rules Layer concept bindings;
- deterministic semantic comparison and conservative automatic resolution;
- global Rules Layer decisions and their append-only provenance;
- campaign overrides and DM authority;
- adjudication work state, clarification history, escalation state, and workflow audit events;
- normal Rules Lawyer authorization and independent source grants;
- immutable global ruleset publication.

An external agent owns reasoning that is not provable by the deterministic Rules Core policy. It consumes the structured API under its own authenticated Dorks & Dice service identity. It receives only source material that identity is independently authorized to access.

The agent is not a Rules API, database client, model-specific extension, or privileged superuser. It creates real Rules Layer decisions only through the same validated decision workflow used by a human Rules Lawyer.

## Deterministic processing first

Discovery always gives existing deterministic machinery the first opportunity to resolve a rule. `RuleAutoResolutionService` remains conservative and can resolve only the cases it already proves safe, such as mechanically identical or verified non-destructive additive implementations.

If deterministic processing can resolve the case, the ordinary automatic-decision path is used and the work history records that result. The agent is not asked to reproduce deterministic comparison.

If deterministic processing requires adjudication, the work detail exposes the existing semantic comparison evidence and reason. A single-source concept may also appear because current policy still requires an explicit Rules Lawyer decision.

Normalization is different: the server may generate deterministic suggestions but unattended heuristics never accept them. An authenticated Rules Lawyer agent may deliberately inspect and explicitly accept an existing suggestion. That action is attributed to the service-principal actor in the same way as a human Rules Lawyer action.

## Work kinds

The current workflow represents three durable kinds:

- `normalization-review`: an accessible unbound source entity has an existing deterministic normalization candidate;
- `global-rule-adjudication`: a bound concept has no usable current global decision after deterministic processing;
- `source-update-review`: an existing global decision is pinned to an older source revision and the normal source-update workflow reports reviewable work.

The work item stores workflow metadata and stable references. It does not copy the authoritative selected rule, decision, source body, or publication truth.

## Lifecycle

```text
                    ┌───────────────┐
                    │    Pending    │
                    └───────┬───────┘
                            │ begin review
                            ▼
                    ┌───────────────┐
                    │  AgentReview  │
                    └───┬─────┬─────┘
                        │     │
       clarification ───┘     └── genuine edge case
            ▼                         ▼
┌────────────────────┐     ┌──────────────────────────┐
│  WaitingForHuman   │     │ ManualResolutionRequired │
└─────────┬──────────┘     └────────────┬─────────────┘
          │ answer                      │ normal human decision
          └──────────────► AgentReview  │ or deliberate reopen
                                        │
                         defer ──────────┴────► Deferred
                                                  │
                                                  └── reopen ─► Pending

A normal Rules Layer decision, accepted normalization binding, or completed
source-update action resolves the underlying condition and is observed as:

                            Completed

"Ready for publication" and "Published" are derived from immutable decision
and ruleset records. They are not independent workflow states.
```

## Human clarification

A Rules Lawyer agent may attach one focused question to a work item and transition it to `WaitingForHuman`. Rules Core persists the exact question, requesting actor, request timestamp, work-item version, human answer, answering actor, and answer timestamp.

The answer returns the work to agent review but is not itself a ruling. The agent or human must still use the normal Rules Layer decision workflow. The question and answer remain in append-only workflow history so review can resume with the same context.

## Manual escalation and defer

An agent can mark a genuine edge case as requiring manual resolution with a concise reason. A human opens the ordinary authoring workflow for the affected concept. If that human later creates a normal decision, the work item recognizes the authoritative decision automatically.

Manual escalation does not create a lock. The item can be deliberately reopened when appropriate. Deferred work likewise retains history and can be reopened.

## Publication

Decision creation and publication remain separate:

```text
process safe work
      ↓
save normal decisions
      ↓
explicit Rules Lawyer publication
      ↓
new immutable global ruleset revision
```

No queue transition automatically publishes. An authenticated human or agent service principal may perform the existing explicit publication action when its operating policy permits it.

A completed work item is reported as ready for publication while its latest decision is absent from the current published ruleset. Once that exact decision is present in an immutable ruleset revision, the workflow derives and reports the published state from the authoritative records.

## Audit and provenance

`rule_adjudication_work_event` is append-only. Consequential events record the stable Dorks & Dice actor identity when an actor exists, event kind, timestamp, work-item version, concise message, and stable rule/source/decision/publication references where applicable.

Restricted source contents are never copied into workflow events. The existing Rules Layer decision and publication records remain authoritative for what was selected and published.

## Authorization and source access

Every adjudication endpoint requires ordinary global Rules Lawyer authority. That authority does not grant source access.

Source grants remain independent:

- an agent sees exact source revisions only for public packages or packages granted to its own service identity;
- restricted bindings may be counted without returning restricted source contents;
- grants belonging to a human Rules Lawyer are never copied to an agent automatically;
- normalization candidates and source-update work remain filtered by the caller's source access.

## Concurrency and idempotency

Discovery uses stable work keys, so repeated discovery does not duplicate the same outstanding condition. Workflow mutations carry an expected work-item version and stale writes conflict.

Rule-adjudication detail also exposes the latest decision observed during review. The normal global decision request supports an optional expected-latest-decision guard. Browser-driven adjudication uses that guard, so a decision written by another actor after review began causes a conflict rather than a silent competing append.

Publication state is always re-read from immutable authoritative records.
