# FarmHand HTTP API

The API targets .NET 10 and PostgreSQL. Browser requests use the same-origin `/api/bff/*` proxy; access and refresh tokens stay in HttpOnly cookies managed by `/api/session/*`.

## Authoritative contract

In Development, inspect `/swagger` and `/swagger/v1/swagger.json` on the API (normally `http://localhost:5037`). Controller DTOs and generated OpenAPI are authoritative for exact methods, fields and validation. Web response schemas are runtime-validated in `web/src/lib/api/schemas.ts`.

This guide describes the current implementation. [Buildathon verification](docs/buildathon/TEST_REPORT.md) separates source review, local tests, simulation and unverified provider/deployment behavior.

## Authentication and authorization

- `POST /api/auth/register` creates a Farmer only. Privileged accounts require an administrator.
- `POST /api/auth/login` issues tokens to trusted API clients. The browser uses `/api/session/login`, which sets cookies and returns no bearer/refresh tokens to JavaScript.
- `POST /api/auth/refresh` rotates a hashed refresh token; reuse detection revokes compromised sessions.
- `POST /api/auth/logout` revokes that refresh token. `/api/auth/revoke` revokes all sessions and increments the token version.
- `GET/PUT /api/auth/me` reads/updates the current profile. Password change, forgotten-password and reset endpoints implement recovery.
- Temporary-password accounts must change the password before using clinical/admin operations. Deactivated users and stale token versions are rejected server-side.

Farmers access their owned resources; vets access eligible request summaries and accepted case work; administrators have platform oversight. Open requests hide farmer contacts. Audit endpoints require Admin. IDs and hidden UI buttons do not confer permission. Local media also requires owning-farm or assigned-case access. Production rejects Auto/Cloudinary storage and requires explicit Local storage/path with an operator-verified persistent mount; optional non-Production Cloudinary URLs remain public.

State-changing web requests require `x-farmhand-csrf: 1`; a supplied Origin must match the request host. Production cookies require HTTPS. The private bearer API has its own JWT/resource checks.

Web `POST /api/session/logout` returns `signedOut` and `everywhereSignedOut`. A completed response clears this device's cookies even if upstream API revocation is unavailable; global revocation is confirmed only when the API revoke call succeeds. A browser network failure before receiving that response cannot clear HttpOnly cookies, so the UI keeps the session and reports sign-out failure. Draft reads/lists are owner-scoped even if local cleanup fails; private BFF responses including media remain `no-store`.

## Main routes

| Area | Routes |
|---|---|
| Farms | `GET/POST /api/farms`, `GET/PUT /api/farms/{id}`, `GET /api/farms/{id}/overview` |
| Livestock | `/api/livestock/categories`, category details, `/api/livestock/animals`, animal details/photo/archive/restore |
| Reports | `POST/GET /api/observations`, `GET/DELETE /api/observations/{id}`, `POST /api/observations/{id}/confirm` |
| Case files | `GET /api/incidents`, `GET /api/incidents/{id}/case`, status/actions/follow-ups/prevention plans |
| Vet directory | `GET /api/VetCoordination/vets` |
| Vet requests | `/api/VetCoordination/vet-requests`, request details, create from incident, respond/decline/cancel/complete |
| Treatment | `/api/treatment-plans`, plan details, `/api/treatment-plans/{id}/steps/{stepId}/complete` |
| Speech | `POST /api/Voice/respond`, `GET /api/Voice/voices`; optional configured providers |
| Payout account | `GET/PUT /api/vet/me/payout-account` |
| Payments | `/api/payments`, `/quote`, `/initiate`, `/verify`, reference lookup/cancellation and signed `/webhook` |
| Admin | `/api/admin/overview`, `/users`, `/vets`, `/farms`, `/incidents`, `/payments`, `/system` |
| Audit | `/api/audit-events` (Admin only) |
| Status | `/api/system/info`, `/health`, `/health/ready` |

Routes are relative to the API origin. Browser callers use, for example, `/api/bff/observations` and `/api/bff/Voice/respond`. JSON uses camelCase, UUID IDs and UTC timestamps. Enums normally serialize as integers; use the frontend enum module/OpenAPI. Paged results contain `items`, `page`, `pageSize`, `totalCount` and `totalPages`. Some directories/subject lists remain unpaged; do not assume every array is a page. A successful 204 response has no JSON body.

## Reporting and veterinary care

