# FarmHand security and responsible-design report

Assessment date: 24 October 2026. Audit baseline: `f3da02e`. This is a focused engineering review with local regression verification, not an independent penetration test or clinical certification. The tested revision, commands, results and unverified areas are in [TEST_REPORT.md](TEST_REPORT.md).

Completed local evidence: **75/75 backend tests passed (57 integration, 18 unit; zero failures/skips)** against isolated PostgreSQL; six Node tests, lint, TypeScript and the production web build passed. Locked restore/install and production NuGet/npm audits completed with zero known vulnerabilities reported. EF model consistency passed with 13 migrations applied locally. The final whole-browser result remains in the test report; partial browser runs are not represented as a completed suite here.

## Threats and boundaries

Primary risks are cross-farm disclosure, unauthorized vet/admin actions, stolen/reused sessions, unsafe animal-health guidance, untrusted uploads, inconsistent clinical/payment state, external provider failures and deployment misconfiguration. API role/resource checks remain authoritative even when browser navigation hides an action.

## Controls and verification

| Control | Implementation inspected | Verification status |
|---|---|---|
| Farmer-only self-registration and privileged account restrictions | Auth/admin controllers and resource authorization service | Source reviewed; HTTP/browser outcomes in test report |
| JWT validation and revocation | Signature/issuer/audience/lifetime, user activation and token version checks | Source reviewed; complete session/deactivation browser result in test report |
| Session confidentiality and CSRF | HttpOnly cookies; Secure in HTTPS production; SameSite; custom header/origin check on mutations; no-store BFF responses | Source reviewed; production cookie deployment unverified; browser result in test report |
| Refresh/password reset | Hashed rotating refresh tokens, reuse handling, single-use password-reset concurrency | PostgreSQL single-use reset/concurrency regression passed; broader session/browser status in test report |
| Farm/case isolation | Ownership, assigned-vet access and linked report/subject checks | Backend handler/database regressions passed for foreign source links, assigned cases beyond 100 and media scope |
| Contact privacy | Open request summaries hide farmer contacts; assigned vet/owner/admin receive authorized fields | Source reviewed; role-specific browser result in test report |
| Upload handling | 25 MiB transport cap; 8 MiB image/10 MiB audio cap; magic-byte validation; controlled local paths | Three streaming-body Node tests and database media-isolation regression passed; signature policy source reviewed |
| Payment integrity | Server quote; amount/currency checks; idempotency; signed webhook and independent verification | Simulated/stubbed provider tests and pending-live reconciliation guards passed; real provider sandbox/settlement unverified |
| Payout data | AES-256-GCM with independent field key; masked response fields | Source reviewed; production key custody/rotation and masked-response browser outcome in test report |
| Production configuration | Reject demo extraction/accounts, simulated payments, weak keys, wildcard hosts, missing recovery email and Auto/Cloudinary storage; require explicit Local storage/path | Configuration acceptance/rejection regressions passed; actual persistent mount/permissions and target environment unverified |
| Availability and errors | Per-IP/per-user limits, bounded provider calls, sanitized errors and trace IDs | Stubbed timeout/circuit/provider privacy tests passed; 510 local reads had zero errors/429s; this does not prove distributed admission control |
| Dependency hygiene | Locked NuGet/npm dependencies; high/critical NuGet warnings treated as errors; production audits | Locked restore, clean npm install and NuGet/npm audits passed; zero known vulnerabilities reported at verification time |
| Device drafts and logout outcomes | Owner-filtered drafts survive cleanup failure without account crossover; truthful local/global/offline sign-out states | Source reviewed with browser regressions; final results in test report |

## Findings and release work

The initial findings were recorded before implementation in [AUDIT_AND_PLAN.md](AUDIT_AND_PLAN.md). The remediations below are present in source. The named backend and Node regressions completed successfully; browser-only behavior retains an explicit reference to the final browser report.

