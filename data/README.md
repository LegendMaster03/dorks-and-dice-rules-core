# Runtime data

This directory documents deployment-owned Rules Core runtime storage. Imported source payloads, user uploads, cached external data, content-addressed blobs, and local databases must not be committed.

The repository `.gitignore` excludes the expected runtime subdirectories. Production storage should be mounted independently of application releases.
