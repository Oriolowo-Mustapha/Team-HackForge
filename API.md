# FarmHand API Reference

**Version:** v1 (unversioned routes, see [Versioning](#versioning))
**Base URL (local):** `http://localhost:5037` or `https://localhost:7215`
**Content type:** `application/json` except the two multipart upload endpoints
**Last updated:** 2026-10-21

> **Documentation status.** This document describes the API as implemented, including known gaps. Swagger UI (`/swagger`) is enabled only in the Development environment and includes the same endpoint contracts with JWT authorization support.

---

## Contents

1. [Quick start](#quick-start)
2. [Authentication](#authentication)
3. [Conventions](#conventions)
4. [Enumerations](#enumerations)
5. [Endpoint summary](#endpoint-summary)
6. [Farms](#farms)
7. [Incidents](#incidents)
8. [Poultry observations](#poultry-observations)
9. [Voice and image check-ins](#voice-and-image-check-ins)
10. [Vet coordination](#vet-coordination)
11. [Payments](#payments)
12. [Goats / livestock](#goats--livestock)
13. [Health](#health)
14. [Data models](#data-models)
15. [Error handling](#error-handling)
16. [Versioning](#versioning)
17. [Known limitations](#known-limitations)

---

## Quick start

```powershell
# 1. Create local configuration
Copy-Item src/FarmHand.API/appsettings.example.json src/FarmHand.API/appsettings.json

# 2. Fill in your keys, then apply the database migration (PostgreSQL must be running)
dotnet ef database update --project src/FarmHand.Infrastructure --startup-project src/FarmHand.API

# 3. Run the API
dotnet run --project src/FarmHand.API --launch-profile https
```

| Resource | URL |
|---|---|
| Swagger UI | `https://localhost:7215/swagger` |
| OpenAPI JSON | `https://localhost:7215/swagger/v1/swagger.json` |
| Health check | `https://localhost:7215/health` |

> The repository currently targets `net10.0`. Install the .NET 10 SDK or retarget the solution before running. See `CODE_AUDIT.md` for the full list of blockers.

---

## Authentication

All controllers are decorated with `[Authorize]`. Only `GET /health` is anonymous.

```
Authorization: Bearer <jwt>
```

Tokens must be signed with HS256 using the configured `Jwt:SecretKey`, and must match `Jwt:Issuer` (`FarmHand`) and `Jwt:Audience` (`FarmHandUsers`). Tokens are validated for issuer, audience, lifetime, and signing key.

**In Swagger UI:** click **Authorize**, paste the raw token (Swagger adds the `Bearer ` prefix).

> **Gap:** there is no login or token-issuance endpoint yet. `IJwtService.GenerateToken` exists but is not exposed by any route, so the documented endpoints cannot be exercised against a real token until an auth endpoint is added. Do not use the fallback key committed in source; see `CODE_AUDIT.md` C4–C5.

| Condition | Response |
|---|---|
| No `Authorization` header | `401 Unauthorized` |
| Invalid signature, wrong issuer/audience | `401 Unauthorized` |
| Expired token | `401 Unauthorized` |
| Valid token, insufficient ownership/role | No checks exist yet — any authenticated caller is allowed |

---

## Conventions

- **JSON casing:** camelCase (`System.Text.Json` web defaults).
- **Ids:** UUID strings, e.g. `"3fa85f64-5717-4562-b3fc-2c963f66afa6"`.
- **Timestamps:** ISO 8601 UTC, e.g. `"2026-10-21T14:05:00Z"`.
- **Enums:** serialized and accepted as **integers** (no `JsonStringEnumConverter` is configured). Use the tables in [Enumerations](#enumerations).
- **Nullability:** nullable fields are omitted or `null`.
- **Route casing:** paths below are lowercase; ASP.NET Core routing is case-insensitive.
- **Correlation:** no request-id header is emitted.

---

## Enumerations

Enums are numeric on the wire. The tables below map number → name.

### Severity

| Value | Name |
|---|---|
| 0 | Low |
| 1 | Medium |
| 2 | High |
| 3 | Critical |

### IncidentStatus

| Value | Name |
|---|---|
| 0 | Open |
| 1 | Acknowledged |
| 2 | UnderMonitoring |
| 3 | FollowUpRequired |
| 4 | Resolved |
| 5 | Reopened |

### RequestType

| Value | Name |
|---|---|
| 0 | RemoteReview |
| 1 | SharedVisit |
| 2 | CooperativePlan |

### RequestStatus

| Value | Name |
|---|---|
| 0 | Pending |
| 1 | Responded |
| 2 | Completed |

### ObservationConfirmationStatus

| Value | Name |
|---|---|
| 0 | Pending |
| 1 | Confirmed |
| 2 | Corrected |

### Outcome (not exposed by any endpoint)

| Value | Name |
|---|---|
| 0 | Improving |
| 1 | Worsening |
| 2 | Resolved |

---

## Endpoint summary

| Method | Path | Purpose | Success |
|---|---|---|---|
| POST | `/api/farms` | Create a farm | 201 |
| GET | `/api/farms/{farmId}/dashboard` | Farm dashboard | 200 |
| POST | `/api/incidents` | Create an incident | 201 |
| GET | `/api/incidents/{farmId}` | List a farm's incidents | 200 |
| POST | `/api/incidents/{incidentId}/status` | Change incident status | 200 |
| POST | `/api/poultry/observations` | Record text observation | 202 |
| POST | `/api/poultry/observations/{observationId}/confirm` | Confirm observation | 200 |
| POST | `/api/voice/check-in` | Audio check-in (multipart) | 202 |
| POST | `/api/voice/image-check-in` | Image check-in (multipart) | 202 |
| POST | `/api/voice/respond` | Text-to-speech | 200 |
| GET | `/api/voice/voices` | List TTS voices | 200 |
| POST | `/api/vetcoordination/incidents/{incidentId}/vet-request` | Create vet request | 202 |
| POST | `/api/vetcoordination/vet-requests/{vetRequestId}/respond` | Vet responds | 200 |
| POST | `/api/vetcoordination/vet-requests/{vetRequestId}/cost-share` | Record cost share | 200 |
| GET | `/api/vetcoordination/incidents/{incidentId}/prevention-plan` | Get prevention plan | 200 |
| POST | `/api/payments` | Record a payment | 200 |
| POST | `/api/goats/records` | Create livestock record | 201 |
| GET | `/api/goats/records` | List livestock records | 200 |
| GET | `/health` | Health check (anonymous) | 200 |

All `/api/*` routes require a bearer token.

---

## Farms

### Create farm

`POST /api/farms`

Request body (`CreateFarmCommand`):

```json
{
  "ownerId": "demo-owner-1",
  "displayName": "Mama Funmi Farm",
  "location": "Odeda, Ogun State, Nigeria"
}
```

| Field | Type | Required | Constraints |
|---|---|---|---|
| `ownerId` | string | yes | non-empty |
| `displayName` | string | yes | non-empty, max 200 |
| `location` | string | yes | non-empty, max 200 |

Response `201 Created`:

```json
{ "id": "0f8fad5b-d9cb-469f-a165-70867728950e" }
```

`Location` points to `/api/farms/{id}/dashboard`.

> **Security gap:** `ownerId` is taken from the body, not the token. Any authenticated user can create a farm for any owner.

```bash
curl -X POST https://localhost:7215/api/farms \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"ownerId":"demo-owner-1","displayName":"Mama Funmi Farm","location":"Odeda, Ogun State"}'
```

### Get dashboard

`GET /api/farms/{farmId}/dashboard`

Response `200 OK` (`DashboardDto`):

```json
{
  "farm": {
    "id": "0f8fad5b-d9cb-469f-a165-70867728950e",
    "ownerId": "demo-owner-1",
    "displayName": "Mama Funmi Farm",
    "location": "Odeda, Ogun State, Nigeria",
    "createdAt": "2026-10-21T12:00:00Z"
  },
  "incidents": [
    {
      "id": "b1d4c0e8-8f0e-4a54-9d8a-2f5f4f7c1a11",
      "farmId": "0f8fad5b-d9cb-469f-a165-70867728950e",
      "flockId": "c2e5d1f9-9a1f-4b65-8e9b-3a6a5a8d2b22",
      "detectedAt": "2026-10-20T08:30:00Z",
      "incidentType": "Mortality spike",
      "severity": 2,
      "triggeringObservations": "2 deaths/day for 7 days",
      "evidenceSummary": "Average mortality doubled versus the prior week.",
      "status": 0,
      "ruleVersion": "poultry-v1",
      "followUpDate": null,
      "resolvedAt": null
    }
  ],
  "actionPlan": {
    "urgent": ["Review incident b1d4c0e8-...: Mortality spike"],
    "scheduled": [],
    "routine": [
      "Record daily poultry observations",
      "Check goat breeding reminders",
      "Inspect crop plots"
    ]
  },
  "vetRequests": []
}
```

Response `404 Not Found` when the farm does not exist.

> `vetRequests` is currently always an empty array, and no ownership check is performed on `farmId`.

---

## Incidents

### Create incident

`POST /api/incidents`

Request body (`CreateIncidentCommand`):

```json
{
  "farmId": "0f8fad5b-d9cb-469f-a165-70867728950e",
  "flockId": "c2e5d1f9-9a1f-4b65-8e9b-3a6a5a8d2b22",
  "incidentType": "Mortality spike",
  "severity": 2,
  "triggeringObservations": "2 deaths/day for 7 days",
  "evidenceSummary": "Average mortality doubled versus the prior week.",
  "ruleVersion": "poultry-v1"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `farmId` | uuid | yes | must not be empty |
| `flockId` | uuid | yes | must not be empty |
| `incidentType` | string | yes | max 100 |
| `severity` | enum int | yes | 0–3 |
| `triggeringObservations` | string | yes | max 1000 |
| `evidenceSummary` | string | yes | non-empty, max 1000 |
| `ruleVersion` | string? | no | max 50 |

`detectedAt` is set server-side to `DateTime.UtcNow`; it cannot be supplied.

Response `201 Created`:

```json
{ "id": "b1d4c0e8-8f0e-4a54-9d8a-2f5f4f7c1a11" }
```

### List incidents

`GET /api/incidents/{farmId}`

Response `200 OK` — array of `IncidentDto`, newest first. No pagination.

```json
[
  {
    "id": "b1d4c0e8-8f0e-4a54-9d8a-2f5f4f7c1a11",
    "farmId": "0f8fad5b-d9cb-469f-a165-70867728950e",
    "flockId": "c2e5d1f9-9a1f-4b65-8e9b-3a6a5a8d2b22",
    "detectedAt": "2026-10-20T08:30:00Z",
    "incidentType": "Mortality spike",
    "severity": 2,
    "triggeringObservations": "2 deaths/day for 7 days",
    "evidenceSummary": "Average mortality doubled versus the prior week.",
    "status": 0,
    "ruleVersion": "poultry-v1",
    "followUpDate": null,
    "resolvedAt": null
  }
]
```

### Change incident status

`POST /api/incidents/{incidentId}/status`

Request body (`UpdateIncidentStatusCommand`):

```json
{
  "incidentId": "b1d4c0e8-8f0e-4a54-9d8a-2f5f4f7c1a11",
  "newStatus": 1,
  "followUpDate": null
}
```

`incidentId` in the route must equal `incidentId` in the body, otherwise `400 Bad Request`.

| `newStatus` | Effect | Preconditions |
|---|---|---|
| 1 Acknowledged | `Acknowledge()` | Current status must be `Open` (0), else the handler throws |
| 2 UnderMonitoring | Sets status and optional `followUpDate` | none |
| 3 FollowUpRequired | Sets status; `followUpDate` defaults to now + 1 day | none |
| 4 Resolved | Sets `resolvedAt` | none |
| 5 Reopened | Clears `resolvedAt` | none |
| 0 Open / unknown | **Silently ignored** | returns 200 |

Response `200 OK` with an empty body.

> Because no exception middleware is configured, a failed `Acknowledge()` precondition and a missing incident currently surface as `500`, not `409`/`404`.

---

## Poultry observations

### Record observation

`POST /api/poultry/observations`

Request body (`RecordObservationCommand`):

```json
{
  "flockId": "c2e5d1f9-9a1f-4b65-8e9b-3a6a5a8d2b22",
  "rawText": "Two birds died today, water normal, feed normal, 9 crates of eggs",
  "idempotencyKey": "a5d2b0f7-8f6c-4f0a-9a5e-0e6b3f7d9c11",
  "imageUrl": null
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `flockId` | uuid | yes | must not be empty |
| `rawText` | string | yes | non-empty, max 2000 |
| `idempotencyKey` | string | yes | non-empty; **not persisted or enforced** |
| `imageUrl` | string? | no | if supplied, triggers vision analysis |

The server calls Gemini first, falling back to Groq, and stores the extracted observation with status `Pending`.

Response `202 Accepted`:

```json
{ "id": "9b7e6d5c-4a3b-2c1d-0e9f-8a7b6c5d4e3f" }
```

> The optional pre-extracted fields in the response of the voice endpoints are computed separately; the command re-runs extraction. Expect duplicate provider calls and possible mismatch.

### Confirm observation

`POST /api/poultry/observations/{observationId}/confirm`

Request body (`ConfirmObservationCommand`):

```json
{
  "observationId": "9b7e6d5c-4a3b-2c1d-0e9f-8a7b6c5d4e3f",
  "correctedNotes": "Two birds died; water and feed normal"
}
```

`observationId` in the route must match the body.

Response `200 OK` with an empty body. Sets `ConfirmationStatus = Confirmed`; `correctedNotes` replaces farmer notes when supplied.

---

## Voice and image check-ins

### Audio check-in

`POST /api/voice/check-in`

`Content-Type: multipart/form-data`

| Part / query | Location | Type | Required |
|---|---|---|---|
| `audioFile` | form file | file | yes |
| `flockId` | query | uuid | yes |
| `idempotencyKey` | query | string | yes |

```bash
curl -X POST "https://localhost:7215/api/voice/check-in?flockId=$FLOCK_ID&idempotencyKey=$KEY" \
  -H "Authorization: Bearer $TOKEN" \
  -F "audioFile=@checkin.webm"
```

Response `202 Accepted`:

```json
{
  "observationId": "9b7e6d5c-4a3b-2c1d-0e9f-8a7b6c5d4e3f",
  "transcript": "Two birds died today, water normal, feed normal",
  "extracted": {
    "mortalityCount": 2,
    "waterConsumption": "normal",
    "feedConsumption": "normal",
    "eggProduction": null,
    "farmerNotes": null
  },
  "creditsCharged": 0
}
```

Errors: `400` missing file or key; `401` unauthenticated; `500` on STT/extraction/persistence failure.

### Image check-in

`POST /api/voice/image-check-in`

`Content-Type: multipart/form-data`

| Part / query | Location | Type | Required |
|---|---|---|---|
| `imageFile` | form file | file | yes |
| `flockId` | query | uuid | yes |
| `idempotencyKey` | query | string | yes |
| `farmerNotes` | query | string | no |

Response `202 Accepted`:

```json
{
  "observationId": "9b7e6d5c-4a3b-2c1d-0e9f-8a7b6c5d4e3f",
  "imageUrl": "https://res.cloudinary.com/demo/image/upload/v1/farmhand/poultry/abc.jpg",
  "visionAnalysis": {
    "description": "Layers in a clean pen with two visibly lethargic birds.",
    "detectedIssues": ["lethargy", "reduced activity"],
    "confidence": 0.72
  },
  "extracted": {
    "mortalityCount": 0,
    "waterConsumption": null,
    "feedConsumption": null,
    "eggProduction": null,
    "farmerNotes": null
  }
}
```

> **Known blockers:** Cloudinary uploads are unsigned and will be rejected; the command path also fails DI because `VisionAnalysisService` depends on an unregistered concrete `GroqVisionService`. See `CODE_AUDIT.md` C3/H3.

### Text to speech

`POST /api/voice/respond`

Request body (`VoiceRespondRequest`):

```json
{
  "text": "Give the birds clean water and isolate the sick ones.",
  "voice": "umar",
  "targetLanguage": "pidgin"
}
```

Response `200 OK`:

```json
{
  "audioUrl": "https://cdn.yarngpt.ai/tts/abc123.mp3",
  "creditsCharged": 0
}
```

### List voices

`GET /api/voice/voices`

Response `200 OK`:

```json
["umar", "tayo", "ngozi"]
```

---

## Vet coordination

### Create vet request

`POST /api/vetcoordination/incidents/{incidentId}/vet-request`

```json
{
  "incidentId": "b1d4c0e8-8f0e-4a54-9d8a-2f5f4f7c1a11",
  "requestType": 1,
  "farmerCost": 2500
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `incidentId` | uuid | yes | must match route |
| `requestType` | enum int | yes | 0–2 |
| `farmerCost` | decimal | yes | >= 0, NGN |

Response `202 Accepted`: `{ "id": "..." }`

### Vet responds

`POST /api/vetcoordination/vet-requests/{vetRequestId}/respond`

```json
{
  "vetRequestId": "d3f1a2b4-5c6d-7e8f-9a0b-1c2d3e4f5a6b",
  "vetPartnerId": "e4a2b3c5-6d7e-8f90-a1b2-c3d4e5f6a7b8"
}
```

Response `200 OK`, empty body. Sets status to `Responded` and stamps `respondedAt`.

> No role check: any authenticated caller can invoke vet actions.

### Record cost share

`POST /api/vetcoordination/vet-requests/{vetRequestId}/cost-share`

```json
{
  "vetRequestId": "d3f1a2b4-5c6d-7e8f-9a0b-1c2d3e4f5a6b",
  "payerType": "coop",
  "amount": 1000
}
```

Response `200 OK`, empty body. `payerType` is free text (for example `farmer`, `coop`, `offtaker`, `insurer`) and is not validated against an enum.

### Get prevention plan

`GET /api/vetcoordination/incidents/{incidentId}/prevention-plan`

Response `200 OK`:

```json
{ "content": "Keep the pen dry and re-check water troughs daily for 7 days." }
```

Response `404 Not Found` when no plan exists.

---

## Payments

### Record payment

`POST /api/payments`

```json
{
  "vetRequestId": "d3f1a2b4-5c6d-7e8f-9a0b-1c2d3e4f5a6b",
  "amount": 2500,
  "transactionReference": "NOMBA_d3f1a2b4_20261021140500"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `vetRequestId` | uuid | yes | must not be empty |
| `amount` | decimal | yes | > 0 |
| `transactionReference` | string | yes | non-empty, not unique |

Response `200 OK`, empty body.

> The payment is marked `Completed` without contacting the payment provider. There is no idempotency and no duplicate-reference detection. See `CODE_AUDIT.md` C7.

---

## Goats / livestock

### Create livestock record

`POST /api/goats/records`

```json
{
  "farmId": "0f8fad5b-d9cb-469f-a165-70867728950e",
  "species": "goat",
  "animalName": "Bella",
  "sex": "female",
  "approximateAge": 24
}
```

Response `201 Created`: `{ "id": "..." }`

> **Known bug:** the handler never persists the record. The id is not retrievable.

### List livestock records

`GET /api/goats/records`

Response `200 OK`:

```json
{ "message": "Goat records endpoint" }
```

Placeholder; no records are returned.

---

## Health

### Health check

`GET /health` — anonymous.

Response `200 OK`:

```json
{
  "status": "healthy",
  "timestamp": "2026-10-21T14:05:00Z"
}
```

---

## Data models

### FarmDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | uuid | no | Farm id |
| `ownerId` | string | no | Owner identifier supplied at creation |
| `displayName` | string | no | Farm name (max 200) |
| `location` | string | no | Free-text location (max 200) |
| `createdAt` | date-time | no | UTC creation timestamp |

### IncidentDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | uuid | no | Incident id |
| `farmId` | uuid | no | Owning farm |
| `flockId` | uuid | no | Affected flock |
| `detectedAt` | date-time | no | UTC detection timestamp |
| `incidentType` | string | no | Short classification |
| `severity` | enum int | no | 0–3 |
| `triggeringObservations` | string | no | Evidence that triggered the incident |
| `evidenceSummary` | string | no | Summary for farmer/vet |
| `status` | enum int | no | 0–5 |
| `ruleVersion` | string | yes | Detection rule version |
| `followUpDate` | date-time | yes | Scheduled follow-up |
| `resolvedAt` | date-time | yes | Resolution timestamp |

### DashboardDto

| Field | Type | Description |
|---|---|---|
| `farm` | FarmDto | Farm summary |
| `incidents` | IncidentDto[] | Incidents, newest first |
| `actionPlan` | ActionPlanDto | Derived action items |
| `vetRequests` | VetRequestDto[] | Currently always empty |

### ActionPlanDto

| Field | Type | Description |
|---|---|---|
| `urgent` | string[] | Critical/high severity incidents |
| `scheduled` | string[] | Incidents requiring follow-up |
| `routine` | string[] | Static husbandry reminders |

### VetRequestDto

| Field | Type | Description |
|---|---|---|
| `id` | uuid | Vet request id |
| `incidentId` | uuid | Related incident |
| `requestType` | enum int | 0–2 |
| `status` | enum int | 0–2 |
| `farmerCost` | decimal | Farmer share, NGN |
| `respondedAt` | date-time? | When a vet responded |

---

## Error handling

The API does not yet implement a uniform error contract. Current behavior:

| Situation | Status | Body |
|---|---|---|
| Model binding failure | 400 | ASP.NET Core `ValidationProblemDetails` |
| FluentValidation failure | **500** | Raw exception (via exception middleware absent) |
| Entity not found (`KeyNotFoundException`) | **500** | Raw exception |
| Controller-level mismatch (`400 BadRequest`) | 400 | Empty body |
| Missing/invalid token | 401 | Empty body |
| Unknown route | 404 | Empty body |
| Unhandled/third-party failure | 500 | `{ "error": "<exception message>" }` on voice endpoints |

**Intended contract (to implement):** RFC 7807 `application/problem+json`:

```json
{
  "type": "https://farmhand.app/errors/validation",
  "title": "Validation failed",
  "status": 400,
  "detail": "FlockId must not be empty.",
  "instance": "/api/poultry/observations",
  "errors": {
    "FlockId": ["FlockId must not be empty."]
  }
}
```

---

## Versioning

Routes are currently unversioned (`/api/<resource>`). Recommended migration:

1. Introduce `/api/v1/...` while keeping the current paths as aliases.
2. Add a deprecation policy (for example, 90 days) and `Sunset` headers for retired paths.
3. Enums are serialized as integers; switching to strings is a breaking change and must ship under a new version.

---

## Known limitations

These affect the accuracy and usefulness of the API contract:

- No token-issuance endpoint; every `[Authorize]` route is unreachable for real clients.
- No ownership or role checks; any authenticated caller can access any farm and act as a vet.
- Validation and not-found errors return 500 instead of 400/404.
- `idempotencyKey` is accepted but never stored or enforced.
- `POST /api/goats/records` returns an id without persisting anything.
- Payments are marked completed without provider verification; references are not unique.
- Cloudinary uploads are unsigned and will fail; image check-in also hits a DI resolution failure.
- Enums are numeric on the wire; there is no string-enum conversion.
- No pagination, filtering, rate limiting, CORS policy, or request-size limits.
- Response `example` values in this document are illustrative, not fixtures.

For the full code review, see [`CODE_AUDIT.md`](./CODE_AUDIT.md).