| Finding | Risk | Release remediation and actual verification |
|---|---|---|
| A case could link a foreign farm's source report | High: private report disclosure | Farm/subject/confirmation validation and defensive source projection; `IncidentSource_MustBeConfirmedAndMatchFarmAndSubject` passed against PostgreSQL |
| Known local media keys lacked record ownership checks | High: private image disclosure | Owner/assigned-case authorization; owner, foreign farmer, prospective vet and assigned-vet database regression passed. BFF media stays no-store; browser result in test report |
| Generated assessment text could claim disease/prescribe | High: unsafe health guidance | Approved `AssessmentSafety` guidance; unsafe narrative, provider red flags, headcount and language-fallback tests passed |
| Provider extraction/photo prose could bypass assessment safeguards | High: unsafe content before confirmation | Approved extraction fields/labels and `PhotoSafety` uncertainty text; advice injection, preserved farmer words and vision-output regressions passed |
| Saved pre-release automated output could bypass new-write safeguards | High: historical unsafe guidance replay | Safe read projection without database rewrite; pure and real-database historical-output tests passed |
| Farmer override could reduce urgent escalation | High: delayed veterinary review | Downward override rejected; farmer-raised Critical escalation survives refresh and repeat confirmation; regressions passed |
| Confirmation used separate saves | High: saved assessment without its required case | One SaveChanges transaction; induced final-write failure rolled back report/assessment/case, and retry did not duplicate the case; database regressions passed |
| Request cancellation could abandon live reconciliation | High: financial inconsistency | Request and checkout cancellation preserve unresolved live reconciliation; Pending/NotFound stubbed provider regressions passed |
| Client state/redirect checks had session-edge gaps | High: prior-user disclosure/unsafe navigation | Cache cleanup, owner-scoped drafts, safe redirects and truthful logout added. Node regressions passed, including dot-segment protocol-relative bypass; browser results in test report |
| Concurrent active requests could duplicate a case's work | Medium: inconsistent coordination | Filtered unique index and non-destructive preflight; competing inserts stored one active request with one conflict; PostgreSQL regression passed |
| Provider exception classes/raw response logs were incomplete | Medium: silent outage or sensitive log content | Timeout/circuit fallback and status/type-only logging; synthetic provider failure/privacy regressions passed; no real provider traffic claimed |

Backend evidence: [BackendBoundaryTests.cs](../../tests/FarmHand.IntegrationTests/BackendBoundaryTests.cs), [ResponsibleAiTests.cs](../../tests/FarmHand.IntegrationTests/ResponsibleAiTests.cs), [ConfigurationTests.cs](../../tests/FarmHand.IntegrationTests/ConfigurationTests.cs), [YarnGptPrivacyTests.cs](../../tests/FarmHand.IntegrationTests/YarnGptPrivacyTests.cs), and [PasswordResetConcurrencyTests.cs](../../tests/FarmHand.IntegrationTests/PasswordResetConcurrencyTests.cs). Node regressions: [navigation.test.mjs](../../web/tests/unit/navigation.test.mjs) and [request-body.test.mjs](../../web/tests/unit/request-body.test.mjs).

## Session and device privacy details

Draft reads/lists require the current `ownerUserId`, and restoration also checks farm ownership. Rows without a known owner are hidden. Account transitions attempt local cleanup; a transient storage failure must not expose retained rows through another account's draft UI. This is application-level account isolation, not encryption or protection from direct access to browser storage tools.

Private BFF responses, including media, retain `Cache-Control: no-store`; the media API also checks ownership/assignment. A completed web sign-out response clears this device's HttpOnly cookies even if upstream API revocation is unavailable. Global sign-out is only confirmed when `/api/auth/revoke` succeeds; otherwise the UI reports local sign-out with unconfirmed other-device revocation. If the browser cannot reach the web sign-out route, it cannot clear HttpOnly cookies: the UI reports failure, retains session state and asks the user to reconnect. Final browser outcomes are in the test report.

## Responsible AI safeguards and limits

Reports are explicitly preliminary observations and urgency guidance. The database's legacy `AIDiagnosis` name does not confer clinical authority. User text/media are untrusted; prompts separate supplied facts from instructions, structured fields are bounded, and models cannot authorize API actions.

