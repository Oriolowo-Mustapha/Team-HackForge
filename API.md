# FarmHand API Reference

**Version:** v1 (unversioned routes)
**Base URL (local):** `http://localhost:5037` or `https://localhost:7215`
**Content type:** `application/json`, except the multipart check-in endpoints
**Last updated:** 2026-10-22

Swagger UI (`/swagger`) is enabled only in Development and includes JWT support.

---

## Contents

1. [Quick start](#quick-start)
2. [Authentication and authorization](#authentication-and-authorization)
3. [Conventions](#conventions)
4. [Enumerations](#enumerations)
5. [Endpoint summary](#endpoint-summary)
6. [Auth](#auth)
7. [Admin](#admin)
8. [Farms](#farms)
9. [Incidents](#incidents)
10. [Poultry observations](#poultry-observations)
11. [Chat observations](#chat-observations)
12. [Voice and image check-ins](#voice-and-image-check-ins)
13. [Vet coordination](#vet-coordination)
14. [Payments](#payments)
15. [Goats and livestock](#goats-and-livestock)
16. [Health](#health)
17. [Data models](#data-models)
18. [Error handling](#error-handling)
19. [Configuration](#configuration)
20. [Known limitations](#known-limitations)

---

## Quick start

```powershell
Copy-Item src/FarmHand.API/appsettings.example.json src/FarmHand.API/appsettings.json
# Fill in PostgreSQL, JWT, AI, Cloudinary, and a non-placeholder Seed:Password.
dotnet run --project src/FarmHand.API --launch-profile https
```

| Resource | URL |
|---|---|
| Swagger UI | `https://localhost:7215/swagger` |
| OpenAPI JSON | `https://localhost:7215/swagger/v1/swagger.json` |
| Health check | `https://localhost:7215/health` |

Migrations run on startup in Development or when `Database:MigrateOnStartup` is `true`.
Demo data is seeded only in Development and only when `Seed:Password` is set to a strong,
non-placeholder value.

---

## Authentication and authorization

| Route | Access |
|---|---|
| `POST /api/auth/register` | Anonymous, rate limited |
| `POST /api/auth/login` | Anonymous, rate limited |
| `GET /health` | Anonymous |
| `/api/admin/*` | `Admin` role |
| All other `/api/*` routes | Any authenticated user, plus resource ownership checks |

```
Authorization: Bearer <jwt>
```

Tokens are HS256 JWTs signed with `Jwt:SecretKey`, with issuer `Jwt:Issuer` (default
`FarmHand`) and audience `Jwt:Audience` (default `FarmHandUsers`). The key must be at least
32 bytes; the app fails fast otherwise.

Claims: `nameid` (user id), `email`, `role` (`Farmer`, `Vet`, or `Admin`), `jti`, `iat`.
Default lifetime is `Jwt:ExpirationMinutes` (60 minutes).

Resource rules:

- Farm, observation, incident, livestock, vet request, cost-share, and payment endpoints
  require the caller to own the underlying farm. Admins bypass ownership checks.
- Responding to a vet request requires the `Vet` or `Admin` role. A vet may only respond as
  the `VetPartner` profile linked to their user account.
- A valid token without the required role or ownership receives `403 Forbidden`.

| Condition | Response |
|---|---|
| No `Authorization` header | `401 Unauthorized` |
| Invalid signature, wrong issuer/audience, or expired token | `401 Unauthorized` |
| Inactive account at login | `401 Unauthorized` |
| Wrong role or farm ownership | `403 Forbidden` |
| More than 10 auth attempts per minute per IP | `429 Too Many Requests` |

---

## Conventions

- **JSON casing:** camelCase.
- **Ids:** UUID strings.
- **Timestamps:** ISO 8601 UTC.
- **Enums:** serialized and accepted as **integers**.
- **Nullability:** nullable fields are omitted or `null`.
- **Route casing:** case-insensitive.

---

## Enumerations

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

`Open` is the initial state only and is rejected as an update target.

### ObservationConfirmationStatus

| Value | Name |
|---|---|
| 0 | Pending |
| 1 | Confirmed |
| 2 | Corrected |

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

---

## Endpoint summary

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/auth/register` | Register a farmer and receive a token |
| POST | `/api/auth/login` | Log in and receive a token |
| POST | `/api/admin/vets` | Register a vet user and linked profile |
| GET | `/api/admin/vets` | List vets |
| DELETE | `/api/admin/vets/{vetId}` | Deactivate a vet and linked user |
| POST | `/api/farms` | Create a farm owned by the caller |
| GET | `/api/farms/{farmId}/dashboard` | Farm dashboard |
| POST | `/api/incidents` | Create an incident |
| GET | `/api/incidents/{farmId}` | List incidents for a farm |
| POST | `/api/incidents/{incidentId}/status` | Transition incident status |
| POST | `/api/poultry/observations` | Record an observation from text |
| POST | `/api/poultry/observations/{observationId}/confirm` | Confirm an observation |
| POST | `/api/chat/observation` | Create a pending observation from text/audio/image |
| POST | `/api/chat/observation/{observationId}/confirm` | Confirm a chat observation |
| POST | `/api/chat/observation/{observationId}/create-incident` | Create an incident from a confirmed observation |
| POST | `/api/voice/check-in` | Voice check-in |
| POST | `/api/voice/image-check-in` | Image check-in |
| POST | `/api/voice/respond` | Text-to-speech |
| GET | `/api/voice/voices` | List available voices |
| POST | `/api/vetcoordination/incidents/{incidentId}/vet-request` | Create a vet request |
| POST | `/api/vetcoordination/vet-requests/{vetRequestId}/respond` | Vet responds to a request |
| POST | `/api/vetcoordination/vet-requests/{vetRequestId}/cost-share` | Record a cost-share contribution |
| GET | `/api/vetcoordination/incidents/{incidentId}/prevention-plan` | Get a prevention plan |
| POST | `/api/payments/initiate` | Create a pending payment and get a reference |
| POST | `/api/payments/record` | Verify and complete a payment |
| POST | `/api/goats/records` | Create a livestock record |
| GET | `/api/goats/records` | Placeholder listing |
| GET | `/health` | Health check |

---

## Auth

### POST `/api/auth/register`

```json
{
  "email": "farmer@example.com",
  "password": "at-least-8-characters",
  "fullName": "Mama Funmi",
  "phoneNumber": "+234-800-000-0000"
}
```

`201 Created`:

```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "farmer@example.com",
  "role": "Farmer",
  "token": "<jwt>",
  "expiresAt": "2026-10-22T13:00:00Z"
}
```

Errors: `400` invalid input, duplicate email, password shorter than 8 characters, or password
longer than 72 UTF-8 bytes; `429` rate limited.

### POST `/api/auth/login`

```json
{ "email": "farmer@example.com", "password": "..." }
```

Returns the same shape as register. Unknown emails, wrong passwords, and inactive accounts all
return `401` with `{ "error": "Invalid email or password." }`.

---

## Admin

All admin routes require the `Admin` role.

### POST `/api/admin/vets`

```json
{
  "email": "vet@example.com",
  "temporaryPassword": "at-least-8-characters",
  "fullName": "Dr. Ade",
  "specialization": "Poultry health",
  "contact": "+234-800-000-0001",
  "phoneNumber": "+234-800-000-0001"
}
```

Creates the `User` and `VetPartner` in one unit of work. `201` returns `vetId`, `userId`,
`email`, `role`, and `specialization`.

### GET `/api/admin/vets`

Returns active and inactive vets:

```json
[
  {
    "id": "...",
    "userId": "...",
    "name": "Dr. Ade",
    "email": "vet@example.com",
    "specialization": "Poultry health",
    "contact": "+234-800-000-0001",
    "isActive": true,
    "createdAt": "2026-10-22T12:00:00Z"
  }
]
```

### DELETE `/api/admin/vets/{vetId}`

Deactivates the vet profile and the linked user account. Returns `{ "message": "Vet deactivated." }`
or `404`.

---

## Farms

### POST `/api/farms`

```json
{ "displayName": "Funmi Poultry", "location": "Ibadan" }
```

The owner is derived from the JWT; owner ids cannot be supplied by the client.
`201 Created` returns `{ "id": "<farmId>" }`.

### GET `/api/farms/{farmId}/dashboard`

Returns a `DashboardDto`:

```json
{
  "farm": {
    "id": "...",
    "ownerId": "<user id string>",
    "displayName": "Funmi Poultry",
    "location": "Ibadan",
    "createdAt": "2026-10-22T12:00:00Z"
  },
  "incidents": [],
  "actionPlan": { "urgent": [], "scheduled": [], "routine": [] },
  "vetRequests": []
}
```

`403` when the caller does not own the farm; `404` when it does not exist.
`vetRequests` is currently always empty.

---

## Incidents

### POST `/api/incidents`

```json
{
  "farmId": "...",
  "livestockAnimalId": "...",
  "livestockCategoryId": null,
  "incidentType": "Mortality spike",
  "severity": 2,
  "triggeringObservations": "Three days of elevated mortality",
  "evidenceSummary": "Mortality exceeded threshold",
  "ruleVersion": "v1"
}
```

At least one of `livestockAnimalId` or `livestockCategoryId` is required. The referenced
animal/category must belong to `farmId`. `DetectedAt` is assigned by the server.
`201 Created` returns `{ "id": "<incidentId>" }`.

### GET `/api/incidents/{farmId}`

Returns `IncidentDto[]` for the farm, newest first, with no pagination. Requires farm ownership.

### POST `/api/incidents/{incidentId}/status`

```json
{ "incidentId": "...", "newStatus": 2, "followUpDate": "2026-10-25T09:00:00Z" }
```

The route id and body `incidentId` must match. `newStatus` must be `1`-`5`; `0` (Open) and
unknown values return `400`. `FollowUpRequired` defaults `followUpDate` to now + 1 day.
Returns `200` on success, `403` for another farm, `404` when missing.

---

## Poultry observations

### POST `/api/poultry/observations`

```json
{
  "farmId": "...",
  "livestockAnimalId": "...",
  "livestockCategoryId": null,
  "rawText": "Two birds died today and water intake is low",
  "idempotencyKey": "client-generated-key",
  "imageUrl": null
}
```

At least one of `livestockAnimalId` or `livestockCategoryId` is required. Reusing an
`idempotencyKey` returns the existing observation id. The server extracts structured fields with
Gemini (Groq fallback) and stores the observation as `Pending`.
`202 Accepted` returns `{ "id": "<observationId>" }`.

### POST `/api/poultry/observations/{observationId}/confirm`

```json
{ "observationId": "...", "correctedNotes": "Optional replacement notes" }
```

The route id and body `observationId` must match. `correctedNotes`, when supplied, replaces the
farmer notes. Returns `200`, `403`, or `404`.

---

## Chat observations

### POST `/api/chat/observation`

`multipart/form-data` fields:

| Field | Type | Required |
|---|---|---|
| `farmId` | uuid | yes |
| `livestockAnimalId` | uuid | one of animal/category |
| `livestockCategoryId` | uuid | one of animal/category |
| `rawText` | string | required unless `audioFile` is supplied |
| `audioFile` | file | optional |
| `imageFile` | file | optional |
| `idempotencyKey` | string | optional; generated when omitted |

Creates a `Pending` observation. Audio is transcribed with YarnGPT; images are uploaded to
Cloudinary and analyzed with Groq Vision. `202` returns the observation id, transcript,
extracted fields, optional image URL, and vision analysis.

### POST `/api/chat/observation/{observationId}/confirm`

```json
{ "correctedNotes": "Optional replacement notes" }
```

Only the observation id and corrected notes are trusted. Does not create an incident.
Returns `200`, `403`, or `404`.

### POST `/api/chat/observation/{observationId}/create-incident`

```json
{
  "severity": 2,
  "incidentType": "Poultry Observation",
  "triggeringObservations": "Optional override",
  "evidenceSummary": "Optional override",
  "ruleVersion": "v1"
}
```

Farm, animal, and category are derived from the stored observation. The observation must not be
`Pending`, otherwise the endpoint returns `409`. `201` returns `{ "incidentId": "..." }`.

---

## Voice and image check-ins

### POST `/api/voice/check-in`

`multipart/form-data`: `audioFile` plus query parameters `farmId`, `idempotencyKey`, and at least
one of `livestockAnimalId` / `livestockCategoryId`.

Transcribes audio, extracts structured fields, and stores a pending observation.
`202` returns `observationId`, `transcript`, `extracted`, and `creditsCharged`.

### POST `/api/voice/image-check-in`

`multipart/form-data`: `imageFile` plus query parameters `farmId`, `idempotencyKey`, at least one
of `livestockAnimalId` / `livestockCategoryId`, and optional `farmerNotes`.

Uploads the image, runs vision analysis, extracts fields, and stores a pending observation.
`202` returns the observation id, image URL, vision analysis, and extracted fields.

### POST `/api/voice/respond`

```json
{ "text": "Give the birds clean water", "voice": "umar", "targetLanguage": "yoruba" }
```

`200` returns `{ "audioUrl": "...", "creditsCharged": 1 }`; provider failures return `502`.

### GET `/api/voice/voices`

Returns the provider's voice list; provider failures return `502`.

---

## Vet coordination

### POST `/api/vetcoordination/incidents/{incidentId}/vet-request`

```json
{ "incidentId": "...", "requestType": 0, "farmerCost": 2500 }
```

Requires farm ownership for the incident. `202` returns `{ "id": "<vetRequestId>" }`.

### POST `/api/vetcoordination/vet-requests/{vetRequestId}/respond`

```json
{ "vetRequestId": "...", "vetPartnerId": null }
```

Requires the `Vet` or `Admin` role. A vet's linked active `VetPartner` profile is used
automatically; a requested `vetPartnerId` must match it. Returns `200`, `403`, or `404`.

### POST `/api/vetcoordination/vet-requests/{vetRequestId}/cost-share`

```json
{ "vetRequestId": "...", "payerType": "coop", "amount": 500 }
```

Requires access to the vet request's farm. Returns `200`, `400`, `403`, or `404`.

### GET `/api/vetcoordination/incidents/{incidentId}/prevention-plan`

Returns `{ "content": "..." }`, `403` for another farm, or `404` when no plan exists.

---

## Payments

### POST `/api/payments/initiate`

```json
{ "vetRequestId": "...", "amount": 2500, "currency": "NGN" }
```

Creates a pending `Payment` owned by the caller's farm and returns
`{ "transactionReference": "..." }`. In `Simulated` mode the reference is HMAC-signed and no
provider call is made; `Live` mode calls Nomba.

### POST `/api/payments/record`

```json
{
  "vetRequestId": "...",
  "amount": 2500,
  "currency": "NGN",
  "transactionReference": "..."
}
```

The reference must exist as a pending payment for the same vet request. Amount and currency must
match the initiated payment. The provider verifies the reference, then the payment is marked
`Completed`. Repeating a successful record is idempotent. Returns `{ "status": "paid" }`.

| Condition | Response |
|---|---|
| Unknown reference | `404` |
| Reference belongs to another vet request | `400` |
| Amount or currency mismatch | `400` |
| Payment status is neither Pending nor Completed | `409` |
| Provider verification fails | `409` |

---

## Goats and livestock

### POST `/api/goats/records`

```json
{
  "farmId": "...",
  "categoryId": "...",
  "species": "goat",
  "animalName": "Bisi",
  "sex": "female",
  "approximateAge": 18
}
```

Requires farm ownership and a category from the same farm. The record is persisted before the id
is returned. `201` returns `{ "id": "<livestockAnimalId>" }`.

### GET `/api/goats/records`

Placeholder: returns `{ "message": "Goat records endpoint" }`.

---

## Health

`GET /health` returns `200` with `{ "status": "healthy", "timestamp": "..." }`. It does not probe
the database.

---

## Data models

### FarmDto

`id`, `ownerId`, `displayName`, `location`, `createdAt`.

### IncidentDto

`id`, `farmId`, `livestockAnimalId?`, `livestockCategoryId?`, `detectedAt`, `incidentType`,
`severity`, `triggeringObservations`, `evidenceSummary`, `status`, `ruleVersion?`,
`followUpDate?`, `resolvedAt?`.

### PoultryObservationDto

`id`, `farmId`, `livestockAnimalId?`, `livestockCategoryId?`, `recordedAt`, `mortalityCount`,
`waterConsumption?`, `feedConsumption?`, `eggProduction?`, `farmerNotes?`, `confirmationStatus`.

### DashboardDto

`farm`, `incidents`, `actionPlan` (`urgent`, `scheduled`, `routine`), `vetRequests`.

### VetRequestDto

`id`, `incidentId`, `requestType`, `status`, `farmerCost`, `respondedAt?`.

---

## Error handling

Unhandled exceptions are converted to RFC 7807 `ProblemDetails`:

| Exception | Status |
|---|---|
| FluentValidation failure | `400` |
| `ArgumentException` / `ArgumentOutOfRangeException` | `400` |
| `UnauthorizedAccessException` | `403` |
| `KeyNotFoundException` | `404` |
| `InvalidOperationException` | `409` |
| `DbUpdateException` | `409` |
| `HttpRequestException` | `502` |
| Anything else | `500` |

Controllers that validate form fields directly return `{ "error": "..." }` with `400`.
Provider failures in voice endpoints return `502` with a generic message; internal exception
details are logged, not returned.

---

## Configuration

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:SecretKey` | HS256 signing key, at least 32 bytes (required) |
| `Jwt:Issuer` / `Jwt:Audience` | Token validation, defaults `FarmHand` / `FarmHandUsers` |
| `Jwt:ExpirationMinutes` | Token lifetime, default `60` |
| `Database:MigrateOnStartup` | Apply migrations outside Development |
| `Seed:Password` | Development demo password; placeholder values refuse to seed |
| `Seed:AdminEmail` / `Seed:FarmerEmail` / `Seed:VetEmail` | Demo account emails |
| `Gemini:ApiKey` / `Groq:ApiKey` | Observation extraction and vision |
| `YarnGpt:ApiKey` | Speech-to-text, text-to-speech, voices |
| `Cloudinary:CloudName` / `ApiKey` / `ApiSecret` | Image uploads |
| `Nomba:Mode` | `Simulated` (default) or `Live` |
| `Nomba:ApiKey` / `SigningSecret` / `BaseUrl` | Nomba payment integration |

---

## Known limitations

- `GET /api/goats/records` is a placeholder.
- `DashboardDto.vetRequests` is always empty.
- Incident listing has no pagination.
- `CaseFileDto` and `PoultryObservationDto` exist but are not exposed by dedicated endpoints.
- The baseline migration assumes a pre-existing schema; `AdoptBaselineMigrationAsync` records
  the baseline when the database already contains the application tables.
