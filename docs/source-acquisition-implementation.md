# Source acquisition implementation notes

The initial acquisition slice uses idempotent PostgreSQL DDL owned by `SourceAcquisitionService`. It intentionally avoids coupling acquisition history to the source grant authorization path. A later persistence refactor may move the schema into the central EF-backed initializer while retaining the current API and security semantics.