Release safety hardening uses server-approved, qualified care language rather than exposing arbitrary generated diagnosis/treatment text. Extraction uses approved consumption categories and symptom labels; clarification questions come from deterministic extraction. Photo assistance maps only approved concerns and supplies fixed uncertainty text. Farmer-submitted wording is retained within existing validation/sanitization/length limits instead of replaced with a provider-written summary or translation. User-authored observations remain distinguishable from care guidance.

Deterministic rules preserve escalation for recognized serious signs and mortality patterns. Model/rule sources remain visible; missing provider configuration must not fabricate a live-model success. Manual intake remains available when extraction is unavailable, and confirmation can use labelled rules. These constraints limit generated prose; they do not prove that a provider's selected numeric fields, concern codes, transcription or urgency are correct.

Historical assessment detail mapping uses the same approved guidance policy instead of rendering saved model summary/action text; historical extraction/photo mapping also limits output. Original records are not overwritten. Farmer-authored notes and veterinarian-authored care plans have different provenance and remain their authors' records. Review/migration of pre-release historical clinical content remains an operator/clinical-review concern; this report does not claim that every free-text historical field has been certified safe.

The rules have a finite vocabulary and are not clinically validated. Absence of a recognized warning sign does not prove an animal is safe. Farmer descriptions, image quality, transcription errors and incomplete histories can affect interpretation. Qualified veterinarians must review clinical suitability before public reliance.

The UK government's [avian influenza guidance](https://www.gov.uk/guidance/avian-influenza-bird-flu) identifies signs including sudden death and breathing difficulties in birds. This supports caution around such reports; it does not validate FarmHand's numerical thresholds, establish a diagnosis or provide an all-species guideline. FarmHand's rule thresholds remain engineering heuristics requiring veterinary review.

Report-language hints and optional speech do not establish clinically safe translation. Where localized safe wording is unavailable, the response must identify an English safety fallback. Native speakers and veterinarians still need to assess comprehension, pronunciation and escalation language. Do not fabricate medication/dosage examples in the demo.

## Remaining risks and operational gates

- **Public deployment is not ready for a working demo:** at `2026-10-24 09:48 UTC`, a read-only request to [public web readiness](https://team-hack-forge.vercel.app/api/health) returned HTTP 503 with `status: unavailable`. The landing page rendered. The precise API URL/database/migration/service cause was not observable from that public check.
- **External media privacy:** Production explicitly rejects Auto/Cloudinary storage and requires Local storage behind authorized media routes. Operators must verify the configured directory is a persistent, permissioned mount. Optional non-Production Cloudinary uploads yield public delivery URLs; authenticated/private cloud delivery and lifecycle retention/deletion remain future work.
- **Provider data handling:** submitted observations and media may reach configured analysis/speech/storage providers. A production privacy notice, user expectations and approved provider-retention terms remain required.
- **Operational secrets and recovery:** private environment files are ignored, but deployment access controls, secret-manager backups, encrypted off-host database/upload backups, rotation and restoration need target-environment evidence. Losing the field-encryption key loses readable payout details.
- **Horizontal scaling:** process-local refresh coordination/rate limiters and local upload storage are not a distributed deployment design. The supplied web service runs one replica.
- **Service status:** configured-provider flags do not prove delivery, model quality, payment settlement or provider availability. Email notification failure must not be mistaken for a vet reading a case.
- **Deployment verification:** Linux container execution, production DNS/TLS provisioning, real email recovery and provider sandbox checks must be recorded before release. A CI billing restriction is separate from a test failure.
- **Assurance:** no independent penetration test, clinical validation, native-speaker validation, complete accessibility audit or production SLO has been established.

The public HTTPS response supplied HSTS, frame denial, `nosniff`, a referrer policy and feature permissions. This confirms those observed headers only; it does not resolve the failed readiness check or establish complete deployment security.

Report suspected vulnerabilities privately to the repository owner. Do not include credentials, exploit details or farmer records in public issues or demonstration artifacts.
