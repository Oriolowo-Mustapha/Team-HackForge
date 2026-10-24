# FarmHand eight-slide pitch content

Presenter rule: replace any pending evidence with actual candidate results from [TEST_REPORT.md](TEST_REPORT.md). These are slide contents, not a rendered presentation. Do not present unverified providers, clinical quality or user research as established results.

## 1. The Problem

**When an animal becomes unwell, the next step needs context.**

- Signs, timing, deaths and earlier actions need to reach a qualified veterinarian.
- Scattered notes and conversations can make that handoff difficult.
- Our working hypothesis: one persistent reporting-to-care workflow can improve clarity.

Visual: observation → missing context → delayed coordination. Speaker note: do not attach unsupported loss or market statistics.

## 2. Who We Serve

**Farmers and the veterinarians who support them.**

- Livestock and poultry farmers recording changes in a named farm/flock/animal.
- Veterinarians reviewing requests, history and follow-up.
- Administrators managing authorized practitioner access.

Speaker note: Nigeria is the initial design context. Phone access, connectivity, language preferences and affordability still need field validation.

## 3. Introducing FarmHand

**Record once. Review clearly. Connect to professional care.**

- Text/manual reporting plus optional photo and voice pathways.
- Qualified urgency guidance with an explicit assessment source.
- Persistent cases, vet requests, treatment instructions and follow-up.

Visual: actual application screenshot captured from the rehearsed candidate, labelled local demo. Avoid invented screenshots. Speaker note: language/speech pathways are implemented; clinical and translation quality remain unverified.

## 4. Product Demonstration

**One synthetic concern becomes a shared case history.**

1. Farmer selects flock, reports signs and reviews numbers.
2. Confirmed urgency opens a case; farmer requests a vet.
3. Vet accepts, shares a sample plan; farmer sees it and adds follow-up.

Show simulated payment only with a visible “No money moves” label. Optional providers are used only after successful rehearsal. Public readiness returned 503 during review; use and identify the verified local instance until public backend readiness is restored.

## 5. Validation and User Value

**A clear test plan; evidence still to collect.**

- Validate reporting, urgency comprehension and follow-up with five farmers.
- Ask a qualified veterinarian to review missing context and unsafe wording.
- Track unassisted task completion, misunderstandings, errors and changes after feedback.

Speaker note: no farmer interviews, endorsements, adoption or business-impact figures have been established. If sessions occur, insert anonymized actual sample/results and limitations, with consent.

## 6. Technical Architecture

**A typed web application connected to a permissioned care workflow.**

- Next.js/TypeScript → same-origin BFF → .NET 10 API → PostgreSQL/EF Core.
- HttpOnly web sessions; API role/resource authorization.
- Configurable analysis, speech, email and payment adapters.
- Local verification: 75 backend tests, six Node tests, lint/typecheck and production web build passed; final browser outcome is in the test report.

Visual: simplified [Mermaid architecture](ARCHITECTURE.md). Speaker note: reference Docker/Caddy topology is distinct from the currently observed public Vercel frontend.

## 7. Security, Responsible AI, and Scalability

**Build. Secure. Scale—with inspectable boundaries.**

- Ownership/assignment checks, bounded uploads, server-verified payment and production configuration guards.
- Qualified care wording, deterministic urgency baseline and veterinary escalation; source labels distinguish rules, demo and provider output.
- Paged/batched queries and health checks; bounded 10/25/50-user local bursts completed 510 reads with zero unexpected errors/throttles on a tiny synthetic dataset.
- A separate local database restore matched 15 selected table counts; production recovery remains unverified.

Speaker note: use the final security/test reports for tested fixes/counts and the performance/recovery reports for their limits. Production rejects public Cloudinary delivery. Do not claim clinical validation, unlimited scaling or a passing CI run when billing prevented execution. Docker configuration validated; actual containers remain unverified because the host lacks Docker Engine.

## 8. Impact, Limitations, and Roadmap

**Our intended impact: clearer handoffs and more traceable follow-up.**

- Current evidence: implemented workflow and the candidate checks actually recorded.
- Remaining gates: public backend readiness, provider sandbox/email verification, private external media, field and clinical/language validation.
- Next: user-informed pricing, queued provider work, shared private storage and distributed coordination as demand grows.

Close: “FarmHand keeps the farmer, the observations and the veterinarian in the same care journey.” Intended impact is not a measured health or financial outcome.
