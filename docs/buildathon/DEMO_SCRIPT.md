# FarmHand three-minute pitch and live demo

Use this only after rehearsing the candidate in [DEMO_GUIDE.md](DEMO_GUIDE.md) and recording successful steps in [TEST_REPORT.md](TEST_REPORT.md). Pre-authenticate separate farmer/vet browser profiles. Use fictional sample data, labelled deterministic rules and simulated payment. Public backend readiness was unavailable during review; identify the local demonstration honestly.

| Time | Screen transition | Spoken script / action |
|---|---|---|
| 0:00–0:20 | Landing → farmer Home | “When a farmer notices breathing difficulty or deaths, the veterinarian needs the signs, timing and history. FarmHand brings those observations and professional follow-up into one persistent record. I’m showing an isolated local demo with fictional cases.” |
| 0:20–0:35 | Animals & flocks → Report | “The report belongs to this farmer’s farm and flock. Farmers can type or enter numbers manually, with optional photo and voice when providers are configured.” Select the rehearsed flock. |
| 0:35–1:00 | Report → review | Enter the prepared synthetic breathing/deaths note and explicit death count. “The farmer checks and corrects the extracted fields before confirming. This demo labels its rule-based analysis; it does not pretend that a model was called.” Inspect the numbers and confirm. |
| 1:00–1:25 | Report → linked case | “This is preliminary urgency guidance, not a confirmed diagnosis. Serious recognized signs are escalated, and the next step is veterinary review. The confirmed report and case are saved together.” Open case; refresh only if rehearsed within time. Request a vet. |
| 1:25–1:55 | Vet Requests → case/plan | Switch to the vet profile. “An eligible vet accepts the request and gains the relevant case context. The API checks assignment and ownership.” Accept; open the case and share the prepared non-prescribing sample follow-up plan. If showing payment, use the prepared local simulated verification and say “No money moves.” |
| 1:55–2:20 | Farmer case → treatment/follow-up | Switch back. “The farmer can read the vet’s instructions, complete a dated step and record progress. The timeline keeps the observation and professional response connected.” Show the saved plan and add the prepared test follow-up. |
| 2:20–2:45 | Architecture/evidence slide | “Next.js and a server-side BFF connect to a .NET 10 API and PostgreSQL. Tokens stay in HttpOnly web cookies. We test role boundaries, payments and the care workflow. The evidence report separates local verification from unverified providers and deployment.” Show only actual completed checks/benchmark results. |
| 2:45–3:00 | Roadmap/closing slide | “Our next evidence is farmer usability, qualified-vet review and language testing. We still need to restore public backend readiness and verify real provider operations. FarmHand keeps the farmer, the observations and the veterinarian in the same care journey.” |

## Timing discipline

Use a short prepared note and harmless sample plan. Have the next role already signed in. Do not type credentials on the projector. If a provider call takes more than a few seconds, state the fallback and switch to the rehearsed manual path. The full judge walkthrough can cover registration, farm creation, payment quote and mobile details after the timed pitch.

## Contingency lines

- **Public backend unavailable:** “The public landing page is online, but its readiness check failed during review. This is the isolated local application; we are not claiming a working public backend.”
- **AI or voice unavailable:** “This provider is unavailable. We keep the entered observations, use explicit manual intake and label deterministic guidance. The professional-review workflow remains the goal.”
- **Payment unavailable:** “This environment has payments disabled/unavailable. We will not represent a redirect as paid or move real money. The recorded local simulation is separate evidence if available.”
- **Local application failure:** “This action did not complete. I’ll show the actual recorded walkthrough if available, clearly labelled, and the architecture/evidence. We will log the failed step rather than claim it passed.”
- **No recorded fallback exists:** Show the source/evidence matrix and state the missing demonstration. Do not invent screenshots or results.

Do not claim clinical accuracy, validated translation, farmer interviews, adoption, prevented losses, live settlement or a numerical performance result unless that exact evidence has been collected and linked.
