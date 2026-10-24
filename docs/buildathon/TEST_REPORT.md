# FarmHand test and verification report

Verified 24 October 2026 against the buildathon candidate. This report records actual local results and keeps hosted/provider evidence separate.

## Revision and environment

- Audit baseline: `f3da02edbac8b2a94fc623e5d036aa35a93061ac`.
- Frozen backend source: `5ae4b43c14ff040d2d2a4afd92d5e377c5c7a0ab`; functional frontend: `dd483898f13f3a7f157781ba791ec7b76d4a0e16`. Subsequent browser-fixture changes correct selectors, draft observation and navigation waits without changing product behavior. The final fixture revision is recorded below.
- Performance source: `c6eed5a627be076ee47b1b6d325feb7ce70765c8`; the later logout changes do not affect its three measured read endpoints.
- Windows 11 build 26200, PowerShell, 8 logical processors, 15.7 GiB RAM; .NET SDK 10.0.402; Node 24.20.0; npm 11.19.0; PostgreSQL 18.6 on loopback port 54329; Playwright Chromium 1.63.0; k6 2.2.0.
- Local API 5037 and production Next.js server 3000. System info confirmed `RuleBasedDemo`, no configured AI/vision/transcription providers, email disabled, Local storage, Demo enabled and Simulated payments. No real providers, financial transactions or farmer records were used.
- Coverage percentage: **not measured**. Test counts are not coverage percentages.

## Actual results

| Check | Exact command or method | Result |
|---|---|---|
| Backend locked restore | `dotnet restore FarmHand.slnx --locked-mode` | Passed, all six projects |
| Backend tests | `dotnet test FarmHand.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --logger console;verbosity=normal` with private isolated `ConnectionStrings__DefaultConnection` | **75 passed: 18 unit + 57 integration; 0 failed, 0 skipped** |
| Release compilation | Serial Release solution build with `-m:1 -nr:false -p:UseSharedCompilation=false` | Passed, zero warnings/errors |
| Database/schema | EF `migrations has-pending-model-changes --configuration Release --no-build`; local startup and `/health/ready` | No pending model changes; 13 applied migrations; readiness HTTP 200 |
| NuGet vulnerability audit | `dotnet list FarmHand.slnx package --vulnerable --include-transitive --no-restore --format json` | No vulnerable packages reported across six projects |
| Frontend locked install | `npm ci` in `web` | Passed; zero audit vulnerabilities reported |
| ESLint | `npm run lint` | Passed |
| TypeScript | `npm run typecheck` | Passed |
| Frontend unit tests | `npm test` | **6 passed, 0 failed/skipped** |
| Production frontend | `npm run build` (Webpack) | Passed: 34 static pages plus dynamic app/API routes |
| Production npm audit | `npm audit --omit=dev --json` | Zero known vulnerabilities reported at audit time |
| Browser suite | `npm run test:e2e -- --reporter=list,json` with private `E2E_PASSWORD` and local Chromium runtime | **18 passed, 0 failed/skipped/flaky**, 131.46 seconds; [sanitized aggregate evidence](results/verification.json) |
| Full-stack readiness | Local `GET /api/health` | HTTP 200, `status: ready` |
| Compose configuration | Standalone official Compose `--env-file .env.example config --quiet` | Passed |
| Caddy configuration | Official Caddy 2.11.7 `validate --config deploy/Caddyfile --adapter caddyfile` with example host | Valid configuration; this does not provision a certificate |
| Linux container builds | `docker compose build` | **Not executed: Docker Engine unavailable locally; hosted CI blocked before execution** |
| Local read benchmark | `k6 run scripts/load-readonly.js`, separately at 10/25/50 VUs | **510 successful reads, 0 unexpected errors, 0 throttles**; [actual metrics and limits](PERFORMANCE_REPORT.md) |
| Database recovery | `scripts/restore-drill.ps1` against quiescent loopback synthetic DB, fresh destination | Restore succeeded; all 15 selected table counts matched; [scope and evidence](RECOVERY_REPORT.md) |
| Credential-pattern/whitespace review | High-confidence credential patterns in tracked text; `git diff --check` | No matching embedded credentials; whitespace check passed |

Backend runtime evidence was independently checked from retained TRX counters: `final_net10.0_20261024125450.trx` (57/57) and `final_net10.0_20261024125501.trx` (18/18), under the local workspace's `work/artifacts/backend/`. Detailed local browser traces/HTML reports stay outside Git because they can contain disposable account/session information. Only aggregate benchmark/recovery results are committed.

## Meaningful regression coverage

