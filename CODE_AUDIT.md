# FarmHand — Codebase Audit

**Date:** 2026-10-21
**Repository:** `Team-HackForge` (branch `master`, HEAD `bda24b7`)
**Scope:** 141 tracked files — .NET Clean Architecture (Domain / Application / Infrastructure / API) with CQRS + MediatR. All source, configuration, migrations, tests, and git history reviewed.
**Method:** Static review plus `dotnet build` verification on the local toolchain.

**Verdict:** **Request changes — not runnable as committed, and has critical authentication and payment-integrity gaps.** The architectural skeleton is solid; runtime wiring and security are unfinished.

| Area | Score | Note |
|---|---|---|
| Architecture | 7/10 | Clean layering, CQRS, validators, domain encapsulation |
| Build / ops | 2/10 | Does not build here; startup fails on DI |
| Security / authz | 2/10 | Forgeable JWT, no tenant isolation, no token issuance |
| Data integrity | 3/10 | No foreign keys, no idempotency, stub payments |
| Tests | 0/10 | Both test projects are empty templates |

---

## Critical

### C1. Build fails: SDK mismatch

All projects target `net10.0` (`Directory.Build.props:7`) but only SDKs 8.0.416 / 9.0.308 are installed, and there is no `global.json`.

```
dotnet build FarmHand.slnx   ->   NETSDK1045 on all 6 projects
```

Package versions are also mixed (`JwtBearer` / EF `9.0.0`, `OpenApi 10.0.12`) against a `net10.0` target framework.

**Fix:** Add a `global.json` and retarget to `net9.0`, or install the .NET 10 SDK and align package versions.

### C2. Application cannot start — seeder not registered

`Program.cs:60` resolves `DemoDataSeeder`, but nothing registers it (`ServiceRegistration.cs` has no entry). Startup throws `InvalidOperationException`.

The seeder also runs unconditionally in every environment and before migrations are applied (`Program.cs:57-62`).

**Fix:** Register the seeder (scoped), guard it with `app.Environment.IsDevelopment()`, and call `Database.Migrate()` first.

### C3. Image observations crash on DI resolution

`VisionAnalysisService` depends on the concrete `GroqVisionService` (`Services/VisionAnalysisService.cs:10-16`), but only `IGroqVisionService` is registered (`ServiceRegistration.cs:50-51`). Any `RecordObservationCommand` with an `ImageUrl` fails to resolve.

**Fix:** Depend on `IGroqVisionService`, or register the concrete type.

### C4. Forgeable JWT and no way to obtain a token

- Hardcoded fallback signing key at `Program.cs:37` and `Services/JwtService.cs:20`, duplicated in `appsettings.example.json:24`.
- Deploy without `Jwt:SecretKey` and anyone can mint valid tokens.
- `JwtService.GenerateToken` is never called and there is no auth/login endpoint, so every `[Authorize]` controller is unreachable for real users.

**Fix:** Fail fast when `Jwt:SecretKey` is missing; add an authentication endpoint; remove the fallback from code and sample config.

### C5. Secrets remain in git history

Commit `b296fb7` contained `appsettings.json` with the database password and JWT key. Commit `bda24b7` only untracked the file; history is unchanged.

**Fix:** Rotate the JWT key and database password, then purge history with `git filter-repo` if the repository is shared.

### C6. No authorization or tenant checks (IDOR)

- `CreateFarmCommand` accepts `OwnerId` from the request body (`Features/Farms/CreateFarmCommand.cs:7`).
- `GetDashboard` accepts any `farmId` (`Controllers/FarmsController.cs:26-33`).
- Vet-only actions are callable by any authenticated user (`Controllers/VetCoordinationController.cs`).

Any authenticated user can read or write any farm's data.

**Fix:** Derive the owner from the token claims and enforce ownership/role checks in handlers or policies.

### C7. Payment integrity

