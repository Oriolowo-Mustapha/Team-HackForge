## Result

FarmHand can now run as a complete local stack. Sign-in reaches the real API and database, and farmer, vet, payment, profile, and administrator workflows have working screens.

## Changes

- Complete vet requests, treatment/payout controls, checkout and payment history, profiles/farm editing, and all admin navigation routes.
- Align frontend/API ports, add bounded request streaming and service timeouts, preserve sessions during transient refresh failures, and distinguish service outages from an offline browser.
- Fix authentication rate limiting so profile reads do not exhaust the credential budget. Make password-reset consumption concurrency-safe.
- Move to patched .NET 10 LTS packages and lock NuGet/npm dependencies. Add explicit production configuration validation and one-time administrator bootstrap.
- Add private-data-safe offline navigation, readiness probes, persistent uploads, HTTPS/reverse-proxy and verified-webhook routing, development/production Compose stacks, container builds, and CI.
- Replace stale API/deployment guidance with current setup, security, backup, recovery, and release instructions.

## Local verification

- 40 backend tests passed, including a real PostgreSQL reset-token race regression.
- 3 request-body limit tests passed.
- 9 Playwright tests passed against the real .NET API/PostgreSQL stack: all role navigation, registration/farm setup, cross-farm access, CSRF, competing acceptance, amount tampering, payment UI and idempotency, treatment plans, onboarding/deactivation, offline loading, and phone layout.
- TypeScript, ESLint, and production web build passed. npm production audit reports zero known vulnerabilities.

## Deployment requirements

Production intentionally refuses demo accounts, simulated payments, weak/missing keys, wildcard hosts, and missing recovery-email configuration. Supply the real domain and provider credentials, verify email delivery and provider settlement, and perform a backup/restore drill in the target environment before public launch. The supplied deployment uses one web replica; distributed refresh coordination is needed before horizontal scaling.