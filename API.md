# FarmHand HTTP API

The API targets .NET 10 and PostgreSQL. The web app calls it through the same-origin `/api/bff/*` proxy; bearer and refresh tokens stay in HttpOnly cookies.

## Authoritative contract

In Development, inspect `/swagger` and `/swagger/v1/swagger.json` on the API (normally `http://localhost:5037`). Controller DTOs and the generated OpenAPI document are authoritative. Web response schemas are in `web/src/lib/api/schemas.ts` and are validated at runtime.

## Authentication and authorization

- `POST /api/auth/register` creates a Farmer account only.
- `POST /api/auth/login` issues access and refresh tokens to trusted API clients. The web app uses `/api/session/login`, which stores tokens in cookies and returns no tokens to browser JavaScript.
- `POST /api/auth/refresh` rotates a refresh token. Reuse detection revokes compromised sessions.
- `POST /api/auth/logout` revokes that refresh token. `POST /api/auth/revoke` revokes every session and increments the user's token version.
- `GET /api/auth/me` returns profile, farms, and vet profile; `PUT /api/auth/me` updates contact details.
- `POST /api/auth/change-password`, `/forgot-password`, and `/reset-password` implement password recovery/change. Temporary credentials must be changed before other clinical/admin endpoints are accessible.

Every protected API verifies role and resource access. Farmers access their own farms; vets access accepted work; administrators have platform oversight. An identifier alone does not grant access. State-changing web requests need `x-farmhand-csrf: 1` and a trusted Origin. Credential endpoints have a separate rate budget from authenticated profile reads.

## Main workflows

| Area | Routes |
| --- | --- |
| Farms | `GET/POST /api/farms`, `GET/PUT /api/farms/{id}`, `GET /api/farms/{id}/overview` |
| Livestock | `/api/livestock/categories`, `/api/livestock/categories/details`, `/api/livestock/animals`, animal details, photo, archive and restore |
| Reports | `POST/GET /api/observations`, `GET/DELETE /api/observations/{id}`, `POST /api/observations/{id}/confirm` |
| Case files | `GET /api/incidents`, `GET /api/incidents/{id}/case`, case status, actions, follow-ups and prevention plans |
| Vet directory | `GET /api/VetCoordination/vets` |
| Vet requests | `GET /api/VetCoordination/vet-requests`, `GET /api/VetCoordination/vet-requests/{id}`, create from an incident, respond, decline, cancel and complete |
| Treatment | `/api/treatment-plans`, `/api/treatment-plans/{id}/steps/{stepId}/complete` |
| Payout account | `GET/PUT /api/vet/me/payout-account` |
| Payments | `/api/payments`, `/api/payments/quote`, `/initiate`, `/verify`, reference lookup and cancellation |
| Admin | `/api/admin/overview`, `/users`, `/vets`, `/farms`, `/incidents`, `/payments`, `/system` |
| Audit | `/api/audit-events` (Admin only) |
| Status | `/api/system/info`, `/health`, `/health/ready` |

See OpenAPI for exact method, parameters, validation, and response shapes for each operation. Paged lists return `items`, `page`, `pageSize`, `totalCount`, and `totalPages`. Enums are serialized as integers; the frontend's enum module supplies their labels.

### Reporting and veterinary care

1. Submit an observation using text, audio, image, or explicit manual entry. Multipart uploads are bounded and file signatures are checked.
2. Review the extracted fields and labelled assessment source. AI suggestions are not a diagnosis.
3. Confirm the report. High/critical observations can create an incident automatically.
4. Create a vet request from the case file. Open requests hide farmer contact details.
5. A vet accepts atomically; competing acceptance attempts cannot assign two vets. Accepted vets receive relevant farmer contact details.
6. If there is a fee, the farmer pays after acceptance. A vet shares a treatment plan and completes the consultation after verified payment, or immediately when there is no fee.
7. The farmer records follow-ups and completes treatment steps in the case timeline.

### Payments

`GET /api/payments/quote?vetRequestId=...` returns the authoritative fee, fixed platform commission, currency, mode and payability. `POST /api/payments/initiate` takes the request ID, fee and currency; an `Idempotency-Key` header makes retries safe. Amount/currency tampering is rejected. Live checkout returns an HTTPS provider URL.

A checkout redirect does not prove payment. `POST /api/payments/verify` asks the server to verify the merchant reference with the provider. The webhook at `/api/payments/webhook` checks signatures and independently re-verifies provider data. Merchant references are preserved separately from provider transaction IDs. Pending checkouts can be cancelled; completed payments cannot.

Modes are `Disabled`, `Simulated`, and `Live`. Simulated references are HMAC-signed and clearly labelled; no money moves. Production configuration rejects simulated payments. Payout account numbers are AES-GCM encrypted and exposed only as masked values.

## Errors and availability

Failures use problem-style JSON with a status, title, optional detail/code, validation errors, and trace ID. Typical statuses are 400 (invalid input), 401 (session), 403 (access), 404 (missing resource), 409 (state conflict), 413 (upload limit), 429 (rate limit), and 503 (unavailable integration). Respect `Retry-After` where supplied. The proxy retains session cookies when a refresh fails transiently rather than treating a server outage as a bad password.

`/health` is liveness. `/health/ready` checks database connectivity and pending migrations. `/api/health` on the web server checks API readiness. These endpoints disclose no credentials or account data.

## Configuration and deployment

See `src/FarmHand.API/appsettings.example.json`, `web/.env.example`, `README.md`, and `DEPLOYMENT.md`. Production requires explicit host filtering, an independent field-encryption key, recovery email delivery, a public HTTPS URL, and persistent upload storage. Provider keys are supplied through the deployment's secret configuration. Demo data and rule-based demo extraction are not enabled in Production.