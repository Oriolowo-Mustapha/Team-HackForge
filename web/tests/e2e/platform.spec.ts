import {
  test,
  expect,
  type APIRequestContext,
  type PlaywrightWorkerArgs,
} from "@playwright/test";

const baseURL = process.env.E2E_BASE_URL ?? "http://127.0.0.1:3000";
const password = process.env.E2E_PASSWORD;

test.beforeAll(async ({ request }) => {
  await expect
    .poll(
      async () => {
        try {
          return (await request.get("/api/health")).status();
        } catch {
          return 0;
        }
      },
      { timeout: 60_000, intervals: [500, 1000, 2000] },
    )
    .toBe(200);
});

async function loginApi(
  playwright: PlaywrightWorkerArgs["playwright"],
  email: string,
) {
  if (!password)
    throw new Error(
      "Set E2E_PASSWORD to the password of the isolated demo/test database. Never run this suite against production.",
    );
  const context = await playwright.request.newContext({
    baseURL,
    extraHTTPHeaders: {
      "x-farmhand-csrf": "1",
      origin: new URL(baseURL).origin,
    },
  });
  const response = await context.post("/api/session/login", {
    data: { email, password },
  });
  expect(response.status(), await response.text()).toBe(200);
  const body = await response.json();
  expect(body).not.toHaveProperty("token");
  expect(body).not.toHaveProperty("refreshToken");
  const state = await context.storageState();
  expect(
    state.cookies
      .filter((cookie) => cookie.name.startsWith("fh_"))
      .every((cookie) => cookie.httpOnly),
  ).toBe(true);
  return context;
}

async function json(
  context: APIRequestContext,
  path: string,
  data?: unknown,
  headers?: Record<string, string>,
) {
  const response =
    data === undefined
      ? await context.get(`/api/bff/${path}`)
      : await context.post(`/api/bff/${path}`, { data, headers });
  expect(
    response.ok(),
    `${path}: ${response.status()} ${await response.text()}`,
  ).toBe(true);
  return response.json();
}

test("public pages, form validation, and static assets", async ({
  page,
  request,
}) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1 })).toContainText(
    "Spot trouble early",
  );
  await expect(page.getByAltText("Hens in a poultry house")).toBeVisible();
  await page.getByRole("link", { name: "Sign in", exact: true }).click();
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(
    page
      .getByRole("alert")
      .filter({ hasText: "Enter your email and password" }),
  ).toBeVisible();
  expect((await request.get("/sw.js")).status()).toBe(200);
  expect((await request.get("/api/health")).status()).toBe(200);
});

for (const role of ["farmer", "vet", "admin"] as const) {
  test(`${role} can sign in and open every primary navigation screen`, async ({
    page,
  }) => {
    test.skip(!password, "E2E_PASSWORD is required for authenticated tests");
    await page.goto("/login");
    await page.getByLabel("Email", { exact: true }).fill(`${role}@farmhand.ai`);
    await page.getByLabel("Password", { exact: true }).fill(password!);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page).toHaveURL(
      new RegExp(`/${role === "farmer" ? "home" : role}$`),
    );
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    const routes =
      role === "farmer"
        ? ["/animals", "/reports", "/cases", "/vets", "/payments", "/profile"]
        : role === "vet"
          ? ["/vet/requests", "/cases", "/payments", "/vet/payout", "/profile"]
          : [
              "/admin/users",
              "/admin/vets",
              "/admin/farms",
              "/admin/cases",
              "/admin/payments",
              "/admin/audit",
              "/admin/system",
              "/profile",
            ];
    for (const route of routes) {
      await page.goto(route);
      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
      await expect(
        page.getByRole("status", { name: "Loading", exact: true }),
      ).toHaveCount(0);
      await expect(
        page.getByText("This page could not be found.", { exact: true }),
      ).toHaveCount(0);
      await expect(
        page.locator('[role="alert"]:not(#__next-route-announcer__)'),
      ).toHaveCount(0);
    }
  });
}

