import { expect, test, type Page } from "@playwright/test";

test.skip(
  process.env.MODERN_UI_RUN_LAB_CORRECTION !== "1",
  "Run explicitly against an isolated API and database.",
);

const apiBaseUrl =
  process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";

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
    timeout: 15_000,
  });
}

async function getClinicianSessionId(page: Page) {
  const sessionId = await page.evaluate(() => {
    const raw = sessionStorage.getItem("avenchart-ui.clinicianSession");
    return raw ? (JSON.parse(raw) as { sessionId?: string }).sessionId : null;
  });
  if (!sessionId) throw new Error("Clinician session ID was not persisted.");
  return sessionId;
}

test.describe("lab result correction governance", () => {
  test("captures correction provenance and protects reviewed reports", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const headers = { "X-Legacy EHR-Session": sessionId };
    const suffix = Date.now();
    const procedureName = `Browser correction proof ${suffix}`;
    const resultName = `Governed glucose ${suffix}`;
    let orderId: number | null = null;
    let reportId: number;

    try {
      const orderResponse = await page.request.post(
        `${apiBaseUrl}/api/procedures/orders`,
        {
          headers,
          data: {
            patientId: "MOD-PAT-0004",
            providerId: null,
            labId: null,
            encounterId: 1000043,
            dateOrdered: "2026-07-29T10:00:00",
            priority: "routine",
            status: "pending",
            procedureCode: `BROWSER-CORR-${suffix}`,
            procedureName,
            procedureType: "laboratory",
            diagnosis: "Z00.00",
            instructions: "Temporary browser correction proof.",
          },
        },
      );
      expect(orderResponse.status()).toBe(201);
      orderId = ((await orderResponse.json()) as { id: number }).id;

      const reportResponse = await page.request.post(
        `${apiBaseUrl}/api/procedures/reports`,
        {
          headers,
          data: {
            orderId,
            dateCollected: "2026-07-29T10:30:00",
            dateReport: "2026-07-29T11:00:00",
            specimenNumber: `BROWSER-${suffix}`,
            reportStatus: "final",
            reviewStatus: "received",
            notes: "Temporary browser correction proof.",
          },
        },
      );
      expect(reportResponse.status()).toBe(201);
      reportId = ((await reportResponse.json()) as { id: number }).id;

      const resultResponse = await page.request.post(
        `${apiBaseUrl}/api/procedures/results`,
        {
          headers,
          data: {
            reportId,
            resultCode: `GLU-${suffix}`,
            resultText: resultName,
            dateTime: "2026-07-29T11:00:00",
            facility: "Main laboratory",
            units: "mg/dL",
            result: "101",
            range: "70-99",
            abnormal: "H",
            comments: "Temporary browser correction proof.",
            status: "final",
          },
        },
      );
      expect(resultResponse.status()).toBe(201);

      await page.goto("/clinician/patients/MOD-PAT-0004/labs");
      const correctionList = page.getByRole("region", {
        name: "Correct local results",
      });
      const correctionItem = correctionList
        .locator(".ne-actions")
        .filter({ hasText: resultName });
      await expect(correctionItem).toBeVisible({ timeout: 30_000 });
      await correctionItem.getByRole("button", { name: "Correct" }).click();

      const correctionForm = page
        .getByRole("heading", { name: "Correct local result" })
        .locator("..");
      await correctionForm.getByLabel("Value").fill("99");
      await correctionForm
        .getByLabel("Correction reason")
        .fill("Verified the analyzer worksheet in the browser proof.");
      await correctionForm
        .getByRole("button", { name: "Save correction" })
        .click();

      const resultRow = page.getByRole("row").filter({ hasText: resultName });
      await expect(resultRow).toContainText("99", { timeout: 30_000 });
      await resultRow.getByText("1 prior local version").click();
      await expect(resultRow).toContainText("corrected by admin");
      await expect(resultRow).toContainText(
        "Verified the analyzer worksheet in the browser proof.",
      );
      await expect(resultRow).toContainText("became Version 2");

      const signResponse = await page.request.put(
        `${apiBaseUrl}/api/procedures/reports/${reportId}/sign`,
        {
          headers,
          data: {
            expectedReviewVersion: 1,
            reason: "Signed during browser correction proof.",
          },
        },
      );
      expect(signResponse.ok()).toBeTruthy();

      await page.reload();
      const protectedItem = page
        .getByRole("region", { name: "Correct local results" })
        .locator(".ne-actions")
        .filter({ hasText: resultName });
      const protectedButton = protectedItem.getByRole("button", {
        name: "Correct",
      });
      await expect(protectedButton).toBeDisabled({ timeout: 30_000 });
      await expect(protectedItem).toContainText(
        "Reopen review before correcting.",
      );

      const reopenResponse = await page.request.put(
        `${apiBaseUrl}/api/procedures/reports/${reportId}/reopen-review`,
        {
          headers,
          data: {
            expectedReviewVersion: 2,
            reason: "Reopened during browser correction proof.",
          },
        },
      );
      expect(reopenResponse.ok()).toBeTruthy();

      await page.reload();
      const reopenedButton = page
        .getByRole("region", { name: "Correct local results" })
        .locator(".ne-actions")
        .filter({ hasText: resultName })
        .getByRole("button", { name: "Correct" });
      await expect(reopenedButton).toBeEnabled({ timeout: 30_000 });
    } finally {
      if (orderId) {
        const deleteResponse = await page.request.delete(
          `${apiBaseUrl}/api/procedures/orders/${orderId}`,
          { headers },
        );
        expect([204, 404]).toContain(deleteResponse.status());
      }
    }
  });
});
