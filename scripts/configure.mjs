import { randomBytes } from "node:crypto";
import { writeFile, access } from "node:fs/promises";
import { fileURLToPath } from "node:url";

const development = process.argv.includes("--development");
const target = new URL(development ? "../.env.dev" : "../.env", import.meta.url);
try { await access(target); throw new Error(`${fileURLToPath(target)} already exists; refusing to replace its encryption keys or database credentials.`); }
catch (error) { if (error.code !== "ENOENT") throw error; }
const values = {
  PUBLIC_HOST: "farmhand.example.com",
  POSTGRES_PASSWORD: randomBytes(32).toString("hex"),
  JWT_SECRET: randomBytes(48).toString("hex"),
  FIELD_ENCRYPTION_KEY: randomBytes(32).toString("base64"),
  SEED_PASSWORD: `FarmHand-${randomBytes(16).toString("hex")}-123!`,
  BREVO_API_KEY: "CHANGE_ME_verified_email_provider_key",
  BREVO_FROM_ADDRESS: "no-reply@example.com",
  PAYMENTS_MODE: development ? "Simulated" : "Disabled",
  FLUTTERWAVE_SECRET_KEY: "",
  FLUTTERWAVE_WEBHOOK_SECRET: "",
  GEMINI_API_KEY: "",
  GROQ_API_KEY: "",
  YARNGPT_API_KEY: "",
};
await writeFile(target, Object.entries(values).map(([key, value]) => `${key}=${value}`).join("\n") + "\n", { flag: "wx", mode: 0o600 });
console.log(`Created ${fileURLToPath(target)} with unique local secrets. Keep it private and backed up.`);
console.log(development ? "Run: docker compose -f compose.dev.yaml --env-file .env.dev up --build -d" : "Set your real domain and verified email-provider credentials in .env before production startup.");
