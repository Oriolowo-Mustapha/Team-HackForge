import { defineConfig, devices } from "@playwright/test";

const target = new URL(process.env.E2E_BASE_URL ?? "http://127.0.0.1:3000");
if (!["localhost", "127.0.0.1", "[::1]"].includes(target.hostname))
  throw new Error(
    "E2E tests create test accounts and require a local, isolated database.",
  );
if (!process.env.E2E_PASSWORD)
  throw new Error("Set E2E_PASSWORD for the isolated demo/test database.");

export default defineConfig({
  testDir: "./tests/e2e",
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://127.0.0.1:3000",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
