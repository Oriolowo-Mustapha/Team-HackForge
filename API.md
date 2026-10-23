# FarmHand API

Voice-first livestock and poultry health API for farmers, veterinarians, and administrators.

This document describes the API implemented by this repository. It is intended as the integration contract for the frontend and as an operational reference for deployment.

Last updated: 2026-10-23

---

## Contents

- [Base URLs](#base-urls)
- [Quick start](#quick-start)
- [Authentication and authorization](#authentication-and-authorization)
- [Conventions](#conventions)
- [Errors](#errors)
- [Pagination](#pagination)
- [Enums](#enums)
- [Endpoint index](#endpoint-index)
- [Auth](#auth)
- [Admin](#admin)
- [Vets](#vets)
- [Vet account](#vet-account)
- [Farms](#farms)
- [Livestock categories](#livestock-categories)
- [Livestock](#livestock)
- [Incidents](#incidents)
- [Incident actions](#incident-actions)
- [Follow-ups](#follow-ups)
- [Poultry observations](#poultry-observations)
- [Chat observations](#chat-observations)
- [Voice](#voice)
- [Vet coordination](#vet-coordination)
- [Treatment plans](#treatment-plans)
- [Payments](#payments)
- [Audit events](#audit-events)
- [Health](#health)
- [Data models](#data-models)
- [End-to-end workflows](#end-to-end-workflows)
- [Configuration](#configuration)
- [Production checklist](#production-checklist)
- [Known limitations](#known-limitations)

---

## Base URLs

| Environment | URL |
| --- | --- |
| Local HTTP | `http://localhost:5037` |
| Local HTTPS | `https://localhost:7215` |
| Swagger UI (Development only) | `http://localhost:5037/swagger` |
| OpenAPI JSON (Development only) | `http://localhost:5037/swagger/v1/swagger.json` |

All API routes are under `/api`, except `/health`.

Requests and responses are JSON unless an endpoint is marked `multipart/form-data`.

---

## Quick start

### 1. Register a farmer

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "farmer@example.com",
  "password": "StrongPass123!",
  "fullName": "Ada Okafor",
  "phoneNumber": "+2348012345678"
}
```

Response: `201 Created`

```json
{
  "message": "Account created successfully. Please log in to receive your access token."
}
```

Registration does **not** return a token.

### 2. Log in

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "farmer@example.com",
  "password": "StrongPass123!"
}
```

Response: `200 OK`

```json
{
  "message": "Login successful.",
  "userId": "4d6f4b4e-...",
  "email": "farmer@example.com",
  "role": "Farmer",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-10-23T12:00:00Z"
}
```

Store `token` and send it on every protected request:

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

The access token expires after 60 minutes by default. See [Known limitations](#known-limitations) for the current refresh-token behavior.

### 3. Create a farm

```http
POST /api/farms
Authorization: Bearer <token>
Content-Type: application/json

{
  "displayName": "Green Valley Farm",
  "location": "Ibadan, Oyo State"
}
```

Response: `201 Created`

```json
{ "id": "8d4b2f6e-..." }
```

### 4. Add a livestock category, then animals

```http
POST /api/livestock/categories
Authorization: Bearer <token>
Content-Type: application/json

{
  "farmId": "8d4b2f6e-...",
  "name": "Poultry",
  "description": "Layer hens"
}
```

```http
POST /api/livestock/records
Authorization: Bearer <token>
Content-Type: application/json

{
  "farmId": "8d4b2f6e-...",
  "categoryId": "1c3a2b44-...",
  "species": 3,
  "animalName": 7,
  "sex": "Female",
  "approximateAge": 8,
  "diseaseHistory": null,
  "recentGestationDate": null,
  "lastVeterinaryVisit": null,
  "vaccinations": "Newcastle, week 4",
  "lastLactationDate": null,
  "parentage": null
}
```

### 5. Record an observation

Text observation:

```http
POST /api/poultry/observations
Authorization: Bearer <token>
Content-Type: application/json

{
  "farmId": "8d4b2f6e-...",
  "livestockCategoryId": "1c3a2b44-...",
  "rawText": "Three hens died since yesterday and the rest look weak",
  "idempotencyKey": "checkin-2026-10-23-001"
}
```

Response: `202 Accepted`

```json
{ "id": "f5d8a8cb-..." }
```

Then confirm the observation:

```http
POST /api/poultry/observations/f5d8a8cb-.../confirm
Authorization: Bearer <token>
Content-Type: application/json

{
  "observationId": "f5d8a8cb-...",
  "correctedNotes": "Three hens died since yesterday; the rest are weak",
  "overrideSeverity": null,
  "overrideReason": null
}
```

If the final severity is `High` (2) or `Critical` (3), an incident is created automatically.

---

## Authentication and authorization

### Token format

- JWT, signed with HMAC-SHA256.
- Issuer: `FarmHand`.
- Audience: `FarmHandUsers`.
- Claims: `nameid` (user id), `email`, `role`, `jti`, `iat`, `token_version`.
- Default lifetime: 60 minutes.

The token is rejected if the user is inactive or the `token_version` claim no longer matches the user's current version.

### Roles

| Role | Purpose |
| --- | --- |
| `Farmer` | Owns farms, livestock, observations, incidents, and vet requests. |
| `Vet` | Responds to assigned vet requests, records treatment plans and cost shares. |
| `Admin` | Full administrative access, including vet registration and deactivation. |

### Authorization rules

| Area | Rule |
| --- | --- |
| Admin endpoints (`/api/admin/*`) | `Admin` role required. |
| Vet self-service (`/api/vet/me/*`) | `Vet` role required. |
| Farm-scoped resources | Admin bypass, otherwise the caller must own the farm (`farm.ownerId == caller userId`). This covers farms, dashboard, categories, livestock, observations, incidents, follow-ups, incident actions, and prevention-plan creation. |
| Vet requests | A vet may access only a request assigned to their own `VetPartnerId`. Admins bypass. Unassigned requests may be answered by any active vet. |
| Public endpoints | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh`, `/health`, `GET /api/livestock/species`, `GET /api/livestock/names`, and `POST /api/payments/webhook`. |
| Everything else | Valid bearer token required. |

A farmer cannot access a farm owned by another user. A vet cannot use most farm-scoped endpoints; vet access is intentionally limited to assigned vet requests, treatment plans, prevention plans, and payments.

### Auth rate limiting

All `/api/auth/*` endpoints are limited to **10 requests per minute per client IP**.

When exceeded:

```http
429 Too Many Requests
```

The response has no JSON body.

### Token invalidation

| Action | Effect |
| --- | --- |
| `POST /api/auth/revoke` | Revokes all refresh tokens for the current user. The current access token remains valid until it expires. |
| `POST /api/auth/change-password` | Changes the password, revokes all refresh tokens, and increments `token_version`, invalidating all existing access tokens. |
| Admin deactivates a vet | The linked user becomes inactive; all tokens are rejected on the next request. |
| Admin resets vet credentials | Changes the password, revokes refresh tokens, and increments `token_version`. |

### Refresh token behavior

`POST /api/auth/refresh` uses a rotating refresh token. On successful login the server creates a refresh token, stores only an SHA-256 hash of it, and returns the plaintext value once in the login response. Each refresh call revokes the current token and issues a new one.

This replaces the previous limitation where login did not issue a refresh token.

---

## Conventions

### JSON

- Property names are `camelCase`.
- IDs are UUID strings.
- Timestamps are ISO 8601 UTC strings, for example `2026-10-23T09:15:30Z`.
- Enums are serialized as **integers** unless a field is explicitly documented as a string.
- Request enum values must be sent as integers. Sending names such as `"High"` fails model binding or validation with `400`.

### Empty responses

Several state-changing endpoints return `200 OK` with an empty body:

- `POST /api/incidents/{incidentId}/status`
- `POST /api/poultry/observations/{observationId}/confirm`
- `POST /api/vet-coordination/vet-requests/{vetRequestId}/respond`
- `POST /api/vet-coordination/vet-requests/{vetRequestId}/cost-share`

Treat a `2xx` status as success; do not attempt to parse a body.

### Dates and times

- Send dates as ISO 8601 strings.
- Server-generated timestamps are UTC.
- `followUpDate` on incident status updates accepts a date-time; when omitted for `FollowUpRequired`, the server uses `now + 1 day`.

### Money

- Monetary values are decimal numbers in **NGN**.
- `Money` defaults to `NGN` when no currency is supplied.
- Payment initiation requires the currency to match the vet request (`NGN` in the current flow).

### Idempotency

Observation intake accepts a client-generated `idempotencyKey`:

- Text observation: field in the JSON body.
- Chat observation: optional form field; generated when omitted.
- Voice check-in: required query parameter.
- Image check-in: required query parameter.

Reusing a key returns the existing observation instead of creating a duplicate. Chat and voice retries are checked before STT, Cloudinary upload, or AI extraction, so a duplicate request does not trigger those paid calls. Reused responses include `"reused": true`; voice retries report `"creditsCharged": 0`.

### File uploads

Multipart endpoints:

- `POST /api/chat/observation`
- `POST /api/voice/check-in`
- `POST /api/voice/image-check-in`

Audio and image size/type limits are enforced by the underlying providers. Send a valid filename and content type.

---

## Errors

Most exceptions are returned as RFC 7807 `ProblemDetails` with `Content-Type: application/problem+json`.

```json
{
  "type": "https://farmhand.ai/problems/400",
  "title": "The request is invalid.",
  "status": 400,
  "detail": "Amount must match the vet request charge of 25000 NGN.",
  "instance": "/api/payments/initiate",
  "traceId": "0HN7..."
}
```

Validation failures add an `errors` object:

```json
{
  "type": "https://farmhand.ai/problems/400",
  "title": "The request is invalid.",
  "status": 400,
  "detail": null,
  "instance": "/api/farms",
  "traceId": "0HN7...",
  "errors": {
    "DisplayName": ["'Display Name' must not be empty."]
  }
}
```

### Status code mapping

| Status | Meaning |
| --- | --- |
| `400` | Validation failure, malformed body, route/body ID mismatch, or invalid state transition. |
| `401` | Missing, invalid, expired, or revoked bearer token; invalid login; invalid refresh token. |
| `403` | Authenticated but not allowed to access the resource. |
| `404` | Resource not found, or no prevention/treatment plan exists. |
| `409` | State conflict (payment required, observation not confirmed, duplicate state). |
| `415` | Unsupported content type (for example, non-multipart request to an upload endpoint). |
| `429` | Auth rate limit exceeded. No body. |
| `500` | Unexpected server error. Details are not exposed. |
| `502` | Upstream AI, speech, Cloudinary, or payment provider failure. |
| `503` | Webhook verification is not configured (fail-closed). |

Some controllers return a plain JSON error instead of `ProblemDetails`:

```json
{ "error": "Invalid email or password." }
```

This is common on auth, admin, and voice endpoints. Clients should handle both shapes.

---

## Pagination

Paginated endpoints return:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

| Endpoint | Parameters | Behavior |
| --- | --- | --- |
| `GET /api/incidents/{farmId}` | `page` (default 1), `pageSize` (default 20) | Rejects `page < 1` and `pageSize < 1` or `> 100` with `400`. |
| `GET /api/admin/vets` | `page` (default 1), `pageSize` (default 20) | Clamps values instead of rejecting: page minimum 1, page size 1–100. |

Other list endpoints return a plain JSON array.

---

## Enums

### Severity

| Value | Name |
| --- | --- |
| `0` | `Low` |
| `1` | `Medium` |
| `2` | `High` |
| `3` | `Critical` |

### IncidentStatus

| Value | Name |
| --- | --- |
| `0` | `Open` |
| `1` | `Acknowledged` |
| `2` | `UnderMonitoring` |
| `3` | `FollowUpRequired` |
| `4` | `Resolved` |
| `5` | `Reopened` |

`Open` is the initial state and is **not** a valid target for a status update.

### RequestType

| Value | Name |
| --- | --- |
| `0` | `RemoteReview` |
| `1` | `SharedVisit` |
| `2` | `CooperativePlan` |

### RequestStatus

| Value | Name |
| --- | --- |
| `0` | `Pending` |
| `1` | `Responded` |
| `2` | `Completed` |

There is currently no API endpoint that transitions a vet request to `Completed`.

### ObservationConfirmationStatus

| Value | Name |
| --- | --- |
| `0` | `Pending` |
| `1` | `Confirmed` |
| `2` | `Corrected` |

### Outcome

| Value | Name |
| --- | --- |
| `0` | `Improving` |
| `1` | `Worsening` |
| `2` | `Resolved` |

### LivestockSpecies

| Value | Name |
| --- | --- |
| `0` | `Goat` |
| `1` | `Sheep` |
| `2` | `Cattle` |
| `3` | `Poultry` |
| `4` | `Pig` |
| `5` | `Other` |

### AnimalName

| Value | Name |
| --- | --- |
| `0` | `Goat01` |
| `1` | `Goat02` |
| `2` | `Goat03` |
| `3` | `Sheep01` |
| `4` | `Sheep02` |
| `5` | `Cow01` |
| `6` | `Cow02` |
| `7` | `Hen01` |
| `8` | `Hen02` |
| `9` | `Other` |

`GET /api/livestock/species` and `GET /api/livestock/names` return these enum values as integer arrays, for example `[0, 1, 2, 3, 4, 5]`.

---

## Endpoint index

The API exposes 49 operations: 48 under `/api` plus `/health`.

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/auth/register` | Anonymous | Register a farmer. |
| `POST` | `/api/auth/login` | Anonymous | Log in and receive a JWT. |
| `POST` | `/api/auth/refresh` | Anonymous | Rotate a refresh token. |
| `POST` | `/api/auth/revoke` | Bearer | Revoke all refresh tokens. |
| `POST` | `/api/auth/change-password` | Bearer | Change password and invalidate sessions. |
| `POST` | `/api/admin/vets` | Admin | Register a vet. |
| `POST` | `/api/admin/vets/{vetId}/reset-credentials` | Admin | Reset vet credentials and email a temporary password. |
| `GET` | `/api/admin/vets` | Admin | List vets. |
| `DELETE` | `/api/admin/vets/{vetId}` | Admin | Deactivate a vet. |
| `GET` | `/api/vets` | Bearer | List active vets. |
| `GET` | `/api/vet/me/payout-account` | Vet | Get own payout account. |
| `PUT` | `/api/vet/me/payout-account` | Vet | Set/update own payout account. |
| `POST` | `/api/farms` | Bearer | Create a farm. |
| `GET` | `/api/farms/{farmId}/dashboard` | Bearer | Farm dashboard. |
| `POST` | `/api/livestock/categories` | Bearer | Create a livestock category. |
| `GET` | `/api/livestock/categories?farmId=` | Bearer | List categories. |
| `POST` | `/api/livestock/records` | Bearer | Create a livestock record. |
| `GET` | `/api/livestock/records?farmId=` | Bearer | List livestock records. |
| `GET` | `/api/livestock/species` | Anonymous | Species dropdown values. |
| `GET` | `/api/livestock/names` | Anonymous | Animal-name dropdown values. |
| `POST` | `/api/incidents` | Bearer | Create an incident. |
| `GET` | `/api/incidents/{farmId}` | Bearer | List incidents with pagination. |
| `POST` | `/api/incidents/{incidentId}/status` | Bearer | Change incident status. |
| `POST` | `/api/incidents/{incidentId}/actions` | Bearer | Create an incident action. |
| `GET` | `/api/incidents/{incidentId}/actions` | Bearer | List incident actions. |
| `POST` | `/api/follow-ups` | Bearer | Record a follow-up. |
| `GET` | `/api/follow-ups?incidentId=` | Bearer | List follow-ups. |
| `POST` | `/api/poultry/observations` | Bearer | Record a text observation. |
| `POST` | `/api/poultry/observations/{observationId}/confirm` | Bearer | Confirm an observation. |
| `POST` | `/api/chat/observation` | Bearer | Record a text/voice/image observation. |
| `POST` | `/api/chat/observation/{observationId}/confirm` | Bearer | Confirm and assess an observation. |
| `POST` | `/api/chat/observation/{observationId}/create-incident` | Bearer | Create an incident from a confirmed observation. |
| `POST` | `/api/voice/check-in` | Bearer | Voice check-in with speech-to-text. |
| `POST` | `/api/voice/image-check-in` | Bearer | Image check-in with vision analysis. |
| `POST` | `/api/voice/respond` | Bearer | Text-to-speech response. |
| `GET` | `/api/voice/voices` | Bearer | List TTS voices. |
| `POST` | `/api/vet-coordination/incidents/{incidentId}/vet-request` | Bearer | Create a vet request. |
| `POST` | `/api/vet-coordination/vet-requests/{vetRequestId}/respond` | Vet/Admin | Accept a vet request. |
| `POST` | `/api/vet-coordination/vet-requests/{vetRequestId}/cost-share` | Bearer | Record a cost-share contribution. |
| `POST` | `/api/vet-coordination/incidents/{incidentId}/prevention-plan` | Bearer | Create a prevention plan. |
| `GET` | `/api/vet-coordination/incidents/{incidentId}/prevention-plan` | Bearer | Get a prevention plan. |
| `POST` | `/api/treatment-plans` | Bearer | Create a treatment plan. |
| `GET` | `/api/treatment-plans/{id}` | Bearer | Get a treatment plan. |
| `POST` | `/api/payments/initiate` | Bearer | Initiate a payment. |
| `POST` | `/api/payments/record` | Bearer | Verify and record a payment. |
| `POST` | `/api/payments/webhook` | Anonymous | Flutterwave webhook. |
| `POST` | `/api/audit-events` | Bearer | Record an audit event. |
| `GET` | `/api/audit-events` | Bearer | List/filter audit events. |
| `GET` | `/health` | Anonymous | Health probe. |

---

## Auth

### `POST /api/auth/register`

Registers a farmer account. The role is always `Farmer`; a role cannot be supplied.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `email` | string | Yes | Valid email, max 255. Normalized to lowercase. |
| `password` | string | Yes | At least 8 characters and at most 72 UTF-8 bytes. |
| `fullName` | string | No | Max 200. |
| `phoneNumber` | string | No | Max 20. |

```json
{
  "email": "farmer@example.com",
  "password": "StrongPass123!",
  "fullName": "Ada Okafor",
  "phoneNumber": "+2348012345678"
}
```

**Response: `201 Created`**

```json
{
  "message": "Account created successfully. Please log in to receive your access token."
}
```

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Validation failed or email already registered. |
| `429` | More than 10 auth attempts in a minute. |

---

### `POST /api/auth/login`

**Body**

```json
{
  "email": "farmer@example.com",
  "password": "StrongPass123!"
}
```

**Response: `200 OK`**

```json
{
  "message": "Login successful.",
  "userId": "4d6f4b4e-...",
  "email": "farmer@example.com",
  "role": "Farmer",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "9f2c...",
  "expiresAt": "2026-10-23T12:00:00Z"
}
```

**Errors**

| Status | Reason |
| --- | --- |
| `401` | Invalid email or password, or inactive account. |
| `429` | Rate limit exceeded. |

The response includes a hashed-backed `refreshToken` issued by the server.

---

### `POST /api/auth/refresh`

**Body**

```json
{ "refreshToken": "9f2c..." }
```

**Response: `200 OK`**

```json
{
  "message": "Token refreshed.",
  "userId": "4d6f4b4e-...",
  "email": "farmer@example.com",
  "role": "Farmer",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "b81f...",
  "expiresAt": "2026-10-23T13:00:00Z"
}
```

The old refresh token is revoked and a new one is returned.

**Errors**

| Status | Reason |
| --- | --- |
| `401` | Invalid, expired, or revoked refresh token; inactive user. |
| `429` | Rate limit exceeded. |

Successful login now creates and returns a refresh token.

---

### `POST /api/auth/revoke`

Requires a bearer token.

Revokes all refresh tokens for the current user.

**Response: `200 OK`**

```json
{ "message": "All sessions revoked." }
```

This does not revoke the current access token; it remains valid until expiry.

---

### `POST /api/auth/change-password`

Requires a bearer token.

**Body**

```json
{
  "currentPassword": "StrongPass123!",
  "newPassword": "EvenStronger456!"
}
```

`newPassword` must be at least 8 characters (max 100 by model validation) and at most 72 UTF-8 bytes. It must differ from the current password.

**Response: `200 OK`**

```json
{ "message": "Password changed. Please sign in again." }
```

All access tokens become invalid because `token_version` is incremented.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Wrong current password, same password, or invalid new password. |
| `401` | Missing/invalid token or inactive account. |

---

## Admin

All routes require the `Admin` role.

### `POST /api/admin/vets`

Creates a `User` with role `Vet` and a linked `VetPartner` profile, then emails login credentials when email delivery is enabled.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `email` | string | Yes | Valid email, max 255. |
| `temporaryPassword` | string | No | If supplied: 8+ characters, max 72 UTF-8 bytes. If omitted, a 14-character password is generated. |
| `fullName` | string | No | Max 200. Used as the vet partner name; defaults to the email. |
| `specialization` | string | Yes | Max 100. |
| `contact` | string | Yes | Max 200. |
| `phoneNumber` | string | No | Max 20. |

```json
{
  "email": "vet@example.com",
  "temporaryPassword": null,
  "fullName": "Dr. Musa Bello",
  "specialization": "Poultry medicine",
  "contact": "+2348098765432"
}
```

**Response: `201 Created`**

```json
{
  "vetId": "4a2d...",
  "userId": "98c1...",
  "email": "vet@example.com",
  "role": "Vet",
  "specialization": "Poultry medicine",
  "credentialsEmailed": true,
  "message": "Vet doctor registered. Login credentials were emailed."
}
```

The temporary password is never returned by the API.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Validation failed, email already registered, or email delivery disabled while no temporary password was supplied. |

If email delivery fails after the account is created, the response still returns `201` with `credentialsEmailed: false`. An admin must then use the reset-credentials endpoint.

---

### `POST /api/admin/vets/{vetId}/reset-credentials`

Generates a new temporary password, emails it, revokes refresh tokens, and increments the vet user's token version.

**Body:** none.

**Response: `200 OK`**

```json
{
  "credentialsEmailed": true,
  "message": "A new temporary password was emailed to the vet."
}
```

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Email delivery is disabled. |
| `404` | Vet or linked user not found. |

If the email fails, the password has still been changed and `credentialsEmailed` is `false`.

---

### `GET /api/admin/vets`

**Query**

| Parameter | Default | Behavior |
| --- | --- | --- |
| `page` | `1` | Clamped to at least 1. |
| `pageSize` | `20` | Clamped to 1–100. |

**Response: `200 OK`**

```json
{
  "items": [
    {
      "id": "4a2d...",
      "userId": "98c1...",
      "name": "Dr. Musa Bello",
      "email": "vet@example.com",
      "specialization": "Poultry medicine",
      "contact": "+2348098765432",
      "isActive": true,
      "hasPayoutAccount": true,
      "createdAt": "2026-10-20T08:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

---

### `DELETE /api/admin/vets/{vetId}`

Deactivates the vet profile and its linked user.

**Response: `200 OK`**

```json
{ "message": "Vet deactivated." }
```

**Errors**

| Status | Reason |
| --- | --- |
| `404` | Vet not found. |

---

## Vets

### `GET /api/vets`

Any authenticated user. Returns active vets that can be assigned to a request.

**Response: `200 OK`**

```json
[
  {
    "id": "4a2d...",
    "name": "Dr. Musa Bello",
    "specialization": "Poultry medicine",
    "contact": "+2348098765432",
    "hasPayoutAccount": true
  }
]
```

The list is ordered by name and excludes inactive vets.

---

## Vet account

Requires the `Vet` role. The current user must have an active linked `VetPartner`.

### `GET /api/vet/me/payout-account`

**Response: `200 OK`**

```json
{
  "configured": true,
  "bankCode": "058",
  "accountName": "Dr. Musa Bello",
  "accountNumberMasked": "********6789",
  "country": "NG",
  "subaccountConfigured": true
}
```

Fields are `null` when not configured. The account number is always masked.

**Errors**

| Status | Reason |
| --- | --- |
| `403` | No authenticated vet account, or vet is inactive. |
| `404` | No vet profile is linked to this account. |

---

### `PUT /api/vet/me/payout-account`

Registers or updates a payout account with the payment provider as a subaccount.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `bankCode` | string | Yes | 2–20 characters. |
| `accountNumber` | string | Yes | 6–34 characters. |
| `accountName` | string | Yes | 3–150 characters. |
| `country` | string | No | Max 10; defaults to `NG`, uppercased. |

```json
{
  "bankCode": "058",
  "accountNumber": "0123456789",
  "accountName": "Dr. Musa Bello",
  "country": "NG"
}
```

**Response: `200 OK`**

```json
{
  "message": "Payout account saved.",
  "payoutAccount": {
    "configured": true,
    "bankCode": "058",
    "accountName": "Dr. Musa Bello",
    "accountNumberMasked": "********6789",
    "country": "NG",
    "subaccountConfigured": true
  }
}
```

In simulated payment mode, a stable fake subaccount ID is generated and saved without calling Flutterwave.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Invalid account details or provider rejected the account. |
| `403` | No authenticated active vet account. |
| `404` | No linked vet profile. |
| `502` | Payment provider temporarily unavailable. |

---

## Farms

### `POST /api/farms`

The owner is always the authenticated caller. An `ownerId` cannot be supplied.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `displayName` | string | Yes | Max 200. |
| `location` | string | Yes | Max 200. |

```json
{
  "displayName": "Green Valley Farm",
  "location": "Ibadan, Oyo State"
}
```

**Response: `201 Created`**

```json
{ "id": "8d4b2f6e-..." }
```

---

### `GET /api/farms/{farmId}/dashboard`

**Response: `200 OK`**

```json
{
  "farm": {
    "id": "8d4b2f6e-...",
    "ownerId": "4d6f4b4e-...",
    "displayName": "Green Valley Farm",
    "location": "Ibadan, Oyo State",
    "createdAt": "2026-10-20T08:00:00Z"
  },
  "incidents": [
    {
      "id": "8f1c...",
      "farmId": "8d4b2f6e-...",
      "livestockAnimalId": null,
      "livestockCategoryId": "1c3a2b44-...",
      "detectedAt": "2026-10-23T09:00:00Z",
      "incidentType": "AI Auto-Detected",
      "severity": 2,
      "triggeringObservations": "Observation f5d8...",
      "evidenceSummary": "Suspected Newcastle disease",
      "status": 0,
      "ruleVersion": "ai-assessment",
      "followUpDate": "2026-10-24T09:00:00Z",
      "resolvedAt": null
    }
  ],
  "actionPlan": {
    "urgent": ["Review incident 8f1c...: AI Auto-Detected"],
    "scheduled": [],
    "routine": [
      "Record daily poultry observations",
      "Check goat breeding reminders",
      "Inspect crop plots"
    ]
  },
  "vetRequests": [
    {
      "id": "3b7a...",
      "incidentId": "8f1c...",
      "requestType": 1,
      "status": 1,
      "farmerCost": 25000,
      "respondedAt": "2026-10-23T10:00:00Z"
    }
  ]
}
```

`vetRequests` is populated from the farm's vet requests. `actionPlan.routine` is a static list, not derived from farm data.

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Caller does not own the farm. |
| `404` | Farm not found. |

---

## Livestock categories

### `POST /api/livestock/categories`

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `farmId` | uuid | Yes | Must be owned by the caller. |
| `name` | string | Yes | Max 100. |
| `description` | string | No | Max 500. |

```json
{
  "farmId": "8d4b2f6e-...",
  "name": "Poultry",
  "description": "Layer hens"
}
```

**Response: `201 Created`**

```json
{ "id": "1c3a2b44-..." }
```

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Farm belongs to another user. |

---

### `GET /api/livestock/categories?farmId={farmId}`

**Response: `200 OK`**

```json
[
  {
    "id": "1c3a2b44-...",
    "farmId": "8d4b2f6e-...",
    "name": "Poultry",
    "description": "Layer hens",
    "animalCount": 120
  }
]
```

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Farm belongs to another user. |

---

## Livestock

### `POST /api/livestock/records`

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `farmId` | uuid | Yes | Must be owned by the caller. |
| `categoryId` | uuid | Yes | Must belong to the same farm. |
| `species` | integer | Yes | `LivestockSpecies` 0–5. |
| `animalName` | integer | Yes | `AnimalName` 0–9. |
| `sex` | string | Yes | Max 20; free text (for example `Female`, `Male`). |
| `approximateAge` | integer | No | Months; 0 or greater. |
| `diseaseHistory` | string | No | Free text. |
| `recentGestationDate` | datetime | No | Must not be in the future. |
| `lastVeterinaryVisit` | datetime | No | Must not be in the future. |
| `vaccinations` | string | No | Free text; comma-separated conventions. |
| `lastLactationDate` | datetime | No | Must not be in the future. |
| `parentage` | string | No | Free text. |

```json
{
  "farmId": "8d4b2f6e-...",
  "categoryId": "1c3a2b44-...",
  "species": 3,
  "animalName": 7,
  "sex": "Female",
  "approximateAge": 8,
  "diseaseHistory": null,
  "recentGestationDate": null,
  "lastVeterinaryVisit": null,
  "vaccinations": "Newcastle, week 4",
  "lastLactationDate": null,
  "parentage": null
}
```

**Response: `201 Created`**

```json
{ "id": "c1a9..." }
```

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Farm belongs to another user. |
| `404` | Category not found. |

---

### `GET /api/livestock/records?farmId={farmId}`

**Response: `200 OK`**

```json
[
  {
    "id": "c1a9...",
    "farmId": "8d4b2f6e-...",
    "categoryId": "1c3a2b44-...",
    "name": "Hen01",
    "species": "Poultry",
    "breed": null,
    "ageMonths": 8,
    "gender": "Female",
    "isActive": true,
    "createdAt": "2026-10-20T08:00:00Z",
    "diseaseHistory": null,
    "recentGestationDate": null,
    "lastVeterinaryVisit": null,
    "vaccinations": "Newcastle, week 4",
    "lastLactationDate": null,
    "parentage": null
  }
]
```

Note the response naming differences from the create request:

- Request `animalName` (integer) becomes response `name` (string).
- Request `species` (integer) becomes response `species` (string enum name).
- Request `sex` becomes response `gender`.
- Request `approximateAge` becomes response `ageMonths`.
- `breed` is stored but is not set by the create endpoint, so it is `null`.

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Caller does not own the farm. |

---

### `GET /api/livestock/species`

Anonymous. Returns the integer values of `LivestockSpecies`.

```json
[0, 1, 2, 3, 4, 5]
```

### `GET /api/livestock/names`

Anonymous. Returns the integer values of `AnimalName`.

```json
[0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
```

---

## Incidents

### `POST /api/incidents`

`detectedAt` is assigned by the server and cannot be supplied.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `farmId` | uuid | Yes | Must be owned by the caller. |
| `livestockAnimalId` | uuid | Conditional | At least one of `livestockAnimalId` or `livestockCategoryId` is required. Must belong to the farm. |
| `livestockCategoryId` | uuid | Conditional | Must belong to the farm. |
| `incidentType` | string | Yes | Max 100. |
| `severity` | integer | Yes | `Severity` 0–3. |
| `triggeringObservations` | string | Yes | Max 1000. |
| `evidenceSummary` | string | Yes | Max 1000. |
| `ruleVersion` | string | No | Optional detection-rule version. |

```json
{
  "farmId": "8d4b2f6e-...",
  "livestockAnimalId": null,
  "livestockCategoryId": "1c3a2b44-...",
  "incidentType": "Mortality spike",
  "severity": 2,
  "triggeringObservations": "Three deaths in 24 hours",
  "evidenceSummary": "Possible Newcastle disease",
  "ruleVersion": "manual"
}
```

**Response: `201 Created`**

```json
{ "id": "8f1c..." }
```

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Validation failed, or animal/category does not belong to the farm. |
| `403` | Caller does not own the farm. |
| `404` | Animal or category not found. |

---

### `GET /api/incidents/{farmId}`

**Query**

| Parameter | Default | Rules |
| --- | --- | --- |
| `page` | `1` | Must be at least 1. |
| `pageSize` | `20` | Must be 1–100. |

**Response: `200 OK`**

```json
{
  "items": [
    {
      "id": "8f1c...",
      "farmId": "8d4b2f6e-...",
      "livestockAnimalId": null,
      "livestockCategoryId": "1c3a2b44-...",
      "detectedAt": "2026-10-23T09:00:00Z",
      "incidentType": "Mortality spike",
      "severity": 2,
      "triggeringObservations": "Three deaths in 24 hours",
      "evidenceSummary": "Possible Newcastle disease",
      "status": 0,
      "ruleVersion": "manual",
      "followUpDate": null,
      "resolvedAt": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Invalid pagination. |
| `403` | Caller does not own the farm. |

---

### `POST /api/incidents/{incidentId}/status`

The route `incidentId` must equal the body `incidentId`.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `incidentId` | uuid | Yes | Must match the route. |
| `newStatus` | integer | Yes | `IncidentStatus` 1–5. `Open` (0) is rejected. |
| `followUpDate` | datetime | No | Used for `UnderMonitoring` and `FollowUpRequired`; defaults to `now + 1 day` for `FollowUpRequired`. |

```json
{
  "incidentId": "8f1c...",
  "newStatus": 3,
  "followUpDate": "2026-10-25T09:00:00Z"
}
```

**Response: `200 OK`** with empty body.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Route/body mismatch, unknown status, `Open`, or invalid follow-up date. |
| `403` | Caller does not own the farm. |
| `404` | Incident not found. |

---

## Incident actions

### `POST /api/incidents/{incidentId}/actions`

The route `incidentId` must equal the body `incidentId`.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `incidentId` | uuid | Yes | Must match the route. |
| `description` | string | Yes | Max 500. |
| `priority` | string | Yes | Max 50; free text. |

```json
{
  "incidentId": "8f1c...",
  "description": "Isolate affected birds",
  "priority": "High"
}
```

**Response: `201 Created`**

```json
{ "id": "2e4f..." }
```

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Route/body mismatch or validation failed. |
| `403` | Caller does not own the related farm. |
| `404` | Incident not found. |

---

### `GET /api/incidents/{incidentId}/actions`

**Response: `200 OK`**

```json
[
  {
    "id": "2e4f...",
    "incidentId": "8f1c...",
    "description": "Isolate affected birds",
    "priority": "High",
    "status": "Pending",
    "completedAt": null,
    "completionNotes": null
  }
]
```

There is currently no endpoint that completes an action, so `status` remains `Pending` through the API.

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Caller does not own the related farm. |
| `404` | Incident not found. |

---

## Follow-ups

### `POST /api/follow-ups`

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `incidentId` | uuid | Yes | Must exist. |
| `farmerResponse` | integer | Yes | `Outcome` 0–2. |
| `notes` | string | No | Free text. |

```json
{
  "incidentId": "8f1c...",
  "farmerResponse": 0,
  "notes": "Birds are eating again"
}
```

**Response: `201 Created`**

```json
{ "id": "7a1b..." }
```

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Caller does not own the related farm. |
| `404` | Incident not found. |

---

### `GET /api/follow-ups?incidentId={incidentId}`

**Response: `200 OK`**

```json
[
  {
    "id": "7a1b...",
    "incidentId": "8f1c...",
    "farmerResponse": 0,
    "notes": "Birds are eating again",
    "recordedAt": "2026-10-23T10:30:00Z"
  }
]
```

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Caller does not own the related farm. |
| `404` | Incident not found. |

---

## Poultry observations

These endpoints accept text and run AI extraction server-side.

### `POST /api/poultry/observations`

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `farmId` | uuid | Yes | Must be owned by the caller. |
| `livestockAnimalId` | uuid | Conditional | At least one of animal/category is required. |
| `livestockCategoryId` | uuid | Conditional | At least one of animal/category is required. |
| `rawText` | string | Yes | 1–2000 characters. |
| `idempotencyKey` | string | Yes | 1–128 characters. |
| `imageUrl` | string | No | If supplied, triggers vision analysis. |
| `preExtracted` | object | No | Internal pre-extraction payload; not part of the supported client contract. |
| `preAnalyzedVisionJson` | string | No | Internal precomputed vision JSON; not part of the supported client contract. |

If both animal and category are supplied, the category must match the animal's category.

```json
{
  "farmId": "8d4b2f6e-...",
  "livestockAnimalId": null,
  "livestockCategoryId": "1c3a2b44-...",
  "rawText": "Three hens died since yesterday and the rest look weak",
  "idempotencyKey": "checkin-2026-10-23-001"
}
```

**Response: `202 Accepted`**

```json
{ "id": "f5d8a8cb-..." }
```

`mortalityCount` extracted by the AI must be between 0 and 10,000, or the request fails with `400`.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Validation failed, invalid AI extraction, or animal/category mismatch. |
| `403` | Caller does not own the farm. |
| `404` | Farm, animal, or category not found. |

---

### `POST /api/poultry/observations/{observationId}/confirm`

Confirms the observation and runs an AI severity assessment using farm history. If the final severity is `High` or `Critical`, an incident is auto-created.

The route `observationId` must equal the body `observationId`.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `observationId` | uuid | Yes | Must match the route. |
| `correctedNotes` | string | No | Replaces the extracted farmer notes. |
| `overrideSeverity` | integer | No | `Severity` 0–3; overrides the AI severity. |
| `overrideReason` | string | No | Max 1000; recorded in incident evidence when an override occurs. |

```json
{
  "observationId": "f5d8a8cb-...",
  "correctedNotes": "Three hens died since yesterday; the rest are weak",
  "overrideSeverity": 2,
  "overrideReason": "Farmer reports rapid spread"
}
```

**Response: `200 OK`** with empty body.

The result object (AI severity, diagnosis, auto-created incident) is not returned by this endpoint. Use the chat confirm endpoint if the client needs those fields.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Route/body mismatch or validation failed. |
| `403` | Caller does not own the related farm. |
| `404` | Observation not found. |

---

## Chat observations

Chat supports text, audio, and image input in one multipart request.

### `POST /api/chat/observation`

`Content-Type: multipart/form-data`

**Form fields**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `farmId` | uuid | Yes | Must be owned by the caller. |
| `rawText` | string | Yes | Declared required by the model. When audio is supplied, the transcript replaces it. |
| `livestockAnimalId` | uuid | Conditional | At least one of animal/category is required. |
| `livestockCategoryId` | uuid | Conditional | At least one of animal/category is required. |
| `idempotencyKey` | string | No | Generated when omitted. |
| `audioFile` | file | No | Transcribed by YarnGPT; the transcript becomes `rawText`. |
| `imageFile` | file | No | Uploaded to Cloudinary and analyzed by the vision service. |

If `imageFile` is supplied, `visionAnalysis` is added to the observation.

**Response: `202 Accepted`**

```json
{
  "observationId": "b4e1...",
  "status": "Pending",
  "reused": false,
  "farmId": "8d4b2f6e-...",
  "livestockAnimalId": null,
  "livestockCategoryId": "1c3a2b44-...",
  "transcript": "Three hens died since yesterday",
  "imageUrl": "https://res.cloudinary.com/...",
  "visionAnalysis": {
    "description": "Several lethargic birds",
    "detectedIssues": ["ruffled feathers"],
    "confidence": 0.82,
    "analyzedAt": "2026-10-23T09:10:00Z"
  },
  "message": "Observation saved as pending. Review and confirm it, then optionally create an incident."
}
```

`status` is the string `Pending`, not an integer.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Missing farm, animal/category, text, or audio. |
| `403` | Farm or animal belongs to another user. |
| `404` | Farm, animal, or category not found. |

---

### `POST /api/chat/observation/{observationId}/confirm`

Runs the AI assessment and returns the full result. A repeated confirm is idempotent and does not create duplicate incidents.

**Body**

```json
{
  "correctedNotes": "Three hens died since yesterday",
  "overrideSeverity": 2,
  "overrideReason": "Rapid spread"
}
```

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `correctedNotes` | string | No | Replaces farmer notes. |
| `overrideSeverity` | integer | No | `Severity` 0–3. |
| `overrideReason` | string | No | Max 1000. |

**Response: `200 OK`**

```json
{
  "observationId": "b4e1...",
  "status": "Confirmed",
  "aiSeverity": "Medium",
  "finalSeverity": "High",
  "severityOverridden": true,
  "aiDiagnosisSummary": "Possible Newcastle disease",
  "aiRecommendedAction": "Isolate affected birds and contact a vet",
  "aiRequiresVetConsultation": true,
  "incidentAutoCreated": true,
  "autoCreatedIncidentId": "8f1c...",
  "autoCreatedIncidents": [
    {
      "id": "8f1c...",
      "farmId": "8d4b2f6e-...",
      "livestockAnimalId": null,
      "livestockCategoryId": "1c3a2b44-...",
      "detectedAt": "2026-10-23T09:15:00Z",
      "incidentType": "AI Auto-Detected",
      "severity": 2,
      "triggeringObservations": "Observation b4e1...: Possible Newcastle disease",
      "evidenceSummary": "Three hens died since yesterday",
      "status": 0,
      "ruleVersion": "ai-assessment",
      "followUpDate": null,
      "resolvedAt": null
    }
  ]
}
```

`aiSeverity`, `finalSeverity`, and `status` are strings. `autoCreatedIncidents` is usually empty or contains one incident.

Incidents are auto-created only when the **final** severity is `High` or `Critical`.

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Observation belongs to another user. |
| `404` | Observation not found. |

---

### `POST /api/chat/observation/{observationId}/create-incident`

Creates an additional incident from a confirmed observation. Farm, animal, and category are derived from the stored observation.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `severity` | integer | Yes | `Severity` 0–3. |
| `incidentType` | string | No | Defaults to `Poultry Observation`. |
| `triggeringObservations` | string | No | Defaults to `Observation {id}`. Max 1000 through command validation. |
| `evidenceSummary` | string | No | Defaults to mortality and notes. Max 1000. |
| `ruleVersion` | string | No | Optional. |

```json
{
  "severity": 2,
  "incidentType": "Suspected Newcastle",
  "triggeringObservations": "Three deaths in 24 hours",
  "evidenceSummary": "Lethargy and ruffled feathers",
  "ruleVersion": "manual"
}
```

**Response: `201 Created`**

```json
{
  "incidentId": "9a2c...",
  "message": "Incident created from observation. Vet attention recommended."
}
```

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Observation belongs to another user. |
| `404` | Observation not found. |
| `409` | Observation is still `Pending`; confirm it first. |

This endpoint does not check whether confirm already auto-created an incident, so calling it after a `High`/`Critical` confirm creates another incident.

---

## Voice

### `POST /api/voice/check-in`

`Content-Type: multipart/form-data`

Audio is transcribed by YarnGPT, then structured by Gemini/Groq.

**Query parameters**

| Parameter | Type | Required | Rules |
| --- | --- | --- | --- |
| `farmId` | uuid | Yes | Must be owned by the caller. |
| `idempotencyKey` | string | Yes | Non-empty. Reusing it returns the existing observation. |
| `livestockAnimalId` | uuid | Conditional | At least one of animal/category is required. |
| `livestockCategoryId` | uuid | Conditional | At least one of animal/category is required. |

**Form fields**

| Field | Type | Required |
| --- | --- | --- |
| `audioFile` | file | Yes |

**Response: `202 Accepted`**

```json
{
  "observationId": "f0a1...",
  "reused": false,
  "transcript": "Two goats are coughing and not eating",
  "extracted": {
    "mortalityCount": 0,
    "waterConsumption": null,
    "feedConsumption": "reduced",
    "eggProduction": null,
    "farmerNotes": "Two goats are coughing and not eating"
  },
  "creditsCharged": 1
}
```

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Missing audio, idempotency key, farm, or animal/category. |
| `403` | Farm or animal belongs to another user. |
| `404` | Farm, animal, or category not found. |
| `502` | Speech-to-text or AI provider failure. |

---

### `POST /api/voice/image-check-in`

`Content-Type: multipart/form-data`

**Query parameters**

| Parameter | Type | Required | Rules |
| --- | --- | --- | --- |
| `farmId` | uuid | Yes | Must be owned by the caller. |
| `idempotencyKey` | string | Yes | Non-empty. |
| `livestockAnimalId` | uuid | Conditional | At least one of animal/category is required. |
| `livestockCategoryId` | uuid | Conditional | At least one of animal/category is required. |
| `farmerNotes` | string | No | Combined with vision output for extraction. |

**Form fields**

| Field | Type | Required |
| --- | --- | --- |
| `imageFile` | file | Yes |

**Response: `202 Accepted`**

```json
{
  "observationId": "cc12...",
  "reused": false,
  "imageUrl": "https://res.cloudinary.com/...",
  "visionAnalysis": {
    "description": "Crowded poultry pen with wet litter",
    "detectedIssues": ["wet litter", "overcrowding"],
    "confidence": 0.78
  },
  "extracted": {
    "mortalityCount": 0,
    "waterConsumption": null,
    "feedConsumption": null,
    "eggProduction": null,
    "farmerNotes": "Pen looks wet"
  }
}
```

**Errors**

Same as voice check-in.

---

### `POST /api/voice/respond`

Converts vet instructions to speech.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `text` | string | Yes | Non-empty. |
| `voice` | string | No | Defaults to `umar`. |
| `targetLanguage` | string | No | For example `yoruba` or `pidgin`. |

```json
{
  "text": "Isolate the affected birds and give clean water.",
  "voice": "umar",
  "targetLanguage": "pidgin"
}
```

**Response: `200 OK`**

```json
{
  "audioUrl": "https://.../response.mp3",
  "creditsCharged": 1
}
```

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Missing text. |
| `502` | Speech synthesis unavailable. |

---

### `GET /api/voice/voices`

**Response: `200 OK`**

```json
["umar", "chioma", "tunde"]
```

The exact list depends on the YarnGPT provider.

**Errors**

| Status | Reason |
| --- | --- |
| `502` | Voice provider unavailable. |

---

## Vet coordination

### `POST /api/vet-coordination/incidents/{incidentId}/vet-request`

Creates a vet request for an incident. The route `incidentId` must equal the body `incidentId`.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `incidentId` | uuid | Yes | Must match the route. |
| `requestType` | integer | Yes | `RequestType` 0–2. |
| `farmerCost` | decimal | Yes | 0 or greater, in NGN. |
| `vetPartnerId` | uuid | No | If supplied, the vet must exist and be active. |

```json
{
  "incidentId": "8f1c...",
  "requestType": 1,
  "farmerCost": 25000,
  "vetPartnerId": "4a2d..."
}
```

**Response: `202 Accepted`**

```json
{ "id": "3b7a..." }
```

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Route/body mismatch, validation failed, or selected vet unavailable. |
| `403` | Caller cannot access the incident. |
| `404` | Incident not found. |

---

### `POST /api/vet-coordination/vet-requests/{vetRequestId}/respond`

Marks a request as `Responded`. Requires the `Vet` or `Admin` role.

Before responding, the request must have a `Completed` payment when `farmerCost > 0`.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `vetRequestId` | uuid | Yes | Must match the route. |
| `vetPartnerId` | uuid | No | Optional. A vet may only supply their own partner id; it is inferred when omitted. |

```json
{
  "vetRequestId": "3b7a...",
  "vetPartnerId": null
}
```

**Response: `200 OK`** with empty body.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Route/body mismatch or validation failed. |
| `403` | Not a vet/admin, no active vet profile, or request assigned to another vet. |
| `404` | Vet request not found. |
| `409` | Payment is required but not completed. |

Unassigned requests may be answered by any active vet. Once assigned, only that vet or an admin may respond.

---

### `POST /api/vet-coordination/vet-requests/{vetRequestId}/cost-share`

Records a contribution toward the cost. The route `vetRequestId` must equal the body `vetRequestId`.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `vetRequestId` | uuid | Yes | Must match the route. |
| `payerType` | string | Yes | Non-empty; free text such as `farmer`, `coop`, `insurer`. |
| `amount` | decimal | Yes | 0 or greater. |

```json
{
  "vetRequestId": "3b7a...",
  "payerType": "coop",
  "amount": 10000
}
```

**Response: `200 OK`** with empty body.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Route/body mismatch or validation failed. |
| `403` | Caller cannot access the vet request. |
| `404` | Vet request not found. |

---

### `POST /api/vet-coordination/incidents/{incidentId}/prevention-plan`

Creates a prevention plan. The route `incidentId` must equal the body `incidentId`.

Farm owners can create prevention plans; a vet without farm ownership cannot.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `incidentId` | uuid | Yes | Must match the route. |
| `content` | string | Yes | 1–2000 characters. |
| `generatedFromHistory` | boolean | No | Defaults to `true`. |

```json
{
  "incidentId": "8f1c...",
  "content": "Vaccinate new birds and quarantine arrivals for 14 days.",
  "generatedFromHistory": true
}
```

**Response: `201 Created`**

```json
{ "id": "d7c4..." }
```

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Route/body mismatch or validation failed. |
| `403` | Caller does not own the related farm. |
| `404` | Incident not found. |

---

### `GET /api/vet-coordination/incidents/{incidentId}/prevention-plan`

**Response: `200 OK`**

```json
{
  "id": "d7c4...",
  "incidentId": "8f1c...",
  "content": "Vaccinate new birds and quarantine arrivals for 14 days.",
  "generatedFromHistory": true
}
```

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Caller cannot access the incident. |
| `404` | No prevention plan exists, or the incident was not found. |

---

## Treatment plans

### `POST /api/treatment-plans`

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `vetRequestId` | uuid | Yes | Must exist and be accessible. |
| `authoredByVetId` | string | Yes | 1–255 characters. For non-admin vets, this must be the authenticated vet's profile id and it is always overwritten server-side. An admin may supply another vet id, but it is normalized when the admin also has a vet profile. |
| `instructionsText` | string | Yes | 1–2000 characters. |
| `language` | string | No | Max 10. |

```json
{
  "vetRequestId": "3b7a...",
  "authoredByVetId": "4a2d...",
  "instructionsText": "Isolate affected birds and administer the prescribed antibiotic for five days.",
  "language": "en"
}
```

**Response: `201 Created`**

```json
{ "id": "ee91..." }
```

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Validation failed. |
| `403` | Caller cannot access the related incident, or the request is assigned to another vet. |
| `404` | Vet request not found. |

---

### `GET /api/treatment-plans/{id}`

**Response: `200 OK`**

```json
{
  "id": "ee91...",
  "vetRequestId": "3b7a...",
  "authoredByVetId": "4a2d...",
  "instructionsText": "Isolate affected birds and administer the prescribed antibiotic for five days.",
  "language": "en"
}
```

**Errors**

| Status | Reason |
| --- | --- |
| `403` | Caller cannot access the related incident. |
| `404` | Treatment plan or vet request not found. |

There is no endpoint to list treatment plans for a vet request.

---

## Payments

Payments are intended to be **pay-before-visit**:

1. Farmer creates a vet request with `farmerCost`.
2. Farmer initiates payment.
3. Farmer completes checkout with Flutterwave (or the simulated flow).
4. Payment is recorded directly or by webhook.
5. Vet responds only after a `Completed` payment exists.

### Amount semantics

| Field | Meaning |
| --- | --- |
| `farmerCost` | The vet's charge. |
| `platformCommission` | 5% of `farmerCost`. |
| `totalAmount` | `farmerCost + platformCommission` (105% of the vet charge). |
| `vetPayoutAmount` | The full vet charge. |

Example: `farmerCost = 25,000 NGN` → commission `1,250`, total `26,250`, vet payout `25,000`.

### `POST /api/payments/initiate`

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `vetRequestId` | uuid | Yes | Must be accessible to the caller. |
| `amount` | decimal | Yes | Must equal the vet request's `farmerCost`. |
| `currency` | string | Yes | 3–10 characters; must match the vet request currency (`NGN`). |

**Headers**

| Header | Required | Rules |
| --- | --- | --- |
| `Idempotency-Key` | No | Max 128 characters. Reusing the same key with the same vet request and details returns the existing pending payment instead of creating another one. |

```json
{
  "vetRequestId": "3b7a...",
  "amount": 25000,
  "currency": "NGN"
}
```

**Response: `200 OK`**

```json
{
  "transactionReference": "FH_3b7a..._1761210000_a1b2c3d4e5f6_9f8e7d6c5b4a3210",
  "checkoutUrl": null,
  "platformCommission": 1250,
  "totalAmount": 26250,
  "vetPayoutAmount": 25000,
  "vetSubaccountUsed": true,
  "subaccountId": "4df9...",
  "idempotencyKey": "a1b2c3d4e5f6"
}
```

If no `Idempotency-Key` header is sent, the server generates one and returns it. Clients should store and reuse it when retrying the same payment initiation.

A vet request can have only one pending payment. Initiating again without a reuse of the same idempotency key returns the existing pending payment when the amount and currency match; otherwise it returns `409`.

In **live mode**, `checkoutUrl` is the Flutterwave checkout link and `transactionReference` is the Flutterwave transaction ID.

In **simulated mode** (`Flutterwave:Mode` is not `Live`):

- No Flutterwave call is made.
- `checkoutUrl` is `null`.
- `transactionReference` is a signed local reference.
- `POST /api/payments/record` accepts that reference.

If the assigned vet has a payout account, the payment is split automatically and `vetSubaccountUsed` is `true`.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Validation failed, or amount/currency does not match the vet request. |
| `403` | Vet request belongs to another farm, or no customer email is available. |
| `404` | Vet request not found. |
| `409` | The vet request is already completed or has a completed payment, or the idempotency key was reused with different details or a non-pending payment; another pending payment with different amount/currency exists for the same request. |
| `502` | Payment provider unavailable or rejected the request. |

---

### `POST /api/payments/record`

Verifies the payment with the provider and marks it completed.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `vetRequestId` | uuid | Yes | Must match the initiated payment. |
| `amount` | decimal | Yes | Must equal the initiated `totalAmount` (including commission). |
| `currency` | string | Yes | Must match the initiated currency. |
| `transactionReference` | string | Yes | The reference returned by initiate. Max 200. |

```json
{
  "vetRequestId": "3b7a...",
  "amount": 26250,
  "currency": "NGN",
  "transactionReference": "FH_3b7a..._1761210000_a1b2c3d4e5f6_9f8e7d6c5b4a3210"
}
```

**Response: `200 OK`**

```json
{ "status": "paid" }
```

Repeating a successful record is idempotent.

**Errors**

| Status | Reason |
| --- | --- |
| `400` | Validation failed or amount/currency mismatch. |
| `403` | Vet request belongs to another farm. |
| `404` | Unknown payment reference or vet request. |
| `409` | Payment is not pending or verified amount/currency does not match. |
| `502` | Provider verification failed or unavailable. |

---

### `POST /api/payments/webhook`

Anonymous, but authenticated by a signature.

Flutterwave posts charge events here. Supported success events:

- `charge.completed`
- `payment.completed`

with `data.status == "successful"`.

**Headers**

| Header | Verification |
| --- | --- |
| `flutterwave-signature` | Base64-encoded HMAC-SHA256 of the raw request body, keyed with `Flutterwave:WebhookSecretHash`. |
| `verif-hash` | Compared directly with `Flutterwave:WebhookSecretHash`. |

At least one header is required.

**Behavior**

- If `Flutterwave:WebhookSecretHash` is not configured: `503` (fail-closed).
- Invalid or missing signature: `401` (empty body).
- Invalid JSON after a valid signature: `400` with `{ "error": "Invalid webhook payload." }`.
- Successful charge: verifies the transaction, finds the pending payment by Flutterwave ID or `tx_ref`, checks the verified amount and currency against the initiated payment, and only then marks it `Completed`.
- Other events: `200` and ignored.

**Response: `200 OK`** with empty body.

The webhook does not use the caller's bearer token.

---

## Audit events

### `POST /api/audit-events`

Admin-restricted. The authenticated administrator's user id is always used as the actor id.

**Body**

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `actorId` | string | Yes | 1–255 characters, accepted for compatibility but ignored. |
| `eventType` | string | Yes | 1–100 characters. |
| `relatedRecordId` | uuid | No | Optional. |
| `details` | string | No | Max 1000. |

```json
{
  "actorId": "4d6f4b4e-...",
  "eventType": "incident.reviewed",
  "relatedRecordId": "8f1c...",
  "details": "Farmer reviewed the incident"
}
```

**Response: `201 Created`**

```json
{ "id": "ab12..." }
```

---

### `GET /api/audit-events`

Any authenticated user can read audit events.

**Query**

| Parameter | Required | Behavior |
| --- | --- | --- |
| `actorId` | No | Filters by actor id. Ignored when `eventType` is supplied. |
| `eventType` | No | Filters by event type; takes precedence over `actorId`. |

With neither filter, all events are returned.

**Response: `200 OK`**

```json
[
  {
    "id": "ab12...",
    "actorId": "4d6f4b4e-...",
    "eventType": "incident.reviewed",
    "relatedRecordId": "8f1c...",
    "timestamp": "2026-10-23T10:45:00Z",
    "details": "Farmer reviewed the incident"
  }
]
```

Writes are restricted to admins; reads remain available to any authenticated user. `actorId` is never trusted from the payload on write.

---

## Health

### `GET /health`

Anonymous.

**Response: `200 OK`**

```json
{
  "status": "healthy",
  "timestamp": "2026-10-23T10:50:00Z"
}
```

This endpoint does not check the database or external providers.

---

## Data models

### FarmDto

| Field | Type |
| --- | --- |
| `id` | uuid |
| `ownerId` | string |
| `displayName` | string |
| `location` | string |
| `createdAt` | datetime |

### IncidentDto

| Field | Type |
| --- | --- |
| `id` | uuid |
| `farmId` | uuid |
| `livestockAnimalId` | uuid? |
| `livestockCategoryId` | uuid? |
| `detectedAt` | datetime |
| `incidentType` | string |
| `severity` | integer |
| `triggeringObservations` | string |
| `evidenceSummary` | string |
| `status` | integer |
| `ruleVersion` | string? |
| `followUpDate` | datetime? |
| `resolvedAt` | datetime? |

### VetRequestDto

| Field | Type |
| --- | --- |
| `id` | uuid |
| `incidentId` | uuid |
| `requestType` | integer |
| `status` | integer |
| `farmerCost` | decimal |
| `respondedAt` | datetime? |

### ActionPlanDto

| Field | Type |
| --- | --- |
| `urgent` | string[] |
| `scheduled` | string[] |
| `routine` | string[] |

### LivestockCategoryDto

| Field | Type |
| --- | --- |
| `id` | uuid |
| `farmId` | uuid |
| `name` | string |
| `description` | string? |
| `animalCount` | integer |

### LivestockRecordDto

| Field | Type |
| --- | --- |
| `id` | uuid |
| `farmId` | uuid |
| `categoryId` | uuid |
| `name` | string |
| `species` | string |
| `breed` | string? |
| `ageMonths` | integer? |
| `gender` | string? |
| `isActive` | boolean |
| `createdAt` | datetime |
| `diseaseHistory` | string? |
| `recentGestationDate` | datetime? |
| `lastVeterinaryVisit` | datetime? |
| `vaccinations` | string? |
| `lastLactationDate` | datetime? |
| `parentage` | string? |

### FollowUpDto

| Field | Type |
| --- | --- |
| `id` | uuid |
| `incidentId` | uuid |
| `farmerResponse` | integer |
| `notes` | string? |
| `recordedAt` | datetime |

### IncidentActionDto

| Field | Type |
| --- | --- |
| `id` | uuid |
| `incidentId` | uuid |
| `description` | string |
| `priority` | string |
| `status` | string |
| `completedAt` | datetime? |
| `completionNotes` | string? |

`status` is `"Pending"` in the current API.

### TreatmentPlanDto

| Field | Type |
| --- | --- |
| `id` | uuid |
| `vetRequestId` | uuid |
| `authoredByVetId` | string |
| `instructionsText` | string |
| `language` | string? |

### PreventionPlanDto

| Field | Type |
| --- | --- |
| `id` | uuid |
| `incidentId` | uuid |
| `content` | string |
| `generatedFromHistory` | boolean |

### AuditEventDto

| Field | Type |
| --- | --- |
| `id` | uuid |
| `actorId` | string |
| `eventType` | string |
| `relatedRecordId` | uuid? |
| `timestamp` | datetime |
| `details` | string? |

### PagedResult\<T\>

| Field | Type |
| --- | --- |
| `items` | array |
| `page` | integer |
| `pageSize` | integer |
| `totalCount` | integer |
| `totalPages` | integer |

---

## End-to-end workflows

### Farmer onboarding

1. `POST /api/auth/register`
2. `POST /api/auth/login`
3. `POST /api/farms`
4. `POST /api/livestock/categories`
5. `POST /api/livestock/records`
6. `GET /api/farms/{farmId}/dashboard`

### Observation to incident

1. `POST /api/poultry/observations` or `POST /api/chat/observation`
2. Confirm with the matching confirm endpoint.
3. If severity is `High` or `Critical`, an incident is created automatically.
4. Optionally call `POST /api/chat/observation/{id}/create-incident` to add another incident.
5. `GET /api/incidents/{farmId}` to list incidents.
6. `POST /api/incidents/{incidentId}/status` to acknowledge, monitor, require follow-up, resolve, or reopen.

### Vet request and payment

1. Farmer creates an incident.
2. Farmer calls `POST /api/vet-coordination/incidents/{incidentId}/vet-request`.
3. Farmer calls `POST /api/payments/initiate` with the request's `farmerCost`.
4. Farmer completes checkout (`checkoutUrl`) or, in simulated mode, uses the returned reference.
5. Client calls `POST /api/payments/record` with the **total amount**.
6. Flutterwave may also call `POST /api/payments/webhook`.
7. Vet calls `POST /api/vet-coordination/vet-requests/{vetRequestId}/respond`.
8. Vet (or farmer) creates a treatment plan with `POST /api/treatment-plans`.

### Vet payout setup

1. Admin registers the vet with `POST /api/admin/vets`.
2. Vet logs in and calls `PUT /api/vet/me/payout-account`.
3. Future payments to requests assigned to that vet include a Flutterwave split.

---

## Configuration

Configuration is read from `appsettings.json`, environment variables, or user secrets. The tracked reference file is `src/FarmHand.API/appsettings.example.json`.

| Key | Purpose | Notes |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection | Required. |
| `Jwt:SecretKey` | JWT signing key | Required, at least 32 bytes, must not contain `CHANGE_ME`. The app fails to start otherwise. |
| `Jwt:Issuer` | JWT issuer | Defaults to `FarmHand`. |
| `Jwt:Audience` | JWT audience | Defaults to `FarmHandUsers`. |
| `Jwt:ExpirationMinutes` | Access-token lifetime | `60` in the example. |
| `Flutterwave:Mode` | `Live` or any other value | Any value other than `Live` uses the simulated payment provider. |
| `Flutterwave:SecretKey` | Flutterwave API secret | Required in live mode. |
| `Flutterwave:PublicKey` | Flutterwave public key | Stored for provider use. |
| `Flutterwave:SigningSecret` | Local payment-reference signing | Falls back to `Jwt:SecretKey` when absent. |
| `Flutterwave:WebhookSecretHash` | Webhook signature secret | Required for webhook verification; without it the webhook returns `503`. |
| `Flutterwave:BaseUrl` | Flutterwave API base | Defaults to `https://api.flutterwave.com/v3`. |
| `Flutterwave:RedirectUrl` | Checkout return URL | Sent to Flutterwave. |
| `Brevo:Enabled` | Enable email delivery | Must be `true` for credential emails. |
| `Brevo:ApiKey` | Brevo API key | Required when enabled. |
| `Brevo:BaseUrl` | Brevo API base | Defaults to `https://api.brevo.com/v3`. |
| `Brevo:FromAddress` | Sender address | Required when enabled; must be verified in Brevo. |
| `Brevo:FromName` | Sender display name | Example: `FarmHand`. |
| `Gemini:ApiKey` | Gemini API key | Used for extraction and severity assessment. |
| `Groq:ApiKey` | Groq API key | Used for extraction fallback and vision analysis. |
| `YarnGpt:ApiKey` | YarnGPT API key | Used for speech-to-text, text-to-speech, and voices. |
| `Cloudinary:CloudName` | Cloudinary cloud name | Used for observation images. |
| `Cloudinary:ApiKey` | Cloudinary API key | Required for uploads. |
| `Cloudinary:ApiSecret` | Cloudinary API secret | Required for uploads. |
| `Database:MigrateOnStartup` | Run EF migrations on boot | Development always migrates and seeds. Production migrates only when `true`. |
| `Seed:Password` | Demo password | Used by the Development seeder. |
| `Seed:AdminEmail` | Demo admin email | Defaults to `admin@farmhand.ai`. |
| `Seed:FarmerEmail` | Demo farmer email | Defaults to `farmer@farmhand.ai`. |
| `Seed:VetEmail` | Demo vet email | Defaults to `vet@farmhand.ai`. |
| `AllowedHosts` | ASP.NET host filtering | `*` in the example. |

### Development seed accounts

When `ASPNETCORE_ENVIRONMENT=Development`, startup migrates the database and seeds demo users using `Seed:Password`:

| Role | Email |
| --- | --- |
| Admin | `admin@farmhand.ai` |
| Farmer | `farmer@farmhand.ai` |
| Vet | `vet@farmhand.ai` |

Do not use the demo password in production.

### Email delivery

Brevo is used for vet credential emails:

- If `Brevo:Enabled` is `false`, admin vet registration requires a `temporaryPassword` and reset-credentials returns `400`.
- If delivery fails after account creation, the account exists but the password may be unknown; use reset-credentials once email works.
- Emails are HTML with a plain-text alternative.
- Sender address must be verified by Brevo.

---

## Production checklist

- Set a strong `Jwt:SecretKey` (32+ random bytes). The app refuses to start otherwise.
- Set `Flutterwave:Mode` to `Live` and provide live `SecretKey`, `PublicKey`, `SigningSecret`, `WebhookSecretHash`, `BaseUrl`, and `RedirectUrl`.
- Register the webhook URL with Flutterwave and configure the same secret hash.
- Set `Brevo:Enabled=true`, a valid API key, and a verified `FromAddress`.
- Set AI and Cloudinary credentials.
- Decide whether `Database:MigrateOnStartup` should run in production. Development always migrates and seeds.
- Serve over HTTPS; the app calls `UseHttpsRedirection`.
- Configure CORS at the host/proxy level if the frontend is on another origin. The app has no CORS policy.
- Swagger is only enabled in Development.
- Back up the database before applying migrations.

---

## Known limitations

These are current implementation constraints, not frontend bugs.

1. **Login returns a refresh token; the refresh token is stored only as an SHA-256 hash.** This is safe.
2. **`POST /api/auth/revoke` does not invalidate the current access token.** It revokes refresh tokens only. Use change-password to invalidate all JWTs, or wait for expiry.
3. **No vet-request completion endpoint.** `VetRequest.Complete()` exists, but no API calls it, so `RequestStatus.Completed` is unreachable through HTTP.
4. **No observation read endpoints.** Observations can be created and confirmed, but not listed or fetched.
5. **No payment read endpoints.** Payments can be initiated and recorded, but not listed.
6. **No treatment-plan list endpoint.** Only create and get-by-id are exposed.
7. **No incident-action completion endpoint.** Actions are created and listed, but remain `Pending`.
8. **No vet-request list endpoint.** Vet requests appear only in the dashboard.
9. **Audit event creation is admin-only; reads remain available to any authenticated user.** `actorId` is taken from the JWT, not the payload.
10. **Payout account numbers are stored unencrypted.** They are masked in API responses, but not encrypted at rest.
11. **`MustChangePassword` is not enforced.** Temporary vet passwords work until the vet changes them.
12. **Treatment-plan authorship is not verified.** `authoredByVetId` is a free-text string supplied by the caller.
13. **No CORS policy.** Cross-origin browser clients will fail unless CORS is added or the SPA is served from the same origin.
14. **Simulated payments do not move money.** `checkoutUrl` is `null`, and locally signed references are accepted by `/api/payments/record`.
15. **Vets have narrow access.** Most farm-scoped endpoints return `403` for vets; only assigned vet requests, related incidents, prevention plans, treatment plans, and payments are accessible.
16. **The dashboard DTO comment is stale.** `DashboardDto.VetRequests` is documented in code as “always empty”, but the handler populates it.
17. **No automated tests.** Verification is currently limited to builds and manual smoke tests.
18. **`POST /api/poultry/observations` binds internal pre-extraction fields.** `preExtracted` and `preAnalyzedVisionJson` are accepted by the public command model even though they are meant for internal callers.
