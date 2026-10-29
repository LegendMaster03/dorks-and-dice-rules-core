# Development utilities

Reserved for language-appropriate generators, validators, import/conversion utilities, and development tooling when the native foundation and implementation language are selected.

Do not import restricted source corpora, credentials, database dumps, or user data into the repository.

Phase 1A: `audit_native_baseline.py` defaults to the authoritative `data/rulesets/first-party-001/native` tree. Offline reconciliation scripts operate on the inert `working` corpus. CI runs both strict native and non-operative working checks.
