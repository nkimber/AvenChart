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

async function establishClinicianSession(
  request: APIRequestContext,
  page: Page,
) {
  const apiBaseUrl =
    process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
  const response = await request.post(`${apiBaseUrl}/api/auth/login`, {
    data: {
      username: process.env.MODERN_UI_STAFF_USERNAME ?? "admin",
      password: process.env.MODERN_UI_STAFF_PASSWORD ?? "pass",
    },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  const login = (await response.json()) as LoginResponse;
  expect(login.authenticated).toBe(true);
  expect(login.sessionId).toBeTruthy();

  await page.addInitScript((session) => {
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

test.describe("FORM-04 legacy clinical-form display adapter", () => {
  test("shows mapped and unmapped Clinic Note source facts without conversion", async ({
    page,
    request,
  }) => {
    await establishClinicianSession(request, page);
    await page.goto("/clinician/patients/MOD-PAT-0001/forms");

    const snapshots = page.getByRole("region", {
      name: "Legacy form snapshots",
    });
    await expect(snapshots.locator("tbody tr")).toHaveCount(2, {
      timeout: 20_000,
    });

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
  });
});
