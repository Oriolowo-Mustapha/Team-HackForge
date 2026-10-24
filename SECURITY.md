# Security boundaries

- The API verifies JWT signatures, token versions, account activation, resource ownership, and role permissions. The web routing proxy is only a navigation aid.
- Access and refresh tokens stay in HttpOnly cookies. Mutations require the FarmHand CSRF header and an origin/host check. Production cookies require HTTPS.
- Refresh tokens are stored as hashes and rotated on use. Password changes, administrative deactivation, and global revocation invalidate sessions. The supplied deployment uses one web replica; distributed refresh coordination is required for multiple replicas.
- Open vet requests hide farmer contacts. The assigned vet receives contacts after acceptance. Audit records and cross-platform administration require the Admin role.
- Payment completion requires server-side provider verification and matching amounts/currency. Webhook signatures are checked. Simulated payments are clearly labelled and rejected in Production configuration.
- Uploads have size and file-signature checks. The frontend proxy enforces its limit while streaming the request body. Private API responses and authenticated HTML are not cached by the service worker.
- Payout account numbers are encrypted with AES-256-GCM. Back up the independent encryption key and manage key rotation as a data migration.
- Production refuses missing recovery email configuration, demo settings, placeholder keys, and wildcard hosts. Do not expose the private API/database directly to the internet.

Report a suspected vulnerability privately to the repository owner rather than posting credentials, exploit details, or farm records in a public issue. This project has not received an independent penetration test.
