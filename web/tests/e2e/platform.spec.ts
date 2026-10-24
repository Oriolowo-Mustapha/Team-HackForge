import {
  test,
  expect,
  type APIRequestContext,
  type PlaywrightWorkerArgs,
  type Page,
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
  let response = await context.post("/api/session/login", {
    data: { email, password },
  });
  if (response.status() === 429) {
    // The suite creates real sessions and must respect the configured 20/IP/minute budget.
    const retryAfter = Number(response.headers()["retry-after"] ?? "60");
    await new Promise((resolve) => setTimeout(resolve, Math.min(60, Math.max(1, Number.isFinite(retryAfter) ? retryAfter : 60)) * 1000));
    response = await context.post("/api/session/login", { data: { email, password } });
  }
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
        image: {
          name: "synthetic-test-icon.png",
          mimeType: "image/png",
          buffer: await (await farmer.get("/icons/favicon-32.png")).body(),
        },
      },
    });
    expect(reportResponse.ok(), await reportResponse.text()).toBe(true);
    const report = await reportResponse.json();
    expect(report.imageUrl).toMatch(/^\/api\/media\//);
    const mediaPath = report.imageUrl.replace("/api/media/", "/api/bff/media/");
    const ownerMedia = await farmer.get(mediaPath);
    expect(ownerMedia.status()).toBe(200);
    expect(ownerMedia.headers()["cache-control"]).toContain("no-store");
    expect(ownerMedia.headers()["content-type"]).toContain("image/png");
    expect((await other.get(mediaPath)).status()).toBe(404);
    expect((await vet.get(mediaPath)).status()).toBe(404);
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
        vetPartnerId: null,
        description: "Please review the flock",
        preferredVisitDate: null,
      },
    );
    const requestId = created.id;
    const open = await json(vet, `VetCoordination/vet-requests/${requestId}`);
    expect(open.farmerCost).toBe(20_000);
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
    expect((await acceptedVet.get(mediaPath)).status()).toBe(200);
    const assigned = await json(
      acceptedVet,
      `VetCoordination/vet-requests/${requestId}`,
    );
    expect(assigned.farmer.email).toBe("farmer@farmhand.ai");
    const quote = await json(
      farmer,
      `payments/quote?vetRequestId=${requestId}`,
    );
    expect(quote.vetFee).toBe(20_000);
    expect(quote.total).toBe(21_000);
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
      { vetRequestId: requestId, amount: 20_000, currency: "NGN" },
      { "idempotency-key": key },
    );
    const repeated = await json(
      farmer,
      "payments/initiate",
      { vetRequestId: requestId, amount: 20_000, currency: "NGN" },
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
      checkout.getByText("Payment complete", { exact: true }),
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

async function storedDrafts(page: Page) {
  return page.evaluate(async () => {
    // Observing a draft must not create an empty database before the application initializes its store.
    if (!(await indexedDB.databases()).some((database) => database.name === "farmhand")) return [];
    const db = await new Promise<IDBDatabase | null>((resolve, reject) => {
      const request = indexedDB.open("farmhand");
      request.onupgradeneeded = () => { request.transaction?.abort(); resolve(null); };
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
    if (!db) return [];
    try {
      if (!db.objectStoreNames.contains("report-drafts")) return [];
      const rows = await new Promise<Array<{ id: string; farmId: string; text: string; createdAt: number; idempotencyKey: string }>>((resolve, reject) => {
        const request = db.transaction("report-drafts").objectStore("report-drafts").getAll();
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
      });
      return rows.map(({ id, farmId, text, createdAt, idempotencyKey }) => ({ id, farmId, text, createdAt, idempotencyKey }));
    } finally { db.close(); }
  });
}

test("farmer and vet finish the clinical workflow through the interface", async ({ playwright, browser }) => {
  test.setTimeout(120_000);
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const vet = await loginApi(playwright, "vet@farmhand.ai");
  const farmerContext = await browser.newContext({ baseURL, storageState: await farmer.storageState() });
  const vetContext = await browser.newContext({ baseURL, storageState: await vet.storageState() });
  try {
    const session = await (await farmer.get("/api/session")).json();
    const vetSession = await (await vet.get("/api/session")).json();
    const farmId = session.user.farms[0].id;
    const groups = await json(farmer, `livestock/categories/details?farmId=${farmId}`);
    const group = groups[0];
    const page = await farmerContext.newPage();
    await page.goto(`/scan?group=${group.id}`);
    await page.getByRole("tab", { name: "Text", exact: true }).click();
    await page.getByLabel("Describe what you see", { exact: true }).fill("Several birds are gasping, weak and eating less since yesterday. Three died suddenly.");
    await page.getByRole("button", { name: "Flock numbers today", exact: false }).click();
    await page.getByLabel("Deaths", { exact: true }).fill("3");
    await page.getByLabel("Feed", { exact: true }).fill("reduced to one bag");
    await page.getByLabel("Water", { exact: true }).fill("half the usual amount");
    await page.getByLabel("Eggs collected", { exact: true }).fill("12");
    const submission = page.waitForResponse((response) => response.url().endsWith("/api/bff/observations") && response.request().method() === "POST");
    await page.getByRole("button", { name: "Analyse report", exact: true }).click();
    const submitted = await (await submission).json();
    await expect(page).toHaveURL(new RegExp(`/reports/${submitted.id}`));
    await expect(page.getByLabel("Deaths", { exact: true })).toHaveValue("3");
    await expect(page.getByRole("textbox", { name: "Feed Optional", exact: true })).toHaveValue("reduced to one bag");
    await expect(page.getByRole("textbox", { name: "Water Optional", exact: true })).toHaveValue("half the usual amount");
    await expect(page.getByRole("textbox", { name: "Eggs Optional", exact: true })).toHaveValue("12");
    await expect(page.getByText("Original report text", { exact: true })).toBeVisible();

    // Exercise the actual Listen control without calling a paid provider.
    await page.route("**/api/bff/voice/respond", (route) => route.fulfill({ status: 503, json: { title: "Speech service unavailable" } }));
    const speech = page.waitForRequest((request) => request.url().endsWith("/api/bff/voice/respond"));
    await page.getByRole("button", { name: "Listen", exact: true }).click();
    await speech;
    await expect(page.getByText("Audio is unavailable right now. Your report text is still available to read.", { exact: true })).toBeVisible();
    await page.unroute("**/api/bff/voice/respond");

    await page.getByRole("button", { name: "Confirm report", exact: true }).click();
    await expect(page.getByText("Report confirmed", { exact: true })).toBeVisible();
    await expect(page.getByText(/This is a triage suggestion, not a diagnosis/)).toBeVisible();
    await expect(page.getByText(/^(High|Critical) ·/)).toBeVisible();
    await expect(page.getByText("Case opened", { exact: true })).toBeVisible();
    await page.getByRole("link", { name: "Ask a vet", exact: true }).click();
    await expect(page).toHaveURL(/\/cases\/[^?]+\?request=1$/);
    const incidentId = new URL(page.url()).pathname.split("/").at(-1)!;
    const requestDialog = page.getByRole("dialog", { name: "Ask a vet", exact: true });
    await requestDialog.getByRole("radio", { name: "Remote review", exact: true }).click();
    await requestDialog.getByLabel("Vet", { exact: true }).selectOption(vetSession.user.vetProfile.id);
    await expect(requestDialog.getByText(/Fixed consultation fee/)).toContainText("21,000");
    const requestCreated = page.waitForResponse((response) => response.url().includes("/vet-request") && response.request().method() === "POST");
    await requestDialog.getByRole("button", { name: "Send request", exact: true }).click();
    const request = await (await requestCreated).json();
    await expect(requestDialog).toBeHidden();

    const doctor = await vetContext.newPage();
    await doctor.goto(`/vet/requests/${request.id}`);
    await doctor.getByRole("button", { name: "Accept request", exact: true }).click();
    await doctor.getByRole("dialog").getByRole("button", { name: "Confirm", exact: true }).click();
    await expect(doctor.getByRole("button", { name: "Write treatment plan", exact: true })).toBeVisible();
    await page.reload();
    await page.getByRole("link", { name: /Review and pay/ }).click();
    await expect(page.getByText("Simulated payment", { exact: true })).toBeVisible();
    const verificationResponse = page.waitForResponse((response) => response.url().endsWith("/api/bff/payments/verify") && response.request().method() === "POST");
    await page.getByRole("button", { name: "Confirm simulated payment", exact: true }).click();
    const verifiedPayment = await (await verificationResponse).json();
    await expect(page.getByText("Payment complete", { exact: true })).toBeVisible();
    await page.goto(`/payment/callback?tx_ref=${encodeURIComponent(verifiedPayment.payment.transactionReference)}`);
    await expect(page.getByText("Payment confirmed", { exact: true })).toBeVisible();

    await doctor.reload();
    await doctor.getByRole("button", { name: "Write treatment plan", exact: true }).click();
    const plan = `UI follow-up ${Date.now()}: isolate affected birds, provide clean water and arrange an examination.`;
    const planDialog = doctor.getByRole("dialog", { name: "Treatment plan", exact: true });
    await planDialog.getByLabel("Instructions", { exact: true }).fill(plan);
    await planDialog.getByLabel("Step 1", { exact: true }).fill("Review water intake tomorrow");
    await planDialog.getByRole("button", { name: "Share plan", exact: true }).click();
    await expect(planDialog).toBeHidden();
    await doctor.getByRole("textbox", { name: "Consultation notes or reason Optional", exact: true }).fill("Remote review completed; follow the plan and contact me if signs worsen.");
    await doctor.getByRole("button", { name: "Complete consultation", exact: true }).click();
    await doctor.getByRole("dialog").getByRole("button", { name: "Confirm", exact: true }).click();
    await expect(doctor.getByRole("dialog")).toBeHidden();

    await page.goto(`/cases/${incidentId}`);
    await expect(page.getByRole("main")).toContainText(plan);
    await page.getByRole("radio", { name: "Improving", exact: true }).click();
    await page.getByLabel("Follow-up notes", { exact: true }).fill("Water intake is improving after the veterinary review.");
    await page.getByRole("button", { name: "Save follow-up", exact: true }).click();
    await expect(page.getByText("Follow-up recorded", { exact: true })).toBeVisible();
    await page.reload();
    await expect(page.getByRole("main")).toContainText(plan);
    await expect(page.getByRole("main")).toContainText("Water intake is improving after the veterinary review.");
    await expect(page.getByRole("heading", { name: "Timeline", exact: true })).toBeVisible();
  } finally {
    await farmerContext.close(); await vetContext.close(); await farmer.dispose(); await vet.dispose();
  }
});

test("offline drafts return to their original farm and explicit zero survives analysis", async ({ playwright, browser }) => {
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const context = await browser.newContext({ baseURL, storageState: await farmer.storageState() });
  try {
    const session = await (await farmer.get("/api/session")).json();
    const farmId = session.user.farms[0].id;
    const groups = await json(farmer, `livestock/categories/details?farmId=${farmId}`);
    const otherFarm = await json(farmer, "farms", { displayName: `Draft switch ${Date.now()}`, location: "Local test" });
    const page = await context.newPage();
    const note = `Draft recovery ${Date.now()}: three birds died yesterday; no deaths today.`;
    await page.goto(`/scan?group=${groups[0].id}`);
    await page.getByRole("tab", { name: "Photo", exact: true }).focus();
    await page.keyboard.press("End");
    await expect(page.getByRole("tab", { name: "Text", exact: true })).toBeFocused();
    await expect(page.getByRole("tab", { name: "Text", exact: true })).toHaveAttribute("aria-selected", "true");
    await page.getByLabel("Describe what you see", { exact: true }).fill(note);
    await page.getByRole("button", { name: "Flock numbers today", exact: false }).click();
    await page.getByLabel("Deaths", { exact: true }).fill("0");
    await context.setOffline(true);
    await page.getByRole("button", { name: "Save for later", exact: true }).click();
    await expect(page.getByText("Saved on this device", { exact: true })).toBeVisible();
    const draft = (await storedDrafts(page)).find((item) => item.text === note)!;
    expect(draft.farmId).toBe(farmId);
    await context.setOffline(false);
    await page.evaluate((id) => localStorage.setItem("fh:active-farm", id), otherFarm.id);
    await page.goto(`/scan?draft=${draft.id}`);
    await expect(page.getByLabel("Describe what you see", { exact: true })).toHaveValue(note);
    await expect.poll(() => page.evaluate(() => localStorage.getItem("fh:active-farm"))).toBe(farmId);
    const restored = (await storedDrafts(page)).find((item) => item.id === draft.id)!;
    expect(restored.createdAt).toBe(draft.createdAt);
    expect(restored.idempotencyKey).toBe(draft.idempotencyKey);
    const revisedNote = `${note} No new signs observed.`;
    await page.getByLabel("Describe what you see", { exact: true }).fill(revisedNote);
    await page.clock.setFixedTime(new Date(Date.now() + 61_000));
    const sessionRefetched = page.waitForResponse((item) => item.url().endsWith("/api/session"));
    await page.evaluate(() => window.dispatchEvent(new Event("visibilitychange")));
    await sessionRefetched;
    await expect(page.getByLabel("Describe what you see", { exact: true })).toHaveValue(revisedNote);
    const response = page.waitForResponse((item) => item.url().endsWith("/api/bff/observations") && item.request().method() === "POST");
    await page.getByRole("button", { name: "Analyse report", exact: true }).click();
    const report = await (await response).json();
    expect(report.farmId).toBe(farmId);
    expect(report.mortalityCount).toBe(0);
    await expect(page).toHaveURL(new RegExp(`/reports/${report.id}`));
    await page.reload();
    await expect(page.getByLabel("Deaths", { exact: true })).toHaveValue("0");
    const repeated = await farmer.post("/api/bff/observations", { multipart: { farmId, livestockCategoryId: groups[0].id, text: revisedNote, mortalityCount: "0", idempotencyKey: draft.idempotencyKey } });
    expect(repeated.ok(), await repeated.text()).toBe(true);
    expect((await repeated.json()).id).toBe(report.id);
  } finally { await context.setOffline(false); await context.close(); await farmer.dispose(); }
});

test("blocked device storage is visible and closing preserves the unsaved form", async ({ playwright, browser }) => {
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const context = await browser.newContext({ baseURL, storageState: await farmer.storageState() });
  try {
    await context.addInitScript(() => Object.defineProperty(indexedDB, "open", { value: () => { throw new DOMException("Storage blocked", "SecurityError"); } }));
    const session = await (await farmer.get("/api/session")).json();
    const groups = await json(farmer, `livestock/categories/details?farmId=${session.user.farms[0].id}`);
    const page = await context.newPage();
    await page.goto(`/scan?group=${groups[0].id}`);
    await page.getByRole("tab", { name: "Text", exact: true }).click();
    await page.getByLabel("Describe what you see", { exact: true }).fill("Keep this unsaved note visible.");
    await expect(page.getByRole("region", { name: "Your report", exact: true }).getByRole("alert")).toContainText("This device could not save your draft");
    await page.getByRole("button", { name: "Close", exact: true }).click();
    await expect(page).toHaveURL(/\/scan\?/);
    await expect(page.getByLabel("Describe what you see", { exact: true })).toHaveValue("Keep this unsaved note visible.");
    await context.setOffline(true);
    await page.getByRole("button", { name: "Save for later", exact: true }).click();
    await expect(page.getByRole("region", { name: "Your report", exact: true }).getByRole("alert")).toContainText("This device could not save your draft");
    await expect(page.getByText("Saved on this device", { exact: true })).toHaveCount(0);
  } finally { await context.setOffline(false); await context.close(); await farmer.dispose(); }
});

test("session-probe expiry clears private drafts before another account signs in", async ({ playwright, browser }) => {
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const context = await browser.newContext({ baseURL, storageState: await farmer.storageState() });
  try {
    const session = await (await farmer.get("/api/session")).json();
    const groups = await json(farmer, `livestock/categories/details?farmId=${session.user.farms[0].id}`);
    const page = await context.newPage();
    const privateNote = `Private draft ${Date.now()}`;
    await page.goto(`/scan?group=${groups[0].id}`);
    await page.getByRole("tab", { name: "Text", exact: true }).click();
    await page.getByLabel("Describe what you see", { exact: true }).fill(privateNote);
    await expect.poll(async () => (await storedDrafts(page)).some((draft) => draft.text === privateNote)).toBe(true);
    // Keep the routing cookie present but invalidate both credentials, exercising /api/session's 200/expired response.
    await context.addCookies([
      { name: "fh_access", value: "expired-test-token", url: baseURL, httpOnly: true, sameSite: "Lax" },
      { name: "fh_refresh", value: "expired-test-refresh", url: baseURL, httpOnly: true, sameSite: "Lax" },
    ]);
    await page.goto("/profile");
    await expect(page).toHaveURL(/\/login\?expired=1/);
    await expect(page.getByText(/Saved drafts are private to the account that created them/)).toBeVisible();
    await expect.poll(async () => (await storedDrafts(page)).length).toBe(0);
    await page.getByLabel("Email", { exact: true }).fill("farmer2@farmhand.ai");
    await page.getByLabel("Password", { exact: true }).fill(password!);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page).toHaveURL(/\/(profile|home)$/);
    await expect(page.getByRole("main")).not.toContainText(privateNote);
    expect(await storedDrafts(page)).toHaveLength(0);
  } finally { await context.close(); await farmer.dispose(); }
});

test("another account cannot read drafts when device cleanup fails transiently", async ({ playwright, browser }) => {
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const context = await browser.newContext({ baseURL, storageState: await farmer.storageState() });
  try {
    const session = await (await farmer.get("/api/session")).json();
    const groups = await json(farmer, `livestock/categories/details?farmId=${session.user.farms[0].id}`);
    const page = await context.newPage();
    const privateNote = `Private retained draft ${Date.now()}`;
    await page.goto(`/scan?group=${groups[0].id}`);
    await page.getByRole("tab", { name: "Text", exact: true }).click();
    await page.getByLabel("Describe what you see", { exact: true }).fill(privateNote);
    await expect.poll(async () => (await storedDrafts(page)).some((draft) => draft.text === privateNote)).toBe(true);
    const draft = (await storedDrafts(page)).find((item) => item.text === privateNote)!;
    await context.clearCookies();
    await page.goto("/login");
    await page.evaluate(() => {
      const original = IDBObjectStore.prototype.clear;
      IDBObjectStore.prototype.clear = function () {
        IDBObjectStore.prototype.clear = original;
        throw new DOMException("Transient cleanup block", "SecurityError");
      };
    });
    await page.getByLabel("Email", { exact: true }).fill("farmer2@farmhand.ai");
    await page.getByLabel("Password", { exact: true }).fill(password!);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page).toHaveURL(/\/home$/);
    // Storage is readable again, and the old row remains: privacy must come from ownership checks.
    expect((await storedDrafts(page)).some((row) => row.id === draft.id)).toBe(true);
    await page.goto(`/scan?draft=${draft.id}`);
    await expect(page.locator('[role="alert"]:not(#__next-route-announcer__)')).toContainText("This draft is no longer on this device");
    await expect(page.locator("body")).not.toContainText(privateNote);
    await page.goto("/scan?drafts=1");
    await expect(page.getByRole("dialog", { name: "Unsent reports", exact: true })).toContainText("No unsent reports");
    await expect(page.locator("body")).not.toContainText(privateNote);
  } finally { await context.close(); await farmer.dispose(); }
});

test("offline sign-out reports an active session and reconnecting permits a real sign-out", async ({ playwright, browser }) => {
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const context = await browser.newContext({ baseURL, storageState: await farmer.storageState() });
  try {
    const page = await context.newPage();
    await page.goto("/profile");
    const signOut = page.getByRole("main").getByRole("button", { name: "Sign out", exact: true });
    await expect(signOut).toBeVisible();
    await context.setOffline(true);
    await signOut.click();
    await expect(page.getByText("Sign-out could not finish. Reconnect and try again; this session is still active.", { exact: true })).toBeVisible();
    await expect(page).toHaveURL(/\/profile$/);
    expect((await context.cookies()).some((cookie) => cookie.name === "fh_refresh")).toBe(true);
    await context.setOffline(false);
    await signOut.click();
    await expect(page).toHaveURL(/\/login\?signedOut=1$/);
    expect((await context.cookies()).filter((cookie) => cookie.name.startsWith("fh_"))).toHaveLength(0);
  } finally { await context.setOffline(false); await context.close(); await farmer.dispose(); }
});

test("sign-out everywhere refreshes an expired access cookie and revokes a second session", async ({ playwright, browser }) => {
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const second = await loginApi(playwright, "farmer@farmhand.ai");
  const context = await browser.newContext({ baseURL, storageState: await farmer.storageState() });
  try {
    const page = await context.newPage();
    await page.goto("/profile");
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    // Expiring the access cookie models the browser's normal access-token expiration; refresh remains valid.
    await context.addCookies([{ name: "fh_access", value: "expired", url: baseURL, expires: 1 }]);
    expect((await context.cookies()).some((cookie) => cookie.name === "fh_access")).toBe(false);
    const response = page.waitForResponse((item) => item.url().endsWith("/api/session/logout"));
    await page.getByRole("main").getByRole("button", { name: "Sign out everywhere", exact: true }).click();
    expect((await (await response).json()).everywhereSignedOut).toBe(true);
    await expect(page).toHaveURL(/\/login\?signedOut=1$/);
    expect((await context.cookies()).filter((cookie) => cookie.name.startsWith("fh_"))).toHaveLength(0);
    expect((await (await second.get("/api/session")).json()).authenticated).toBe(false);
  } finally { await context.close(); await farmer.dispose(); await second.dispose(); }
});

test("unconfirmed global revocation clears this device and shows a partial sign-out notice", async ({ playwright, browser }) => {
  const farmer = await loginApi(playwright, "farmer@farmhand.ai");
  const context = await browser.newContext({ baseURL, storageState: await farmer.storageState() });
  try {
    const page = await context.newPage();
    await page.goto("/profile");
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await context.addCookies([
      { name: "fh_access", value: "expired-global-test", url: baseURL, httpOnly: true },
      { name: "fh_refresh", value: "invalid-global-test", url: baseURL, httpOnly: true },
    ]);
    const response = page.waitForResponse((item) => item.url().endsWith("/api/session/logout"));
    await page.getByRole("main").getByRole("button", { name: "Sign out everywhere", exact: true }).click();
    expect((await (await response).json()).everywhereSignedOut).toBe(false);
    await expect(page).toHaveURL(/\/login\?partialSignOut=1$/);
    await expect(page.getByText(/Sign-out on other devices could not be confirmed/)).toBeVisible();
    expect((await context.cookies()).filter((cookie) => cookie.name.startsWith("fh_"))).toHaveLength(0);
    // No successful revoke occurred: the independent API session is still active.
    expect((await (await farmer.get("/api/session")).json()).authenticated).toBe(true);
  } finally { await context.close(); await farmer.dispose(); }
});