1. `POST /api/observations` accepts multipart form fields: `farmId`, optional animal/category ID, `text`, `audio`, `image`, `manualEntry`, explicit deaths/feed/water/eggs, and an `idempotencyKey` (or `Idempotency-Key` header).
2. Reusing a valid key returns the existing report. Review/correct extracted fields before confirmation. Farmer wording is retained within field limits; provider extraction/vision is constrained to approved categories/labels with server clarification/uncertainty wording. No model-written translation silently replaces the farmer's notes. Unavailable extraction returns an explicit error; `manualEntry=true` permits deliberate reporting without model extraction.
3. Confirmation applies qualified server-controlled urgency/care wording and a deterministic urgency minimum. Farmer overrides can increase urgency, not reduce that minimum. High/critical confirmation opens a case. Report, assessment and required case are saved consistently; repeated confirmation does not create another case.
4. Assessment source is visible. Production disables demo extraction but confirmation may still use labelled deterministic rules. These are preliminary, unvalidated urgency rules, not a confirmed disease diagnosis or medication prescription.
5. Report-language metadata recognizes English, Yoruba, Hausa, Igbo and Nigerian Pidgin. Current safe response wording uses explicitly labelled English fallback where localized wording is unreviewed; optional TTS uses the response language. Translation/clinical quality and real speech-provider behavior are unverified.
   Saved assessment/photo/extraction output is constrained during DTO projection as well as new writes. Historical automated assessment prose is not replayed as current care guidance; original records, farmer wording and professional plans are not overwritten.
6. Owner requests veterinary help. An eligible vet accepts atomically; one active request per incident is enforced by the database. Accepted vets receive authorized case/contact context.
7. The farmer pays after acceptance where a fee applies. Assigned vet shares a treatment plan; paid consultation completion requires verified payment. Farmer reads instructions and records follow-up/step completion in the case timeline.

## Uploads and limits

Declared MIME type is not trusted. Server validation recognizes supported image/audio signatures and caps images at 8 MiB and audio at 10 MiB. The BFF/Kestrel transport cap is 25 MiB; observation submission additionally caps its multipart request at 20 MiB. Individual endpoint/read limits may be tighter. Keep uploads below the smallest applicable limit. JPEG/PNG/WebP/HEIC images and WebM/Ogg/MP4/M4A/MP3/WAV audio are supported by signature checks, with provider/device compatibility still conditional.

## Payments

Use `GET /api/payments/quote?vetRequestId=...` for the authoritative fee, commission, total, currency, mode and `canPay`. New requests currently use NGN 20,000 plus fixed 5% commission; historical stored requests may differ.

`POST /api/payments/initiate` accepts request ID, matching fee/currency and optional `Idempotency-Key`. Tampered fee/currency/commission values are rejected. Matching retries return the existing pending payment; only one pending checkout is permitted. Payment is initiated after vet acceptance. Live mode returns a provider HTTPS checkout URL.

A redirect does not prove payment. `/api/payments/verify` asks the server to verify the merchant reference with the provider. The webhook checks a configured signature and independently verifies transaction status, amount and currency. Merchant references remain separate from provider transaction IDs. Completed payments cannot be cancelled; pending live transactions must be reconciled before abandoning their vet request.

Modes are `Disabled`, `Simulated` and `Live`; invalid modes fail configuration validation. Simulated references are HMAC-signed, clearly labelled and rejected in Production. No money moves in a simulated test. Payout account numbers use AES-GCM encryption and are returned masked. Real sandbox/settlement behavior is unverified until recorded.

## Errors, rate limits and readiness

Central failures use problem-style JSON with status, title, optional detail/code/validation errors and trace ID. Some controller validation/auth responses use `{ error: ... }`; clients should support both. Typical statuses: 400 invalid input, 401 session, 403 access, 404 missing/hidden resource, 409 state conflict, 413 transport size, 429 rate limit, 502/503 unavailable upstream. File validation may return 400. Never interpret an unavailable provider as a fabricated successful result.

Current in-process budgets: credential endpoints 20/IP/minute; session/profile policy 120/IP/minute; AI policy 30/user/10 minutes; payments policy 30/user/minute; global budget 600/user/minute (IP fallback when anonymous). The 429 response is problem JSON, with `Retry-After` when supplied. Health endpoints bypass the limiter. These budgets are not distributed across replicas.

`/health` is process liveness. `/health/ready` checks database connectivity and pending migrations. Web `/api/health` forwards readiness. Provider configuration flags in `/api/system/info` do not prove delivery or external service health. A transient refresh outage returns unavailable without treating the saved cookies as an invalid password.

## Deployment and verification

See `src/FarmHand.API/appsettings.example.json`, `web/.env.example`, [README.md](README.md), [DEPLOYMENT.md](DEPLOYMENT.md), and [buildathon security report](docs/buildathon/SECURITY_REPORT.md). Keep API/database private in the reference stack. Run migration duplicate preflight before applying the unique active-request index. Test configured email, model/speech and payment providers separately; source definitions and simulation do not verify production delivery.
