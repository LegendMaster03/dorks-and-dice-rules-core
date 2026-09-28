# Persistence and authorization ownership

Rules Core owns the existing PostgreSQL database, source ingestion state, normalized
source records, provenance, source grants, Rules Layers, adjudication work, immutable
revisions, and publication state. Those records do not move to Rules Wiki.

Rules Wiki is stateless aside from ordinary process memory. It forwards authenticated
UI requests to Rules Core through Site delegation and relies on Rules Core for every
source-access and rules-authority decision.
