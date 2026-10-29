# First native rewrite deployment

Owner authorization: merge the rewrite into main, deploy and monitor it, and migrate consumers only after successful acceptance.

A successful main Phase 3B CI run triggers deployment of its exact source SHA. The self-hosted release rejects stale releases, unsafe networks, missing Site private-tunnel permission, name ownership conflicts, host ports, data mounts and public-distribution configuration before changing services. Both ingress containers must use the same commit-tagged image and actual image ID.

The reviewed native fingerprint is 870ca5e89a97b9176f0a3b55fb6cb9467227d13562a83dd7d72ecee5a57ecda6. Compose does not create a database, remove orphans or delete volumes. Failed post-start verification stops only the new image; it never restores the dormant original runtime.

The 60-second observation verifies readiness, restart counts, surface isolation and closed anonymous distribution. Its success is infrastructure evidence only. Native Wiki deployment and genuine Site-ticket/browser acceptance must follow. Consumer migration remains gated on that end-to-end acceptance.

Required existing prerequisites: the Core host .env under /mnt/HDDs/www/dorks-and-dice-rules-core, configured pair/control-plane networks, Site attached to backend/control plane, an effective environment grant from rules-wiki to rules-core, and a runner labeled dorks-and-dice-rules-core. If policy is supplied through a different provider, inspect and explicitly accommodate it before deployment; do not infer authorization from network membership.
