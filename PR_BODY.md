## Summary

Makes FarmHand work end to end and adds a real web app.

**Backend (.NET 9)**
- Payment creation fixed. Flutterwave Standard now uses our own `tx_ref`, server-side `verify_by_reference` and webhook re-verification. A clearly labelled **Simulated** mode (HMAC-signed references, no money moves) is used for demos.
- AI: Gemini with a Groq fallback replaces the retired models. When no AI provider is configured, a transparent rule-based triage is used, and every result is labelled with its source. An AI outage returns `503 ai_unavailable` instead of a fake success.
- New workflows:
  - observations with text, voice or photo, review and confirm;
  - case files with timeline, actions, follow-ups and prevention notes;
  - vet request accept, decline, cancel and complete, plus treatment plans with schedules;
  - payment quote, verify and cancel;
  - admin overview, users, vets, farms, payments, audit log and system health.
- Security:
  - audit log restricted to admins;
  - atomic vet claim, so two vets can't accept the same request;
  - token versioning and forced password change;
  - rate limits;
  - signature-checked uploads;
  - encrypted payout fields;
  - cross-farm access returns 403;
  - farmer contact details shown only to the vet who accepted.
- EF Core migration `PlatformCompletion`, plus a deterministic demo seeder that labels its records as demo data.

**Web app (`web/`, Next.js 16, strict TypeScript, Tailwind v4)**
- Backend-for-frontend: tokens live only in HttpOnly cookies, browser JavaScript never sees them, and mutations require a CSRF header and an origin check.
- Design system built from the brief's palette, with a dark sidebar on desktop and a dark bottom nav with a raised "Report" button on mobile.
- Screens:
  - landing, sign in and registration, password reset;
  - first-farm setup and the farmer dashboard;
  - animals and groups;
  - **health scanner**: camera, upload, voice or text, real upload progress, drafts saved on the device, manual fallback;
  - report review and confirm;
  - cases and the case file.
- PWA manifest and icons; CC0 livestock photography with credits.

## Not finished yet (follow-up commits)
- Payment, vet and admin screens in the web app; the service worker and offline page caching.
- Playwright journeys, updated .NET unit and integration tests, and the README, deployment and demo docs.

## Verification
- `npm run typecheck` and `npm run lint` pass in `web/`.
- API smoke test against local Postgres covered:
  - report → confirm → High-severity case;
  - cross-farm access → 403;
  - open requests hide contact details;
  - atomic accept;
  - simulated payment, including idempotent initiation;
  - treatment plan, then the vet completes the request;
  - invalid transition → 409;
  - farmer reading audit events → 403.
- Screens checked in Chromium at 390 px and 1440 px against the seeded demo data.
- The existing .NET test projects have **not** been updated for the new payment semantics yet, so some tests are expected to fail until the follow-up commit.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01U15LREww1P8tGbHZvFVnfA
