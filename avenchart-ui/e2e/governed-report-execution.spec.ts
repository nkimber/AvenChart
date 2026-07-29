import AxeBuilder from "@axe-core/playwright";
import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

async function signIn(
  page: Page,
  username = process.env.MODERN_UI_STAFF_USERNAME ?? "admin",
  password = process.env.MODERN_UI_STAFF_PASSWORD ?? "pass",
) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/clinician\/dashboard$/, {
    timeout: 20_000,
  });
}

async function sessionId(page: Page) {
  const value = await page.evaluate(() => {
    const raw = sessionStorage.getItem(
      "avenchart-ui.clinicianSession",
    );
    return raw ? (JSON.parse(raw) as { sessionId?: string }).sessionId : null;
  });
  if (!value) throw new Error("Clinician session was not persisted.");
  return value;
}

async function createActiveDefinition(
  request: APIRequestContext,
  apiBaseUrl: string,
  headers: Record<string, string>,
  input: {
    stableKey: string;
    title: string;
    purpose: string;
    family: string;
    rowPolicy: string;
  },
) {
  const createdResponse = await request.post(
    `${apiBaseUrl}/api/reports/definitions`,
    {
      headers,
      data: {
        stableKey: input.stableKey,
        title: input.title,
        ownerUsername: "admin",
        purpose: input.purpose,
        reportFamily: input.family,
        sensitivity: "restricted",
        rowPolicy: input.rowPolicy,
        retentionDays: 30,
        allowedRecipients: ["requesting-user", "report-owner"],
        deliveryModes: ["local-download"],
        reason: "Create a browser report-execution fixture.",
      },
    },
  );
  expect(createdResponse.status()).toBe(201);
  const created = (await createdResponse.json()) as {
    definitionId: string;
    revisions: { version: number }[];
  };
  let version = created.revisions[0]?.version ?? 0;
  for (const [action, reason] of [
    ["review", "Owner reviewed the browser execution contract."],
    ["approve", "Approve the browser execution contract."],
    ["activate", "Activate the browser execution contract."],
  ] as const) {
    const response = await request.post(
      `${apiBaseUrl}/api/reports/definitions/${created.definitionId}/${action}`,
      { headers, data: { expectedVersion: version, reason } },
    );
    expect(response.ok()).toBeTruthy();
    const detail = (await response.json()) as {
      revisions: { version: number }[];
    };
    version = detail.revisions[0]?.version ?? version + 1;
  }
  return created.definitionId;
}

