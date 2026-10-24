# FarmHand buildathon evidence

Team HackForge · DevFest Abeokuta 2026 · Build. Secure. Scale.

Submission deadline: **28 October 2026, 11:59 PM WAT**, as published on the [official competition page](https://www.devfestabeokuta.com/buildathon). The official category weights are Product and Functionality 25, Problem and User Value 20, Technical Implementation 20, Security and Responsible Design 15, Scalability and Architecture 10, and Demo and Presentation 10. Any engineering score estimate is unofficial.

## Start here

| Reader | Document |
|---|---|
| Judge looking for evidence | [Buildathon evidence matrix](BUILDATHON_EVIDENCE.md) |
| Judge exploring the product | [Project overview](PROJECT_OVERVIEW.md), [demo guide](DEMO_GUIDE.md) |
| Product reviewer | [PRD](PRD.md), [user validation plan](USER_VALIDATION_PLAN.md) |
| Technical reviewer | [Architecture](ARCHITECTURE.md), [security report](SECURITY_REPORT.md), [test report](TEST_REPORT.md) |
| Performance/recovery reviewer | [Bounded local benchmark](PERFORMANCE_REPORT.md), [local restore drill](RECOVERY_REPORT.md) |
| Presenter | [Eight-slide content](PITCH_DECK_CONTENT.md), [three-minute script](DEMO_SCRIPT.md) |
| Team preparing the release | [Audit and implementation plan](AUDIT_AND_PLAN.md), [deployment runbook](../../DEPLOYMENT.md) |
| Performance and recovery reviewer | [Measured local read bursts](PERFORMANCE_REPORT.md), [backup/restore drill](RECOVERY_REPORT.md) |
| Final evaluation | [Unofficial evidence-based scorecard](SCORECARD.md), [99 passing tests](TEST_REPORT.md) |

## How to read the evidence

- **Source reviewed** means the implementation or test definition was inspected; it does not mean that test passed.
- **Locally verified** means an actual command or browser check ran against the candidate, with its result recorded in [TEST_REPORT.md](TEST_REPORT.md).
- **Simulated** means the isolated demonstration deliberately uses generated sample records, deterministic rules or signed simulated payment references. No money moves.
- **Unverified** means the required runtime, provider, infrastructure or human evidence has not been collected.
- **Proposed** means roadmap work; it is not included in the current feature claim.

The audit baseline is merged commit `f3da02edbac8b2a94fc623e5d036aa35a93061ac`. Fixes and documents on the buildathon branch must be assessed against the final tested revision recorded in the test report. Earlier-session results do not automatically apply to that revision.

Completed local evidence includes 75 backend tests (57 integration, 18 unit, zero skips), six Node tests, lint/type checks, production web build and clean production dependency audits. The bounded 10/25/50-user runs completed 510 authenticated reads with zero unexpected errors/throttles; a separate local restore matched 15 selected table counts. These are scoped local results. Final whole-browser outcomes and the tested revision are recorded in the test report; Docker container execution and real provider/deployment checks remain separate gates.

## Demonstration boundaries

The local demo uses fictional seed records and a private generated password. Account addresses in the guide identify local seed users; they are not public application credentials. The public [FarmHand URL](https://team-hack-forge.vercel.app/) and the supplied Docker Compose deployment are separate environments. Do not infer the public backend topology or successful external integrations from the reference deployment.

FarmHand offers preliminary urgency guidance and a route to veterinary review. Clinical accuracy, translation quality, farmer adoption, veterinary endorsements and economic impact have not been established by this submission. The validation plan describes the evidence still needed.
