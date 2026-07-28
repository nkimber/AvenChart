import { expect, test, type Page } from "@playwright/test";

test.describe.configure({ mode: "serial" });
test.skip(
  process.env.MODERN_UI_RUN_ISOLATED_MUTATIONS !== "1",
  "Run through npm run test:mutation-proofs so shared count tests stay isolated.",
);

async function signInClinician(page: Page) {
  await page.goto("/login");
  await page
    .getByLabel("Username")
    .fill(process.env.MODERN_UI_STAFF_USERNAME ?? "admin");
  await page
    .getByLabel("Password")
    .fill(process.env.MODERN_UI_STAFF_PASSWORD ?? "pass");
  for (let attempt = 0; attempt < 3; attempt += 1) {
    await page.getByRole("button", { name: "Sign in" }).click();
    try {
      await expect(page).toHaveURL(/\/clinician\/dashboard$/, {
        timeout: 15_000,
      });
      return;
    } catch {
      if (attempt === 2) throw new Error("Clinician sign-in did not complete.");
    }
  }
}

async function getClinicianSessionId(page: Page) {
  const sessionId = await page.evaluate(() => {
    const raw = sessionStorage.getItem(
      "avenchart-ui.clinicianSession",
    );
    return raw ? (JSON.parse(raw) as { sessionId?: string }).sessionId : null;
  });
  if (!sessionId) throw new Error("Clinician session ID was not persisted.");
  return sessionId;
}

test.describe("isolated mutation workflows", () => {
  test("staff can claim and reply to a patient message from the inbox", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const subject = `Inbox claim proof ${Date.now()}`;
    const reply = `Browser-verified reply ${Date.now()}`;
    let messageId: string | null = null;

    try {
      const created = await page.request.post(`${apiBaseUrl}/api/messages`, {
        headers: { "X-Legacy EHR-Session": sessionId },
        data: {
          patientId: "MOD-PAT-0004",
          title: subject,
          body: "Please confirm the message workflow.",
          assignedTo: "gold-provider-01",
        },
      });
      expect(created.ok()).toBeTruthy();
      const mutation = (await created.json()) as { id?: string };
      messageId = mutation.id ?? null;
      expect(messageId).toBeTruthy();

      const params = new URLSearchParams({
        patient: "MOD-PAT-0004",
        subject,
      });
      await page.goto(`/clinician/messages?${params}`);
      const inboxItem = page
        .getByRole("button")
        .filter({ hasText: subject });
      await expect(inboxItem).toBeVisible({ timeout: 30_000 });
      await inboxItem.click();

      const message = page.locator("article.msg-item").filter({
        has: page.getByRole("heading", { name: subject }),
      });
      await expect(message).toBeVisible({ timeout: 30_000 });
      await message
        .getByRole("button", { name: "Reassign to me" })
        .click();
      await expect(message.getByText("Assigned to you")).toBeVisible();

      await message
        .getByRole("button", { name: "Reply", exact: true })
        .click();
      await message.getByLabel("Reply").fill(reply);
      await message
        .getByRole("button", { name: "Reply", exact: true })
        .click();
      await expect(message).toContainText(reply);
      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "Reply recorded." }),
      ).toBeVisible();
    } finally {
      if (messageId) {
        const deleted = await page.request.delete(
          `${apiBaseUrl}/api/messages/${messageId}`,
          { headers: { "X-Legacy EHR-Session": sessionId } },
        );
        expect(deleted.ok()).toBeTruthy();
      }
    }
  });

  test("lab reviewer claim, sign, reopen, and bulk-sign lifecycle uses protected contracts", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const procedureName = `Review lifecycle proof ${Date.now()}`;
    let orderId: number | null = null;

    try {
      const createdOrder = await page.request.post(
        `${apiBaseUrl}/api/procedures/orders`,
        {
          headers: { "X-Legacy EHR-Session": sessionId },
          data: {
            patientId: "MOD-PAT-0901",
            providerId: 101,
            labId: 501,
            encounterId: 1009011,
            dateOrdered: "2026-07-27",
            priority: "routine",
            status: "complete",
            procedureCode: "QA-REVIEW",
            procedureName,
            procedureType: "laboratory",
            diagnosis: "Z00.00",
            instructions: "Temporary browser verification fixture",
          },
        },
      );
      expect(createdOrder.ok()).toBeTruthy();
      const orderMutation = (await createdOrder.json()) as { id?: number };
      orderId = orderMutation.id ?? null;
      expect(orderId).toBeTruthy();

      const createdReport = await page.request.post(
        `${apiBaseUrl}/api/procedures/reports`,
        {
          headers: { "X-Legacy EHR-Session": sessionId },
          data: {
            orderId,
            dateCollected: "2026-07-27T10:00:00Z",
            dateReport: "2026-07-27T11:00:00Z",
            specimenNumber: `QA-${Date.now()}`,
            reportStatus: "final",
            reviewStatus: "received",
            notes: "Temporary browser verification fixture",
          },
        },
      );
      expect(createdReport.ok()).toBeTruthy();

      await page.goto(
        "/clinician/labs?reportStatus=unreviewed&patientId=MOD-PAT-0901",
      );
      let reportRow = page.getByRole("row").filter({ hasText: procedureName });
      await expect(reportRow).toBeVisible({ timeout: 30_000 });
      await reportRow.getByRole("button", { name: "Claim" }).click();
      await expect(reportRow.getByText("Assigned to you")).toBeVisible();

      page.once("dialog", (dialog) => dialog.accept());
      await reportRow
        .getByRole("button", { name: "Sign reviewed" })
        .click();
      await expect(reportRow).toHaveCount(0);

      await page.goto(
        "/clinician/labs?reportStatus=reviewed&patientId=MOD-PAT-0901",
      );
      reportRow = page.getByRole("row").filter({ hasText: procedureName });
      await expect(reportRow).toContainText("Reviewed by admin", {
        timeout: 30_000,
      });

      page.once("dialog", (dialog) => dialog.accept());
      await reportRow
        .getByRole("button", { name: "Reopen review" })
        .click();
      await expect(reportRow).toHaveCount(0);

      await page.goto(
        "/clinician/labs?reportStatus=unreviewed&patientId=MOD-PAT-0901",
      );
      reportRow = page.getByRole("row").filter({ hasText: procedureName });
      await expect(reportRow).toBeVisible({ timeout: 30_000 });
      await reportRow.getByRole("checkbox").check();
      page.once("dialog", (dialog) => dialog.accept());
      await page.getByRole("button", { name: "Sign selected (1)" }).click();
      await expect(reportRow).toHaveCount(0);
    } finally {
      if (orderId) {
        const deleted = await page.request.delete(
          `${apiBaseUrl}/api/procedures/orders/${orderId}`,
          { headers: { "X-Legacy EHR-Session": sessionId } },
        );
        expect(deleted.ok()).toBeTruthy();
      }
    }
  });
});
