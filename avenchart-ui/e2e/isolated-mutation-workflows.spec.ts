import { expect, test, type Page } from "@playwright/test";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

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

function deletePortalMailboxFixtures(messageIds: string[]) {
  const ids = messageIds
    .map((value) => Number(value))
    .filter((value) => Number.isInteger(value) && value > 0);
  if (ids.length === 0) return;
  const numericIds = ids.join(",");
  const textIds = ids.map((value) => `'${value}'`).join(",");
  const sql = [
    "begin;",
    `delete from patient_portal_message_audit_events where message_id in (${textIds}) or thread_id in (${numericIds}) or related_message_ids && array[${textIds}]::text[];`,
    `delete from portal_mailbox_messages where id in (${numericIds});`,
    "commit;",
  ].join(" ");
  execFileSync(
    "docker",
    [
      "compose",
      "exec",
      "-T",
      "postgres",
      "psql",
      "-X",
      "-U",
      "legacy-ehr",
      "-d",
      "legacy-ehr_modernized",
      "-v",
      "ON_ERROR_STOP=1",
      "-c",
      sql,
    ],
    {
      cwd: fileURLToPath(
        new URL("../../avenchart/", import.meta.url),
      ),
      stdio: "pipe",
    },
  );
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

  test("staff can create a catalog prescription, approve its portal refill request, and inspect audit history", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const prescriptionNote = `Temporary catalog prescription ${Date.now()}`;
    const requestNote = `Temporary refill request ${Date.now()}`;
    let prescriptionId: string | null = null;
    let portalSessionId: string | null = null;
    const messageIds: string[] = [];

    try {
      await page.goto("/clinician/patients/MOD-PAT-0004/chart");
      const prescriptions = page
        .getByRole("heading", { name: /Prescriptions/ })
        .locator("xpath=ancestor::section");
      await prescriptions
        .getByRole("button", { name: "Add prescription" })
        .click();
      await prescriptions
        .getByLabel("Drug name or RXCUI")
        .fill("Metformin");
      await prescriptions
        .getByRole("button", { name: "Search catalog" })
        .click();
      const catalogOption = prescriptions
        .getByRole("option", { name: /Metformin/ })
        .first();
      await expect(catalogOption).toBeVisible({ timeout: 15_000 });
      await catalogOption.click();
      await prescriptions
        .getByLabel("Directions")
        .fill("1 tablet daily with food");
      await prescriptions.getByLabel("Quantity").fill("30");
      await prescriptions.getByLabel("Authorized refills").fill("0");
      await prescriptions.getByLabel("Diagnosis").fill("E11.9");
      await prescriptions
        .getByLabel("Prescription note")
        .fill(prescriptionNote);
      await prescriptions
        .getByRole("button", { name: "Create local prescription" })
        .click();
      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "Prescription created in the local target." }),
      ).toBeVisible();

      const clinicalLists = await page.request.get(
        `${apiBaseUrl}/api/clinical-lists/MOD-PAT-0004`,
        {
          headers: { "X-Legacy EHR-Session": sessionId },
        },
      );
      expect(clinicalLists.ok()).toBeTruthy();
      const createdPrescription = (
        (await clinicalLists.json()) as {
          prescriptions?: Array<{
            id: string;
            drug: string;
            note?: string | null;
          }>;
        }
      ).prescriptions?.find((item) => item.note === prescriptionNote);
      prescriptionId = createdPrescription?.id ?? null;
      expect(prescriptionId).toBeTruthy();
      expect(createdPrescription?.drug).toContain("Metformin");

      const portalLogin = await page.request.post(
        `${apiBaseUrl}/api/patient-portal/login`,
        {
          data: {
            username:
              process.env.MODERN_UI_PORTAL_USERNAME ??
              "mod-pat-0004@example.test",
            password:
              process.env.MODERN_UI_PORTAL_PASSWORD ?? "PortalPass207!",
          },
        },
      );
      expect(portalLogin.ok()).toBeTruthy();
      portalSessionId = (
        (await portalLogin.json()) as { sessionId?: string | null }
      ).sessionId ?? null;
      expect(portalSessionId).toBeTruthy();

      const requested = await page.request.post(
        `${apiBaseUrl}/api/patient-portal/prescriptions/${encodeURIComponent(prescriptionId!)}/refill-request`,
        {
          headers: {
            "X-Legacy EHR-Patient-Portal-Session": portalSessionId!,
          },
          data: {
            requestDate: new Date().toISOString().slice(0, 10),
            note: requestNote,
          },
        },
      );
      expect(requested.ok()).toBeTruthy();
      const requestResult = (await requested.json()) as {
        sentMessage?: { id?: string };
        recipientMessage?: { id?: string };
      };
      for (const id of [
        requestResult.sentMessage?.id,
        requestResult.recipientMessage?.id,
      ]) {
        if (id) messageIds.push(id);
      }

      await page.goto(
        "/clinician/renewals?patient=MOD-PAT-0004&view=requests",
      );
      const requestCard = page.locator("article.rx-renew-item").filter({
        hasText: requestNote,
      });
      await expect(requestCard).toBeVisible({ timeout: 30_000 });
      await expect(requestCard).toContainText(requestNote);
      await expect(requestCard).toContainText("0 current refills");
      await requestCard
        .getByRole("button", { name: "Review and approve" })
        .click();
      await requestCard.getByLabel("Additional refills").fill("2");
      await requestCard
        .getByLabel("Approval note")
        .fill("Browser-verified approval");
      await requestCard
        .getByRole("button", { name: "Approve request" })
        .click();
      await expect(requestCard).toHaveCount(0, { timeout: 30_000 });
      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "approved and reconciled" }),
      ).toBeVisible();

      await page
        .getByRole("button", { name: "All active", exact: true })
        .click();
      const prescriptionCard = page.locator("article.rx-renew-item").filter({
        hasText: prescriptionId!,
      });
      await expect(prescriptionCard).toBeVisible({ timeout: 30_000 });
      await expect(prescriptionCard).toContainText("2 refills");
      await prescriptionCard.getByRole("button", { name: /History/ }).click();
      await expect(
        prescriptionCard.getByRole("heading", {
          name: "Prescription audit history",
        }),
      ).toBeVisible();
      await expect(prescriptionCard).toContainText("Create");
      await expect(prescriptionCard).toContainText(
        "Refill Request Approved",
      );
      await expect(prescriptionCard).toContainText(
        "Browser-verified approval",
      );
    } finally {
      deletePortalMailboxFixtures(messageIds);
      if (prescriptionId) {
        const deletedPrescription = await page.request.delete(
          `${apiBaseUrl}/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}`,
          { headers: { "X-Legacy EHR-Session": sessionId } },
        );
        expect(deletedPrescription.ok()).toBeTruthy();
      }
      if (portalSessionId) {
        await page.request.delete(
          `${apiBaseUrl}/api/patient-portal/session`,
          {
            headers: {
              "X-Legacy EHR-Patient-Portal-Session": portalSessionId,
            },
          },
        );
      }
    }
  });
});
