# FarmHand reproducible demonstration

This guide uses an **isolated local demonstration with fictional sample records, labelled rules and simulated payments**. Follow the candidate results in [TEST_REPORT.md](TEST_REPORT.md). Rehearse the exact sequence on that revision before presenting.

## Access and current public limitation

Public landing page: [team-hack-forge.vercel.app](https://team-hack-forge.vercel.app/). At `2026-10-24 09:48 UTC`, its `/api/health` returned HTTP 503 (`status: unavailable`) even though the landing page rendered. Public registration, persistence and the complete workflow are therefore not verified. Do not present landing-page availability as a working public backend.

The reproducible alternative is the local stack below. Local seed identities are not credentials for the public deployment. Give judges a private generated demo password for an explicitly authorized isolated instance; never publish real account credentials or create privileged production demo accounts.

## Start the isolated stack

Prerequisites: Node.js 24, Docker Engine/Desktop with Compose, and available loopback ports 3000/5037. From the repository root:

```sh
node scripts/configure.mjs --development
docker compose -f compose.dev.yaml --env-file .env.dev up --build -d
docker compose -f compose.dev.yaml --env-file .env.dev ps
```

Open [http://localhost:3000](http://localhost:3000) and confirm [web readiness](http://localhost:3000/api/health) returns HTTP 200. Read `SEED_PASSWORD` privately from the generated, ignored `.env.dev`; do not project that file or copy the value into this guide.

| Role | Local seed account | Purpose |
|---|---|---|
| Farmer | `farmer@farmhand.ai` | Owns the fictional sample farm and records |
| Veterinarian | `vet@farmhand.ai` | Reviews/accepts eligible requests and shares a plan |
| Administrator | `admin@farmhand.ai` | Optional access/configuration demonstration |

The seeder includes additional local accounts for isolation/concurrency testing. All named farmers, veterinarians and clinical histories in the seed are fictional. They are not endorsements. The no-Docker setup is documented in [README.md](../../README.md); it still requires a separately configured isolated PostgreSQL database and private generated secrets.

Stop without deleting sample data:

```sh
docker compose -f compose.dev.yaml --env-file .env.dev down
```

## Prepare the presentation

Use separate browser profiles or browsers for farmer and vet so cookies and cached state are not mixed. Keep a small phone viewport available for the farmer experience. Sign in before starting the timed pitch; keep the landing page, report screen and vet requests ready. Check that the system banner identifies demo/rules/simulated modes accurately. If containers have not been executed on the presentation host, start and verify them before the event rather than during the pitch.

The short pitch uses an existing seeded flock. A longer judge walkthrough should also create a new farmer account, farm and flock, refresh the page, and verify the records persist. Do not claim onboarding passed until that candidate browser test or manual walkthrough has run.

## Farmer-to-vet walkthrough

1. **Sign in and choose a subject.** As the local farmer, open Home, inspect the farm switcher and Animals & flocks, then choose a seeded poultry group for a new report. Explain that reports belong to a specific owned farm and subject.
2. **Report a concern.** Open Report (`/scan`), choose Text and enter this synthetic scenario: “Five birds died suddenly this morning. Several are gasping and not eating. This is a buildathon test case.” In Flock numbers today, explicitly enter deaths `5`, reduced feed and reduced water. These are test data, not a diagnosis or real farm event.
3. **Review extraction.** Select Analyse report. In the provider-free demo, any deterministic extraction must be labelled rules/demo. Inspect the report and correct numbers/notes before confirmation. If extraction is unavailable, choose Send without analysis and inspect the saved manual fields. Do not claim a live model responded when it did not.
4. **Confirm and inspect urgency.** Confirm the reviewed report, inspect source/qualified guidance and safe immediate care steps. The sample contains urgent signs; a high/critical case should be created. Open the linked case and refresh to demonstrate database persistence. The report is preliminary; the veterinarian must assess the animals.
5. **Request professional help.** From the case choose Request a vet, select an open or targeted request and submit. The UI should explain the fee/payment timing. The authoritative quote comes from the server. New requests currently use NGN 20,000 plus a 5% commission (NGN 21,000 total), while older seeded requests may differ.
6. **Accept as veterinarian.** In the separate vet profile, open Requests (`/vet/requests`) and inspect the eligible request. Accept it, then open the case. Explain that acceptance grants the assigned vet access to the relevant farmer contact/case context, not to every farm.
7. **Demonstrate payment safely.** Return to the farmer case and its payment action. Inspect the quote and visible Simulated label, initiate and verify the simulated checkout. State: “No money moves.” Do not enter a real card, use live credentials or claim settlement. If payments are Disabled, show the truthful unavailable state and skip completion claims that require payment.
8. **Share a sample plan.** As assigned vet, use Add treatment plan with harmless demonstration text: “Buildathon sample only: record feed, water and new signs; arrange professional examination; update the case if the condition changes.” Add a dated recording/follow-up step. Avoid drug names/dosages. Save and, if eligible, complete the consultation with a clearly labelled demo note.
9. **Close the loop.** As farmer, refresh the case, read the vet's plan, complete the sample step and record a follow-up outcome with a test note. Show the timeline and current status. Consultation completion and case resolution are separate actions; do not imply the animals recovered because a button was clicked.

## Optional features

Photo/voice intake, detected language and Listen playback require configured providers and browser/device permissions. Only include them in the timed demo after a separate successful rehearsal with synthetic content. Keep text/manual reporting available. Language hints cover English, Yoruba, Hausa, Igbo and Nigerian Pidgin; they do not prove validated clinical translation. If the server uses English safety wording, show that fallback explicitly.

On a disconnected device, demonstrate an unsent draft only if local storage succeeds. A draft is not a server-persisted report. Restore connectivity, send it, confirm it and refresh before claiming persistence. The offline page does not contain private clinical records.

## Failure contingencies

| Failure | Honest response |
|---|---|
| Public readiness is 503 | Use the rehearsed isolated local instance and disclose the public backend gate |
| AI/transcription/TTS unavailable | Use typed/manual input; show labelled rules and optional audio-unavailable state |
| Network lost | Show truthful device-only draft state; do not claim server submission |
| Payment disabled/provider unavailable | Show status; use only preconfigured local simulation, never live money |
| Local stack fails | Show actual previously captured, consent-safe artifacts if available; identify them as recorded; otherwise walk through architecture/source and report the failure |
| Role/session expires | Sign in to the intended isolated role; verify prior-user private state is cleared |

Do not replace unavailable evidence with invented screenshots, model output, payment success or user validation. Record the rehearsal date, candidate revision, successes and failures in the test report.
