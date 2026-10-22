# FarmHand Code Audit — v2

**Repository:** Team-HackForge
**Commit audited:** `3230f5f` (`master`, clean working tree)
**Date:** 2026-10-22
**Scope:** API, Application, Domain, Infrastructure, migrations, configuration, tests
**Method:** Static review of all C# sources and project files, plus `dotnet build FarmHand.slnx` (0 warnings, 0 errors) and `dotnet test` (2 placeholder tests pass).

---

## Verdict

The architecture is sound and the merge fixed several v1 findings: real bcrypt login, JWT fail-fast, enforced idempotency lookup, signed Cloudinary requests, EF foreign keys, and DI registration. However, the **primary ingestion paths are broken end-to-end**, authorization is still authentication-only, and the payment and admin workflows cannot complete. This is not demo-ready without the critical fixes below.

| Severity | Count |
|---|---|
| Critical | 9 |
| High | 11 |
| Medium | 12 |
| Low | 8 |

---

## Fixed since v1

- Token issuance now exists with bcrypt verification (`AuthController`, `DemoDataSeeder` aside).
- Program fails fast when `Jwt:SecretKey` is missing; the hardcoded fallback was removed from `Program.cs`.
- Observation idempotency keys are persisted and looked up.
- Cloudinary uploads now attempt request signing (the signature is still incorrect, C8).
- `IVisionAnalysisService` / `IGroqVisionService` / `IDemoDataSeeder` are registered.
- FK constraints and unique indexes added for farms, livestock, observations, incidents, vet requests, payments.
- Global exception handler prevents raw exception text from reaching clients.
- Swagger/OpenAPI and `API.md` delivered.

---

## Critical issues

### C1. Voice and image check-ins always fail validation and FK constraints

`VoiceController.cs:85-90`, `VoiceController.cs:180-187`, `RecordObservationCommand.cs:112`, `PoultryObservationConfiguration.cs:24-27`

Both endpoints send `Guid.Empty` as `LivestockAnimalId` and `null` as `LivestockCategoryId`. The validator requires a non-empty animal when the category is null, so FluentValidation throws; if it did not, `PoultryObservations.LivestockAnimalId` is a required FK, so the insert would fail. The flagship voice feature returns 500 on every request.

**Fix:** make `LivestockAnimalId` nullable (or model flock-level observations with a required `FlockId`/category), update the validator, the FK, and the migration; or resolve the category/animal from the request (`flockId` is currently accepted but discarded).

### C2. Chat incident creation writes an observation id into `FlockId`

`ChatController.cs:169-178`

`CreateIncidentFromObservation` passes `observationId` as the `flockId` argument, with a comment acknowledging it. `Incidents.FlockId` has an FK to `Flock`, so the insert fails with a 500. There is also no lookup of the observation, farm, or ownership.

**Fix:** load the observation and its animal/category, derive the farm and flock/animal from it, validate ownership, then create the incident.

### C3. Incidents require a `Flock` that nothing can create

`IncidentConfiguration.cs:24-27`, migration `20261021213428_LivestockRestructure.cs`, `DemoDataSeeder.cs`

The livestock restructure moved observations to `LivestockAnimal`/`LivestockCategory`, but incidents still require a `Flock`. There is no Flock repository, controller, endpoint, or seed row. Consequently, clients cannot obtain a valid `flockId`, and every incident creation is a guess.

**Fix:** migrate incidents to `LivestockAnimalId`/`LivestockCategoryId`, or reintroduce a Flock aggregate with endpoints and seed data.

### C4. Livestock creation never persists

`CreateLivestockRecordCommand.cs:42`, `EfRepository.cs:31-33`

The handler calls `AddAsync` and returns the id, but never calls `SaveChangesAsync`. `AddAsync` only attaches the entity. The row is not committed and the returned id is not retrievable. `GoatsController` still returns 201 and misleading docs.

**Fix:** inject `IUnitOfWork` and call `SaveChangesAsync`, or use `IUnitOfWork.LivestockAnimals`.

### C5. Vet deactivation deactivates the wrong user (or none)

`AdminController.cs:96-105`

The code looks up the user by `vet.Name` (a display name) through `GetByEmailAsync`, so it almost always finds nothing. `VetPartner` has no `UserId`/email link, and `IsActive` is toggled through reflection instead of a domain method.