test.describe("REP-02 governed report execution", () => {
  test("previews, runs, downloads, and records blocked scope", async ({
    page,
  }) => {
    const suffix = `${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const definitions: string[] = [];

    await signIn(page);
    const headers = { "X-Legacy EHR-Session": await sessionId(page) };
    try {
      const practiceTitle = `Browser patient execution ${suffix}`;
      const practicePurpose =
        "Verify governed browser preview, run, evidence, and download.";
      const practiceDefinition = await createActiveDefinition(
        page.request,
        apiBaseUrl,
        headers,
        {
          stableKey: `tmp-report-execution-ui-${suffix}-practice`,
          title: practiceTitle,
          purpose: practicePurpose,
          family: "patients",
          rowPolicy: "practice-wide",
        },
      );
      definitions.push(practiceDefinition);

      const scopedTitle = `Browser scoped execution ${suffix}`;
      const scopedDefinition = await createActiveDefinition(
        page.request,
        apiBaseUrl,
        headers,
        {
          stableKey: `tmp-report-execution-ui-${suffix}-scoped`,
          title: scopedTitle,
          purpose: "Verify visible fail-closed browser scope evidence.",
          family: "appointments",
          rowPolicy: "facility-scoped",
        },
      );
      definitions.push(scopedDefinition);

      await page.goto("/clinician/reports");
      const workspace = page.locator(".report-execution-workspace");
      await expect(
        workspace.getByRole("heading", {
          name: "Governed report execution",
        }),
      ).toBeVisible({ timeout: 20_000 });
      await expect(workspace).toContainText("local-report-execution-v3");
      await expect(workspace).toContainText("local-report-queue-v1");
      await expect(workspace).toContainText("3 automatic attempts");
      await expect(workspace).toContainText("Local download only");
      const operations = workspace.locator(".report-operations-workspace");
      await expect(
        operations.getByRole("heading", { name: "Report operations" }),
      ).toBeVisible();
      await expect(operations).toContainText("local-report-operations-v1");
      await expect(operations).toContainText("not production-approved");

      await workspace
        .getByLabel("Active definition")
        .selectOption(practiceDefinition);
      await expect(workspace).toContainText(practicePurpose);
      await workspace
        .getByRole("button", { name: "Preview 10 rows" })
        .click();
      const preview = workspace
        .getByRole("heading", { name: "Non-persistent preview" })
        .locator("xpath=parent::section");
      await expect(preview).toContainText("1,000 total rows", {
        timeout: 20_000,
      });
      await expect(
        preview.getByRole("region", { name: "Governed report preview" }),
      ).toContainText("MOD-PAT-");

      await workspace
        .getByRole("button", { name: "Run governed report" })
        .click();
      const runEvidence = workspace
        .getByRole("heading", { name: "Run evidence" })
        .locator("xpath=parent::section");
      await expect(runEvidence).toContainText("revision 1", {
        timeout: 20_000,
      });
      await expect(runEvidence).toContainText(
        "local-report-queue-v1 / attempt 1 of 3",
        { timeout: 20_000 },
      );
      await expect(runEvidence).toContainText("Artifact retention");
      await expect(
        workspace.getByRole("region", {
          name: "Governed report run history",
        }),
      ).toContainText("completed", { timeout: 60_000 });

      const downloadPromise = page.waitForEvent("download");
      await workspace.getByRole("button", { name: "Download" }).click();
      const download = await downloadPromise;
      expect(download.suggestedFilename()).toMatch(/\.csv$/);
      await expect(
        runEvidence.getByRole("region", {
          name: "Governed report run events",
        }),
      ).toContainText("downloaded");

      const accessibility = await new AxeBuilder({ page })
        .include(".report-execution-workspace")
        .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
        .analyze();
      expect(
        accessibility.violations.filter((violation) =>
          ["serious", "critical"].includes(violation.impact ?? ""),
        ),
      ).toEqual([]);

      await workspace
        .getByLabel("Active definition")
        .selectOption(scopedDefinition);
      await expect(workspace).toContainText(
        "The current account lacks the active staff or facility relationship",
      );
      await expect(
        workspace.getByRole("button", { name: "Preview 10 rows" }),
      ).toBeDisabled();
      await workspace
        .getByRole("button", { name: "Record blocked run" })
        .click();
      await expect(
        workspace.getByRole("region", {
          name: "Governed report run history",
        }),
      ).toContainText("scope-identity-unavailable", { timeout: 20_000 });
      await expect(workspace.getByRole("button", { name: "Download" })).toHaveCount(
        0,
      );
      await expect(
        workspace.getByRole("region", {
          name: "Governed report run events",
        }),
      ).toContainText("failed");
      await operations
        .getByPlaceholder("Run ID, definition, title, or failure code")
        .fill(scopedTitle);
      await operations.getByLabel("Needs attention only").check();
      await operations
        .getByRole("button", { name: "Apply operations filters" })
        .click();
      const operatorQueue = operations.getByRole("region", {
        name: "Governed report operator run queue",
      });
      await expect(operatorQueue).toContainText(scopedTitle, {
        timeout: 20_000,
      });
      await expect(operatorQueue).toContainText(
        "scope-identity-unavailable",
      );
      await operatorQueue
        .getByRole("button", { name: "Inspect operations evidence" })
        .click();
      await expect(
        operations.getByRole("region", {
          name: "Governed report operator run events",
        }),
      ).toContainText("failed");
    } finally {
      for (const definitionId of definitions) {
        const cleanup = await page.request.delete(
          `${apiBaseUrl}/api/reports/definitions/${definitionId}/test-fixture`,
          { headers },
        );
        expect(cleanup.status()).toBe(204);
      }
    }
  });

  test("executes pinned facility and assigned-patient scope for an active provider", async ({
    page,
  }) => {
    const suffix = `${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const definitions: string[] = [];
    const adminLogin = await page.request.post(
      `${apiBaseUrl}/api/auth/login`,
      { data: { username: "admin", password: "pass" } },
    );
    expect(adminLogin.ok()).toBeTruthy();
    const adminSession = (await adminLogin.json()) as { sessionId: string };
    const adminHeaders = {
      "X-Legacy EHR-Session": adminSession.sessionId,
    };

    try {
      const facilityPurpose =
        "Verify browser facility scope for an active provider.";
      const facilityDefinition = await createActiveDefinition(
        page.request,
        apiBaseUrl,
        adminHeaders,
        {
          stableKey: `tmp-report-execution-ui-${suffix}-facility`,
          title: `Browser provider facility execution ${suffix}`,
          purpose: facilityPurpose,
          family: "appointments",
          rowPolicy: "facility-scoped",
        },
      );
      definitions.push(facilityDefinition);

      const assignedPurpose =
        "Verify browser patient assignment scope for an active provider.";
      const assignedDefinition = await createActiveDefinition(
        page.request,
        apiBaseUrl,
        adminHeaders,
        {
          stableKey: `tmp-report-execution-ui-${suffix}-assigned`,
          title: `Browser provider assigned execution ${suffix}`,
          purpose: assignedPurpose,
          family: "patients",
          rowPolicy: "patient-assigned",
        },
      );
      definitions.push(assignedDefinition);

      await signIn(page, "gold-provider-01", "pass");
      await page.goto("/clinician/reports");
      const workspace = page.locator(".report-execution-workspace");
      await expect(workspace).toContainText(
        "staff 101 / MAIN / 83 assigned patients",
        { timeout: 20_000 },
      );
      await expect(
        workspace.getByRole("heading", { name: "Report operations" }),
      ).toHaveCount(0);

      await workspace
        .getByLabel("Active definition")
        .selectOption(facilityDefinition);
      await expect(
        workspace.getByRole("button", { name: "Preview 10 rows" }),
      ).toBeEnabled();
      await workspace
        .getByRole("button", { name: "Preview 10 rows" })
        .click();
      await expect(
        workspace
          .getByRole("heading", { name: "Non-persistent preview" })
          .locator("xpath=parent::section"),
      ).toContainText("501 total rows", { timeout: 20_000 });
      await expect(workspace).toContainText("333 scoped patients");
      await workspace
        .getByRole("button", { name: "Run governed report" })
        .click();
      const evidence = workspace
        .getByRole("heading", { name: "Run evidence" })
        .locator("xpath=parent::section");
      await expect(evidence).toContainText("facility 10", {
        timeout: 20_000,
      });
      await expect(evidence).toContainText("333 patients");
      await expect(evidence).toContainText("attempt 1 of 3", {
        timeout: 20_000,
      });

      await workspace
        .getByLabel("Active definition")
        .selectOption(assignedDefinition);
      await workspace
        .getByRole("button", { name: "Preview 10 rows" })
        .click();
      await expect(
        workspace
          .getByRole("heading", { name: "Non-persistent preview" })
          .locator("xpath=parent::section"),
      ).toContainText("83 total rows", { timeout: 20_000 });
      await workspace
        .getByRole("button", { name: "Run governed report" })
        .click();
      await expect(evidence).toContainText("83 patients", {
        timeout: 20_000,
      });
      await expect(
        workspace.getByRole("region", {
          name: "Governed report run history",
        }),
      ).toContainText("completed", { timeout: 60_000 });

      const accessibility = await new AxeBuilder({ page })
        .include(".report-execution-workspace")
        .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
        .analyze();
      expect(
        accessibility.violations.filter((violation) =>
          ["serious", "critical"].includes(violation.impact ?? ""),
        ),
      ).toEqual([]);
    } finally {
      for (const definitionId of definitions) {
        const cleanup = await page.request.delete(
          `${apiBaseUrl}/api/reports/definitions/${definitionId}/test-fixture`,
          { headers: adminHeaders },
        );
        expect(cleanup.status()).toBe(204);
      }
    }
  });
});