test("new farmer registration and first-farm setup", async ({ page }) => {
  await page.goto("/register");
  await page
    .getByLabel("Full name", { exact: true })
    .fill("FarmHand Workflow Test");
  await page
    .getByLabel("Email", { exact: true })
    .fill(`farmhand-${Date.now()}@example.invalid`);
  await page
    .getByLabel("Password", { exact: true })
    .fill("Test-Account-Workflow-123!");
  await page
    .getByLabel("Confirm password", { exact: true })
    .fill("Test-Account-Workflow-123!");
  await page
    .getByRole("button", { name: "Create account", exact: true })
    .click();
  await expect(page).toHaveURL(/\/setup$/);
  await page
    .getByLabel("Farm name", { exact: true })
    .fill("Workflow Test Farm");
  await page.getByRole("radio", { name: "Type manually", exact: true }).click();
  await page.getByLabel("Town or City / LGA", { exact: true }).fill("Lagos");
  await page.getByLabel("State", { exact: true }).selectOption("Lagos");
  await page.getByRole("button", { name: "Continue", exact: true }).click();
  await page.getByRole("radio", { name: "Layers", exact: true }).click();
  await page.getByLabel("Group name", { exact: true }).fill("Test flock");
  await page.getByLabel("How many animals?", { exact: false }).fill("20");
  await page.getByRole("button", { name: "Finish setup", exact: true }).click();
  await expect(page).toHaveURL(/\/home$/);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
});