**Fix:** add `UserId`/`Email` to `VetPartner`, create a proper `Deactivate()` method, and deactivate by foreign key in the same transaction.

### C6. The only Admin account cannot log in, and no API can create one

`DemoDataSeeder.cs:63`

The seeded admin password hash is the literal `$2a$11$hashedpassword`, which BCrypt cannot verify. Registration only creates `Farmer`; `AdminController` only creates `Vet`. `[Authorize(Roles = "Admin")]` endpoints are therefore unreachable.

**Fix:** seed a real bcrypt hash from configuration or a startup secret, and provide a controlled admin bootstrap path.

### C7. Payments cannot complete, and verification is incomplete

`NombaPaymentService.cs:34-43`, `NombaPaymentService.cs:45-88`, `RecordPaymentCommand.cs:36-49`, `PaymentConfiguration.cs:20`

`InitiatePayment` fabricates a reference locally without contacting Nomba. `RecordPayment` then requires that reference to verify successfully against Nomba, which cannot succeed for a fabricated reference. Additionally, the verified amount/currency is never compared with the requested amount, and replaying a reference hits the unique index and returns 500.

**Fix:** call the real initiation endpoint and persist a pending payment, verify and compare amount/currency, add an idempotency key, and map duplicate references to 409.

### C8. Cloudinary upload signature omits signed parameters

`CloudinaryService.cs:43-57`

The signature covers `folder` and `timestamp`, but the request also sends `quality=auto` and `fetch_format=auto`. Cloudinary requires every non-excluded parameter to be part of the signature, so uploads are rejected as invalid.

**Fix:** include `quality` and `fetch_format` in the signed, alphabetically ordered parameter string (and keep them out of the multipart body only if intentionally unsigned, which is not supported).

### C9. Migration history is a single baseline

`src/FarmHand.Infrastructure/Persistence/Migrations/`

Only `20261021213428_LivestockRestructure` exists and it creates every table from scratch. Any database that applied earlier migrations (including the previously committed `FixAuditIssues`) will fail at startup because the tables already exist, and `__EFMigrationsHistory` will not match.

**Fix:** restore the full migration chain, or explicitly document and script a database reset for non-production environments.

---

## High issues

### H1. Authentication is not authorization

- `FarmsController` trusts `OwnerId` from the body; any authenticated user can create a farm for anyone and read any dashboard (`FarmsController.cs:22-57`).
- `Incidents`, `Poultry`, `Voice`, `Chat`, `Payments`, `Goats` have no ownership checks.
- `VetCoordination` allows any authenticated user to respond as a vet, choose any `VetPartnerId`, and set cost shares (`VetCoordinationController.cs:49-77`).
- `User.FarmId` exists but is never set or enforced.
- Only `/api/admin/*` checks a role, and that role is unobtainable (C6).

**Fix:** derive the owner from `ClaimTypes.NameIdentifier`, add farm membership checks, require `Vet`/`Admin` roles for vet actions, and filter every query by the caller's farm.

### H2. Validation and not-found errors still return 500

`Program.cs:141-155`, `ValidationBehavior.cs:27`

FluentValidation throws `ValidationException`, and handlers throw `KeyNotFoundException`; the global handler maps everything to a generic 500. `API.md` and the controllers claim 400/404.

**Fix:** add exception mapping (`ValidationException` → 400 ProblemDetails with `errors`, `KeyNotFoundException` → 404, `InvalidOperationException` from domain → 409), and keep 500 for unknown failures.

### H3. Login and registration are unhardened

`AuthController.cs:37-89`

No email format, password policy, rate limiting, lockout, refresh, or revocation. Duplicate registration races produce a 500 from the unique index. Default token lifetime is 7 days.

**Fix:** add FluentValidation, `AddRateLimiter` on auth routes, a lockout counter, and short-lived access tokens with refresh.

### H4. Predictable JWT secret in the example configuration

`appsettings.example.json`, `JwtService.cs:20`

The example ships the exact fallback secret, and `JwtService` still silently defaults to it. Developers who copy the example will run with a public key; if the example is deployed, tokens are forgeable.

**Fix:** replace the example with `CHANGE_ME`, remove the fallback entirely, and require at least 32 random bytes.

### H5. Automatic migration at startup in every environment

`Program.cs:114-125`

`MigrateAsync` runs before the environment check, despite the comment saying "Development only". Production deploys apply schema changes implicitly; a failed migration takes the app down.

