# FarmHand product requirements

Status: implementation-aligned MVP specification, 24 October 2026. Source-reviewed behavior is distinguished from verified results in [TEST_REPORT.md](TEST_REPORT.md). Future work is explicitly marked proposed.

## Vision and MVP boundary

Help farmers turn a concerning observation into a persistent, understandable record and a qualified veterinarian's response. The MVP covers farmer reporting, preliminary urgency guidance, professional coordination and documented follow-up. It is not an autonomous diagnostic or prescribing system, emergency dispatch service, pharmacy, insurance product or laboratory service.

## Users and working personas

| User | Job to accomplish | Constraint to validate |
|---|---|---|
| Livestock/poultry farmer | Record changes, understand urgency, contact a vet and retain care instructions | Connectivity, typing/language preference, phone sharing and affordability |
| Veterinarian | Review eligible requests, accept a case, inspect history and share care/follow-up instructions | Time, case quality, professional account verification and travel availability |
| Administrator | Create/manage privileged accounts, monitor configuration and audit activity | Secure account recovery, least privilege and responsible operational access |

These are design personas, not interviewed individuals. See [USER_VALIDATION_PLAN.md](USER_VALIDATION_PLAN.md).

## Functional requirements and acceptance criteria

| ID | Requirement | Acceptance criterion | Delivery status |
|---|---|---|---|
| FR-01 | Register/sign in as farmer | A new account receives Farmer privileges; invalid credentials fail; session refresh works; confirmed web logout clears current-device access; global revocation and offline failure are reported truthfully | Implemented; runtime status in test report |
| FR-02 | Manage own farms and livestock | Create/select an owned farm and group/animal; saved records survive refresh; foreign IDs do not grant access | Implemented |
| FR-03 | Capture a concern | Submit notes and explicit numeric fields; optionally attach supported image/audio within server limits; show errors and pending state | Implemented; providers conditional |
| FR-04 | Review extracted fields | Present notes/deaths/feed/water/eggs with source; retain farmer wording within field limits, restrict provider suggestions to approved categories/labels, allow correction and require explicit confirmation | Implemented with release hardening |
| FR-05 | Provide qualified urgency guidance | Show source and veterinary disclaimer; server-controlled assessment/photo wording must not prescribe medication or claim a confirmed disease; urgent deterministic signs cannot be silently downgraded; historical automated assessment output receives the same safe display projection without rewriting original records | Implemented in release source; security results in test report |
| FR-06 | Persist confirmation consistently | Confirmation, assessment and any automatically created case are saved consistently; retries do not create duplicates | Implemented in release source; regression evidence in test report |
| FR-07 | Request veterinary help | Owner creates an open or targeted request for an eligible case; competing requests/acceptance cannot create duplicate active assignments | Implemented with release hardening |
| FR-08 | Review as authorized vet | Eligible vet sees a request summary; farmer contacts and detailed case access obey assignment rules | Implemented |
| FR-09 | Share care instructions | Assigned vet creates a treatment plan and optional dated steps; farmer sees it and can record completion | Implemented |
| FR-10 | Record follow-up | Farmer adds an outcome and notes; case history/status reflect the operation after refresh | Implemented |
| FR-11 | Quote and reconcile payment | Fee/currency come from server; initiation retries are idempotent; provider verification, not a redirect, establishes payment | Implemented; local simulation, live provider unverified |
| FR-12 | Support responsible speech/language access | Optional transcription and playback expose unavailable states; detected language is retained; English safety fallback is disclosed when localized guidance is unavailable | Implemented pathway; quality unverified |
| FR-13 | Handle poor connectivity and shared-device privacy | Save drafts only when storage succeeds; owner-filter draft reads after failed cleanup; report network/logout outcomes honestly; private BFF responses including media remain no-store | Implemented; final browser regressions in test report |
| FR-14 | Administer access | Admin-only privileged account and audit operations; temporary passwords require change; deactivated accounts lose access | Implemented |

## Main user journeys

**Farmer to veterinarian:** register/login → choose/create farm and flock/animal → report → inspect/correct → confirm → open case → request vet → vet accepts → farmer reviews authoritative quote and pays in the configured mode → vet shares plan → farmer reads plan and records follow-up → consultation/case status is updated through authorized actions. Treatment guidance may be shared after acceptance; paid consultation completion requires verified payment where a fee applies.

