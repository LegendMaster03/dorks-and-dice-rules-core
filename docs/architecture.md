# Rules Core architecture

## Purpose

Rules Core is a separately deployable Tool. It owns rules content, source normalization, Rules Layer decisions, source-access grants, ruleset revisions, and rule resolution. The main Dorks & Dice site owns account identity, global site roles, campaign membership/authority, site-mode resolution, and Tool registration/routing.

## Content pipeline

```text
immutable source material
        |
        v
normalized 5e.tools-compatible entities
        |
        v
content identity / revision registry
        |
        v
Rules Layer adjudication
        |
        v
published Dorks & Dice ruleset revision
        |
        v
campaign Rules Layer overrides
        |
        v
resolved rules API
```

The internal rule-document format is a lossless 5e.tools-compatible superset. Dorks & Dice-specific rule semantics should be namespaced rather than replacing upstream fields. Application state such as permissions, grants, adjudication workflow, and campaign overrides remains relational data rather than being embedded into source documents.

## Source and access boundaries

Open/distributable sources can be built into Rules Core where their licenses permit it. Non-open material enters at a user's direction through supported connections or imports. Storage may be content-addressed and globally deduplicated, but access grants remain per user.

Core invariant:

> Deduplication may share storage. It must never share permission.

Content identity is independent of acquisition method. A PDF, 5e.tools record, structured JSON import, or other supported representation can resolve to the same source work/entity/revision without requiring repeated Rules Lawyer adjudication.

## Rules Layers

1. **Source Layer** - immutable representations of what each source says.
2. **Global Rules Layer** - Dorks & Dice decisions curated by users with the Dorks-mode `Rules Lawyer` authority.
3. **Campaign Rules Layer** - campaign-specific decisions/overrides controlled by the campaign's authorized DM/editor.
4. **Resolved Rules** - generated effective rules for a user/campaign/source-access context.

Global and campaign edit authority is separate from source-content access. A Rules Lawyer may be able to adjudicate metadata about an implementation without automatically gaining access to restricted source text.

## Authorization axes

Rules Core must answer two independent questions for each relevant operation:

- **May this identity make this change?** Global Rules Lawyer authority or campaign-scoped authority comes from Dorks & Dice.
- **May this identity access this source content?** Source grants are enforced by Rules Core.

UI visibility is not an authorization boundary. API endpoints must enforce both requirements independently.

## Database and runtime storage

Rules Core uses an external PostgreSQL database running independently of the application container, normally on the same TrueNAS server. Large/raw source artifacts and cache payloads may use deployment-owned content-addressed storage outside Git while PostgreSQL stores relational metadata, fingerprints, permissions, revisions, and normalized JSONB entities.

## First implementation milestone

The first vertical slice will prove:

1. source package registration;
2. one lossless 5e.tools-shaped entity;
3. stable content identity/fingerprint;
4. a Rules Layer decision;
5. a published ruleset revision;
6. resolved API output with provenance;
7. global/campaign authorization hooks;
8. per-user source-access enforcement.

The first real source corpus remains intentionally limited to representative SRD abilities, skills, and sizes before broader ingestion.
