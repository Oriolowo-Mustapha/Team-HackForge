# FarmHand project overview

FarmHand is an AI-assisted livestock health reporting and veterinary coordination application built by Team HackForge. It connects observations about an animal or flock to a reviewed report, a case record, a veterinarian's response, and follow-up care.

## Problem and intended users

A farmer noticing reduced feeding, breathing difficulties or deaths needs to describe what changed, preserve the history and obtain professional help. A conversation without a persistent animal record can lose the timing, number affected and earlier actions. A veterinarian needs that context before deciding what to investigate or recommend.

These are the product's working problem hypotheses, not findings from completed interviews. The initial audience is livestock and poultry farmers in Nigeria, veterinarians supporting those farms, and administrators responsible for authorized practitioner accounts. Connectivity, language preferences, shared phones and affordability must be tested with those users.

## Value proposition

**Record the concern once, review it, and carry the same context into veterinary care.** FarmHand's value is the connected workflow: named farms and animals/flocks, structured observations, explicit assessment sources, saved cases, authorized vet requests, treatment instructions and follow-up records. This persistent, permissioned workflow distinguishes it from a generic chat interface.

AI helps structure free-text or transcribed observations and can contribute an urgency assessment. Farmer wording is retained within validation/length limits rather than replaced by a model-written translation or summary. Provider extraction and photo concerns are restricted to approved fields/labels, and care guidance uses qualified server-controlled wording. It does not establish a veterinary diagnosis. Deterministic urgency rules support escalation; the veterinarian remains responsible for professional examination and treatment decisions. Voice and language pathways can reduce typing effort when configured, but their accuracy and suitability are unvalidated.

Historical stored assessment/photo/extraction output is constrained when projected for display, without overwriting original database records. Farmer-authored observations and veterinarian-authored treatment plans remain distinct from automated guidance. These output controls do not establish that the underlying observations, transcription or provider-selected fields are correct.

## Current functionality

The following capabilities are implemented in source. Runtime evidence and remaining limitations are tracked in [TEST_REPORT.md](TEST_REPORT.md).

Completed local checks include 75 backend tests (zero failures/skips), six Node tests, lint, TypeScript, production web build and production dependency audits reporting zero known vulnerabilities. The [performance report](PERFORMANCE_REPORT.md) records 510 successful bounded reads across 10/25/50 virtual users; the [recovery report](RECOVERY_REPORT.md) records a separate local restore with 15 matching selected table counts. These do not establish field impact, clinical validity, sustained production capacity or a completed public deployment. Final whole-browser results remain in the test report.

| Capability | Current boundary |
|---|---|
| Farmer registration and session management | Farmers self-register; administrators create privileged accounts; web tokens use HttpOnly cookies |
| Farm and livestock records | Multiple farms, flocks/groups and individual animals; location entry and optional browser GPS |
| Reporting | Text, explicit manual fields, and optional image/voice intake; review/correction before confirmation |
| Urgency guidance | Labelled provider/rule source; qualified preliminary guidance; high/critical confirmed reports can create cases |
| Veterinary coordination | Open or targeted requests; authorized acceptance; assigned-vet case context; treatment plans and scheduled steps |
| Follow-up | Case status, actions, farmer updates and treatment-step completion |
| Consultation payments | Server-generated quote, idempotent initiation and server verification; local demo is explicitly simulated |
| Administration | Users, vets, farms, cases, payments, audit and integration configuration status |
| Connectivity support | Owner-filtered on-device drafts, truthful storage/logout failures and a public offline fallback; server operations still require connectivity |
| Language and speech | Report-language metadata for English, Yoruba, Hausa, Igbo and Nigerian Pidgin; optional provider transcription/TTS; current approved care response is English with a disclosed fallback for other report languages; clinical and translation quality unverified |

New vet requests use a configured-in-code flat fee of NGN 20,000, with a 5% platform commission added at payment time. Always show the server quote: older sample requests may contain a different historical fee. Pricing is a product decision requiring affordability research, not an established willingness-to-pay result.

## Verified technology stack

- Next.js 16, React 19, strict TypeScript, TanStack Query and Zod for the web application.
- ASP.NET Core on .NET 10, MediatR and FluentValidation for the API/application layer.
- PostgreSQL with EF Core/Npgsql and tracked migrations.
- JWT bearer validation in the API; server-side BFF session cookies in the web app.
- Configurable Gemini/Groq analysis, Spitch/YarnGPT speech, Brevo email and Flutterwave payments. Production image storage requires authorized local delivery with a persistent mount; optional Cloudinary storage is limited to non-Production environments.
- Docker Compose reference deployment with Caddy HTTPS; .NET/xUnit, Node tests and Playwright verification definitions.

See [ARCHITECTURE.md](ARCHITECTURE.md) for component boundaries and the distinction between reference deployment and the public environment.

## Current limitations

- External AI, transcription, speech, email and payment settlement require real provider configuration and independent verification.
- Rules and model urgency suggestions have not undergone clinical validation. Current approved clinical guidance is English; a non-English report retains its detected-language metadata and receives an explicitly labelled English safety fallback. A language code does not prove translation quality.
- Sample farmer/vet names, records and histories are fictional, not testimonials or evidence of usage.
- Offline drafts are device-local; FarmHand is not a fully synchronized offline clinical application.
- The reference stack uses a single web replica and local upload volume. Distributed refresh coordination, shared storage and operational capacity planning are next-stage work.
- Production explicitly rejects `Storage:Provider=Auto` and `Cloudinary`; it requires `Local` plus a configured persistent storage path. The operator must verify the mount and permissions. Optional non-Production Cloudinary URLs remain public delivery URLs; future cloud storage needs authenticated/private delivery.
- Compose and Caddy configurations have been validated, but Docker Engine is absent on the verification host, so Linux container builds/runtime remain unverified. Local database restore succeeded; off-host production recovery, upload/key restoration and real provider/deployment checks remain required.

## Roadmap

1. Complete observed farmer/vet usability sessions and publish anonymized findings and consequent changes.
2. Have qualified veterinarians assess urgency rules, wording and escalation limits; test language pathways with native speakers.
3. Verify the target deployment's email, provider sandbox, HTTPS, storage privacy, backup retention and restore process.
4. Add durable background jobs for notifications and long speech/model work, shared private object storage, distributed session coordination and metrics/alerts as demand warrants.
5. Evaluate pricing, accessibility and cooperative workflows with users before claiming affordability or impact.
