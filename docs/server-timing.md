# Rules Core Server-Timing — native mechanics

The current native host reports operation-level timings on supported authenticated mechanics endpoints using the standard HTTP `Server-Timing` response header. These values describe native calculation time measured within each handler; they are **not** whole-service or database timings.

Implementation: `src/RulesCore.Host/NativeMechanicsEndpoints.cs`. Each reported value uses the `metric;dur=<milliseconds>` form with invariant numeric formatting. Current metrics:

| Category | Metrics |
| --- | --- |
| Harvesting | `native-harvesting-catalog`, `native-harvesting-table`, `native-harvesting-outcome` |
| Crafting and starting inventory | `native-manufacturing`, `native-starting-equipment` |
| Equipment | `native-equipment-proficiencies`, `native-equipment-coverage`, `native-equipment-training`, `native-equipment-training-coverage` |
| Advancement and character | `native-advancement-eligibility`, `native-character-project` |
| Conditions, creatures, and resources | `native-condition-roll`, `native-creature-project`, `native-resource-resolve` |

The former `rules-core-db`, `rules-core-reference-*`, and whole-request middleware instrumentation belonged to the retired PostgreSQL/Source Layer service. They are **not** supplied by the current native host. Do not assume a native timing metric is present for failed validation, authentication rejection, or routes that do not call the timing helper.

The Site may report its own `platform-*` request/delegation timings; those metrics are owned by the Site, not Rules Core. Compare operation timings with outer Tool latency only after accounting for authentication and transport overhead.