- `RecordPaymentCommand` trusts client-supplied amount and reference, then immediately marks the payment `Completed` (`Features/Payments/RecordPaymentCommand.cs:26-30`).
- `NombaPaymentService` always returns success and never calls Nomba (`Payments/NombaPaymentService.cs:30-37`); `IPaymentService` is not used by the payment flow.
- `Payment.TransactionReference` has no unique index, and payments are never reconciled against the vet request.

**Fix:** Verify with the provider before marking paid, make the reference unique, and reconcile totals.

### C8. Goats feature silently discards data

The record is constructed but never added, then `SaveChanges` is called (`Features/Goats/CreateLivestockRecordCommand.cs:25-37`). The endpoint returns an ID for a row that was never persisted.

**Fix:** Add an `ILivestockRecordRepository` and call `AddAsync` before saving.

### C9. No exception pipeline

There is no `UseExceptionHandler` / `AddProblemDetails`, so `ValidationException` and `KeyNotFoundException` surface as HTTP 500. Controllers return raw exception text to clients (`VoiceController.cs:89,159,269,284`).

**Fix:** Add a global exception handler mapping validation to 400, not-found to 404, and return a generic message to clients.

### C10. Zero foreign-key constraints

All 15 tables in `20261021164412_AddImageSupportToPoultryObservation.cs` are created with indexes only — there is not a single `ForeignKey`. Configurations use `HasIndex` but never `HasOne`/`WithMany`, so flocks, incidents, and payments can reference nonexistent rows.

**Fix:** Add EF relationships (or explicit FK constraints) and regenerate migrations.

---

## High

### H1. Idempotency is not implemented

`IdempotencyKey` is validated then ignored (`Features/Poultry/RecordObservationCommand.cs:11,76`); `ExistsUnconfirmedAsync` is dead code. Client retries create duplicate observations.

### H2. Every AI path is paid for twice

- `VoiceCheckIn` extracts at `VoiceController.cs:61`, then sends a command that extracts again (`RecordObservationCommand.cs:33-47`).
- Image check-in runs Groq vision in the controller (`VoiceController.cs:118`) and again inside the command via `VisionAnalysisService`.

This doubles cost and latency, and the returned values may not match what was stored.

### H3. Cloudinary integration is unsigned and will fail

Upload sends `api_key` but no `signature`/`timestamp` (`Services/CloudinaryService.cs:38-47`); delete is also unsigned (`:71-79`, with an explicit "in production you'd sign this" comment). `ApiSecret` is read and never used.

### H4. LLM input is treated as trusted

Raw farmer text is string-interpolated into prompts (`AI/Clients/GeminiAiClient.cs:27`, `GroqAiClient.cs:27`, `AI/Services/GroqVisionService.cs:47`). Prompt injection can steer extraction, and parsed `MortalityCount`/confidence are never range-checked, so negative or absurd values can persist.

### H5. Gemini API key in URL query string

`GeminiAiClient.cs:44` puts the key in the query string, which leaks into proxy and access logs. Use the `x-goog-api-key` header.

### H6. Hardcoded, likely-retired model IDs

`gemini-1.5-flash`, `mixtral-8x7b-32768`, and `llama-3.2-11b-vision-preview` are hardcoded. Move them to configuration and verify them against current provider catalogs before demo day.

### H7. HttpClient created per request and undisposed

`VoiceController.cs:200` news up an `HttpClient` per request with no disposal. API keys come from `Environment.GetEnvironmentVariable` (`:196-198`) instead of `IConfiguration`, and this duplicates the existing `GroqVisionService`.

### H8. PII in logs

Full voice transcripts are logged at `Information` (`VoiceController.cs:58`) and Cloudinary URLs at `:115`. Add redaction and a retention policy.

### H9. No rate limiting, CORS, upload-size, or content-type validation

AI/STT endpoints are expensive and file uploads accept anything (`VoiceController.cs:41-57,97-114`). Add request limits, validation, and throttling.

### H10. Migration history is unusable

A single migration named `AddImageSupportToPoultryObservation` actually creates the entire schema. There is no initial migration and no `Database.Migrate()` call, so a fresh environment cannot reliably provision.

### H11. Core product logic is not implemented