**Provider unavailable:** retain entered observations → clearly report analysis unavailable → permit explicit manual reporting → show labelled deterministic urgency guidance on confirmation → offer the normal veterinary-request route. A model outage must not be presented as a successful live AI call.

**Returning farmer:** sign in → dashboard points to pending reports, open cases or due follow-up → inspect the persistent case timeline → record progress. Notifications are derived from records; they are not a guarantee that a veterinarian has read the case.

## Nonfunctional and security requirements

| Area | MVP requirement | Evidence or limit |
|---|---|---|
| Authorization | Enforce role, ownership, assignment and linked-resource consistency in the API | Source and security regressions; UI hiding alone is insufficient |
| Session privacy | HttpOnly cookies, Secure on production HTTPS, SameSite and same-origin mutation checks; clear identity-bound client state at session boundaries | BFF implementation and browser checks |
| Data integrity | Validate input, bound uploads, enforce relevant uniqueness and transactional persistence | Validators, constraints and concurrency tests |
| Availability | Distinguish unavailable integration, rejected input and expired session; bounded provider requests and readiness checks | Source-defined controls; target uptime unmeasured |
| Performance | Measure 10/25/50-user local read workloads, latency/throughput/errors and dataset limits | [Measured](PERFORMANCE_REPORT.md): 510 successful bounded reads, zero unexpected errors/429s; no production capacity promise |
| Accessibility | Keyboard-operable primary forms/navigation, labelled controls, clear errors and usable phone layout | Browser checks; no WCAG certification claimed |
| Recovery | Persist database/uploads; back up encryption keys; demonstrate isolated restoration | [Local database drill](RECOVERY_REPORT.md): 15 selected table counts matched; production retention, uploads/key restoration and RPO/RTO unverified |
| Observability | Safe logs and request trace IDs; production needs error/latency/provider/backup monitoring | Basic controls implemented; hosted monitoring/alerts unverified |

## Responsible AI

Treat user text/media and model output as untrusted. Restrict provider extraction/vision to approved categories and concern labels; supply clarification/photo uncertainty/assessment wording from the server; qualify guidance, retain source and direct serious signs to veterinary review. Preserve farmer-submitted wording within existing sanitization/length limits instead of silently rewriting or translating it. A farmer's own report is distinct from server-generated care advice. The deterministic rules are not a clinically validated classifier. Avoid medication names/dosages in synthetic demonstration instructions. Native-speaker and qualified-veterinarian review are required before claiming language or clinical quality.

Historical assessment, extraction and photo DTOs must constrain saved automated output when read. This is a display policy: original database records remain intact, and farmer observations and veterinarian-authored plans must not be silently replaced by generated prose. Do not treat output constraints as proof of correct transcription, extraction, image interpretation or clinical urgency.

No model may grant roles, assign ownership, choose the paying farmer or bypass API permissions. Optional provider calls may send submitted health content externally; deployment needs an explicit user-facing data notice and approved provider data-handling arrangements. Production must reject Auto/Cloudinary storage and use explicit Local storage with an operator-verified persistent mount and authorized media delivery. Optional non-Production Cloudinary delivery remains public; private cloud delivery is proposed.

## Success metrics and validation

Proposed evaluation targets, not achieved results: at least 4 of 5 recruited farmers complete the main reporting task without moderator intervention; at least 4 of 5 explain the difference between preliminary guidance and a diagnosis; no participant is encouraged by the demo to delay urgent professional help; no unauthorized cross-farm access in defined regression cases; and the complete local workflow passes on the submitted revision.

Collect task completion, time, critical misunderstandings, observed errors and preference/affordability feedback. Report sample size and limitations. Clinical outcomes, prevented losses, adoption and revenue are outside the evidence presently collected.

Engineering evidence already collected is separate from those proposed user metrics: 75 backend tests (57 integration/18 unit; no skips), six Node tests, lint/typecheck/production build, locked restore/install, clean production dependency audits, EF model consistency, local load bursts and a fresh-database restore. See the test report for final browser outcomes and exact tested state. No participant task-completion result is inferred from automated tests.

## Proposed next-stage work

Private shared media delivery, queued provider/notification work, distributed refresh/rate coordination, broader accessibility/device testing, clinician-reviewed localization, user-tested pricing and production monitoring are proposed. They must not be presented as completed MVP capabilities.
