import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

async function signIn(page: Page) {
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

test.describe("REC-01/02 managed record controls", () => {
  test("withholds, classifies, validates, releases, and cleans up an intake", async ({
    page,
  }) => {
    const marker = `TMP-RECORD-UI-${Date.now()}`;
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    let intakeId: string | null = null;

    await signIn(page);
    const staffSessionId = await sessionId(page);
    const headers = { "X-Legacy EHR-Session": staffSessionId };

    try {
      await page.goto(
        "/clinician/patients/MOD-PAT-0001/documents",
      );
      const managedRecords = page.getByRole("region", {
        name: "Managed record intake",
      });
      await expect(managedRecords).toBeVisible({ timeout: 20_000 });
      await expect(managedRecords).toContainText(
        "local-record-control-v1",
      );
      await expect(managedRecords).toContainText(
        "Anti-malware verified: no",
      );

      await managedRecords
        .getByText("Adapter and production boundary")
        .click();
      await expect(
        managedRecords.locator(".managed-record-policy li"),
      ).toHaveCount(7);

      await managedRecords
        .getByRole("button", { name: "New managed intake" })
        .click();
      const intakeForm = managedRecords.getByRole("form", {
        name: "Create managed record intake",
      });
      await intakeForm.getByLabel("Record title").fill(marker);
      await intakeForm.getByLabel("File", { exact: true }).setInputFiles({
        name: `${marker}.txt`,
        mimeType: "text/plain",
        buffer: Buffer.from(`${marker} browser content`, "utf8"),
      });
      await intakeForm
        .getByLabel("Author or originator")
        .fill("Browser lifecycle test");
      await intakeForm
        .getByLabel("Capture reason")
        .fill("Synthetic REC-01 browser capture");
      await intakeForm
        .getByRole("button", { name: "Capture outside chart" })
        .click();

      const recordCard = managedRecords
        .locator("article.managed-record-card")
        .filter({ hasText: marker });
      await expect(recordCard).toBeVisible({ timeout: 20_000 });
      await expect(recordCard).toContainText("captured");
      await expect(recordCard).toContainText("withheld");
      await expect(recordCard).toContainText("not verified");
      await expect(recordCard).not.toContainText(
        "Released as patient document",
      );

      const intakeList = await page.request.get(
        `${apiBaseUrl}/api/records/?patientId=MOD-PAT-0001`,
        { headers },
      );
      expect(intakeList.ok()).toBeTruthy();
      const intake = (
        (await intakeList.json()) as {
          items: Array<{ intakeId: string; title: string }>;
        }
      ).items.find((item) => item.title === marker);
      expect(intake).toBeTruthy();
      intakeId = intake!.intakeId;

      const documentsBeforeRelease = await page.request.get(
        `${apiBaseUrl}/api/documents/MOD-PAT-0001?includeArchived=true`,
        { headers },
      );
      expect(documentsBeforeRelease.ok()).toBeTruthy();
      expect(JSON.stringify(await documentsBeforeRelease.json())).not.toContain(
        marker,
      );

      await recordCard.getByRole("button", { name: "Reclassify" }).click();
      const classification = managedRecords.getByRole("form", {
        name: "Update managed record classification",
      });
      await classification
        .getByLabel("Record class")
        .selectOption("correspondence");
      await classification
        .getByLabel("Sensitivity")
        .selectOption("restricted");
      await classification
        .getByLabel("Revision reason")
        .fill("Synthetic classification correction");
      await classification
        .getByRole("button", { name: "Save classification revision" })
        .click();
      await expect(recordCard).toContainText("correspondence");
      await expect(recordCard).toContainText("restricted");

      const reason = managedRecords.getByLabel(
        "Workflow reason for the selected intake",
      );
      await reason.fill("Place content into controlled quarantine");
      await recordCard.getByRole("button", { name: "quarantine" }).click();
      await expect(recordCard).toContainText("quarantined");

      await reason.fill("Start bounded local structural validation");
      await recordCard
        .getByRole("button", { name: "Start local validation" })
        .click();
      await expect(recordCard).toContainText("scanning");

      await reason.fill("Exercise visible validation failure");
      await recordCard.getByRole("button", { name: "fail" }).click();
      await expect(recordCard).toContainText("failed");
      await expect(recordCard).toContainText(
        "Exercise visible validation failure",
      );

      await reason.fill("Retry after synthetic failure review");
      await recordCard.getByRole("button", { name: "retry" }).click();
      await expect(recordCard).toContainText("quarantined");

      await reason.fill("Restart bounded local structural validation");
      await recordCard
        .getByRole("button", { name: "Start local validation" })
        .click();
      await expect(recordCard).toContainText("scanning");

      await reason.fill("Release after local checksum validation");
      await recordCard.getByRole("button", { name: "release" }).click();
      await expect(recordCard).toContainText("available");
      await expect(recordCard).toContainText(
        /Released as patient document \d+\./,
      );
      await expect(recordCard).toContainText("not verified");

      await recordCard.getByRole("button", { name: "History" }).click();
      const history = managedRecords.getByRole("region", {
        name: "Immutable intake history",
      });
      await expect(history).toContainText("8 events");
      await expect(history.locator("li")).toHaveCount(8);
      await expect(history.locator("li").first()).toContainText("release");
      await expect(history.locator("li").last()).toContainText("captured");

      const documentsAfterRelease = await page.request.get(
        `${apiBaseUrl}/api/documents/MOD-PAT-0001?includeArchived=true`,
        { headers },
      );
      expect(documentsAfterRelease.ok()).toBeTruthy();
      expect(JSON.stringify(await documentsAfterRelease.json())).toContain(
        marker,
      );

      const accessibility = await new AxeBuilder({ page })
        .include(".managed-records")
        .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
        .analyze();
      expect(
        accessibility.violations.filter(
          ({ impact }) => impact === "serious" || impact === "critical",
        ),
      ).toEqual([]);
    } finally {
      if (intakeId) {
        const cleanup = await page.request.delete(
          `${apiBaseUrl}/api/records/${intakeId}/test-fixture`,
          { headers },
        );
        expect([204, 404]).toContain(cleanup.status());
      }
    }
  });
});
