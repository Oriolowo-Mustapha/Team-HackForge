# FarmHand buildathon audit and execution plan

Audit date: 24 October 2026. Baseline: `f3da02e` (current merged `master`). Earlier audit findings were rechecked against the merged multilingual, voice and fixed-fee changes. This is an engineering assessment, not an official judging result.

## Phase 1 findings, before implementation

| Subsystem | Status | Defect / risk | Severity | Planned fix | Rubric | Priority |
|---|---|---|---|---|---|---|
| Farmer report | Implemented | Analysis discards explicit flock numbers; draft may move to wrong farm | High | Preserve explicit zero/positive values; restore original owned farm; UI regressions | Product, Technical | P1 |
| Case authorization | Implemented, vulnerable | Own case may reference foreign source report and expose its contents | High | Enforce farm/subject/confirmation relationship; defensive projection; regression | Security | P1 |
| Confirmation | Implemented, unsafe edges | Can downgrade urgent guidance; separate saves may omit case after interruption | High | Preserve deterministic urgency floor; atomic confirmation/diagnosis/case save | Product, Security | P1 |
| AI guidance | Implemented | Arbitrary multilingual provider text can prescribe or claim diagnosis; prompt-only safety | High | Server-approved care text and qualified summary; deterministic minimum urgency; unsafe-output tests | Security, Product | P1 |
| Session privacy / redirects | Implemented | Expiry cleanup inconsistent; previous cache survives login; control-character redirect bypass | High | Clear identity-bound cache/drafts consistently; validate same-origin redirect; regressions | Security, Technical | P1 |
| Payment lifecycle | Implemented, simulated locally | Request cancellation can discard pending live payment reconciliation | High | Block cancellation until live checkout is reconciled | Security, Product | P1 |
| Media | Implemented | Signed-in caller can read another farm's known local media key | High | Authorize owning record/case; no-store; test foreign access | Security | P1 |
| Voice reply | Newly merged | Client paths bypass BFF and return 404 | Medium | Correct routing; preserve optional-provider error states | Product, Demo | P2 |
| Vet coordination | Implemented | Concurrent active duplicates possible; cases limited to first 100 assigned requests | Medium | Database uniqueness/conflict handling; query assigned access directly | Product, Technical | P2 |
| Provider failures / logs | Partially hardened | Polly exceptions bypass fallback; YarnGPT logs response bodies | Medium | Recognize resilience errors; status-only logs and regressions | Security, Technical | P2 |
| Dashboard / database | Implemented | Active requests/reminders disappear behind latest 50; materialized counts/history | Medium | Filter before limits, aggregate counts, bounded queries, stable paging | Scalability, Product | P2 |
| UI errors / keyboard | Partially implemented | Draft errors and cancellation failures silent; animal-list errors hidden; chip keyboard gaps | Medium | Truthful persistence states, retries, keyboard behavior | Product, Demo | P2 |
| API documentation | Newly expanded, inconsistent | Describes obsolete rate limits, payment order and permissions | Medium | Align with actual source and tested behavior | Technical, Demo | P2 |
| Submission evidence | Missing | No required buildathon documents, validation plan, measured load/restore evidence | Medium | Nine required documents plus honest validation/performance evidence | All categories | P1 |
| Production / CI | Reference deployment implemented | Live topology/providers unknown; previous CI jobs never started due account billing | External gate | Read-only public checks; rerun local equivalents; keep external failures separate | Technical, Demo | P1 |

## Execution order and ownership

1. Preserve merged team work on `feat/buildathon-readiness`; implement isolated backend safety/authorization fixes and frontend data/session fixes with focused regressions.
2. Fix dashboard/query correctness, optional voice routing, provider failures and practical usability defects without changing the stack or visual brand.
3. Run .NET tests with real local PostgreSQL, frontend tests/lint/typecheck/production build, and complete local browser journeys. Fix failures without weakening meaningful assertions.
4. Prepare grounded product/PRD/architecture/security/test/demo/pitch/evidence documents. Label simulated payment, rules, and unverified provider/language behavior.
5. Run bounded loopback-only 10/25/50-user read benchmarks and a local database backup/restore drill; publish actual measurements and dataset limitations.
6. Review the final diff, commit meaningful groups, push a review branch and draft PR. Do not deploy, touch production data, perform live payments, rotate secrets or force-push.

## Evidence rules

Source review is not a passing runtime test. Earlier test counts require a fresh run on this candidate. A CI job blocked before execution is an infrastructure restriction, not a test failure. No clinical validation, farmer interviews, veterinary endorsement, adoption, production capacity or live provider success is assumed. Coverage is reported only if measured. The official [competition page](https://www.devfestabeokuta.com/buildathon) supplies the six category weights and submission deadline (28 October 2026, 11:59 PM WAT); all scores here are unofficial estimates.
