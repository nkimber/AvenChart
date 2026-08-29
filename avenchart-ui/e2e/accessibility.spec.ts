// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import {
  clinicianRoutes,
  patientChartRoutes,
  portalRoutes,
} from "./support/routes.ts";

type AccessibilityFinding = {
  page: string;
  rule: string;
  help: string;
  targets: string[];
};

// The administrator's seeded default access context is the main facility. Keep
// browser fixtures in that facility so the test exercises the intended screen
// rather than an intentionally obscured cross-facility 404 response.
const clinicianFixture = {
  patientId: "MOD-PAT-0408",
  patientResult: /Bell, Arjun.*MOD-PAT-0408/,
  documentEncounter: 1004081,
};

async function findSeriousAccessibilityViolations(
  page: Page,
  label: string,
): Promise<AccessibilityFinding[]> {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .analyze();
  return results.violations
    .filter(
      ({ impact }) => impact === "serious" || impact === "critical",
    )
    .map(({ id, help, nodes }) => ({
      page: label,
      rule: id,
      help,
      targets: nodes.map(({ target }) => target.join(" ")),
    }));
}

function expectNoSeriousAccessibilityViolations(
  violations: AccessibilityFinding[],
) {
  expect(
    violations,
    violations
      .map(
        ({ page, rule, help, targets }) =>
          `${page}\n${rule}: ${help}\n${targets.map((target) => `  ${target}`).join("\n")}`,
      )
      .join("\n\n"),
  ).toEqual([]);
}

async function signInClinician(page: Page) {
  await page.goto("/login");
  await page
    .getByLabel("Username")
    .fill(process.env.MODERN_UI_STAFF_USERNAME ?? "admin");
  await page
    .getByLabel("Password")
    .fill(process.env.MODERN_UI_STAFF_PASSWORD ?? "pass");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/clinician\/dashboard$/, {
    timeout: 20_000,
  });
}