`IPoultryRuleEngine`, `ICostShareCalculator`, `CaseFileDto`, and `PoultryObservationDto` have no implementations or usages. `AuditEvent` is never written (no audit trail for payments). `TransactionBehavior` is a no-op and unregistered. `JwtService` is registered but never used.

---

## Medium / Minor

| # | Finding | Location |
|---|---|---|
| M1 | `GetByIdWithActionsAsync` / `GetByIdWithDetailsAsync` do not include children | `Repositories/IncidentRepository.cs:18-21`, `VetRequestRepository.cs:13-16` |
| M2 | Dashboard always returns an empty vet-request list | `Features/Farms/GetDashboardQuery.cs:41-44` |
| M3 | Status command silently no-ops for `Open`/unknown and returns 200; transitions unguarded, no concurrency token | `Features/Incidents/UpdateIncidentStatusCommand.cs:33-48` |
| M4 | Soft delete is dead code: `IsDeleted` never filtered, repositories hard-delete | `Domain/Entities/BaseEntity.cs` |
| M5 | UnitOfWork news up repositories while they are also DI-registered; no livestock repository | `Repositories/UnitOfWork.cs:28-38`, `ServiceRegistration.cs:27-36` |
| M6 | `Money` accepts negative amounts and arbitrary currencies; `Email` unused; cost shares never reconciled | `Domain/ValueObjects/Money.cs:5-12` |
| M7 | Seeder sets protected IDs via reflection; synchronous `.Any()` inside async method | `SeedData/DemoDataSeeder.cs:23,28,39` |
| M8 | Unbounded list endpoints (`GetAllAsync`, incidents) — no paging | `Repositories/EfRepository.cs:26-29` |
| M9 | `Directory.Packages.props` is inert (no `ManagePackageVersionsCentrally`); versions declared twice | `Directory.Packages.props` |
| M10 | Repo hygiene: `.agoya/` committed, `Class1.cs` left over, stale `.http` request, no README/CI, Swagger missing JWT scheme, placeholder tests | repo-wide |
| M11 | `UseHttpsRedirection` active while `launchSettings.json` advertises an HTTP-only profile | `Program.cs:71`, `Properties/launchSettings.json` |

---

## What is done well

- Clear Dependency Rule: Domain has no infrastructure references; Application depends only on Domain abstractions.
- CQRS handlers are small, single-purpose, and consistently pass `CancellationToken`.
- FluentValidation validators co-located with commands and wired through `ValidationBehavior`; MediatR `LoggingBehavior` present.
- Domain entities use protected setters plus intention-revealing methods (`Incident.Resolve`, `Payment.MarkCompleted`) — real encapsulation, not anemic models.
- EF configurations are explicit (max lengths, indexes, owned `Money` types) rather than convention-only.
- AI extraction has a sensible Gemini→Groq fallback (`AI/Services/FarmAiService.cs:31-51`), and vision prompts deliberately avoid diagnosis/treatment.
- `appsettings.example.json` + `.gitignore` is the right pattern (needs history cleanup and no real default key).

---

## Recommended remediation order

1. Add `global.json` and retarget to `net9.0` (or install .NET 10) — nothing can be verified until `dotnet build` passes.
2. Register `DemoDataSeeder`, gate it to Development, add `Database.Migrate()`, and fix the `GroqVisionService` DI registration.
3. Remove JWT fallback keys, add a token-issuing auth endpoint, enforce owner/role checks on every farm/incident/payment route, and purge git history.
4. Add `UseExceptionHandler` / `AddProblemDetails` and validation-to-400 mapping; stop returning `ex.Message`.
5. Fix the Goats handler, persist and consume `IdempotencyKey`, and de-duplicate AI calls (extract once, pass results into the command).
6. Add EF relationships/foreign keys and unique indexes (`Payment.TransactionReference`, observation dedupe), plus a real initial migration.
7. Sign Cloudinary requests, move model IDs/keys to configuration, and add rate limiting and upload constraints.
8. Write domain/application tests for the incident state machine, cost-share math, and payment recording before extending features.
