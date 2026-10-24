# Deploying FarmHand

## Required infrastructure and configuration

Use a host with Docker Compose, a domain pointing to that host, and inbound ports 80/443. `compose.yaml` supplies PostgreSQL, a migration job, the private API, the web server, and Caddy for HTTPS. Database and upload volumes are persistent. Only the web proxy and signature-verified payment webhook are publicly routed; PostgreSQL and the rest of the API are private.

Generate private configuration:

```sh
node scripts/configure.mjs
```

Edit `.env` and set `PUBLIC_HOST`, `BREVO_API_KEY`, and a verified `BREVO_FROM_ADDRESS`. The generated database password, JWT secret, and field-encryption key are unique. Never commit `.env`, send it in an issue, or rotate its encryption key without a data migration. Back up the field-encryption key with the database: losing it makes encrypted payout details unreadable.

Payments default to `Disabled`. To enable actual charges, set `PAYMENTS_MODE=Live`, `FLUTTERWAVE_SECRET_KEY`, and `FLUTTERWAVE_WEBHOOK_SECRET`. Register `https://YOUR_DOMAIN/api/payments/webhook` with Flutterwave. Caddy routes this endpoint directly to the private API, which checks signatures and independently verifies transactions, amount, and currency. Verify a real provider sandbox transaction before switching to live credentials.

Set Gemini/Groq and Spitch/transcription provider keys to enable AI and voice services. Unavailable production text extraction returns an explicit unavailable response; the manual reporting workflow remains available. Demo rule-based extraction is disabled in Production. Confirmation can still use explicitly labelled deterministic urgency rules, and approved care wording is server-controlled. This is preliminary, clinically unvalidated guidance, not a successful live model call. Production requires explicit `Storage:Provider=Local` and `Storage:LocalPath`; `Auto` and `Cloudinary` are rejected. The supplied stack mounts persistent `uploads` storage. Verify persistence and filesystem permissions on your actual host: a configured path alone does not prove either. Optional non-Production Cloudinary uploads use public delivery URLs; private cloud delivery remains unimplemented.

## Existing-database migration preflight

Back up the database and upload volume before applying new migrations. The buildathon release adds `IX_VetRequests_OneActivePerIncident`, a filtered unique index for non-deleted requests whose status is Pending (`0`) or Responded (`1`). A fresh database needs no data repair. On an existing database, run this read-only query before starting the migration service:

```sql
SELECT "IncidentId", COUNT(*) AS "ActiveRequestCount"
FROM "VetRequests"
WHERE "Status" IN (0, 1) AND "IsDeleted" = FALSE
GROUP BY "IncidentId"
HAVING COUNT(*) > 1;
```

If rows are returned, stop the release and have the data owner review the competing requests, assigned vets and payment reconciliation. Preserve clinical/payment history. Do not automatically delete records or invent a winning assignment. Resolve duplicates through an explicitly reviewed data change, rerun the query, then apply the migration. The migration fails with an explanatory error while duplicates remain; it does not deduplicate data. Run only one migration job per database.

## First deployment

```sh
docker compose config --quiet
docker compose up --build -d
docker compose ps
```

The one-off migration service completes before the API starts. The web server waits for database/schema readiness. Caddy obtains an HTTPS certificate for the configured domain. The API uses HTTP only inside the private Docker network; browser cookies retain the Secure flag.

Create the initial administrator once:

```sh
# Set these in your terminal using your secret manager, without putting them in shell history.
export AdminBootstrap__Email
export AdminBootstrap__Password
docker compose run --rm -e AdminBootstrap__Email -e AdminBootstrap__Password api --bootstrap-admin
```

On PowerShell, set the corresponding `$env:AdminBootstrap__Email` and `$env:AdminBootstrap__Password` variables before the same Docker command. The password must satisfy the API policy. Sign in and change the temporary password immediately. Administrator bootstrap refuses to run when an administrator already exists. Remove the bootstrap secrets from the shell afterward. Production does not seed demo accounts.

## Release and rollback

1. Require the `Verify FarmHand` workflow to pass and enable branch protection on the default branch. CI runs backend tests, frontend checks, production builds, dependency auditing, full-stack browser tests, and container builds.
2. Back up PostgreSQL and uploads before migrations. Test restoration on a separate database. Store encryption keys separately in a backed-up secret manager.
3. Deploy a specific reviewed commit. Run `docker compose up --build -d` and check `docker compose ps` and logs. Never run competing migration jobs against the same database.
4. Check `https://YOUR_DOMAIN/api/health` and the admin system-health page. Test registration, sign-in, password reset email delivery, uploading an observation, case confirmation, vet acceptance, treatment sharing, and provider checkout verification.
5. When rolling back, restore an application version compatible with the migrated schema. Do not automatically downgrade the database; restore the pre-release backup if the schema is incompatible.

## Operations

- Persist and back up both database and upload volumes. Do not use `docker compose down -v` on a production host unless the data is intentionally being destroyed.
- Monitor container health, API error rates, email delivery, AI-provider failures, and webhook verification. Logs omit credentials and raw account numbers; restrict access to logs and diagnostics.
- Readiness checks cover the database/schema, not real provider delivery. Integration configuration flags are not provider health probes. Define a real alert destination for repeated 503s, error-rate/latency changes, failed recovery email, provider errors and failed backups; the repository does not provision hosted alerting.
- The Compose stack uses one web replica. Token refresh coalescing is process-local; implement distributed refresh coordination before scaling the web service to multiple replicas.
- The private proxy network is `172.30.50.0/24` by default. Set `NETWORK_SUBNET` if it conflicts with your host network. Only that internal proxy network is trusted for forwarded client addresses. If another load balancer is added in front of Caddy, configure its trusted-proxy handling deliberately.
- Keep .NET, Node, PostgreSQL, container images, and locked dependencies patched. NuGet high/critical audit findings fail restore; npm production auditing runs in CI.
- Run a restore drill and the provider/email checks with your own infrastructure before a public launch. Passing local tests does not verify real provider credentials, DNS, certificates, backups, or payment settlement.

## Public deployment gate observed during buildathon review

At `2026-10-24 09:48 UTC`, `GET https://team-hack-forge.vercel.app/api/health` returned HTTP 503 with `status: unavailable`, while the landing page rendered. The public check did not reveal whether the cause was API configuration/reachability, database, pending migrations or another service condition. Inspect the authorized hosting environment's `FARMHAND_API_URL`, API readiness and private logs to determine the cause. Do not publish secrets or assume the reference Compose topology describes Vercel's backend. No deployment or production-data change is authorized by this observation. Recheck public and protected workflows after an explicitly authorized deployment fix.

See [buildathon verification](docs/buildathon/TEST_REPORT.md) for actual local, CI, benchmark and recovery evidence. For baseline `f3da02e`, GitHub Actions run `40077309842` did not start the full-stack job because the account was locked for billing; container checks were skipped. Resolve that account restriction or run equivalent checks on an authorized Docker-enabled host before calling the release verified.
