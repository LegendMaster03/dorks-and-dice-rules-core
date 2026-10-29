# First-party ruleset package

Runtime readers may load only `native/`. The inert `working/` tree retains staged source candidates, incomplete definitions and proposals; it must never serve as API data or fallback. Reviewed rules are moved to native and removed from working when final. This physical boundary replaces runtime approval detectors and per-rule allowlists. See `docs/rewrite/phase-1a-audit.md` for classification and the file/record ledger. Pinned licensing and provenance remain in `data/reference/`.