test("permissions, CSRF, and complete clinical/payment lifecycle", async ({
  playwright,
  browser,
}) => {
  test.skip(!password, "E2E_PASSWORD is required for authenticated tests");
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const other = await loginApi(playwright, "farmer2@farmhand.ai");
  const vet = await loginApi(playwright, "vet@farmhand.ai");
  const secondVet = await loginApi(playwright, "vet2@farmhand.ai");
  const farmerBrowser = await browser.newContext({
    baseURL,
    storageState: await farmer.storageState(),
  });
  try {
    const session = await (await farmer.get("/api/session")).json();
    for (let i = 0; i < 25; i++)
      expect((await farmer.get("/api/session")).status()).toBe(200);
    const farmId = session.user.farms[0].id;
    expect(
      (await other.get(`/api/bff/farms/${farmId}/overview`)).status(),
    ).toBe(403);
    expect((await farmer.get("/api/bff/audit-events")).status()).toBe(403);
    expect(
      (
        await farmer.post("/api/bff/farms", {
          headers: {
            "x-farmhand-csrf": "",
            origin: "https://untrusted.example",
          },
          data: { displayName: "Blocked", location: "Test" },
        })
      ).status(),
    ).toBe(403);
    const groups = await json(
      farmer,
      `livestock/categories/details?farmId=${farmId}`,
    );
    const group =
      groups.find((item: { species: string }) => item.species === "Chicken") ??
      groups[0];
    const reportResponse = await farmer.post("/api/bff/observations", {
      multipart: {
        farmId,
        livestockCategoryId: group.id,
        text: "Several birds are lethargic with reduced feed intake and three deaths today.",
        manualEntry: "true",
        mortalityCount: "3",
        idempotencyKey: crypto.randomUUID(),
      },
    });
    expect(reportResponse.ok(), await reportResponse.text()).toBe(true);
    const report = await reportResponse.json();
    const confirmation = await json(
      farmer,
      `observations/${report.id}/confirm`,
      {
        overrideSeverity: 2,
        overrideReason:
          "Three deaths and several lethargic birds require a veterinarian.",
        farmerNotes: "Three deaths today, reduced appetite.",
      },
    );
    expect(confirmation.autoCreatedIncidentId).toBeTruthy();
    const incidentId = confirmation.autoCreatedIncidentId;
    const created = await json(
      farmer,
      `VetCoordination/incidents/${incidentId}/vet-request`,
      {
        incidentId,
        requestType: 0,
        farmerCost: 1500,
        vetPartnerId: null,
        description: "Please review the flock",
        preferredVisitDate: null,
      },
    );
    const requestId = created.id;
    const open = await json(vet, `VetCoordination/vet-requests/${requestId}`);
    expect(open.farmer).toBeNull();
    const claims = await Promise.all([
      vet.post(`/api/bff/VetCoordination/vet-requests/${requestId}/respond`, {
        data: { vetRequestId: requestId },
      }),
      secondVet.post(
        `/api/bff/VetCoordination/vet-requests/${requestId}/respond`,
        { data: { vetRequestId: requestId } },
      ),
    ]);
    expect(claims.filter((response) => response.ok())).toHaveLength(1);
    expect(
      claims.filter(
        (response) => response.status() === 409 || response.status() === 403,
      ),
    ).toHaveLength(1);
    const acceptedVet = claims[0].ok() ? vet : secondVet;
    const assigned = await json(
      acceptedVet,
      `VetCoordination/vet-requests/${requestId}`,
    );
    expect(assigned.farmer.email).toBe("farmer@farmhand.ai");
    const quote = await json(
      farmer,
      `payments/quote?vetRequestId=${requestId}`,
    );
    expect(quote.total).toBe(1575);
    expect(quote.mode).toBe("Simulated");
    const key = crypto.randomUUID();
    expect(
      (
        await farmer.post("/api/bff/payments/initiate", {
          data: { vetRequestId: requestId, amount: 1, currency: "NGN" },
        })
      ).status(),
    ).toBe(400);
    const payment = await json(
      farmer,
      "payments/initiate",
      { vetRequestId: requestId, amount: 1500, currency: "NGN" },
      { "idempotency-key": key },
    );
    const repeated = await json(
      farmer,
      "payments/initiate",
      { vetRequestId: requestId, amount: 1500, currency: "NGN" },
      { "idempotency-key": key },
    );
    expect(repeated.transactionReference).toBe(payment.transactionReference);
    expect(repeated.idempotencyKey).toBe(key);
    expect(
      (
        await other.post("/api/bff/payments/verify", {
          data: { transactionReference: payment.transactionReference },
        })
      ).status(),
    ).toBe(403);
    const checkout = await farmerBrowser.newPage();
    await checkout.goto(`/pay/${requestId}`);
    await checkout
      .getByRole("button", { name: "Confirm simulated payment", exact: true })
      .click();
    await expect(
      checkout.getByText("Completed", { exact: true }),
    ).toBeVisible();
    const verified = await json(farmer, "payments/verify", {
      transactionReference: payment.transactionReference,
    });
    expect(verified.payment.status).toBe("Completed");
    expect(verified.payment.mode).toBe("Simulated");
    await json(acceptedVet, "treatment-plans", {
      vetRequestId: requestId,
      instructionsText:
        "Isolate affected birds, provide clean water and follow the veterinarian's advice.",
      language: "en",
      schedule: [
        {
          action: "Review flock and water intake",
          dueDate: new Date(Date.now() + 86400000).toISOString(),
        },
      ],
    });
    const complete = await json(
      acceptedVet,
      `VetCoordination/vet-requests/${requestId}/complete`,
      { notes: "Consultation and treatment plan shared with farmer." },
    );
    expect(complete.status).toBe(2);
    expect(
      (
        await acceptedVet.post(
          `/api/bff/VetCoordination/vet-requests/${requestId}/respond`,
          { data: { vetRequestId: requestId } },
        )
      ).status(),
    ).toBe(409);
    const caseFile = await json(farmer, `incidents/${incidentId}/case`);
    expect(caseFile.treatmentPlans.length).toBeGreaterThan(0);
    await checkout.goto(`/cases/${incidentId}`);
    await expect(checkout.getByRole("main")).toContainText(
      "Isolate affected birds, provide clean water",
    );
  } finally {
    await farmerBrowser.close();
    await Promise.all(
      [farmer, other, vet, secondVet].map((context) => context.dispose()),
    );
  }
});