**Fix:** gate behind `IsDevelopment()` or a feature flag, and run migrations as a deployment step.

### H6. Voice image analysis bypasses DI and leaks resources

`VoiceController.cs:222-285`

The controller duplicates `GroqVisionService`, creates a new `HttpClient` per request (never disposed), reads `GROQ_API_KEY`/`YARN_GPT_API_KEY` from environment variables instead of configuration, and has an unused `apiKey` variable. Sockets are exhausted under load.

**Fix:** use `IVisionAnalysisService`, which is already registered.

### H7. Rule engine, cost-share, and transaction abstractions are dead

`IPoultryRuleEngine.cs`, `ICostShareCalculator.cs`, `TransactionBehavior.cs`

Interfaces are declared but have no implementations or callers. Observations never trigger rule evaluation, so incidents are manual only. `TransactionBehavior` is a no-op and is not registered.

**Fix:** implement or delete; if incident generation is intended, wire the rule engine into observation confirmation.

### H8. Chat preview and confirm trust client input

`ChatController.cs:44-155`

The preview accepts any `livestockAnimalId`/`livestockCategoryId` without verifying existence or ownership; confirm accepts client-supplied extracted values and arbitrary `VisionAnalysisJson`. There is no rate limit on AI calls. A client can fabricate observations and burn provider credits.

**Fix:** validate animal/category membership, ignore client-supplied extracted values (or mark them as farmer-corrected), and rate-limit AI endpoints.

### H9. Vet registration is non-atomic and unlinked

`AdminController.cs:46-55`, `VetPartner.cs`

User and VetPartner are saved in two separate `SaveChanges` calls. A failure leaves an orphan user. There is no `UserId` on `VetPartner`, so the two records cannot be related or safely deactivated.

**Fix:** persist both in one transaction (or one aggregate), add `UserId`, and use it for deactivation.

### H10. Tests are placeholders

`tests/FarmHand.UnitTests/UnitTest1.cs`, `tests/FarmHand.IntegrationTests/UnitTest1.cs`

Both tests are empty and assert nothing; `dotnet test` reports 2 passing tests that verify no behavior. There is no coverage of domain invariants, validation, idempotency, auth, or the payment flow.

**Fix:** add unit tests for `Incident` transitions, `Money`, validators, and idempotency, plus integration tests with `WebApplicationFactory` and a disposable PostgreSQL container.

### H11. Seed guard hides partial databases

`DemoDataSeeder.cs:19`

`if (_dbContext.Farms.Any()) return;` means a database with any farm but missing users, categories, or vets is never repaired. Seeding also assigns ids through reflection.

**Fix:** check each seed set independently and use constructor/factory ids.

---

## Medium issues

### M1. No soft-delete query filters

`BaseEntity.MarkDeleted`, `EfRepository` — `IsDeleted` is never filtered globally and `GetByIdAsync` uses `FindAsync`, which returns deleted rows. Some repositories filter, others do not.

### M2. No pagination

Incidents, observations, vets, and dashboard incidents are returned unbounded.

### M3. Idempotency is check-then-insert

`RecordObservationCommand.cs:47-52` + unique index. Two concurrent requests with the same key can both pass the check; one then fails with a unique violation (500). Handle `DbUpdateException` and return the existing row.

### M4. Payment and reference edge cases

`PaymentConfiguration.cs:20` enforces a unique `TransactionReference`, but default `string.Empty` is used for pending payments; any future pending-payment flow will collide. No provider error details are surfaced.

### M5. AI resilience

No timeouts, retries, or circuit breakers despite `Polly` and `Microsoft.Extensions.Http.Polly` being referenced. `FarmAiService` treats caller cancellation as a provider failure (`TaskCanceledException`). Model ids (`mixtral-8x7b-32768`, `llama-3.2-11b-vision-preview`) should be verified against current Groq availability.

### M6. Prompt injection and output trust

Farmer text is interpolated into prompts without delimiters or a JSON schema constraint. Model output is parsed with regex and deserialized without bounds (except mortality). A crafted message can influence extracted values.

### M7. Missing platform protections

No CORS policy, rate limiting, HSTS/security headers, upload size limits, or request timeouts. Voice polling can hold a request for up to ~45 s (`YarnGptService`).

### M8. Configuration and startup validation

