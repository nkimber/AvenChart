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
      "/clinician/patients/MOD-PAT-0001/documents",
    );
    await page.getByRole("button", { name: "Add document" }).click();
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
        "/clinician/patients/MOD-PAT-0001/documents#intake",
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
        "/clinician/patients/MOD-PAT-0001/documents#metadata",
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
        "/clinician/patients/MOD-PAT-0001/documents#content-versions",
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
        "/clinician/patients/MOD-PAT-0001/documents#review",
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
        "/clinician/patients/MOD-PAT-0001/documents#archive",
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
        "/clinician/patients/MOD-PAT-0001/documents#inline-preview",
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
    const sessionId = await page.evaluate(() => {
      const raw = sessionStorage.getItem(
        "avenchart-ui.clinicianSession",
      );
      return raw ? (JSON.parse(raw) as { sessionId?: string }).sessionId : null;
    });
    expect(sessionId).toBeTruthy();
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const templateMarker = `TMP-DOC-TEMPLATE-AXE-${testInfo.project.name}-${Date.now()}`;
    const templateFixtureResponse = await page.request.post(
      `${apiBaseUrl}/api/administration/document-templates/`,
      {
        headers: { "X-AvenChart-Session": sessionId! },
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
          headers: { "X-AvenChart-Session": sessionId! },
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
      await templateOutput.getByLabel("Find patient *").fill("MOD-PAT-0001");
      await templateOutput.getByRole("button", { name: "Search" }).click();
      const templatePatientResult = templateOutput.getByRole("button", {
        name: /Stone, Avery.*MOD-PAT-0001/,
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
        { headers: { "X-AvenChart-Session": sessionId! } },
      );
      expect([204, 404]).toContain(deleted.status());
    }
    const settingMarker = `TMP-ADM-SETTING-AXE-${testInfo.project.name}-${Date.now()}`;
    const settingFixtureResponse = await page.request.post(
      `${apiBaseUrl}/api/administration/practice-settings/practice.name/change-requests`,
      {
        headers: { "X-AvenChart-Session": sessionId! },
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
        { headers: { "X-AvenChart-Session": sessionId! } },
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
      authorizationRegistry.getByText("local-acl-compatibility-v1"),
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
      identityReadiness.getByText("local-identity-adapter-v1", {
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      identityReadiness.getByText("local adapter active", { exact: true }),
    ).toBeVisible();
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
        headers: { "X-AvenChart-Session": sessionId! },
        data: {
          patientId: "MOD-PAT-0001",
          categoryId: 3,
          name: ocrMarker,
          docDate: "2026-07-28",
          encounter: 1000013,
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
      const deleted = await page.request.delete(
        `${apiBaseUrl}/api/documents/${ocrFixtureId}`,
        { headers: { "X-AvenChart-Session": sessionId! } },
      );
      expect([204, 404]).toContain(deleted.status());
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
    await page.getByLabel("Patient name or ID").fill("MOD-PAT-0004");
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
    const sessionId = await page.evaluate(() => {
      const raw = sessionStorage.getItem(
        "avenchart-ui.clinicianSession",
      );
      return raw ? (JSON.parse(raw) as { sessionId?: string }).sessionId : null;
    });
    expect(sessionId).toBeTruthy();

    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const marker = `TMP-CLIN-AUTH-AXE-${testInfo.project.name}-${Date.now()}`;
    const fixtureResponse = await page.request.post(
      `${apiBaseUrl}/api/patients/MOD-PAT-0001/authorizations`,
      {
        headers: { "X-AvenChart-Session": sessionId! },
        data: {
          payer: marker,
          service: `${marker} service`,
          assignedTo: "admin",
          dueAt: "2026-08-05",
          reason: `${marker} dynamic-state accessibility fixture`,
        },
      },
    );
    expect(fixtureResponse.status()).toBe(201);
    const fixtureId = ((await fixtureResponse.json()) as { id: string }).id;

    try {
      await navigateWithinApplication(
        page,
        "/clinician/patients/MOD-PAT-0001/authorizations",
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
          "/clinician/patients/MOD-PAT-0001/authorizations#workflow",
        ),
      );
    } finally {
      const deleted = await page.request.delete(
        `${apiBaseUrl}/api/patients/MOD-PAT-0001/authorizations/${fixtureId}/test-fixture`,
        { headers: { "X-AvenChart-Session": sessionId! } },
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