test("administrator onboarding enforces temporary passwords and revokes deactivated accounts", async ({
  playwright,
}) => {
  const admin = await loginApi(playwright, "admin@farmhand.ai");
  const email = `vet-onboarding-${Date.now()}@example.invalid`;
  const temporaryPassword = "Temporary-Veterinarian-123!";
  const newPassword = "Changed-Veterinarian-456!";
  const vet = await playwright.request.newContext({
    baseURL,
    extraHTTPHeaders: {
      "x-farmhand-csrf": "1",
      origin: new URL(baseURL).origin,
    },
  });
  try {
    const created = await json(admin, "admin/vets", {
      email,
      temporaryPassword,
      fullName: "Dr. Workflow Test",
      specialization: "Poultry",
      contact: "Test veterinarian",
      phoneNumber: null,
    });
    const login = await vet.post("/api/session/login", {
      data: { email, password: temporaryPassword },
    });
    expect(login.ok()).toBe(true);
    expect((await login.json()).mustChangePassword).toBe(true);
    expect(
      (await vet.get("/api/bff/VetCoordination/vet-requests")).status(),
    ).toBe(403);
    await json(vet, "auth/change-password", {
      currentPassword: temporaryPassword,
      newPassword,
    });
    const changed = await vet.post("/api/session/login", {
      data: { email, password: newPassword },
    });
    expect(changed.ok()).toBe(true);
    expect((await changed.json()).mustChangePassword).toBe(false);
    await json(admin, `admin/users/${created.userId}/deactivate`, {});
    expect(
      (await vet.get("/api/bff/VetCoordination/vet-requests")).status(),
    ).toBe(401);
  } finally {
    await admin.dispose();
    await vet.dispose();
  }
});

test("offline navigation uses a public fallback", async ({ page, context }) => {
  await page.goto("/");
  await page.evaluate(() => navigator.serviceWorker.ready);
  await page.reload();
  await context.setOffline(true);
  await page.goto("/home");
  await expect(page.getByRole("heading", { level: 1 })).toContainText(
    /offline|connection/i,
  );
  await context.setOffline(false);
});

test("farmer dashboard fits a phone and exposes mobile navigation", async ({
  playwright,
  browser,
}) => {
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const context = await browser.newContext({
    baseURL,
    viewport: { width: 390, height: 844 },
    storageState: await farmer.storageState(),
  });
  try {
    const page = await context.newPage();
    await page.goto("/home");
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await expect(
      page.getByRole("navigation", { name: "Main", exact: true }),
    ).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth + 1,
      ),
    ).toBe(true);
    await expect(
      page.getByRole("link", { name: "Report a health concern", exact: true }),
    ).toBeVisible();
  } finally {
    await context.close();
    await farmer.dispose();
  }
});

test("GPS locations accept zero coordinates and survive a farm-name edit", async ({ playwright, browser }) => {
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const context = await browser.newContext({ baseURL, storageState: await farmer.storageState(), geolocation: { latitude: 0, longitude: 0 }, permissions: ["geolocation"] });
  const farmName = `GPS regression ${Date.now()}`;
  try {
    expect((await farmer.post("/api/bff/farms", { data: { displayName: "Invalid coordinate pair", location: "Test", latitude: 1 } })).status()).toBe(400);
    await context.route("https://nominatim.openstreetmap.org/**", (route) => route.fulfill({ json: { address: { city: "Lagos", state: "Lagos", country: "Nigeria" } } }));
    const page = await context.newPage();
    await page.goto("/setup?another=1");
    await page.getByLabel("Farm name", { exact: true }).fill(farmName);
    await page.getByRole("button", { name: "Detect My Location", exact: true }).click();
    await expect(page.getByText("GPS Location Locked", { exact: true })).toBeVisible();
    await page.getByRole("button", { name: "Continue", exact: true }).click();
    await page.getByRole("button", { name: "Skip for now", exact: true }).click();
    await expect(page).toHaveURL(/\/home$/);
    await page.goto("/profile");
    await page.getByLabel("Farm name", { exact: true }).fill(`${farmName} renamed`);
    await page.getByRole("button", { name: "Save farm", exact: true }).click();
    await expect(page.getByText("Farm details saved", { exact: true })).toBeVisible();
    const farms = await json(farmer, "farms");
    const saved = farms.find((farm: { displayName: string }) => farm.displayName === `${farmName} renamed`);
    expect(saved.latitude).toBe(0);
    expect(saved.longitude).toBe(0);
    expect(saved.locationSource).toBe("device_gps");
  } finally { await context.close(); await farmer.dispose(); }
});