The real PostgreSQL tests cover foreign source-report rejection, report/case atomic rollback, idempotent confirmation, severity floor and increased urgency across refresh/retry, private media owner/assigned-vet access, competing active vet requests, more than 100 assigned cases, pending live-payment reconciliation, stable paging, batched farm counts, SQL financial totals, older active requests/reminders and paid dashboard prompts. Password-reset concurrency still uses simultaneous competing contexts.

Provider stubs exercise unsafe diagnoses/doses, injected photo/extraction prose, safe historical reads, fallback timeout/circuit failures, caller cancellation and sanitized YarnGPT/Spitch errors. Production configuration tests reject unsafe modes, including public cloud delivery. These are controlled regressions, not live-provider or clinical validation.

The browser suite includes role navigation, registration/farm/flock setup, cross-farm permissions, CSRF, actual PNG upload/private delivery, competing vet acceptance, fixed server quotes, simulated payment verification/callbacks, clinician plans and farmer follow-up, admin temporary-password/deactivation, public offline fallback, phone layout and synthetic zero-coordinate GPS. Added browser checks exercise the complete UI care journey, explicit zero/positive numbers, farm-bound drafts, blocked storage, owner isolation after failed cleanup and truthful local/global/offline logout.

## Failures encountered and corrected

- A real rule defect missed the plural phrase `sudden deaths`; fixed and regression-tested.
- Simultaneous database-fixture migrations collided on the new index. Fixtures are serialized against their shared database; explicit concurrency tests remain concurrent. No test assertions were removed or skipped.
- Restricted Windows execution denied generated-route directories and local testhost/browser loopback IPC. Running the authorized checks with the required permissions resolved those environment failures. Serial MSBuild avoided a worker failure with no compiler diagnostics.
- One local preview was started before the completed standalone server file existed, so the browser readiness prerequisite failed and 17 tests did not run. It was restarted from the completed build before the actual workflow run.
- An initial complete browser run passed 13 and failed five new checks. The failures were test defects: Optional label names, an unscoped alert matching Next's route announcer, reloading before route completion, and an IndexedDB observer creating an empty database before app initialization. Selectors/waits and the non-mutating observer were corrected without weakening value, persistence or isolation assertions.
- The next run passed 17 and found one remaining scenario error: a Medium-urgency report was expected to create an urgent case. The fixture now explicitly reports gasping and sudden deaths, asserts High/Critical urgency, and then exercises the full UI care journey. That journey passed individually and in the final 18/18 run. Application triage was not weakened.

`npm ci` emitted upstream ESLint peer-range warnings; verification still passed. Node emitted native TypeScript module-detection warnings during unit tests. These warnings are not reported as test failures.

## Public application and CI

At **2026-10-24 09:48 UTC**, a read-only [public readiness request](https://team-hack-forge.vercel.app/api/health) returned HTTP 503 with `status: unavailable`, while the landing page rendered. HSTS, frame denial, `nosniff`, referrer and feature-permission headers were observed. The public check cannot identify the private API URL/database/migration root cause. No public accounts or records were created.

The final read-only recheck at **2026-10-24 16:50:39 UTC** still returned HTTP 503 with the same unavailable response. Local success does not close this production gate.

For baseline `f3da02e`, [Actions run 40077309842](https://github.com/Oriolowo-Mustapha/Team-HackForge/actions/runs/40077309842) reported: the account was locked due to billing. Full-stack executed **zero steps**, containers were skipped. This is an external infrastructure restriction, not an executed application-test failure. Candidate PR CI status is recorded with the final review link when available.

## Remaining verification gates

Real AI/vision/transcription/TTS quality, Brevo recovery delivery, Flutterwave sandbox settlement/webhook delivery, production secrets/hosts, Docker/Linux runtime, deployment DNS/TLS and off-host restoration remain unverified. Local rules and simulated payment success do not establish provider success. Chromium phone viewport and mocked GPS do not establish Safari/Firefox behavior, real device geolocation or formal accessibility compliance.

The benchmark is a short shared-account burst against tiny synthetic data; no sustained production capacity or SLO is claimed. Restore count equality on one disk is not an off-host recovery guarantee. Clinical accuracy, native-language comprehension, farmer interviews, endorsements, adoption and economic impact have not been established.

## Final browser fixture revision

`eb73e5ccbbfe94d3b069c02a4268a6f4b0218e40`. All **99 tests** passed across .NET (75), Node (6) and Chromium (18). Later submission-document/script commits do not change the application source tested here. The benchmark remains explicitly tied to its earlier measured revision. Detailed private browser JSON is retained locally at `work/artifacts/e2e-final.json`; only sanitized aggregate results are committed.