Cloudinary, Nomba, Gemini, Groq, and YarnGPT keys are optional at startup and fail at request time with 500s. Add options validation (`ValidateOnStart`) and clear startup errors.

### M9. Documentation drift

`API.md` has been refreshed for the current routes and behavior, and the stale Swagger/controller remarks were corrected. Remaining drift: `ChatController` and `AuthController` have no `[ProducesResponseType]` attributes, the chat endpoint contracts are documented only in `API.md`, and Swagger does not describe the multipart chat form. Regenerate or hand-check `API.md` after every route change.

### M10. Duplicate and unused registrations

`ServiceRegistration.cs:48-53` registers `YarnGptService` twice and `IGroqVisionService` twice. `Polly` packages, `IJwtService`, `AuditEvent`, `Class1.cs`, and `Directory.Packages.props` (central versions are not enabled) are unused.

### M11. Audit trail not written

`AuditEvent` is mapped and migrated but no code writes one. Regulated actions (payment, incident status, vet registration) are unlogged beyond console logs.

### M12. Inconsistent casing and DTO drift

`IncidentDto`/`Incident` still expose `FlockId`; livestock lives in `LivestockAnimalId`. Dashboard `vetRequests` is always empty; action plan is string-based.

---

## Low issues

- Seeder uses `GetType().GetProperty("Id")!.SetValue(...)` instead of a factory or `InternalsVisibleTo` (L1).
- `CreatedAtAction` points at POST actions, producing odd `Location` headers (L2).
- `Class1.cs` and stale comments in `VoiceController.cs:191-194` (L3).
- `/health` does not check the database or dependencies (L4).
- Redundant explicit usings in `Program.cs`; `AddMediatR(Assembly)` uses an obsolete overload (L5).
- `Payment.Status` is a string instead of an enum (L6).
- No request correlation id in logs (L7).
- `Directory.Packages.props` lists versions that are not applied (L8).

---

## Positive patterns

- Clean separation: Domain / Application (CQRS + MediatR) / Infrastructure / API.
- FluentValidation pipeline with per-command validators.
- Domain behavior in entities (`Incident` transitions, `PoultryObservation` confirmation, `Money` value object).
- JWT validation configured for issuer, audience, lifetime, and signing key; bcrypt hashing; generic login error message.
- EF configurations with explicit FKs and unique indexes; migration present.
- API documentation and Swagger with bearer auth and XML comments.
- Global exception handler no longer leaks stack traces or raw messages (voice controllers still do).
- Idempotency keys and provider verification are the right direction, even where implementation is incomplete.

---

## Remediation order

1. **C1, C2, C3** — restore a coherent livestock/flock model and make voice, chat, and incident creation agree with the schema.
2. **C4, C6, C9** — persistence and bootstrap: goats save, real admin seed, migration chain.
3. **C7, C8** — payment and Cloudinary flows end-to-end.
4. **H1, H2** — ownership/role checks and correct status codes.
5. **H3-H5** — auth hardening, secret management, startup migration policy.
6. **H6-H11** — DI cleanup, rule engine decision, atomic vet registration, tests.
7. **M-series** — pagination, soft delete, resilience, docs refresh.

---

## Authorization matrix (current)

| Route | Policy | Ownership check |
|---|---|---|
| `/api/auth/*` | Anonymous | n/a |
| `/api/farms/*` | Authenticated | None; owner from body |
| `/api/incidents/*` | Authenticated | None |
| `/api/poultry/*` | Authenticated | None |
| `/api/voice/*` | Authenticated | None |
| `/api/chat/*` | Authenticated | None |
| `/api/vetcoordination/*` | Authenticated | None; no Vet role |
| `/api/payments/*` | Authenticated | None |
| `/api/goats/*` | Authenticated | None |
| `/api/admin/*` | `Admin` role | Role only; role unobtainable |
| `/health` | Anonymous | n/a |

---

## Open questions

1. Is `Flock` still a product concept, or should incidents move to `LivestockAnimal`/`LivestockCategory` (C3)?
2. Are flock-level observations required, or should every observation reference an animal (C1)?
3. Is the Nomba sandbox expected to verify locally fabricated references, or must `/initiate` call the provider (C7)?
4. Who bootstraps the first Admin account (C6)?
5. Should the migration be reset for the hackathon, or does a deployed database already have history (C9)?
