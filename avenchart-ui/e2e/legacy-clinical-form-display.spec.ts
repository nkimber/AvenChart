// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from "@axe-core/playwright";
import {
  expect,
  test,
  type APIRequestContext,
  type Page,
} from "@playwright/test";

type LoginResponse = {
  authenticated: boolean;
  sessionId: string;
  username: string;
  displayName: string;
  role: string;
  staffId?: number | null;
};

const manifestId = "90f00000-0000-4000-a000-000000000001";

async function loginStaff(
  request: APIRequestContext,
  username: string,
  password = "pass",
) {
  const apiBaseUrl =
    process.env.MODERN_UI_API_BASE_URL ?? "http://127.0.0.1:5001";
  const response = await request.post(`${apiBaseUrl}/api/auth/login`, {
    data: { username, password },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  const login = (await response.json()) as LoginResponse;
  expect(login.authenticated).toBe(true);
  expect(login.sessionId).toBeTruthy();
  return login;
}

async function setClinicianSession(page: Page, login: LoginResponse) {
  await page.evaluate((session) => {
    sessionStorage.setItem(
      "avenchart-ui.clinicianSession",
      JSON.stringify(session),
    );
  }, {
    sessionId: login.sessionId,
    username: login.username,
    displayName: login.displayName,
    role: login.role,
    staffId: login.staffId ?? null,
  });
}

async function resetManifestFixture(
  request: APIRequestContext,
  admin: LoginResponse,
) {
  const apiBaseUrl =
    process.env.MODERN_UI_API_BASE_URL ?? "http://127.0.0.1:5001";
  const response = await request.delete(
    `${apiBaseUrl}/api/form-engine/legacy-migration-manifests/${manifestId}/test-fixture`,
    { headers: { "X-AvenChart-Session": admin.sessionId } },
  );
  expect(response.ok(), await response.text()).toBeTruthy();
}

test.describe("FORM-04 legacy clinical-form display adapter", () => {
  test("governs the local manifest and displays source facts without conversion", async ({
    page,
    request,
  }) => {
    const admin = await loginStaff(
      request,
      process.env.MODERN_UI_STAFF_USERNAME ?? "admin",
      process.env.MODERN_UI_STAFF_PASSWORD ?? "pass",
    );
    const reviewer = await loginStaff(request, "gold-provider-01");
    await resetManifestFixture(request, admin);
    await page.goto("/");
    await setClinicianSession(page, reviewer);

    try {
      await page.goto("/clinician/patients/MOD-PAT-0001/forms");

      const snapshots = page.getByRole("region", {
        name: "Legacy form snapshots",
      });
      await expect(snapshots.locator("tbody tr")).toHaveCount(6, {
        timeout: 20_000,
      });
      await expect(page.getByRole("status")).toContainText(
        "Encounter choices are unavailable for this session",
      );

      const soapRow = snapshots
        .locator("tbody tr")
        .filter({ hasText: "row 882001" });
      await expect(soapRow).toContainText("SOAP");
      await expect(soapRow).toContainText("All source fields mapped");
      await soapRow.getByRole("button", { name: "Open snapshot" }).click();
      const soapDetail = page.getByRole("region", {
        name: /SOAP source row 882001/,
      });
      await expect(soapDetail).toContainText(
        "local-legacy-soap-display-v1",
        { timeout: 20_000 },
      );
      await expect(soapDetail).toContainText("form_soap");
      await expect(soapDetail).toContainText("subjective");
      await expect(soapDetail).toContainText("objective");
      await expect(soapDetail).toContainText("assessment");
      await expect(soapDetail).toContainText(
        "Continue medications and return in two weeks.",
      );
      await expect(soapDetail).toContainText(
        "No unmapped source fields or values were found.",
      );
      await soapDetail
        .getByRole("button", { name: "Close snapshot" })
        .click();

      const instructionRow = snapshots
        .locator("tbody tr")
        .filter({ hasText: "row 881001" });
      await expect(instructionRow).toContainText("Clinical Instructions");
      await expect(instructionRow).toContainText("All source fields mapped");
      await instructionRow
        .getByRole("button", { name: "Open snapshot" })
        .click();
      const instructionDetail = page.getByRole("region", {
        name: /Clinical Instructions source row 881001/,
      });
      await expect(instructionDetail).toContainText(
        "local-legacy-clinical-instructions-display-v1",
        { timeout: 20_000 },
      );
      await expect(instructionDetail).toContainText(
        "form_clinical_instructions",
      );
      await expect(instructionDetail).toContainText(
        "Continue the current regimen.",
      );
      await expect(instructionDetail).toContainText(
        "No unmapped source fields or values were found.",
      );
      await instructionDetail
        .getByRole("button", { name: "Close snapshot" })
        .click();

      const migrationManifest = page.getByRole("region", {
        name: "Clinic Note migration manifest",
      });
      await expect(migrationManifest).toContainText("Review evidence only.");
      await expect(migrationManifest).toContainText(
        "Production approval is not recorded",
      );
      await expect(migrationManifest).toContainText("execution is disabled");
      await expect(migrationManifest).toContainText(
        "has created 0 governed instances",
      );
      await expect(migrationManifest).toContainText(
        "local-clinical-form-migration-manifest-v1",
      );
      await expect(migrationManifest).toContainText("eligible-for-review");
      await expect(migrationManifest).toContainText("blocked");
      await expect(migrationManifest).toContainText(
        "0 → none_required; 1 → required_in; 2 → pending_investigation",
      );
      await expect(
        migrationManifest
          .locator("dl div")
          .filter({ hasText: "Manifest SHA-256" })
          .locator("dd"),
      ).toHaveText(/^[0-9a-f]{64}$/);

      await migrationManifest
        .getByLabel("Decision reason")
        .fill("Clinical reviewer accepts the bounded synthetic field mapping.");
      await migrationManifest
        .getByRole("button", { name: "Complete local review" })
        .click();
      await expect(migrationManifest).toContainText("gold-provider-01 at", {
        timeout: 20_000,
      });
      await expect(migrationManifest).toContainText(
        "in-review · revision 1 · version 2",
      );
      const decisionHistory = migrationManifest.getByRole("table", {
        name: "Migration manifest decision history",
      });
      await expect(decisionHistory.locator("tbody tr")).toHaveCount(2);
      await expect(decisionHistory).toContainText("review");

      await setClinicianSession(page, admin);
      await page.reload();
      await expect(migrationManifest).toContainText(
        "in-review · revision 1 · version 2",
        { timeout: 20_000 },
      );
      await migrationManifest
        .getByLabel("Decision reason")
        .fill("Administrator accepts the local synthetic manifest evidence.");
      await migrationManifest
        .getByRole("button", { name: "Approve locally" })
        .click();
      await expect(migrationManifest).toContainText(
        "locally-approved · revision 1 · version 3",
        { timeout: 20_000 },
      );
      await expect(migrationManifest).toContainText("admin at");
      await expect(migrationManifest).toContainText(
        "Production approval remains not recorded",
      );
      await expect(migrationManifest).toContainText(
        "execution remains disabled",
      );
      await expect(decisionHistory.locator("tbody tr")).toHaveCount(3);
      await expect(decisionHistory).toContainText("approve");

      const mappedRow = snapshots
        .locator("tbody tr")
        .filter({ hasText: "row 880001" });
      await expect(mappedRow).toContainText("All source fields mapped");
      await mappedRow.getByRole("button", { name: "Open snapshot" }).click();

      const mappedDetail = page.getByRole("region", {
        name: /Clinic Note source row 880001/,
      });
      await expect(mappedDetail).toContainText("Display only.", {
        timeout: 20_000,
      });
      await expect(mappedDetail).toContainText(
        "local-legacy-clinic-note-display-v1",
      );
      await expect(mappedDetail).toContainText("followup_required");
      await expect(mappedDetail).toContainText("follow_up_status");
      await expect(mappedDetail).toContainText("Required in");
      await expect(mappedDetail).toContainText(
        "No unmapped source fields or values were found.",
      );
      await expect(
        mappedDetail
          .locator("dl div")
          .filter({ hasText: "Source SHA-256" })
          .locator("dd"),
      ).toHaveText(/^[0-9a-f]{64}$/);

      const accessibility = await new AxeBuilder({ page })
        .include(
          'section[aria-labelledby="legacy-clinical-form-history-heading"]',
        )
        .include(
          'section[aria-labelledby="selected-legacy-clinical-form-heading"]',
        )
        .include(
          'section[aria-labelledby="legacy-clinical-form-migration-heading"]',
        )
        .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
        .analyze();
      expect(
        accessibility.violations.filter((violation) =>
          ["serious", "critical"].includes(violation.impact ?? ""),
        ),
      ).toEqual([]);

      await mappedDetail
        .getByRole("button", { name: "Close snapshot" })
        .click();
      const unmappedRow = snapshots
        .locator("tbody tr")
        .filter({ hasText: "row 880002" });
      await expect(unmappedRow).toContainText("1 unmapped fact");
      await unmappedRow.getByRole("button", { name: "Open snapshot" }).click();

      const unmappedDetail = page.getByRole("region", {
        name: /Clinic Note source row 880002/,
      });
      await expect(unmappedDetail).toContainText(
        "Legacy follow-up code 9 is not mapped",
        { timeout: 20_000 },
      );
      await expect(unmappedDetail).toContainText(
        "migration approval is not recorded",
      );
      await expect(unmappedDetail).toContainText(
        "It has no governed instance ID",
      );
    } finally {
      await resetManifestFixture(request, admin);
    }
  });
});