async function signInPortal(page: Page) {
  await page.goto("/portal/login");
  await page
    .getByLabel("Email or username")
    .fill(
      process.env.MODERN_UI_PORTAL_USERNAME ?? "mod-pat-0004@example.test",
    );
  await page
    .getByLabel("Password")
    .fill(process.env.MODERN_UI_PORTAL_PASSWORD ?? "PortalPass207!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/portal\/home$/, { timeout: 20_000 });
}

async function getClinicianRequestHeaders(page: Page) {
  const accessContext = await page.evaluate(() => {
    const raw = sessionStorage.getItem("avenchart-ui.clinicianSession");
    if (!raw) {
      return null;
    }
    const session = JSON.parse(raw) as {
      sessionId?: string;
      facilityId?: number | null;
      purposeOfUse?: string;
    };
    return {
      sessionId: session.sessionId,
      facilityId: session.facilityId,
      purposeOfUse: session.purposeOfUse,
    };
  });

  expect(accessContext?.sessionId).toBeTruthy();
  expect(accessContext?.facilityId).toBeTruthy();
  expect(accessContext?.purposeOfUse).toBeTruthy();
  return {
    "X-AvenChart-Session": accessContext!.sessionId!,
    "X-AvenChart-Facility-Id": String(accessContext!.facilityId),
    "X-AvenChart-Purpose-Of-Use": accessContext!.purposeOfUse!,
  };
}

async function navigateWithinApplication(page: Page, path: string) {
  const loginPath = path.startsWith("/portal") ? "/portal/login" : "/login";
  const signIn = path.startsWith("/portal") ? signInPortal : signInClinician;

  if (new URL(page.url()).pathname === loginPath) {
    await signIn(page);
  }

  const navigate = async () => {
    await page.evaluate((nextPath) => {
      window.history.pushState({}, "", nextPath);
      window.dispatchEvent(new PopStateEvent("popstate"));
    }, path);
    await page.waitForTimeout(150);
  };

  await navigate();
  if (new URL(page.url()).pathname === loginPath) {
    await signIn(page);
    await navigate();
  }

  await expect(page).toHaveURL(new RegExp(`${path.replaceAll("/", "\\/")}$`));
  await expect(page.locator(".route-loading")).toHaveCount(0, {
    timeout: 15_000,
  });
  await expect(page.locator("#main-content"), path).toBeVisible({
    timeout: 15_000,
  });
}

test.describe("accessibility gate", () => {
  test("login pages provide a working skip target and focus announced sign-in failures", async ({
    page,
  }) => {
    const cases = [
      {
        path: "/login",
        usernameLabel: "Username",
        passwordLabel: "Password",
        endpoint: "**/api/auth/login",
      },
      {
        path: "/portal/login",
        usernameLabel: "Email or username",
        passwordLabel: "Password",
        endpoint: "**/api/patient-portal/login",
      },
    ];

    for (const login of cases) {
      await page.goto(login.path);
      const skipLink = page.getByRole("link", { name: "Skip to main content" });
      await skipLink.focus();
      await expect(skipLink).toBeFocused();
      await page.keyboard.press("Enter");
      await expect(page.locator("#main-content")).toBeFocused();

      await page.route(login.endpoint, async (route) => {
        await route.fulfill({
          contentType: "application/json",
          body: JSON.stringify({
            authenticated: false,
            failureReason: "Synthetic sign-in failure.",
          }),
        });
      });
      await page.getByLabel(login.usernameLabel).fill("accessibility-test");
      await page.getByLabel(login.passwordLabel).fill("not-a-password");
      await page.getByRole("button", { name: "Sign in" }).click();
      const alert = page.getByRole("alert");
      await expect(alert).toHaveText("Synthetic sign-in failure.");
      await expect(alert).toBeFocused();
      const alertId = await alert.getAttribute("id");
      expect(alertId).toBeTruthy();
      await expect(page.getByLabel(login.usernameLabel)).toHaveAttribute(
        "aria-describedby",
        alertId!,
      );
      await page.unrouteAll({ behavior: "ignoreErrors" });
    }
  });

  test("public entry and login surfaces have no serious WCAG violations", async ({
    page,
  }) => {
    const violations: AccessibilityFinding[] = [];
    for (const path of ["/", "/login", "/portal/login"]) {
      await page.goto(path);
      await expect(page.locator("body")).toBeVisible();
      violations.push(
        ...(await findSeriousAccessibilityViolations(page, path)),
      );
    }
    expectNoSeriousAccessibilityViolations(violations);
  });

  test("representative clinician workspaces have no serious WCAG violations", async ({
    page,
  }, testInfo) => {
    testInfo.setTimeout(600_000);
    await signInClinician(page);
    const violations: AccessibilityFinding[] = [];
    for (const path of [...clinicianRoutes, ...patientChartRoutes]) {
      await navigateWithinApplication(page, path);
      violations.push(
        ...(await findSeriousAccessibilityViolations(page, path)),
      );
    }
    await navigateWithinApplication(
      page,
      `/clinician/patients/${clinicianFixture.patientId}/documents`,
    );
    const addDocument = page.getByRole("button", { name: "Add document" });
    await expect(addDocument).toBeVisible({ timeout: 15_000 });
    await addDocument.click({ timeout: 15_000 });
    await expect(
      page.getByRole("heading", { name: "Choose how to file it" }),
    ).toBeVisible();
    await page
      .getByRole("button", { name: /Scanner capture Local receipt/ })
      .click();
    await expect(page.getByLabel("Scanner or capture source *")).toBeVisible();
    await expect(page.getByLabel("Captured pages *")).toBeVisible();
    violations.push(
        ...(await findSeriousAccessibilityViolations(
          page,
          `/clinician/patients/${clinicianFixture.patientId}/documents#intake`,
      )),
    );
    await page.getByRole("button", { name: "Close intake" }).click();
    await page.getByRole("button", { name: "Edit filing" }).first().click();
    await page.getByRole("button", { name: "Filing history" }).first().click();
    await expect(page.getByLabel("Change reason *")).toBeVisible();
    await expect(
      page.getByRole("heading", { name: "Filing history" }),
    ).toBeVisible();
    violations.push(
        ...(await findSeriousAccessibilityViolations(
          page,
          `/clinician/patients/${clinicianFixture.patientId}/documents#metadata`,
      )),
    );
    await page.getByRole("button", { name: "Close edit" }).first().click();
    await page.getByRole("button", { name: "Filing history" }).first().click();
    await page
      .getByRole("button", { name: "Content versions" })
      .first()
      .click();
    await page
      .getByRole("button", { name: "Replace content" })
      .first()
      .click();
    await expect(
      page.getByRole("heading", {
        name: "Create the next immutable version",
      }),
    ).toBeVisible();
    await expect(
      page.getByRole("heading", { name: "Content version history" }),
    ).toBeVisible();
    violations.push(
        ...(await findSeriousAccessibilityViolations(
          page,
          `/clinician/patients/${clinicianFixture.patientId}/documents#content-versions`,
      )),
    );
    await page
      .getByRole("button", { name: "Review document" })
      .first()
      .click();
    await expect(
      page.getByRole("heading", { name: "Review lifecycle" }),
    ).toBeVisible();
    await expect(page.getByLabel("Approval rationale *")).toBeVisible();
    violations.push(
        ...(await findSeriousAccessibilityViolations(
          page,
          `/clinician/patients/${clinicianFixture.patientId}/documents#review`,
      )),
    );
    await page
      .getByRole("button", { name: "Archive document" })
      .first()
      .click();
    await expect(
      page.getByRole("heading", { name: "Archive lifecycle" }),
    ).toBeVisible();
    await expect(page.getByLabel("Archive reason *")).toBeVisible();
    violations.push(
        ...(await findSeriousAccessibilityViolations(
          page,
          `/clinician/patients/${clinicianFixture.patientId}/documents#archive`,
      )),
    );
    await page
      .getByRole("button", { name: "Preview", exact: true })
      .first()
      .click();
    await expect(
      page.getByRole("heading", { name: /^Previewing / }),
    ).toBeVisible();
    violations.push(
        ...(await findSeriousAccessibilityViolations(
          page,
          `/clinician/patients/${clinicianFixture.patientId}/documents#inline-preview`,
      )),
    );
    await navigateWithinApplication(page, "/clinician/documents");
    await page
      .getByRole("button", { name: /Route document|Update route/ })
      .first()
      .click();
    await expect(page.getByLabel("Routing reason *")).toBeVisible();
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/documents#routing-editor",
      )),
    );
    await page.getByRole("button", { name: "Close routing form" }).click();
    await page
      .getByRole("button", { name: "Routing history" })
      .first()
      .click();
    await expect(
      page.getByRole("heading", { name: "Routing history" }),
    ).toBeVisible();
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/documents#routing-history",
      )),
    );
    const requestHeaders = await getClinicianRequestHeaders(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://127.0.0.1:5001";
    const templateMarker = `TMP-DOC-TEMPLATE-AXE-${testInfo.project.name}-${Date.now()}`;
    const templateFixtureResponse = await page.request.post(
      `${apiBaseUrl}/api/administration/document-templates/`,
      {
        headers: requestHeaders,
        data: {
          name: templateMarker,
          content:
            "Accessibility care instructions for ***NAME***, DOB ***DOB***.",
          active: true,
        },
      },
    );
    expect(templateFixtureResponse.status()).toBe(201);
    const templateFixtureId = (
      (await templateFixtureResponse.json()) as { id: string }
    ).id;
    try {
      const versionFixtureResponse = await page.request.post(
        `${apiBaseUrl}/api/administration/document-templates/${templateFixtureId}/binary-versions`,
        {
          headers: requestHeaders,
          data: {
            fileName: `${templateMarker}.txt`,
            mimetype: "text/plain",
            contentBase64:
              "QWNjZXNzaWJpbGl0eSB0ZW1wbGF0ZSBwcm9vZi4=",
          },
        },
      );
      expect(versionFixtureResponse.status()).toBe(201);
      await navigateWithinApplication(page, "/clinician/document-templates");
      const templateLibrary = page
        .getByRole("heading", { name: "Template library" })
        .locator("xpath=ancestor::section");
      await templateLibrary
        .getByLabel("Search templates")
        .fill(templateMarker);
      await templateLibrary.getByRole("button", { name: "Apply" }).click();
      await templateLibrary
        .getByRole("button", { name: new RegExp(templateMarker) })
        .click();
      const templateOutput = page
        .getByRole("heading", {
          name: "Preview and patient attachment",
        })
        .locator("xpath=ancestor::section");
      await templateOutput
        .getByLabel("Find patient *")
        .fill(clinicianFixture.patientId);
      await templateOutput.getByRole("button", { name: "Search" }).click();
      const templatePatientResult = templateOutput.getByRole("button", {
        name: clinicianFixture.patientResult,
      });
      await expect(templatePatientResult).toBeVisible({ timeout: 15_000 });
      await templatePatientResult.click();
      await expect(
        page.getByRole("heading", { name: "Binary versions" }),
      ).toBeVisible();
      await expect(
        page.getByRole("heading", { name: "Audit history" }),
      ).toBeVisible();
      violations.push(
        ...(await findSeriousAccessibilityViolations(
          page,
          "/clinician/document-templates#lifecycle",
        )),
      );
    } finally {
      const deleted = await page.request.delete(
        `${apiBaseUrl}/api/administration/document-templates/${templateFixtureId}/test-fixture`,
        { headers: requestHeaders },
      );
      expect([204, 404]).toContain(deleted.status());
    }
    const settingMarker = `TMP-ADM-SETTING-AXE-${testInfo.project.name}-${Date.now()}`;
    const settingFixtureResponse = await page.request.post(
      `${apiBaseUrl}/api/administration/practice-settings/practice.name/change-requests`,
      {
        headers: requestHeaders,
        data: {
          value: settingMarker,
          reason: settingMarker,
        },
      },
    );
    expect(settingFixtureResponse.status()).toBe(201);
    const settingFixtureId = (
      (await settingFixtureResponse.json()) as {
        request: { requestId: string };
      }
    ).request.requestId;
    try {
      await navigateWithinApplication(page, "/clinician/admin");
      await page.getByRole("button", { name: "Configuration" }).click();
      const governance = page.getByLabel("Practice configuration governance");
      await expect(
        governance.getByRole("heading", {
          name: "Practice configuration governance",
        }),
      ).toBeVisible();
      const fixtureRow = governance
        .getByLabel("Practice setting change requests")
        .getByRole("button")
        .filter({ hasText: settingMarker });
      await expect(fixtureRow).toBeVisible({ timeout: 15_000 });
      await fixtureRow.click();
      await expect(
        governance.getByLabel("Change request detail"),
      ).toBeVisible();
      violations.push(
        ...(await findSeriousAccessibilityViolations(
          page,
          "/clinician/admin#practice-configuration-governance",
        )),
      );
    } finally {
      const deleted = await page.request.delete(
        `${apiBaseUrl}/api/administration/practice-setting-change-requests/${settingFixtureId}/test-fixture`,
        { headers: requestHeaders },
      );
      expect([204, 404]).toContain(deleted.status());
    }
    await navigateWithinApplication(page, "/clinician/admin");
    await page
      .getByRole("button", { name: /^Access control \(/ })
      .click();
    const authorizationRegistry = page.getByRole("region", {
      name: "Authorization policy coverage",
    });
    await expect(
      authorizationRegistry.getByRole("heading", {
        name: "Authorization policy coverage",
      }),
    ).toBeVisible();
    await expect(
      authorizationRegistry.getByText("local-acl-access-context-v2"),
    ).toBeVisible();
    await authorizationRegistry
      .getByRole("button", { name: "Open" })
      .first()
      .click();
    await expect(
      authorizationRegistry.getByLabel("Authorization policy detail"),
    ).toBeVisible();
    const identityReadiness = page.getByRole("region", {
      name: "Identity-provider readiness",
    });
    await expect(
      identityReadiness.getByRole("heading", {
        name: "Identity-provider readiness",
      }),
    ).toBeVisible();
    await expect(
      identityReadiness.getByText("external-subject-mapping-v1", {
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      identityReadiness.getByText("local adapter active", { exact: true }),
    ).toHaveCount(2);
    await expect(
      identityReadiness.getByText("disabled owner gated", { exact: true }),
    ).toBeVisible();
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/admin#authorization-policy-registry",
      )),
    );
    const ocrMarker = `TMP-OCR-AXE-${testInfo.project.name}-${Date.now()}`;
    const ocrFixtureResponse = await page.request.post(
      `${apiBaseUrl}/api/documents/scanner-captures`,
      {
        headers: requestHeaders,
        data: {
          patientId: clinicianFixture.patientId,
          categoryId: 3,
          name: ocrMarker,
          docDate: "2026-07-28",
          encounter: clinicianFixture.documentEncounter,
          captureSource: "accessibility scanner",
          pageCount: 2,
          notes: `Accessibility scanner fixture ${ocrMarker}`,
        },
      },
    );
    expect(ocrFixtureResponse.status()).toBe(201);
    const ocrFixtureId = Number(
      ((await ocrFixtureResponse.json()) as { id: number }).id,
    );
    try {
      await navigateWithinApplication(page, "/clinician/document-ocr");
      await page.getByLabel("Search documents").fill(ocrMarker);
      await page.getByRole("button", { name: "Apply filters" }).click();
      const ocrCard = page
        .getByLabel("Document OCR queue")
        .locator(".document-ocr-card")
        .filter({ hasText: ocrMarker });
      await expect(ocrCard).toBeVisible({ timeout: 15_000 });
      await ocrCard.getByRole("button", { name: "Start OCR" }).click();
      await expect(
        ocrCard.getByRole("button", { name: "Close OCR form" }),
      ).toBeVisible();
      violations.push(
        ...(await findSeriousAccessibilityViolations(
          page,
          "/clinician/document-ocr#ocr-editor",
        )),
      );
      await ocrCard.getByRole("button", { name: "Close OCR form" }).click();
      await ocrCard.getByRole("button", { name: "OCR history" }).click();
      await expect(
        ocrCard.getByRole("heading", { name: "OCR history" }),
      ).toBeVisible();
      violations.push(
        ...(await findSeriousAccessibilityViolations(
          page,
          "/clinician/document-ocr#ocr-history",
        )),
      );
    } finally {
      // Documents are clinical records. Archive the synthetic capture after
      // exercise and prove that its physical-delete route remains retired.
      const archived = await page.request.put(
        `${apiBaseUrl}/api/documents/${ocrFixtureId}/soft-delete`,
        {
          headers: requestHeaders,
          data: {
            reason: `Accessibility OCR fixture cleanup ${ocrMarker}`,
            expectedArchived: false,
          },
        },
      );
      expect([200, 404]).toContain(archived.status());
      const deleteAttempt = await page.request.delete(
        `${apiBaseUrl}/api/documents/${ocrFixtureId}`,
        { headers: requestHeaders },
      );
      expect(deleteAttempt.status()).toBe(410);
    }
    await navigateWithinApplication(page, "/clinician/patients/new");
    await page.getByLabel("Chart number").fill("TMP-PAT-REG-AXE");
    await page.getByLabel("First name").fill("Nora");
    await page.getByLabel("Last name").fill("Kim");
    await page.getByLabel("Sex").selectOption("Female");
    await page.getByLabel("Date of birth").fill("2002-05-05");
    await page.getByLabel("Home phone").fill("(619) 555-1004");
    await page
      .getByLabel("Email", { exact: true })
      .fill("mod-pat-0004@example.test");
    await page
      .getByRole("button", { name: "Review and register" })
      .click();
    await expect(
      page.getByText("Review possible existing records before continuing."),
    ).toBeVisible({ timeout: 15_000 });
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/patients/new#duplicate-check",
      )),
    );
    await navigateWithinApplication(page, "/clinician/renewals");
    await page
      .getByLabel("Patient name or ID")
      .fill(clinicianFixture.patientId);
    await page
      .getByRole("button", { name: "Apply patient scope" })
      .click();
    await page
      .getByRole("button", { name: "All active", exact: true })
      .click();
    const routeButton = page
      .locator("button:not([disabled])")
      .filter({ hasText: /Record pharmacy|Change local route/ })
      .first();
    await expect(routeButton).toBeVisible({ timeout: 15_000 });
    await routeButton.click();
    await expect(page.getByLabel("Local pharmacy")).toBeVisible();
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/renewals#local-pharmacy-route",
      )),
    );
    await page.getByRole("button", { name: "Cancel" }).click();
    const editButton = page
      .locator("button:not([disabled])")
      .filter({ hasText: "Edit prescription" })
      .first();
    await expect(editButton).toBeVisible();
    await editButton.click();
    await expect(page.getByLabel("Edit reason")).toBeVisible();
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/renewals#prescription-edit",
      )),
    );
    expectNoSeriousAccessibilityViolations(violations);
  });

  test("authorization workflow dynamic state has no serious WCAG violations", async ({
    page,
  }, testInfo) => {
    await signInClinician(page);
    const requestHeaders = await getClinicianRequestHeaders(page);

    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://127.0.0.1:5001";
    const marker = `TMP-CLIN-AUTH-AXE-${testInfo.project.name}-${Date.now()}`;
    const requestedAt = new Date();
    const dueAt = new Date(requestedAt);
    dueAt.setUTCDate(dueAt.getUTCDate() + 4);
    const fixtureResponse = await page.request.post(
      `${apiBaseUrl}/api/patients/${clinicianFixture.patientId}/authorizations`,
      {
        headers: requestHeaders,
        data: {
          payer: marker,
          service: `${marker} service`,
          // Keep both dates valid regardless of the day CI runs.
          requestedAt: requestedAt.toISOString().slice(0, 10),
          assignedTo: "admin",
          dueAt: dueAt.toISOString().slice(0, 10),
          reason: `${marker} dynamic-state accessibility fixture`,
        },
      },
    );
    const fixtureResponseBody = await fixtureResponse.text();
    expect(fixtureResponse.status(), fixtureResponseBody).toBe(201);
    const fixtureId = (JSON.parse(fixtureResponseBody) as { id: string }).id;

    try {
      await navigateWithinApplication(
        page,
        `/clinician/patients/${clinicianFixture.patientId}/authorizations`,
      );
      const queueItem = page
        .locator("button.authorization-queue-item")
        .filter({ hasText: marker });
      await expect(queueItem).toBeVisible({ timeout: 15_000 });
      await queueItem.click();
      await expect(page.locator(".authorization-history")).toContainText(
        marker,
      );
      await page
        .getByRole("button", { name: "Submit for review" })
        .click();
      await expect(
        page
          .locator("form.authorization-editor")
          .filter({ hasText: "Draft → Submitted" }),
      ).toBeVisible();

      expectNoSeriousAccessibilityViolations(
        await findSeriousAccessibilityViolations(
          page,
          `/clinician/patients/${clinicianFixture.patientId}/authorizations#workflow`,
        ),
      );
    } finally {
      const deleted = await page.request.delete(
        `${apiBaseUrl}/api/patients/${clinicianFixture.patientId}/authorizations/${fixtureId}/test-fixture`,
        { headers: requestHeaders },
      );
      expect([204, 404]).toContain(deleted.status());
    }
  });

  test("representative portal workspaces have no serious WCAG violations", async ({
    page,
  }) => {
    await signInPortal(page);
    const violations: AccessibilityFinding[] = [];
    for (const path of portalRoutes) {
      await navigateWithinApplication(page, path);
      violations.push(
        ...(await findSeriousAccessibilityViolations(page, path)),
      );
    }
    await navigateWithinApplication(page, "/portal/records");
    await page.getByRole("button", { name: "Health summary" }).click();
    await expect(page.getByRole("heading", { name: "Refill request history" }))
      .toBeVisible();
    await expect(page.locator(".refill-history-source")).toBeVisible({
      timeout: 15_000,
    });
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/portal/records#health-refill-history",
      )),
    );
    expectNoSeriousAccessibilityViolations(violations);
  });
});
