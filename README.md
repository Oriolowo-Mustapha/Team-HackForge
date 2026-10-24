# FarmHand

FarmHand connects livestock and poultry observations to reviewed reports, case files, veterinarians, treatment plans, and verified consultation payments.

The web app uses Next.js, strict TypeScript, and HttpOnly sessions. The API uses .NET 10 LTS, EF Core, and PostgreSQL. Farmers, vets, and administrators each have a dedicated interface.

## Run the complete local demonstration

Install Node.js 24 and Docker Engine/Desktop with Compose. From the repository root:

```sh
node scripts/configure.mjs --development
docker compose -f compose.dev.yaml --env-file .env.dev up --build -d
```

Open [http://localhost:3000](http://localhost:3000). Read `SEED_PASSWORD` from the generated `.env.dev` file. Use that password with:

| Role | Email |
| --- | --- |
| Farmer | farmer@farmhand.ai |
| Veterinarian | vet@farmhand.ai |
| Administrator | admin@farmhand.ai |

The demo uses a separate database, clearly labelled demo data, rule-based analysis, and simulated payments. No money moves. Both services must be healthy before the frontend starts. Demo credentials must never be used on a production database.

To stop the demo while preserving data:

```sh
docker compose -f compose.dev.yaml --env-file .env.dev down
```

## Run without Docker

Install the SDK specified by `global.json`, PostgreSQL 18, and Node.js 24. Create a database and an ignored `src/FarmHand.API/appsettings.Development.json` using `appsettings.example.json` as the configuration reference. Supply a real local database password, a random JWT key, and a 32-byte base64 field-encryption key. For a seeded demo, explicitly set `Demo:Enabled=true`, `Seed:Password`, `Ai:DemoFallback=true`, and `Flutterwave:Mode=Simulated`.

```sh
dotnet restore FarmHand.slnx --locked-mode
dotnet run --project src/FarmHand.API
```

In a second terminal:

```sh
cd web
npm ci
# Copy .env.example to .env.local if you need configuration overrides.
npm run dev
```

The API listens on `http://localhost:5037`. The web app defaults to that same port through `FARMHAND_API_URL`; it does not expose API credentials to browser JavaScript. If your API uses a different address, set `FARMHAND_API_URL` before starting the web server.

## Verify

```sh
dotnet test FarmHand.slnx -c Release
cd web
npm ci
npm run lint
npm run typecheck
npm test
npm run build
npm audit --omit=dev
npx playwright install chromium --only-shell
```

Run the production web build against an isolated seeded API/database, set `E2E_PASSWORD` to that database's seed password, and run `npm run test:e2e`. The Playwright suite rejects remote targets because it creates test accounts and clinical records. It covers role navigation, registration/setup, permissions, CSRF, competing vet acceptance, simulated payment verification, treatment plans, and offline navigation. CI provisions this isolated stack automatically.

`npm run build` explicitly uses Webpack for reliable builds on Windows and Linux. For a deployed standalone build, copy `public/` and `.next/static/` into the standalone output; the web Dockerfile does this automatically.

## Production

Follow [DEPLOYMENT.md](DEPLOYMENT.md) for HTTPS, secrets, migrations, initial administrator creation, webhook delivery, backups, and release validation. Production refuses demo accounts, demo text extraction, simulated payments, missing email recovery, weak keys, and wildcard host filtering. Confirmation can still use explicitly labelled deterministic urgency rules when assessment providers are unavailable; this is preliminary guidance, not a diagnosis or evidence of a live AI call.

See [API.md](API.md) for the current API conventions and [SECURITY.md](SECURITY.md) for security boundaries and operational requirements.

## Buildathon submission

Start with the [buildathon evidence index](docs/buildathon/README.md) for the overview, PRD, architecture, security/test reports, validation plan and reproducible demo/pitch materials. Source review, local verification, simulated features and unverified providers are labelled separately. The [test report](docs/buildathon/TEST_REPORT.md) records the submitted candidate's actual results and public deployment limitations; landing-page availability alone does not establish backend readiness.
