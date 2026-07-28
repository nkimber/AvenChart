import { expect, test, type Page } from "@playwright/test";
import { Buffer } from "node:buffer";
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

async function signInPortal(page: Page) {
  await page.goto("/portal/login");
  await page
    .getByLabel("Email or username")
    .fill(
      process.env.MODERN_UI_PORTAL_USERNAME ??
        "mod-pat-0004@example.test",
    );
  await page
    .getByLabel("Password")
    .fill(process.env.MODERN_UI_PORTAL_PASSWORD ?? "PortalPass207!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/portal\/home$/, { timeout: 15_000 });
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
    `delete from prescription_refill_request_lifecycle where staff_message_id in (${numericIds}) or thread_id in (${numericIds});`,
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

function runProviderAssignmentSql(sql: string) {
  return execFileSync(
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
      "-t",
      "-A",
      "-c",
      sql,
    ],
    {
      cwd: fileURLToPath(
        new URL("../../avenchart/", import.meta.url),
      ),
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"],
    },
  ).trim();
}

function deleteProviderAssignmentFixtures(reasons: string[]) {
  if (reasons.length === 0) return;
  const reasonLiterals = reasons
    .map((reason) => `'${reason.replaceAll("'", "''")}'`)
    .join(",");
  runProviderAssignmentSql(
    `delete from patient_provider_assignment_events where patient_id = 'MOD-PAT-0004' and reason in (${reasonLiterals});`,
  );
}

function deletePatientAdministrationFixtures(
  eventIds: string[],
  marker: string,
) {
  const ids = eventIds.filter((eventId) =>
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      eventId,
    ),
  );
  const eventPredicate =
    ids.length > 0
      ? `event_id in (${ids.map((eventId) => `'${eventId}'::uuid`).join(",")})`
      : "false";
  const markerLiteral = marker.replaceAll("'", "''");
  runProviderAssignmentSql(
    [
      "begin;",
      `delete from insurance_records where patient_id = 'MOD-PAT-0004' and policy_number like '%${markerLiteral}%';`,
      `delete from patient_administration_audit_events where patient_id = 'MOD-PAT-0004' and (${eventPredicate} or before_values::text like '%${markerLiteral}%' or after_values::text like '%${markerLiteral}%');`,
      "commit;",
    ].join(" "),
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

  test("staff can retain and remove clinical-list lifecycle history with required reasons", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const suffix = Date.now();
    const fixtures = [
      {
        type: "problem",
        title: `Temporary problem ${suffix}`,
        path: "problems",
        data: {
          patientId: "MOD-PAT-0004",
          title: `Temporary problem ${suffix}`,
          dateTime: "2026-07-27",
          diagnosis: "Z00.00",
          comments: "Temporary lifecycle proof",
        },
      },
      {
        type: "allergy",
        title: `Temporary allergy ${suffix}`,
        path: "allergies",
        data: {
          patientId: "MOD-PAT-0004",
          title: `Temporary allergy ${suffix}`,
          dateTime: "2026-07-27",
          reaction: "Temporary reaction",
          severity: "mild",
          comments: "Temporary lifecycle proof",
        },
      },
      {
        type: "medication",
        title: `Temporary medication ${suffix}`,
        path: "medications",
        data: {
          patientId: "MOD-PAT-0004",
          title: `Temporary medication ${suffix}`,
          dateTime: "2026-07-27",
          diagnosis: "Z00.00",
          comments: "Temporary lifecycle proof",
        },
      },
      {
        type: "immunization",
        title: `Temporary vaccine ${suffix}`,
        path: "immunizations",
        data: {
          patientId: "MOD-PAT-0004",
          vaccine: `Temporary vaccine ${suffix}`,
          administeredAt: "2026-07-27T09:00:00",
          manufacturer: "Temporary manufacturer",
          lotNumber: `LOT-${suffix}`,
          note: "Temporary lifecycle proof",
        },
      },
    ] as const;
    const created: Array<{
      type: (typeof fixtures)[number]["type"];
      path: string;
      id: string;
      title: string;
    }> = [];

    try {
      for (const fixture of fixtures) {
        const response = await page.request.post(
          `${apiBaseUrl}/api/clinical-lists/${fixture.path}`,
          {
            headers: { "X-Legacy EHR-Session": sessionId },
            data: fixture.data,
          },
        );
        expect(response.ok()).toBeTruthy();
        const mutation = (await response.json()) as { id?: string };
        expect(mutation.id).toBeTruthy();
        created.push({
          type: fixture.type,
          path: fixture.path,
          id: String(mutation.id),
          title: fixture.title,
        });
      }

      await page.goto("/clinician/patients/MOD-PAT-0004/chart");

      for (const fixture of created) {
        let row = page
          .locator("li.cl-clinical-row-interactive")
          .filter({ hasText: fixture.title });
        await expect(row).toBeVisible({ timeout: 30_000 });
        const actionName =
          fixture.type === "immunization"
            ? `Mark ${fixture.title} entered in error`
            : `Deactivate ${fixture.title}`;
        await row.getByRole("button", { name: actionName }).click();
        await row
          .getByLabel("Clinical reason")
          .fill(`Lifecycle proof reason for ${fixture.type}`);
        await row.getByRole("button", { name: "Confirm" }).click();

        row = page
          .locator("li.cl-clinical-row-interactive")
          .filter({ hasText: fixture.title });
        await expect(row).toContainText(
          fixture.type === "immunization" ? "Lifecycle proof reason" : "Inactive",
          { timeout: 30_000 },
        );
        await row
          .getByRole("button", { name: `Delete ${fixture.title}` })
          .click();
        await row
          .getByLabel("Type DELETE to confirm")
          .fill("DELETE");
        await row
          .getByRole("button", { name: "Delete permanently" })
          .click();
        await expect(row).toHaveCount(0);
      }
    } finally {
      for (const fixture of created) {
        const response = await page.request.delete(
          `${apiBaseUrl}/api/clinical-lists/${fixture.path}/${encodeURIComponent(fixture.id)}`,
          { headers: { "X-Legacy EHR-Session": sessionId } },
        );
        expect([204, 404]).toContain(response.status());
      }
    }
  });

  test("primary-provider changes require a reason and retain actor-stamped assignment history", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const changeReason = `Provider assignment proof ${Date.now()}`;
    const restoreReason = `Provider assignment restore ${Date.now()}`;
    const fixtureReasons = [changeReason, restoreReason];

    const patientResponse = await page.request.get(
      `${apiBaseUrl}/api/patients/MOD-PAT-0004`,
      { headers: { "X-Legacy EHR-Session": sessionId } },
    );
    expect(patientResponse.ok()).toBeTruthy();
    const patient = (await patientResponse.json()) as {
      providerId?: number | null;
      primaryProviderName?: string | null;
    };
    const originalProviderId = patient.providerId ?? null;
    const originalProviderName =
      patient.primaryProviderName?.trim() || "Unassigned";

    const optionsResponse = await page.request.get(
      `${apiBaseUrl}/api/patients/provider-options`,
      { headers: { "X-Legacy EHR-Session": sessionId } },
    );
    expect(optionsResponse.ok()).toBeTruthy();
    const options = (await optionsResponse.json()) as {
      providers: Array<{ id: number; displayName: string }>;
    };
    const alternate = options.providers.find(
      (provider) => provider.id !== originalProviderId,
    );
    expect(alternate).toBeTruthy();

    let assignmentChanged = false;
    try {
      await page.goto("/clinician/patients/MOD-PAT-0004/summary");
      const providerSection = page.locator("section").filter({
        has: page.getByRole("heading", { name: "Primary provider" }),
      });
      await providerSection.getByRole("button", { name: "Edit" }).click();
      await providerSection
        .getByLabel("Provider")
        .selectOption(String(alternate!.id));
      const saveButton = providerSection.getByRole("button", {
        name: "Save provider",
      });
      await expect(saveButton).toBeDisabled();
      await providerSection.getByLabel("Change reason").fill(changeReason);
      await expect(saveButton).toBeEnabled();
      await saveButton.click();
      assignmentChanged = true;

      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "Primary provider assignment saved." }),
      ).toBeVisible({ timeout: 20_000 });
      const historyItem = providerSection
        .locator(".provider-history-list li")
        .filter({ hasText: changeReason });
      await expect(historyItem).toBeVisible({ timeout: 20_000 });
      await expect(historyItem).toContainText(originalProviderName);
      await expect(historyItem).toContainText(alternate!.displayName);
      await expect(historyItem).toContainText("By admin");
      await expect(historyItem.locator("time")).toBeVisible();

      const historyResponse = await page.request.get(
        `${apiBaseUrl}/api/patients/MOD-PAT-0004/provider-assignment-history`,
        { headers: { "X-Legacy EHR-Session": sessionId } },
      );
      expect(historyResponse.ok()).toBeTruthy();
      const history = (await historyResponse.json()) as {
        currentProviderId?: number | null;
        events: Array<{
          fromProviderId?: number | null;
          toProviderId?: number | null;
          reason: string;
          actor: string;
          occurredAt: string;
        }>;
      };
      expect(history.currentProviderId).toBe(alternate!.id);
      expect(history.events).toContainEqual(
        expect.objectContaining({
          fromProviderId: originalProviderId,
          toProviderId: alternate!.id,
          reason: changeReason,
          actor: "admin",
          occurredAt: expect.any(String),
        }),
      );
    } finally {
      if (assignmentChanged) {
        const restored = await page.request.put(
          `${apiBaseUrl}/api/patients/MOD-PAT-0004/provider-assignment`,
          {
            headers: { "X-Legacy EHR-Session": sessionId },
            data: {
              providerId: originalProviderId,
              reason: restoreReason,
            },
          },
        );
        expect(restored.ok()).toBeTruthy();
      }
      deleteProviderAssignmentFixtures(fixtureReasons);
      const residue = runProviderAssignmentSql(
        `select count(*) from patient_provider_assignment_events where patient_id = 'MOD-PAT-0004' and reason in ('${changeReason}', '${restoreReason}');`,
      );
      expect(residue).toBe("0");
    }
  });

  test("contact, demographic, and insurance mutations retain bounded actor-stamped before-and-after history", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const marker = `admin-audit-${Date.now()}`;
    const headers = { "X-Legacy EHR-Session": sessionId };
    const eventIds = new Set<string>();

    const patientResponse = await page.request.get(
      `${apiBaseUrl}/api/patients/MOD-PAT-0004`,
      { headers },
    );
    expect(patientResponse.ok()).toBeTruthy();
    const patient = (await patientResponse.json()) as {
      firstName: string;
      lastName: string;
      preferredName?: string | null;
      sex?: string | null;
      dateOfBirth: string;
      street?: string | null;
      city?: string | null;
      state?: string | null;
      postalCode?: string | null;
      email?: string | null;
      phone?: string | null;
      phoneCell?: string | null;
      hipaaAllowSms?: string | null;
      hipaaAllowEmail?: string | null;
      maritalStatus?: string | null;
      occupation?: string | null;
      race?: string | null;
      ethnicity?: string | null;
      interpreter?: string | null;
      familySize?: string | null;
      monthlyIncome?: string | null;
      homeless?: string | null;
      financialReviewDate?: string | null;
      insurance: Array<{ id: string; policyNumber?: string | null }>;
    };
    const originalContact = {
      phoneHome: patient.phone ?? "",
      phoneCell: patient.phoneCell ?? "",
      email: patient.email ?? "",
      hipaaAllowSms: patient.hipaaAllowSms ?? "NO",
      hipaaAllowEmail: patient.hipaaAllowEmail ?? "NO",
    };
    const originalDemographics = {
      firstName: patient.firstName,
      lastName: patient.lastName,
      preferredName: patient.preferredName ?? "",
      sex: patient.sex ?? "",
      dateOfBirth: patient.dateOfBirth,
      street: patient.street ?? "",
      city: patient.city ?? "",
      state: patient.state ?? "",
      postalCode: patient.postalCode ?? "",
      maritalStatus: patient.maritalStatus ?? "",
      occupation: patient.occupation ?? "",
      race: patient.race ?? "",
      ethnicity: patient.ethnicity ?? "",
      interpreter: patient.interpreter ?? "",
      familySize: patient.familySize ?? "",
      monthlyIncome: patient.monthlyIncome ?? "",
      homeless: patient.homeless ?? "NO",
      financialReviewDate: patient.financialReviewDate ?? "",
    };
    const changedContact = {
      ...originalContact,
      email: `${marker}@example.test`,
    };
    const changedDemographics = {
      ...originalDemographics,
      occupation: `Audit proof ${marker}`,
    };
    const createdInsurance = {
      type: "secondary",
      provider: `Audit Health ${marker}`,
      planName: "Mutation proof",
      policyNumber: marker,
      groupNumber: "QA-ADMIN",
      relationship: "self",
      subscriberFirstName: patient.firstName,
      subscriberLastName: patient.lastName,
      subscriberDateOfBirth: patient.dateOfBirth,
      subscriberSex: patient.sex ?? "unknown",
    };
    const updatedInsurance = {
      ...createdInsurance,
      planName: "Updated mutation proof",
      policyNumber: `${marker}-updated`,
    };
    let insuranceId: string | null = null;

    try {
      const contactUpdate = await page.request.put(
        `${apiBaseUrl}/api/patients/MOD-PAT-0004/contact`,
        { headers, data: changedContact },
      );
      expect(contactUpdate.ok()).toBeTruthy();

      const noOpContactUpdate = await page.request.put(
        `${apiBaseUrl}/api/patients/MOD-PAT-0004/contact`,
        { headers, data: changedContact },
      );
      expect(noOpContactUpdate.ok()).toBeTruthy();

      const demographicsUpdate = await page.request.put(
        `${apiBaseUrl}/api/patients/MOD-PAT-0004/demographics`,
        { headers, data: changedDemographics },
      );
      expect(demographicsUpdate.ok()).toBeTruthy();

      const insuranceCreate = await page.request.post(
        `${apiBaseUrl}/api/patients/MOD-PAT-0004/insurance`,
        { headers, data: createdInsurance },
      );
      expect(insuranceCreate.ok()).toBeTruthy();
      const afterCreate = (await insuranceCreate.json()) as {
        insurance: Array<{ id: string; policyNumber?: string | null }>;
      };
      insuranceId =
        afterCreate.insurance.find(
          (insurance) => insurance.policyNumber === marker,
        )?.id ?? null;
      expect(insuranceId).toBeTruthy();

      const insuranceUpdate = await page.request.put(
        `${apiBaseUrl}/api/patients/insurance/${encodeURIComponent(insuranceId!)}`,
        { headers, data: updatedInsurance },
      );
      expect(insuranceUpdate.ok()).toBeTruthy();

      const insuranceDelete = await page.request.delete(
        `${apiBaseUrl}/api/patients/insurance/${encodeURIComponent(insuranceId!)}`,
        { headers },
      );
      expect(insuranceDelete.ok()).toBeTruthy();
      insuranceId = null;

      const historyResponse = await page.request.get(
        `${apiBaseUrl}/api/patients/MOD-PAT-0004/administration-history`,
        { headers },
      );
      expect(historyResponse.ok()).toBeTruthy();
      const history = (await historyResponse.json()) as {
        eventCount: number;
        returnedCount: number;
        resultLimit: number;
        events: Array<{
          eventId: string;
          area: string;
          action: string;
          entityId?: string | null;
          changedFields: string[];
          beforeValues: Record<string, string | null>;
          afterValues: Record<string, string | null>;
          actor: string;
          occurredAt: string;
        }>;
      };
      expect(history.returnedCount).toBeLessThanOrEqual(history.resultLimit);
      expect(history.eventCount).toBeGreaterThanOrEqual(
        history.returnedCount,
      );
      const markerEvents = history.events.filter((event) =>
        JSON.stringify(event).includes(marker),
      );
      markerEvents.forEach((event) => eventIds.add(event.eventId));
      expect(
        markerEvents.map((event) => `${event.area}:${event.action}`).sort(),
      ).toEqual(
        [
          "contact:updated",
          "demographics:updated",
          "insurance:created",
          "insurance:deleted",
          "insurance:updated",
        ].sort(),
      );
      expect(
        markerEvents.filter(
          (event) =>
            event.area === "contact" && event.action === "updated",
        ),
      ).toHaveLength(1);
      for (const event of markerEvents) {
        expect(event.actor).toBe("admin");
        expect(event.occurredAt).toEqual(expect.any(String));
        expect(event.changedFields.length).toBeGreaterThan(0);
      }
      expect(
        markerEvents.find((event) => event.area === "contact")?.afterValues
          .email,
      ).toBe(changedContact.email);
      expect(
        markerEvents.find((event) => event.area === "demographics")
          ?.afterValues.occupation,
      ).toBe(changedDemographics.occupation);

      await page.goto("/clinician/patients/MOD-PAT-0004/summary");
      const historySection = page.locator("section").filter({
        has: page.getByRole("heading", {
          name: "Administrative change history",
        }),
      });
      await expect(historySection).toBeVisible();
      await expect(
        historySection.getByText("By admin").first(),
      ).toBeVisible({ timeout: 20_000 });
      await historySection
        .getByLabel("Show")
        .selectOption("demographics");
      const demographicEvent = historySection
        .locator(".administration-history-list > li")
        .filter({ hasText: marker });
      await expect(demographicEvent).toContainText("Demographics updated");
      await demographicEvent.locator("summary").click();
      await expect(demographicEvent).toContainText("Occupation");
      await expect(demographicEvent).toContainText(
        changedDemographics.occupation,
      );

      await historySection.getByLabel("Show").selectOption("insurance");
      await expect(
        historySection
          .locator(".administration-history-list > li")
          .filter({ hasText: "Insurance created" }),
      ).toHaveCount(1);
      await expect(
        historySection
          .locator(".administration-history-list > li")
          .filter({ hasText: "Insurance updated" }),
      ).toHaveCount(1);
      await expect(
        historySection
          .locator(".administration-history-list > li")
          .filter({ hasText: "Insurance deleted" }),
      ).toHaveCount(1);
    } finally {
      if (insuranceId) {
        await page.request.delete(
          `${apiBaseUrl}/api/patients/insurance/${encodeURIComponent(insuranceId)}`,
          { headers },
        );
      }
      const restoreContact = await page.request.put(
        `${apiBaseUrl}/api/patients/MOD-PAT-0004/contact`,
        { headers, data: originalContact },
      );
      expect(restoreContact.ok()).toBeTruthy();
      const restoreDemographics = await page.request.put(
        `${apiBaseUrl}/api/patients/MOD-PAT-0004/demographics`,
        { headers, data: originalDemographics },
      );
      expect(restoreDemographics.ok()).toBeTruthy();

      const restoredHistoryResponse = await page.request.get(
        `${apiBaseUrl}/api/patients/MOD-PAT-0004/administration-history`,
        { headers },
      );
      if (restoredHistoryResponse.ok()) {
        const restoredHistory = (await restoredHistoryResponse.json()) as {
          events: Array<{
            eventId: string;
            beforeValues: Record<string, string | null>;
            afterValues: Record<string, string | null>;
          }>;
        };
        restoredHistory.events
          .filter((event) => JSON.stringify(event).includes(marker))
          .forEach((event) => eventIds.add(event.eventId));
      }
      deletePatientAdministrationFixtures([...eventIds], marker);

      const restoredPatientResponse = await page.request.get(
        `${apiBaseUrl}/api/patients/MOD-PAT-0004`,
        { headers },
      );
      expect(restoredPatientResponse.ok()).toBeTruthy();
      const restoredPatient = (await restoredPatientResponse.json()) as {
        email?: string | null;
        occupation?: string | null;
        insurance: Array<{ policyNumber?: string | null }>;
      };
      expect(restoredPatient.email ?? "").toBe(originalContact.email);
      expect(restoredPatient.occupation ?? "").toBe(
        originalDemographics.occupation,
      );
      expect(
        restoredPatient.insurance.some((insurance) =>
          insurance.policyNumber?.includes(marker),
        ),
      ).toBe(false);
      const residue = runProviderAssignmentSql(
        `select (select count(*) from patient_administration_audit_events where patient_id = 'MOD-PAT-0004' and (before_values::text like '%${marker}%' or after_values::text like '%${marker}%')) + (select count(*) from insurance_records where patient_id = 'MOD-PAT-0004' and policy_number like '%${marker}%');`,
      );
      expect(residue).toBe("0");
    }
  });

  test("registration pauses for duplicate review and requires a deliberate separate-patient acknowledgement", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const publicId = `TMP-PAT-REG-DUP-${Date.now()}`;
    const headers = { "X-Legacy EHR-Session": sessionId };
    let created = false;

    try {
      await page.goto("/clinician/patients/new");
      await page.getByLabel("Chart number").fill(publicId);
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
      await expect(page).toHaveURL(/\/clinician\/patients\/new$/);
      const duplicateCheck = page.getByRole("region", {
        name: "Duplicate record check",
      });
      await expect(
        duplicateCheck.getByText(
          "Review possible existing records before continuing.",
        ),
      ).toBeVisible({ timeout: 20_000 });
      const candidate = duplicateCheck
        .locator(".patient-registration-duplicate-list li")
        .filter({ hasText: "MOD-PAT-0004" });
      await expect(candidate).toContainText("Kim, Nora");
      await expect(candidate).toContainText("100% match");
      await expect(candidate).toContainText(
        "Same first name, last name, and date of birth",
      );
      await expect(
        candidate.getByRole("link", { name: "Open existing chart" }),
      ).toHaveAttribute(
        "href",
        "/clinician/patients/MOD-PAT-0004/summary",
      );

      const separatePatientButton = page.getByRole("button", {
        name: "Register separate patient",
      });
      await expect(separatePatientButton).toBeDisabled();
      await duplicateCheck
        .getByLabel(
          /I reviewed these records and intend to register a separate patient/,
        )
        .check();
      await expect(separatePatientButton).toBeEnabled();
      await separatePatientButton.click();
      created = true;

      await expect(page).toHaveURL(
        new RegExp(
          `/clinician/patients/${encodeURIComponent(publicId)}/summary$`,
        ),
        { timeout: 20_000 },
      );
      const chartDuplicateSection = page.locator("section").filter({
        has: page.getByRole("heading", {
          name: "Potential duplicate records",
        }),
      });
      await expect(chartDuplicateSection).toContainText("Kim, Nora");
      await expect(chartDuplicateSection).toContainText("100% match");
    } finally {
      if (created) {
        const deleted = await page.request.delete(
          `${apiBaseUrl}/api/patients/${encodeURIComponent(publicId)}`,
          { headers },
        );
        expect(deleted.status()).toBe(204);
      }
      const residue = await page.request.get(
        `${apiBaseUrl}/api/patients/${encodeURIComponent(publicId)}`,
        { headers },
      );
      expect(residue.status()).toBe(404);
    }
  });

  test("staff can file, refile, and version text, bounded binary, and http document-link records from the patient chart", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const marker = `TMP-DOC-INTAKE-${Date.now()}`;
    const noteName = `${marker}-NOTE`;
    const refiledNoteName = `${noteName}-REFILED`;
    const fileName = `${marker}-FILE`;
    const imageName = `${marker}-IMAGE`;
    const unsupportedName = `${marker}-UNSUPPORTED`;
    const linkName = `${marker}-LINK`;
    const ocrName = `${marker}-SCANNED-OCR`;
    const metadataReason = `Correct filing metadata ${marker}`;
    const originalNoteContent = `Browser-created clinical note ${marker}.`;
    const replacementNoteContent = `Corrected clinical note content ${marker}.`;
    const replacementNoteFileName = `${marker}-NOTE-V2.txt`;
    const contentReason = `Correct document content ${marker}`;
    const binaryReason = `Replace source PDF ${marker}`;
    const approvalReason = `Approve verified content ${marker}`;
    const reopenReason = `Reopen for source review ${marker}`;
    const denialReason = `Deny incomplete source ${marker}`;
    const archiveReason = `Archive superseded chart copy ${marker}`;
    const restoreReason = `Restore after chart reconciliation ${marker}`;
    const routingReason = `Route directive review ${marker}`;
    const routingCompletionReason = `Complete directive handoff ${marker}`;
    const ocrStartReason = `Begin OCR review ${marker}`;
    const ocrFailureReason = `Low contrast source ${marker}`;
    const ocrRetryReason = `Retry after local image adjustment ${marker}`;
    const ocrCompletionReason = `Verify extracted referral text ${marker}`;
    const ocrCorrectionReason = `Correct source surname ${marker}`;
    const ocrExtractedText =
      `Referral received for Morgan Sample. Browser OCR proof ${marker}.`;
    const ocrCorrectedText =
      `Referral received for Morgan Samuels. Browser OCR proof ${marker}.`;
    const routingDueAt = new Date(Date.now() + 72 * 60 * 60 * 1000);
    const routingDueLocal = new Date(
      routingDueAt.valueOf() - routingDueAt.getTimezoneOffset() * 60_000,
    )
      .toISOString()
      .slice(0, 16);
    const originalPdfBytes = Buffer.from(
      "%PDF-1.4\n% Modern UI document proof\n",
    );
    const replacementPdfBytes = Buffer.from(
      "%PDF-1.4\n% Modern UI replacement proof\n",
    );
    const replacementPdfFileName = `${marker}-FILE-V2.pdf`;
    const imageBytes = Buffer.from(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
      "base64",
    );
    const unsupportedBytes = Buffer.from(`Unsupported archive ${marker}`);
    const headers = { "X-Legacy EHR-Session": sessionId };
    let ocrDocumentId: number;

    async function getMarkerDocuments() {
      const response = await page.request.get(
        `${apiBaseUrl}/api/documents/MOD-PAT-0001?includeArchived=true`,
        { headers },
      );
      expect(response.ok()).toBeTruthy();
      const body = (await response.json()) as {
        documents: Array<{
          id: number;
          name: string;
          categoryId: number;
          categoryName: string;
          docDate: string;
          encounter?: number | null;
          mimetype?: string | null;
          fileName?: string | null;
          storageMethod?: string | null;
          url?: string | null;
          notes?: string | null;
          deleted: number;
          archiveStateActor?: string | null;
          archiveStateAt?: string | null;
          archiveEventCount: number;
          previewKind: string;
          previewStatus: string;
          canPreviewInline: boolean;
          canDownload: boolean;
          isScannedAttachment: boolean;
          captureSource: string;
          scanPageCount: number;
          ocrStatus: string;
        }>;
      };
      return body.documents.filter((document) =>
        document.name.includes(marker),
      );
    }

    try {
      const otherEncountersResponse = await page.request.get(
        `${apiBaseUrl}/api/encounters/?patientId=MOD-PAT-0002&from=1900-01-01&limit=1`,
        { headers },
      );
      expect(otherEncountersResponse.ok()).toBeTruthy();
      const otherEncounters = (await otherEncountersResponse.json()) as {
        encounters: Array<{ encounter: number }>;
      };
      expect(otherEncounters.encounters[0]).toBeTruthy();
      const crossPatientLink = await page.request.post(
        `${apiBaseUrl}/api/documents`,
        {
          headers,
          data: {
            patientId: "MOD-PAT-0001",
            categoryId: 3,
            name: `${marker}-CROSS-PATIENT`,
            docDate: "2026-07-28",
            encounter: otherEncounters.encounters[0].encounter,
            content: "This attachment must not persist.",
            notes: marker,
          },
        },
      );
      expect(crossPatientLink.status()).toBe(400);
      const invalidMediaType = await page.request.post(
        `${apiBaseUrl}/api/documents/binary`,
        {
          headers,
          data: {
            patientId: "MOD-PAT-0001",
            categoryId: 3,
            name: `${marker}-INVALID-TYPE`,
            docDate: "2026-07-28",
            encounter: null,
            fileName: "invalid.bin",
            mimetype: "not a media type",
            contentBase64: Buffer.from("invalid").toString("base64"),
            notes: marker,
          },
        },
      );
      expect(invalidMediaType.status()).toBe(400);

      await page.goto(
        "/clinician/patients/MOD-PAT-0001/documents",
      );
      await page.getByRole("button", { name: "Add document" }).click();
      await page.getByLabel("Document name *").fill(noteName);
      await page
        .getByLabel("Filing category *")
        .selectOption({ label: "Medical Record" });
      await page
        .getByLabel("Related encounter")
        .selectOption("1000013");
      await page
        .getByLabel("Note content *")
        .fill(originalNoteContent);
      await page.getByLabel("Filing notes").fill(`Proof ${marker}`);
      await page
        .getByRole("button", { name: "File clinical note" })
        .click();

      const noteCard = page.locator("article").filter({ hasText: noteName });
      await expect(noteCard).toBeVisible({ timeout: 20_000 });
      await expect(noteCard).toContainText("Medical Record");
      await expect(noteCard).toContainText("#1000013");
      await expect(noteCard).toContainText("Just filed");

      const noteBeforeUpdate = (await getMarkerDocuments()).find(
        (document) => document.name === noteName,
      );
      expect(noteBeforeUpdate).toBeTruthy();
      const crossPatientMetadata = await page.request.put(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/metadata`,
        {
          headers,
          data: {
            categoryId: 3,
            name: noteName,
            docDate: "2026-07-28",
            encounter: otherEncounters.encounters[0].encounter,
            notes: `Proof ${marker}`,
            reason: metadataReason,
          },
        },
      );
      expect(crossPatientMetadata.status()).toBe(400);

      await noteCard.getByRole("button", { name: "Edit filing" }).click();
      await noteCard.getByLabel("Document name *").fill(refiledNoteName);
      await noteCard
        .getByLabel("Filing category *")
        .selectOption({ label: "Lab Report" });
      await noteCard.getByLabel("Document date *").fill("2026-07-27");
      await noteCard.getByLabel("Related encounter").selectOption("1000011");
      await noteCard
        .getByLabel("Filing notes")
        .fill(`Refiled proof ${marker}`);
      await noteCard.getByLabel("Change reason *").fill(metadataReason);
      await noteCard
        .getByRole("button", { name: "Save filing change" })
        .click();

      const refiledNoteCard = page
        .locator("article")
        .filter({ hasText: refiledNoteName });
      await expect(refiledNoteCard).toBeVisible({ timeout: 20_000 });
      await expect(refiledNoteCard).toContainText("Lab Report");
      await expect(refiledNoteCard).toContainText("#1000011");
      await expect(refiledNoteCard).toContainText(metadataReason);
      await expect(refiledNoteCard).toContainText("By admin");
      await expect(refiledNoteCard).toContainText(noteName);
      await expect(refiledNoteCard).toContainText("Medical Record");

      const historyResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/metadata-history`,
        { headers },
      );
      expect(historyResponse.ok()).toBeTruthy();
      const history = (await historyResponse.json()) as {
        currentName: string;
        currentCategoryName: string;
        currentEncounter?: number | null;
        eventCount: number;
        events: Array<{
          changedFields: string[];
          reason: string;
          actor: string;
        }>;
      };
      expect(history).toMatchObject({
        currentName: refiledNoteName,
        currentCategoryName: "Lab Report",
        currentEncounter: 1000011,
        eventCount: 1,
      });
      expect(history.events[0]).toMatchObject({
        changedFields: [
          "category",
          "name",
          "documentDate",
          "encounter",
          "notes",
        ],
        reason: metadataReason,
        actor: "admin",
      });

      const noOpMetadata = await page.request.put(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/metadata`,
        {
          headers,
          data: {
            categoryId: 2,
            name: refiledNoteName,
            docDate: "2026-07-27",
            encounter: 1000011,
            notes: `Refiled proof ${marker}`,
            reason: `No-op ${marker}`,
          },
        },
      );
      expect(noOpMetadata.ok()).toBeTruthy();
      const historyAfterNoOp = await page.request.get(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/metadata-history`,
        { headers },
      );
      expect(historyAfterNoOp.ok()).toBeTruthy();
      await expect(historyAfterNoOp.json()).resolves.toMatchObject({
        eventCount: 1,
      });

      await refiledNoteCard
        .getByRole("button", { name: "Replace content" })
        .click();
      await expect(
        refiledNoteCard.getByText("Create the next immutable version"),
      ).toBeVisible();
      await expect(
        refiledNoteCard.locator(".patient-document-version-target strong"),
      ).toHaveText("Version 1");
      await refiledNoteCard
        .getByLabel("Stored file name *")
        .fill(replacementNoteFileName);
      await refiledNoteCard
        .getByLabel("New document content *")
        .fill(replacementNoteContent);
      await refiledNoteCard
        .getByLabel("Replacement reason *")
        .fill(contentReason);
      await refiledNoteCard
        .getByRole("button", { name: "Create next version" })
        .click();

      await expect(
        refiledNoteCard.getByRole("heading", {
          name: "Content version history",
        }),
      ).toBeVisible({ timeout: 20_000 });
      await expect(refiledNoteCard.getByText(contentReason)).toBeVisible();
      await expect(refiledNoteCard.getByText("By admin")).toBeVisible();
      await expect(
        refiledNoteCard.getByText("Current version", { exact: true }),
      ).toBeVisible();
      await expect(
        refiledNoteCard.getByText("Prior version", { exact: true }),
      ).toBeVisible();

      const noteVersionsResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/versions`,
        { headers },
      );
      expect(noteVersionsResponse.ok()).toBeTruthy();
      const noteVersions = (await noteVersionsResponse.json()) as {
        currentVersion: number;
        versionCount: number;
        versions: Array<{
          version: number;
          versionStatus: string;
          revisionActor?: string | null;
          revisionReason?: string | null;
          fileName?: string | null;
          mimetype?: string | null;
          sizeBytes?: number | null;
          hash?: string | null;
        }>;
      };
      expect(noteVersions).toMatchObject({
        currentVersion: 2,
        versionCount: 2,
      });
      expect(noteVersions.versions[0]).toMatchObject({
        version: 2,
        versionStatus: "Current version",
        revisionActor: "admin",
        revisionReason: contentReason,
        fileName: replacementNoteFileName,
        mimetype: "text/plain",
        sizeBytes: Buffer.byteLength(replacementNoteContent),
        hash: expect.any(String),
      });
      expect(noteVersions.versions[1]).toMatchObject({
        version: 1,
        versionStatus: "Prior version",
        revisionActor: null,
        revisionReason: null,
        mimetype: "text/plain",
        hash: expect.any(String),
      });

      const originalVersionContent = await page.request.get(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/versions/1/content`,
        { headers },
      );
      expect(originalVersionContent.ok()).toBeTruthy();
      await expect(originalVersionContent.json()).resolves.toMatchObject({
        version: 1,
        versionStatus: "Prior version",
        content: originalNoteContent,
        isBinary: false,
      });
      const originalVersionDownload = await page.request.get(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/versions/1/download`,
        { headers },
      );
      expect(originalVersionDownload.ok()).toBeTruthy();
      expect(await originalVersionDownload.text()).toBe(originalNoteContent);

      const staleReplacement = await page.request.put(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/content`,
        {
          headers,
          data: {
            fileName: `${marker}-STALE.txt`,
            content: `Stale content ${marker}`,
            reason: `Stale replacement ${marker}`,
            expectedVersion: 1,
          },
        },
      );
      expect(staleReplacement.status()).toBe(409);
      await expect(staleReplacement.json()).resolves.toMatchObject({
        currentVersion: 2,
      });

      const noOpReplacement = await page.request.put(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/content`,
        {
          headers,
          data: {
            fileName: replacementNoteFileName,
            content: replacementNoteContent,
            reason: `No-op content ${marker}`,
            expectedVersion: 2,
          },
        },
      );
      expect(noOpReplacement.status()).toBe(400);
      const noteHistoryAfterNoOp = await page.request.get(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/versions`,
        { headers },
      );
      await expect(noteHistoryAfterNoOp.json()).resolves.toMatchObject({
        currentVersion: 2,
        versionCount: 2,
      });

      await refiledNoteCard
        .getByRole("button", { name: "Preview", exact: true })
        .click();
      await expect(
        refiledNoteCard.getByRole("heading", {
          name: `Previewing ${refiledNoteName}`,
        }),
      ).toBeVisible();
      await expect(
        refiledNoteCard.getByLabel(`Text content of ${refiledNoteName}`),
      ).toHaveText(replacementNoteContent);
      await expect(refiledNoteCard).toContainText("text/plain");
      await expect(refiledNoteCard).toContainText("Version 2");
      await refiledNoteCard
        .getByRole("button", { name: "Close preview" })
        .last()
        .click();

      await refiledNoteCard
        .getByRole("button", { name: "Review document" })
        .click();
      await expect(
        refiledNoteCard.getByRole("heading", { name: "Review lifecycle" }),
      ).toBeVisible();
      await expect(
        refiledNoteCard.getByRole("button", {
          name: "Approve",
          exact: true,
        }),
      ).toHaveAttribute("aria-pressed", "true");
      await refiledNoteCard
        .getByLabel("Approval rationale *")
        .fill(approvalReason);
      await refiledNoteCard
        .getByRole("button", { name: "Approve document" })
        .click();
      await expect(refiledNoteCard.getByText(approvalReason)).toBeVisible({
        timeout: 20_000,
      });
      await expect(
        refiledNoteCard
          .locator(".patient-document-review-history")
          .getByText("Approved", { exact: true }),
      ).toBeVisible();
      await expect(refiledNoteCard).toContainText("admin");
      await expect(refiledNoteCard).toContainText("Version 2");

      const staleDenial = await page.request.put(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/sign`,
        {
          headers,
          data: {
            reviewStatus: "denied",
            reason: `Stale denial ${marker}`,
            expectedReviewStatus: "pending",
          },
        },
      );
      expect(staleDenial.status()).toBe(409);
      await expect(staleDenial.json()).resolves.toMatchObject({
        currentStatus: "approved",
      });

      await refiledNoteCard
        .getByRole("button", { name: "Reopen review" })
        .click();
      await refiledNoteCard.getByLabel("Reopen reason *").fill(reopenReason);
      await refiledNoteCard
        .getByRole("button", { name: "Reopen review" })
        .last()
        .click();
      await expect(refiledNoteCard.getByText(reopenReason)).toBeVisible({
        timeout: 20_000,
      });
      await expect(
        refiledNoteCard
          .locator(".patient-document-review-history")
          .getByText("Reopened", { exact: true }),
      ).toBeVisible();

      await refiledNoteCard
        .getByRole("button", { name: "Review document" })
        .click();
      await refiledNoteCard.getByRole("button", { name: "Deny" }).click();
      await refiledNoteCard.getByLabel("Denial reason *").fill(denialReason);
      await refiledNoteCard
        .getByRole("button", { name: "Deny document" })
        .click();
      await expect(refiledNoteCard.getByText(denialReason)).toBeVisible({
        timeout: 20_000,
      });
      await expect(
        refiledNoteCard
          .locator(".patient-document-review-history")
          .getByText("Denied", { exact: true }),
      ).toBeVisible();
      await expect(refiledNoteCard).toContainText("3 events");

      const terminalApproval = await page.request.put(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/sign`,
        {
          headers,
          data: {
            reviewStatus: "approved",
            reason: `Invalid terminal approval ${marker}`,
            expectedReviewStatus: "denied",
          },
        },
      );
      expect(terminalApproval.status()).toBe(409);
      await expect(terminalApproval.json()).resolves.toMatchObject({
        currentStatus: "denied",
      });

      const reviewHistoryResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/review-history`,
        { headers },
      );
      expect(reviewHistoryResponse.ok()).toBeTruthy();
      await expect(reviewHistoryResponse.json()).resolves.toMatchObject({
        currentStatus: "denied",
        currentReviewer: "admin",
        eventCount: 3,
        returnedCount: 3,
        resultLimit: 100,
        events: [
          {
            fromStatus: "pending",
            toStatus: "denied",
            action: "Denied",
            reason: denialReason,
            actor: "admin",
            documentVersion: 2,
            contentHash: expect.any(String),
          },
          {
            fromStatus: "approved",
            toStatus: "pending",
            action: "Reopened",
            reason: reopenReason,
            actor: "admin",
            documentVersion: 2,
            contentHash: expect.any(String),
          },
          {
            fromStatus: "pending",
            toStatus: "approved",
            action: "Approved",
            reason: approvalReason,
            actor: "admin",
            documentVersion: 2,
            contentHash: expect.any(String),
          },
        ],
      });

      await refiledNoteCard
        .getByRole("button", { name: "Archive document" })
        .click();
      await expect(
        refiledNoteCard.getByRole("heading", {
          name: "Archive lifecycle",
        }),
      ).toBeVisible();
      await expect(refiledNoteCard.getByText("Active", { exact: true })).toBeVisible();
      await refiledNoteCard
        .getByLabel("Archive reason *")
        .fill(archiveReason);
      await refiledNoteCard
        .getByRole("button", { name: "Archive document", exact: true })
        .click();
      await expect(refiledNoteCard).toHaveCount(0, { timeout: 20_000 });

      const defaultRegisterResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/MOD-PAT-0001`,
        { headers },
      );
      expect(defaultRegisterResponse.ok()).toBeTruthy();
      const defaultRegister = (await defaultRegisterResponse.json()) as {
        activeCount: number;
        archivedCount: number;
        includesArchived: boolean;
        documents: Array<{ id: number }>;
      };
      expect(defaultRegister.includesArchived).toBe(false);
      expect(
        defaultRegister.documents.some(
          (document) => document.id === noteBeforeUpdate!.id,
        ),
      ).toBe(false);
      expect(defaultRegister.archivedCount).toBeGreaterThanOrEqual(1);

      const staleArchive = await page.request.put(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/soft-delete`,
        {
          headers,
          data: {
            reason: `Stale archive replay ${marker}`,
            expectedArchived: false,
          },
        },
      );
      expect(staleArchive.status()).toBe(409);
      await expect(staleArchive.json()).resolves.toMatchObject({
        currentArchived: true,
      });

      await page.getByLabel("Show archived").check();
      const archivedNoteCard = page
        .locator("article")
        .filter({ hasText: refiledNoteName });
      await expect(archivedNoteCard).toBeVisible({ timeout: 20_000 });
      await expect(
        archivedNoteCard.getByText("Archived", { exact: true }).first(),
      ).toBeVisible();
      await expect(
        archivedNoteCard.getByRole("button", { name: "Edit filing" }),
      ).toHaveCount(0);
      await archivedNoteCard
        .getByRole("button", { name: "Restore document" })
        .click();
      await archivedNoteCard
        .getByLabel("Restore reason *")
        .fill(restoreReason);
      await archivedNoteCard
        .getByRole("button", {
          name: "Restore to active register",
        })
        .click();

      const restoredNoteCard = page
        .locator("article")
        .filter({ hasText: refiledNoteName });
      await expect(restoredNoteCard).toBeVisible({ timeout: 20_000 });
      await expect(
        restoredNoteCard.getByRole("button", { name: "Edit filing" }),
      ).toBeVisible();
      await restoredNoteCard
        .getByRole("button", { name: "Archive history" })
        .click();
      await expect(restoredNoteCard.getByText("2 transitions")).toBeVisible();
      const archiveEvents = restoredNoteCard.locator(
        ".patient-document-archive-history > li",
      );
      await expect(archiveEvents).toHaveCount(2);
      await expect(archiveEvents.nth(0)).toContainText("Restored");
      await expect(archiveEvents.nth(0)).toContainText(restoreReason);
      await expect(archiveEvents.nth(1)).toContainText("Archived");
      await expect(archiveEvents.nth(1)).toContainText(archiveReason);
      await expect(restoredNoteCard).toContainText("Version 2");
      await expect(restoredNoteCard).toContainText("denied");

      const staleRestore = await page.request.put(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/restore`,
        {
          headers,
          data: {
            reason: `Stale restore replay ${marker}`,
            expectedArchived: true,
          },
        },
      );
      expect(staleRestore.status()).toBe(409);
      await expect(staleRestore.json()).resolves.toMatchObject({
        currentArchived: false,
      });

      const archiveHistoryResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/${noteBeforeUpdate!.id}/archive-history`,
        { headers },
      );
      expect(archiveHistoryResponse.ok()).toBeTruthy();
      await expect(archiveHistoryResponse.json()).resolves.toMatchObject({
        currentArchived: false,
        currentStateActor: "admin",
        eventCount: 2,
        returnedCount: 2,
        resultLimit: 100,
        events: [
          {
            action: "Restored",
            fromArchived: true,
            toArchived: false,
            reason: restoreReason,
            actor: "admin",
            documentVersion: 2,
            reviewStatus: "denied",
            contentHash: expect.any(String),
          },
          {
            action: "Archived",
            fromArchived: false,
            toArchived: true,
            reason: archiveReason,
            actor: "admin",
            documentVersion: 2,
            reviewStatus: "denied",
            contentHash: expect.any(String),
          },
        ],
      });

      await page.getByRole("button", { name: "Add document" }).click();
      await page
        .getByRole("button", { name: /Upload file Up to/ })
        .click();
      await page.getByLabel("Document file *").setInputFiles({
        name: "too-large.pdf",
        mimeType: "application/pdf",
        buffer: Buffer.alloc(26_214_401),
      });
      await expect(page.getByRole("alert")).toContainText(
        "accepts files up to 25.0 MB",
      );
      await page.getByLabel("Document file *").setInputFiles({
        name: `${marker}.pdf`,
        mimeType: "application/pdf",
        buffer: originalPdfBytes,
      });
      await page.getByLabel("Document name *").fill(fileName);
      await page.getByLabel("Filing notes").fill(`Binary proof ${marker}`);
      await page
        .getByRole("button", { name: "Upload document" })
        .click();

      const fileCard = page.locator("article").filter({ hasText: fileName });
      await expect(fileCard).toBeVisible({ timeout: 20_000 });
      await expect(fileCard).toContainText("application/pdf");
      await expect(fileCard).toContainText("Just filed");

      await fileCard
        .getByRole("button", { name: "Replace content" })
        .click();
      await fileCard
        .getByLabel("Choose the complete replacement file")
        .setInputFiles({
          name: replacementPdfFileName,
          mimeType: "application/pdf",
          buffer: replacementPdfBytes,
        });
      await fileCard.getByLabel("Replacement reason *").fill(binaryReason);
      await fileCard
        .getByRole("button", { name: "Create next version" })
        .click();
      await expect(fileCard.getByText(binaryReason)).toBeVisible({
        timeout: 20_000,
      });
      await expect(fileCard.getByText("By admin")).toBeVisible();

      const fileDocument = (await getMarkerDocuments()).find(
        (document) => document.name === fileName,
      );
      expect(fileDocument).toBeTruthy();
      const fileVersionsResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/${fileDocument!.id}/versions`,
        { headers },
      );
      expect(fileVersionsResponse.ok()).toBeTruthy();
      await expect(fileVersionsResponse.json()).resolves.toMatchObject({
        currentVersion: 2,
        versionCount: 2,
        versions: [
          {
            version: 2,
            revisionActor: "admin",
            revisionReason: binaryReason,
            fileName: replacementPdfFileName,
            mimetype: "application/pdf",
            sizeBytes: replacementPdfBytes.length,
            hash: expect.any(String),
          },
          {
            version: 1,
            mimetype: "application/pdf",
            sizeBytes: originalPdfBytes.length,
            hash: expect.any(String),
          },
        ],
      });
      const originalPdfContent = await page.request.get(
        `${apiBaseUrl}/api/documents/${fileDocument!.id}/versions/1/content`,
        { headers },
      );
      expect(originalPdfContent.ok()).toBeTruthy();
      const originalPdfVersion = (await originalPdfContent.json()) as {
        isBinary: boolean;
        contentBase64?: string | null;
      };
      expect(originalPdfVersion.isBinary).toBe(true);
      expect(Buffer.from(originalPdfVersion.contentBase64 ?? "", "base64")).toEqual(
        originalPdfBytes,
      );

      await fileCard
        .getByRole("button", { name: "Preview", exact: true })
        .click();
      const pdfPreview = fileCard.getByTitle(`${fileName} PDF preview`);
      await expect(pdfPreview).toBeVisible();
      await expect(pdfPreview).toHaveAttribute("src", /^blob:/);
      await expect(fileCard).toContainText("application/pdf");
      await fileCard
        .getByRole("button", { name: "Close preview" })
        .last()
        .click();

      await page.getByRole("button", { name: "Add document" }).click();
      await page
        .getByRole("button", { name: /Upload file Up to/ })
        .click();
      await page.getByLabel("Document file *").setInputFiles({
        name: `${marker}.png`,
        mimeType: "image/png",
        buffer: imageBytes,
      });
      await page.getByLabel("Document name *").fill(imageName);
      await page.getByLabel("Filing notes").fill(`Image proof ${marker}`);
      await page
        .getByRole("button", { name: "Upload document" })
        .click();

      const imageCard = page.locator("article").filter({ hasText: imageName });
      await expect(imageCard).toBeVisible({ timeout: 20_000 });
      await expect(imageCard).toContainText("Inline image");
      await imageCard
        .getByRole("button", { name: "Preview", exact: true })
        .click();
      const imagePreview = imageCard.getByAltText(`Preview of ${imageName}`);
      await expect(imagePreview).toBeVisible();
      await expect(imagePreview).toHaveAttribute("src", /^blob:/);
      await imageCard
        .getByRole("button", { name: "Close preview" })
        .last()
        .click();

      const unsupportedResponse = await page.request.post(
        `${apiBaseUrl}/api/documents/binary`,
        {
          headers,
          data: {
            patientId: "MOD-PAT-0001",
            categoryId: 3,
            name: unsupportedName,
            docDate: "2026-07-28",
            encounter: null,
            fileName: `${marker}.zip`,
            mimetype: "application/zip",
            contentBase64: unsupportedBytes.toString("base64"),
            notes: `Unsupported preview proof ${marker}`,
          },
        },
      );
      expect(unsupportedResponse.status()).toBe(201);
      await page.getByRole("button", { name: "Refresh" }).click();
      const unsupportedCard = page
        .locator("article")
        .filter({ hasText: unsupportedName });
      await expect(unsupportedCard).toBeVisible({ timeout: 20_000 });
      await expect(unsupportedCard).toContainText("application/zip");
      await expect(unsupportedCard).toContainText("Download only");
      await expect(
        unsupportedCard.getByRole("button", { name: "Preview", exact: true }),
      ).toHaveCount(0);
      await expect(
        unsupportedCard.getByRole("button", {
          name: `Download ${unsupportedName}`,
        }),
      ).toBeVisible();

      await page.getByRole("button", { name: "Add document" }).click();
      await page
        .getByRole("button", { name: /Scanner capture Local receipt/ })
        .click();
      await page.getByLabel("Document name *").fill(ocrName);
      await page.getByLabel("Filing category *").selectOption("3");
      await page.getByLabel("Document date *").fill("2026-07-28");
      await page.getByLabel("Related encounter").selectOption("1000013");
      await page
        .getByLabel("Scanner or capture source *")
        .fill("chart scanner");
      await page.getByLabel("Captured pages *").fill("3");
      await page
        .getByLabel("Filing notes")
        .fill(`Scanner capture proof ${marker}`);
      await page
        .getByRole("button", { name: "File scanner capture" })
        .click();
      await expect(page.getByText("Scanner capture receipt filed")).toBeVisible();

      const ocrDocument = (await getMarkerDocuments()).find(
        (document) => document.name === ocrName,
      );
      expect(ocrDocument).toMatchObject({
        mimetype: "application/pdf",
        isScannedAttachment: true,
        captureSource: "chart scanner",
        scanPageCount: 3,
        ocrStatus: "OCR pending",
      });
      expect(ocrDocument?.notes).toContain("Captured by: admin");
      ocrDocumentId = Number(ocrDocument?.id);
      expect(ocrDocumentId).toBeGreaterThan(0);

      await page.getByRole("button", { name: "Add document" }).click();
      await page
        .getByRole("button", { name: /External link HTTP/ })
        .click();
      await page.getByLabel("Document name *").fill(linkName);
      await page
        .getByLabel("Filing category *")
        .selectOption({ label: "Advance Directive" });
      await page
        .getByLabel("External document URL *")
        .fill("ftp://example.test/not-permitted");
      await page
        .getByRole("button", { name: "File external link" })
        .click();
      await expect(page.getByRole("alert")).toContainText(
        "must use http or https",
      );
      await page
        .getByLabel("External document URL *")
        .fill(`https://example.test/${marker}`);
      await page.getByLabel("Filing notes").fill(`Link proof ${marker}`);
      await page
        .getByRole("button", { name: "File external link" })
        .click();

      const linkCard = page.locator("article").filter({ hasText: linkName });
      await expect(linkCard).toBeVisible({ timeout: 20_000 });
      await expect(linkCard).toContainText("Advance Directive");
      await expect(linkCard).toContainText("External link");
      await expect(
        linkCard.getByRole("link", { name: "Open link" }),
      ).toHaveAttribute("href", `https://example.test/${marker}`);

      const linkDocument = (await getMarkerDocuments()).find(
        (document) => document.name === linkName,
      );
      expect(linkDocument).toBeTruthy();

      await page.goto("/clinician/documents");
      await page
        .getByLabel(
          "Patient, chart, document, category, destination, or assignee",
        )
        .fill(marker);
      await page.getByRole("button", { name: "Apply filters" }).click();
      const routingQueue = page.getByLabel("Document routing queue");
      const routingCard = routingQueue
        .locator(".document-routing-card")
        .filter({ hasText: linkName });
      await expect(routingCard).toBeVisible({ timeout: 20_000 });
      await expect(routingCard).toContainText("Awaiting review");
      await expect(routingCard).toContainText("Clinical review");
      await expect(routingCard).toContainText("High");
      await expect(routingCard).toContainText("Inferred / not yet routed");

      await routingCard
        .getByRole("button", { name: "Route document" })
        .click();
      await routingCard.getByLabel("Assign to").selectOption("admin");
      await routingCard.getByLabel("Due *").fill(routingDueLocal);
      await routingCard.getByLabel("Routing reason *").fill(routingReason);
      await routingCard
        .getByRole("button", { name: "Save routing" })
        .click();
      await expect(routingCard).toContainText("In progress", {
        timeout: 20_000,
      });
      await expect(routingCard).toContainText("Administrator (admin)");
      await expect(routingCard).toContainText("Task version");
      await expect(routingCard).toContainText("v1");

      const staleRoute = await page.request.put(
        `${apiBaseUrl}/api/documents/${linkDocument!.id}/routing`,
        {
          headers,
          data: {
            destination: "Records review",
            priority: "Standard",
            assignedTo: null,
            reason: `Stale routing attempt ${marker}`,
            dueAt: new Date(Date.now() + 96 * 60 * 60 * 1000).toISOString(),
            expectedTaskVersion: 0,
          },
        },
      );
      expect(staleRoute.status()).toBe(409);
      await expect(staleRoute.json()).resolves.toMatchObject({
        currentTaskVersion: 1,
        currentStatus: "in_progress",
      });

      await routingCard
        .getByRole("button", { name: "Routing history" })
        .click();
      await expect(routingCard.getByText("1 of 1 events")).toBeVisible();
      await expect(
        routingCard
          .locator(".patient-document-history-list")
          .getByText(routingReason),
      ).toBeVisible();
      await expect(routingCard).toContainText("task v1");

      await routingCard
        .getByRole("button", { name: "Complete work" })
        .click();
      await routingCard
        .getByLabel("Completion note *")
        .fill(routingCompletionReason);
      await routingCard
        .getByRole("button", { name: "Complete routing work" })
        .click();
      await expect(routingCard).toHaveCount(0, { timeout: 20_000 });

      await page.getByLabel("Status").selectOption("completed");
      await page.getByRole("button", { name: "Apply filters" }).click();
      const completedRoutingCard = page
        .getByLabel("Document routing queue")
        .locator(".document-routing-card")
        .filter({ hasText: linkName });
      await expect(completedRoutingCard).toBeVisible({ timeout: 20_000 });
      await expect(completedRoutingCard).toContainText("Completed");
      await expect(
        completedRoutingCard.getByRole("button", { name: "Complete work" }),
      ).toHaveCount(0);
      await expect(
        completedRoutingCard.getByRole("button", { name: "Reopen route" }),
      ).toBeVisible();
      await completedRoutingCard
        .getByRole("button", { name: "Routing history" })
        .click();
      const routeEvents = completedRoutingCard.locator(
        ".patient-document-history-list > li",
      );
      await expect(routeEvents).toHaveCount(2);
      await expect(routeEvents.nth(0)).toContainText("Completed");
      await expect(routeEvents.nth(0)).toContainText(routingCompletionReason);
      await expect(routeEvents.nth(1)).toContainText("Routed");
      await expect(routeEvents.nth(1)).toContainText(routingReason);

      const staleCompletion = await page.request.post(
        `${apiBaseUrl}/api/documents/${linkDocument!.id}/routing/complete`,
        {
          headers,
          data: {
            reason: `Stale routing completion ${marker}`,
            expectedTaskVersion: 1,
          },
        },
      );
      expect(staleCompletion.status()).toBe(409);
      await expect(staleCompletion.json()).resolves.toMatchObject({
        currentTaskVersion: 2,
        currentStatus: "completed",
      });

      const routingHistoryResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/${linkDocument!.id}/routing-history`,
        { headers },
      );
      expect(routingHistoryResponse.ok()).toBeTruthy();
      await expect(routingHistoryResponse.json()).resolves.toMatchObject({
        currentTaskVersion: 2,
        currentStatus: "completed",
        currentAssignedTo: "admin",
        currentDestination: "Clinical review",
        currentPriority: "High",
        eventCount: 2,
        returnedCount: 2,
        resultLimit: 100,
        events: [
          {
            action: "completed",
            fromStatus: "in_progress",
            toStatus: "completed",
            reason: routingCompletionReason,
            actor: "admin",
            taskVersion: 2,
            documentVersion: 1,
            reviewStatus: "pending",
          },
          {
            action: "routed",
            fromStatus: "inferred",
            toStatus: "in_progress",
            reason: routingReason,
            actor: "admin",
            taskVersion: 1,
            documentVersion: 1,
            reviewStatus: "pending",
          },
        ],
      });

      const activeQueueResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/routing-queue?status=active&limit=1`,
        { headers },
      );
      expect(activeQueueResponse.ok()).toBeTruthy();
      const activeQueue = (await activeQueueResponse.json()) as {
        count: number;
        counts: { active: number };
      };
      expect(activeQueue.count).toBe(activeQueue.counts.active);
      await page.goto("/clinician/dashboard");
      const dashboardDocumentQueue = page
        .locator('a[href="/clinician/documents"]')
        .filter({ hasText: "Documents to route" });
      await expect(
        dashboardDocumentQueue.locator(".dash-stat-value"),
      ).toHaveText(String(activeQueue.counts.active), { timeout: 20_000 });

      const ocrSourceBeforeResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/${ocrDocumentId}/content`,
        { headers },
      );
      expect(ocrSourceBeforeResponse.ok()).toBeTruthy();
      const ocrSourceBefore = (await ocrSourceBeforeResponse.json()) as {
        uploadedAt: string;
        hash?: string | null;
        contentBase64?: string | null;
        ocrStatus: string;
      };
      expect(ocrSourceBefore.ocrStatus).toBe("OCR pending");
      const ocrSourceBytesBefore = Buffer.from(
        ocrSourceBefore.contentBase64 ?? "",
        "base64",
      );
      expect(ocrSourceBytesBefore.length).toBeGreaterThan(0);
      expect(ocrSourceBytesBefore.toString("utf8", 0, 8)).toContain("%PDF");

      await page.goto("/clinician/document-ocr");
      await page.getByLabel("Search documents").fill(marker);
      await page.getByRole("button", { name: "Apply filters" }).click();
      const ocrQueue = page.getByLabel("Document OCR queue");
      const ocrCard = ocrQueue
        .locator(".document-ocr-card")
        .filter({ hasText: ocrName });
      await expect(ocrCard).toBeVisible({ timeout: 20_000 });
      await expect(ocrCard).toContainText("Ready for OCR");
      await expect(ocrCard).toContainText("chart scanner / 3 pages");
      await expect(ocrCard).toContainText("Task v0 / document v1");

      await ocrCard.getByRole("button", { name: "Start OCR" }).click();
      await ocrCard.getByLabel("Work note *").fill(ocrStartReason);
      await ocrCard
        .getByRole("button", { name: "Start OCR", exact: true })
        .last()
        .click();
      await expect(ocrCard).toContainText("OCR running", {
        timeout: 20_000,
      });
      await expect(ocrCard).toContainText("Task v1 / document v1");

      const staleOcrStart = await page.request.post(
        `${apiBaseUrl}/api/documents/${ocrDocumentId}/ocr/start`,
        {
          headers,
          data: {
            expectedTaskVersion: 0,
            reason: `Stale OCR start ${marker}`,
          },
        },
      );
      expect(staleOcrStart.status()).toBe(409);
      await expect(staleOcrStart.json()).resolves.toMatchObject({
        currentTaskVersion: 1,
        currentStatus: "running",
      });

      await ocrCard
        .getByRole("button", { name: "Record failure" })
        .click();
      await ocrCard.getByLabel("Failure reason *").fill(ocrFailureReason);
      await ocrCard
        .getByRole("button", { name: "Record failure", exact: true })
        .last()
        .click();
      await expect(ocrCard).toContainText("OCR failed", {
        timeout: 20_000,
      });
      await expect(ocrCard).toContainText(ocrFailureReason);
      await expect(ocrCard).toContainText("Task v2 / document v1");

      await ocrCard.getByRole("button", { name: "OCR history" }).click();
      await expect(ocrCard.getByText("2 of 2 events")).toBeVisible();
      const failedOcrEvents = ocrCard.locator(
        ".patient-document-history-list > li",
      );
      await expect(failedOcrEvents).toHaveCount(2);
      await expect(failedOcrEvents.nth(0)).toContainText(ocrFailureReason);
      await expect(failedOcrEvents.nth(1)).toContainText(ocrStartReason);
      await ocrCard
        .getByRole("button", { name: "Close OCR history" })
        .click();

      await ocrCard.getByRole("button", { name: "Retry OCR" }).click();
      await ocrCard.getByLabel("Work note *").fill(ocrRetryReason);
      await ocrCard.getByRole("button", { name: "Start OCR" }).click();
      await expect(ocrCard).toContainText("OCR running", {
        timeout: 20_000,
      });
      await expect(ocrCard).toContainText("Task v3 / document v1");

      await ocrCard.getByRole("button", { name: "Complete OCR" }).click();
      await ocrCard.getByLabel("Extracted text *").fill(ocrExtractedText);
      await ocrCard.getByLabel("Work note *").fill(ocrCompletionReason);
      await ocrCard
        .getByRole("button", { name: "Complete OCR", exact: true })
        .last()
        .click();
      await expect(ocrCard).toHaveCount(0, { timeout: 20_000 });

      await page.getByLabel("Status").selectOption("completed");
      await page.getByRole("button", { name: "Apply filters" }).click();
      const completedOcrCard = page
        .getByLabel("Document OCR queue")
        .locator(".document-ocr-card")
        .filter({ hasText: ocrName });
      await expect(completedOcrCard).toBeVisible({ timeout: 20_000 });
      await expect(completedOcrCard).toContainText("OCR complete");
      await expect(completedOcrCard).toContainText(
        `${ocrExtractedText.length} retained characters`,
      );
      await expect(completedOcrCard).toContainText(ocrExtractedText);

      await completedOcrCard
        .getByRole("button", { name: "Correct extracted text" })
        .click();
      await expect(
        completedOcrCard.getByLabel("Extracted text *"),
      ).toHaveValue(ocrExtractedText);
      await completedOcrCard
        .getByLabel("Extracted text *")
        .fill(ocrCorrectedText);
      await completedOcrCard
        .getByLabel("Correction reason *")
        .fill(ocrCorrectionReason);
      await completedOcrCard
        .getByRole("button", { name: "Save correction" })
        .click();
      await expect(completedOcrCard).toContainText(
        `${ocrCorrectedText.length} retained characters`,
        { timeout: 20_000 },
      );
      await expect(completedOcrCard).toContainText(ocrCorrectedText);
      await expect(completedOcrCard).toContainText(
        "Task v5 / document v1",
      );

      const staleOcrCorrection = await page.request.post(
        `${apiBaseUrl}/api/documents/${ocrDocumentId}/ocr/correct`,
        {
          headers,
          data: {
            expectedTaskVersion: 4,
            extractedText: `Stale corrected text ${marker}`,
            reason: `Stale OCR correction ${marker}`,
          },
        },
      );
      expect(staleOcrCorrection.status()).toBe(409);
      await expect(staleOcrCorrection.json()).resolves.toMatchObject({
        currentTaskVersion: 5,
        currentStatus: "completed",
      });

      await completedOcrCard
        .getByRole("button", { name: "OCR history" })
        .click();
      await expect(completedOcrCard.getByText("5 of 5 events")).toBeVisible();
      await expect(
        completedOcrCard.getByText(
          `Current extracted text (${ocrCorrectedText.length} characters)`,
        ),
      ).toBeVisible();
      const completedOcrEvents = completedOcrCard.locator(
        ".patient-document-history-list > li",
      );
      await expect(completedOcrEvents).toHaveCount(5);
      await expect(completedOcrEvents.nth(0)).toContainText("Corrected");
      await expect(completedOcrEvents.nth(0)).toContainText(
        ocrCorrectionReason,
      );
      await expect(completedOcrEvents.nth(1)).toContainText("Completed");
      await expect(completedOcrEvents.nth(2)).toContainText("Retried");
      await expect(completedOcrEvents.nth(3)).toContainText("Failed");
      await expect(completedOcrEvents.nth(4)).toContainText("Started");

      const ocrHistoryResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/${ocrDocumentId}/ocr-history`,
        { headers },
      );
      expect(ocrHistoryResponse.ok()).toBeTruthy();
      await expect(ocrHistoryResponse.json()).resolves.toMatchObject({
        documentId: ocrDocumentId,
        currentTaskVersion: 5,
        currentStatus: "completed",
        currentOcrStatus: "OCR complete",
        currentExtractedText: ocrCorrectedText,
        eventCount: 5,
        returnedCount: 5,
        resultLimit: 100,
        events: [
          {
            action: "corrected",
            fromStatus: "completed",
            toStatus: "completed",
            reason: ocrCorrectionReason,
            actor: "admin",
            taskVersion: 5,
            documentVersion: 1,
            fromExtractedTextLength: ocrExtractedText.length,
            toExtractedTextLength: ocrCorrectedText.length,
            fromExtractedTextHash: expect.any(String),
            toExtractedTextHash: expect.any(String),
          },
          {
            action: "completed",
            fromStatus: "running",
            toStatus: "completed",
            reason: ocrCompletionReason,
            actor: "admin",
            taskVersion: 4,
          },
          {
            action: "retried",
            fromStatus: "failed",
            toStatus: "running",
            reason: ocrRetryReason,
            actor: "admin",
            taskVersion: 3,
          },
          {
            action: "failed",
            fromStatus: "running",
            toStatus: "failed",
            reason: ocrFailureReason,
            actor: "admin",
            taskVersion: 2,
          },
          {
            action: "started",
            fromStatus: "queued",
            toStatus: "running",
            reason: ocrStartReason,
            actor: "admin",
            taskVersion: 1,
          },
        ],
      });

      const ocrSourceAfterResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/${ocrDocumentId}/content`,
        { headers },
      );
      expect(ocrSourceAfterResponse.ok()).toBeTruthy();
      const ocrSourceAfter = (await ocrSourceAfterResponse.json()) as {
        uploadedAt: string;
        hash?: string | null;
        contentBase64?: string | null;
        ocrStatus: string;
      };
      expect(ocrSourceAfter).toMatchObject({
        uploadedAt: ocrSourceBefore.uploadedAt,
        hash: ocrSourceBefore.hash,
        contentBase64: ocrSourceBefore.contentBase64,
        ocrStatus: "OCR complete",
      });

      const activeOcrQueueResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/ocr-queue?status=active&limit=1`,
        { headers },
      );
      expect(activeOcrQueueResponse.ok()).toBeTruthy();
      const activeOcrQueue = (await activeOcrQueueResponse.json()) as {
        count: number;
        counts: { active: number };
      };
      expect(activeOcrQueue.count).toBe(activeOcrQueue.counts.active);
      await page.goto("/clinician/dashboard");
      const dashboardOcrQueue = page
        .locator('a[href="/clinician/document-ocr"]')
        .filter({ hasText: "OCR items active" });
      await expect(dashboardOcrQueue.locator(".dash-stat-value")).toHaveText(
        String(activeOcrQueue.counts.active),
        { timeout: 20_000 },
      );

      const documents = await getMarkerDocuments();
      expect(documents).toHaveLength(6);
      expect(documents).toEqual(
        expect.arrayContaining([
          expect.objectContaining({
            name: refiledNoteName,
            categoryId: 2,
            docDate: "2026-07-27",
            encounter: 1000011,
            mimetype: "text/plain",
            notes: `Refiled proof ${marker}`,
          }),
          expect.objectContaining({
            name: fileName,
            mimetype: "application/pdf",
            storageMethod: "database",
            previewKind: "pdf",
            canPreviewInline: true,
          }),
          expect.objectContaining({
            name: imageName,
            mimetype: "image/png",
            storageMethod: "database",
            previewKind: "image",
            canPreviewInline: true,
          }),
          expect.objectContaining({
            name: unsupportedName,
            mimetype: "application/zip",
            storageMethod: "database",
            previewKind: "binary",
            canPreviewInline: false,
            canDownload: true,
          }),
          expect.objectContaining({
            name: linkName,
            categoryName: "Advance Directive",
            storageMethod: "web_url",
            url: `https://example.test/${marker}`,
          }),
          expect.objectContaining({
            name: ocrName,
            mimetype: "application/pdf",
            isScannedAttachment: true,
            captureSource: "chart scanner",
            scanPageCount: 3,
            ocrStatus: "OCR complete",
          }),
        ]),
      );

      const binaryDocument = documents.find(
        (document) => document.name === fileName,
      );
      expect(binaryDocument).toBeTruthy();
      const contentResponse = await page.request.get(
        `${apiBaseUrl}/api/documents/${binaryDocument!.id}/content`,
        { headers },
      );
      expect(contentResponse.ok()).toBeTruthy();
      await expect(contentResponse.json()).resolves.toMatchObject({
        name: fileName,
        mimetype: "application/pdf",
        isBinary: true,
      });
    } finally {
      const fixtures = await getMarkerDocuments();
      for (const fixture of fixtures) {
        const deleted = await page.request.delete(
          `${apiBaseUrl}/api/documents/${fixture.id}`,
          { headers },
        );
        expect([204, 404]).toContain(deleted.status());
      }
      await expect.poll(async () => (await getMarkerDocuments()).length).toBe(0);
      expect(
        runProviderAssignmentSql(
          `select
            (select count(*) from patient_document_metadata_events where reason like '%${marker}%')
            + (select count(*) from patient_document_content_events where reason like '%${marker}%')
            + (select count(*) from patient_document_review_events where reason like '%${marker}%')
            + (select count(*) from patient_document_archive_events where reason like '%${marker}%')
            + (select count(*) from patient_document_routing_tasks where routing_reason like '%${marker}%')
            + (select count(*) from patient_document_routing_events where reason like '%${marker}%')
            + (select count(*) from patient_document_ocr_tasks where failure_reason like '%${marker}%')
            + (select count(*) from patient_document_ocr_events where reason like '%${marker}%')
            + (select count(*) from patient_document_versions where file_name like '%${marker}%');`,
        ),
      ).toBe("0");
    }
  });

  test("administrators can page, render, version, attach, and audit document templates", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const marker = `TMP-DOC-TEMPLATE-${Date.now()}`;
    const primaryName = `${marker}-00`;
    const binaryFileName = `${marker}-version.txt`;
    const binaryContent = `Binary patient-template proof ${marker}.`;
    const headers = { "X-Legacy EHR-Session": sessionId };
    const templateIds = new Set<string>();

    async function getMarkerTemplates() {
      const response = await page.request.get(
        `${apiBaseUrl}/api/administration/document-templates/?search=${encodeURIComponent(
          marker,
        )}&includeInactive=true&offset=0&limit=100`,
        { headers },
      );
      expect(response.ok()).toBeTruthy();
      return (
        (await response.json()) as {
          total: number;
          items: Array<{ id: string; name: string }>;
        }
      ).items;
    }

    async function getMarkerDocuments() {
      const response = await page.request.get(
        `${apiBaseUrl}/api/documents/MOD-PAT-0001?includeArchived=true`,
        { headers },
      );
      expect(response.ok()).toBeTruthy();
      return (
        (await response.json()) as {
          documents: Array<{ id: number; name: string }>;
        }
      ).documents.filter((document) => document.name.includes(marker));
    }

    try {
      await page.goto("/clinician/document-templates");
      await expect(
        page.getByRole("heading", { name: "Document Templates" }),
      ).toBeVisible();
      await page.getByLabel("Template name *").fill(primaryName);
      await page
        .getByLabel("Text content *")
        .fill(
          `Care plan for ***NAME*** (DOB ***DOB***, chart ***PATIENT_ID***).\n\nBrowser proof ${marker}.`,
        );
      await page.getByRole("button", { name: "Save template" }).click();
      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "Document template created." }),
      ).toBeVisible();
      await expect(
        page.getByRole("heading", { name: `Edit ${primaryName}` }),
      ).toBeVisible();

      const primary = (await getMarkerTemplates()).find(
        (template) => template.name === primaryName,
      );
      expect(primary).toBeTruthy();
      templateIds.add(primary!.id);

      for (let index = 1; index < 9; index += 1) {
        const response = await page.request.post(
          `${apiBaseUrl}/api/administration/document-templates/`,
          {
            headers,
            data: {
              name: `${marker}-${String(index).padStart(2, "0")}`,
              content: `Paged template ${index} for ***NAME***.`,
              active: true,
            },
          },
        );
        expect(response.status()).toBe(201);
        templateIds.add(((await response.json()) as { id: string }).id);
      }

      const library = page
        .getByRole("heading", { name: "Template library" })
        .locator("xpath=ancestor::section");
      await library.getByLabel("Search templates").fill(marker);
      await library.getByRole("button", { name: "Apply" }).click();
      await expect(library.getByText("Page 1 of 2 · 9 results")).toBeVisible({
        timeout: 20_000,
      });
      await expect(
        library.getByRole("button", { name: new RegExp(primaryName) }),
      ).toBeVisible();
      await library.getByRole("button", { name: "Next" }).click();
      await expect(library.getByText("Page 2 of 2 · 9 results")).toBeVisible();
      await expect(
        library.getByRole("button", {
          name: new RegExp(`${marker}-08`),
        }),
      ).toBeVisible();
      await library.getByRole("button", { name: "Previous" }).click();
      await library
        .getByRole("button", { name: new RegExp(primaryName) })
        .click();

      const activeCheckbox = page.getByLabel(
        "Active for preview and patient attachment",
      );
      await activeCheckbox.uncheck();
      await page.getByRole("button", { name: "Save template" }).click();
      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "Document template updated." })
          .last(),
      ).toBeVisible();
      await expect(
        page.getByRole("button", { name: "Render text preview" }),
      ).toBeDisabled();
      await activeCheckbox.check();
      await page.getByRole("button", { name: "Save template" }).click();
      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "Document template updated." })
          .last(),
      ).toBeVisible();

      const output = page
        .getByRole("heading", {
          name: "Preview and patient attachment",
        })
        .locator("xpath=ancestor::section");
      await output.getByLabel("Find patient *").fill("MOD-PAT-0001");
      await output.getByRole("button", { name: "Search" }).click();
      const patientResult = output.getByRole("button", {
        name: /Stone, Avery.*MOD-PAT-0001/,
      });
      await expect(patientResult).toBeVisible({ timeout: 15_000 });
      await patientResult.click();
      await expect(output.getByText("Stone, Avery")).toBeVisible();
      await output.getByLabel("Filing category *").selectOption("3");
      await output.getByLabel("Document date *").fill("2026-07-28");
      await output
        .getByRole("button", { name: "Render text preview" })
        .click();
      await expect(output.getByText(/Care plan for Avery Stone/)).toBeVisible();

      await output
        .getByRole("button", { name: "Attach rendered text" })
        .click();
      await expect(output.getByText(/Patient document \d+ was created/)).toBeVisible(
        { timeout: 20_000 },
      );
      await expect(
        output.getByRole("link", { name: "Open patient documents" }),
      ).toHaveAttribute(
        "href",
        "/clinician/patients/MOD-PAT-0001/documents",
      );

      const versions = page
        .getByRole("heading", { name: "Binary versions" })
        .locator("xpath=ancestor::section");
      await versions.locator('input[type="file"]').setInputFiles({
        name: binaryFileName,
        mimeType: "text/plain",
        buffer: Buffer.from(binaryContent),
      });
      const versionRow = versions.getByRole("row").filter({
        hasText: binaryFileName,
      });
      await expect(versionRow).toContainText("v1", { timeout: 20_000 });
      await versionRow.getByRole("button", { name: "Preview" }).click();
      await expect(
        output.getByText(`Binary patient-template proof ${marker}.`),
      ).toBeVisible();

      const downloadPromise = page.waitForEvent("download");
      await versionRow.getByRole("button", { name: "Download" }).click();
      const download = await downloadPromise;
      expect(download.suggestedFilename()).toBe(binaryFileName);

      await versionRow.getByRole("button", { name: "Attach" }).click();
      await expect(output.getByText(/Patient document \d+ was created/)).toBeVisible(
        { timeout: 20_000 },
      );

      const historySection = page
        .getByRole("heading", { name: "Audit history" })
        .locator("xpath=ancestor::section");
      await expect(historySection.getByText("6 of 6 events")).toBeVisible({
        timeout: 20_000,
      });
      await expect(
        historySection.getByText("patient attachment generated").first(),
      ).toBeVisible();
      await expect(historySection.getByText(/Actor: admin/).first()).toBeVisible();
      await expect(historySection.getByText(/Patient: MOD-PAT-0001/).first()).toBeVisible();

      const historyResponse = await page.request.get(
        `${apiBaseUrl}/api/administration/document-templates/${primary!.id}/history`,
        { headers },
      );
      expect(historyResponse.ok()).toBeTruthy();
      const historyBody = (await historyResponse.json()) as {
        eventCount: number;
        returnedCount: number;
        resultLimit: number;
        events: Array<{
          action: string;
          username: string;
          patientId?: string | null;
        }>;
      };
      expect(historyBody).toMatchObject({
        eventCount: 6,
        returnedCount: 6,
        resultLimit: 100,
      });
      expect(historyBody.events[0]).toMatchObject({
        action: "patient-attachment-generated",
        username: "admin",
        patientId: "MOD-PAT-0001",
      });
      expect(historyBody.events.map((event) => event.action)).toEqual([
        "patient-attachment-generated",
        "binary-version-uploaded",
        "patient-attachment-generated",
        "activated",
        "retired",
        "created",
      ]);
      expect(await getMarkerDocuments()).toHaveLength(2);
    } finally {
      for (const document of await getMarkerDocuments()) {
        const deleted = await page.request.delete(
          `${apiBaseUrl}/api/documents/${document.id}`,
          { headers },
        );
        expect([204, 404]).toContain(deleted.status());
      }
      for (const template of await getMarkerTemplates()) {
        templateIds.add(template.id);
      }
      for (const templateId of templateIds) {
        const deleted = await page.request.delete(
          `${apiBaseUrl}/api/administration/document-templates/${templateId}/test-fixture`,
          { headers },
        );
        expect([204, 404]).toContain(deleted.status());
      }
      await expect.poll(async () => (await getMarkerTemplates()).length).toBe(0);
      await expect.poll(async () => (await getMarkerDocuments()).length).toBe(0);
      expect(
        runProviderAssignmentSql(
          `select
            (select count(*) from document_templates where name like '${marker}%')
            + (select count(*) from document_template_events where summary like '%${marker}%')
            + (select count(*) from document_template_binary_versions where file_name like '${marker}%')
            + (select count(*) from patient_documents where name like '%${marker}%');`,
        ),
      ).toBe("0");
    }
  });

  test("staff can operate the global refill lifecycle, reject stale edits, route locally, and expose patient-visible outcomes", async ({
    page,
    context,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    const prescriptionNote = `Temporary catalog prescription ${Date.now()}`;
    const requestNote = `Temporary refill request ${Date.now()}`;
    const deniedRequestNote = `Temporary denied refill ${Date.now()}`;
    const clarificationResponse = "Please confirm the preferred pharmacy.";
    const approvalResponse = "Browser-verified approval";
    const completionResponse =
      "Local staff review is complete; contact the pharmacy for dispensing status.";
    const denialResponse =
      "A follow-up visit is required before another refill can be authorized.";
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
      await expect(page.locator(".rx-scan-evidence")).toContainText(
        "Protected global refill queue",
      );
      await requestCard
        .getByRole("button", { name: "Request clarification" })
        .click();
      await requestCard
        .getByLabel("Patient-visible clarification question")
        .fill(clarificationResponse);
      await requestCard
        .getByRole("button", { name: "Request clarification" })
        .click();
      await expect(requestCard).toContainText("clarification-requested", {
        timeout: 30_000,
      });
      await expect(requestCard).toContainText(clarificationResponse);
      await requestCard
        .getByRole("button", { name: "Review and approve" })
        .click();
      await requestCard.getByLabel("Additional refills").fill("2");
      await requestCard
        .getByLabel("Patient-visible approval response")
        .fill(approvalResponse);
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

      const currentClinicalLists = await page.request.get(
        `${apiBaseUrl}/api/clinical-lists/MOD-PAT-0004`,
        {
          headers: { "X-Legacy EHR-Session": sessionId },
        },
      );
      expect(currentClinicalLists.ok()).toBeTruthy();
      const currentPrescription = (
        (await currentClinicalLists.json()) as {
          prescriptions?: Array<{
            id: string;
            version: string;
            startDate?: string | null;
            dosage?: string | null;
            quantity?: string | null;
            doseAmount?: number | null;
            doseUnit?: string | null;
            frequency?: string | null;
            durationDays?: number | null;
            route?: string | null;
            refills: number;
            diagnosis?: string | null;
            note?: string | null;
          }>;
        }
      ).prescriptions?.find((item) => item.id === prescriptionId);
      expect(currentPrescription?.version).toBeTruthy();

      await prescriptionCard
        .getByRole("button", { name: "Edit prescription" })
        .click();
      const competingUpdate = await page.request.put(
        `${apiBaseUrl}/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId!)}`,
        {
          headers: { "X-Legacy EHR-Session": sessionId },
          data: {
            expectedVersion: currentPrescription!.version,
            startDate: currentPrescription!.startDate,
            dosage: currentPrescription!.dosage,
            quantity: "31",
            doseAmount: currentPrescription!.doseAmount,
            doseUnit: currentPrescription!.doseUnit,
            frequency: currentPrescription!.frequency,
            durationDays: currentPrescription!.durationDays,
            route: currentPrescription!.route,
            refills: currentPrescription!.refills,
            diagnosis: currentPrescription!.diagnosis,
            note: currentPrescription!.note,
            editReason: "Competing browser-test edit",
          },
        },
      );
      expect(competingUpdate.ok()).toBeTruthy();
      await prescriptionCard.getByLabel("Quantity").fill("32");
      await prescriptionCard
        .getByLabel("Edit reason")
        .fill("This stale edit must be rejected");
      await prescriptionCard
        .getByRole("button", { name: "Save prescription" })
        .click();
      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "changed in another session" }),
      ).toBeVisible();
      await expect(prescriptionCard).toContainText("Qty 31");

      await prescriptionCard
        .getByRole("button", { name: "Edit prescription" })
        .click();
      await prescriptionCard.getByLabel("Quantity").fill("30");
      await prescriptionCard
        .getByLabel("Edit reason")
        .fill("Browser-verified prescription edit");
      await prescriptionCard
        .getByRole("button", { name: "Save prescription" })
        .click();
      await expect(
        page.getByRole("status").filter({ hasText: "updated." }),
      ).toBeVisible();
      await expect(prescriptionCard).toContainText("Qty 30");

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
        approvalResponse,
      );
      await expect(prescriptionCard).toContainText(
        "Browser-verified prescription edit",
      );

      await prescriptionCard
        .getByRole("button", { name: "Record pharmacy" })
        .click();
      const pharmacySelect =
        prescriptionCard.getByLabel("Local pharmacy");
      const northstarPharmacyId = await pharmacySelect
        .locator("option")
        .filter({ hasText: "Northstar Community Pharmacy" })
        .getAttribute("value");
      expect(northstarPharmacyId).toBeTruthy();
      await pharmacySelect.selectOption(northstarPharmacyId!);
      await prescriptionCard
        .getByLabel("Routing note")
        .fill("Browser-verified local route");
      await prescriptionCard
        .getByRole("button", { name: "Record local route" })
        .click();
      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "No external transmission occurred." }),
      ).toBeVisible();
      await expect(prescriptionCard).toContainText(
        "Local route evidence: Northstar Community Pharmacy",
      );
      await prescriptionCard.getByRole("button", { name: /History/ }).click();
      await expect(prescriptionCard).toContainText("Route Pharmacy");
      await expect(prescriptionCard).toContainText(
        "Browser-verified local route",
      );

      await page
        .getByRole("button", { name: /^Portal requests/ })
        .click();
      await page
        .getByRole("button", { name: /^Approved \(/ })
        .click();
      const approvedRequestCard = page
        .locator("article.rx-renew-item")
        .filter({ hasText: requestNote });
      await expect(approvedRequestCard).toBeVisible({ timeout: 30_000 });
      await expect(approvedRequestCard).toContainText(approvalResponse);
      await approvedRequestCard
        .getByRole("button", { name: "Mark completed" })
        .click();
      await approvedRequestCard
        .getByLabel("Patient-visible completion note")
        .fill(completionResponse);
      await approvedRequestCard
        .getByRole("button", { name: "Mark locally completed" })
        .click();
      await expect(approvedRequestCard).toHaveCount(0, {
        timeout: 30_000,
      });
      await page
        .getByRole("button", { name: /^Completed \(/ })
        .click();
      const completedRequestCard = page
        .locator("article.rx-renew-item")
        .filter({ hasText: requestNote });
      await expect(completedRequestCard).toBeVisible({
        timeout: 30_000,
      });
      await expect(completedRequestCard).toContainText("completed");
      await expect(completedRequestCard).toContainText(completionResponse);

      const deniedRequest = await page.request.post(
        `${apiBaseUrl}/api/patient-portal/prescriptions/${encodeURIComponent(prescriptionId!)}/refill-request`,
        {
          headers: {
            "X-Legacy EHR-Patient-Portal-Session": portalSessionId!,
          },
          data: {
            requestDate: new Date().toISOString().slice(0, 10),
            note: deniedRequestNote,
          },
        },
      );
      expect(deniedRequest.ok()).toBeTruthy();
      const deniedRequestResult = (await deniedRequest.json()) as {
        sentMessage?: { id?: string };
        recipientMessage?: { id?: string };
      };
      for (const id of [
        deniedRequestResult.sentMessage?.id,
        deniedRequestResult.recipientMessage?.id,
      ]) {
        if (id) messageIds.push(id);
      }

      await page
        .getByRole("button", { name: /^Open \(/ })
        .click();
      const deniedRequestCard = page
        .locator("article.rx-renew-item")
        .filter({ hasText: deniedRequestNote });
      await expect(deniedRequestCard).toBeVisible({ timeout: 30_000 });
      await deniedRequestCard.getByRole("button", { name: "Deny" }).click();
      await deniedRequestCard
        .getByLabel("Patient-visible denial reason")
        .fill(denialResponse);
      await deniedRequestCard
        .getByRole("button", { name: "Deny request" })
        .click();
      await expect(deniedRequestCard).toHaveCount(0, { timeout: 30_000 });
      await page
        .getByRole("button", { name: /^Denied \(/ })
        .click();
      const deniedHistoryCard = page
        .locator("article.rx-renew-item")
        .filter({ hasText: deniedRequestNote });
      await expect(deniedHistoryCard).toBeVisible({ timeout: 30_000 });
      await expect(deniedHistoryCard).toContainText("denied");
      await expect(deniedHistoryCard).toContainText(denialResponse);

      const portalPage = await context.newPage();
      await signInPortal(portalPage);
      await portalPage.goto("/portal/records");
      await portalPage
        .getByRole("button", { name: "Health summary" })
        .click();
      const completedRefillHistory = portalPage
        .locator(".refill-history-list li")
        .filter({ hasText: requestNote });
      await expect(completedRefillHistory).toBeVisible({
        timeout: 30_000,
      });
      await expect(completedRefillHistory).toContainText(
        createdPrescription?.drug ?? "Metformin",
      );
      await expect(completedRefillHistory).toContainText("Completed");
      await expect(completedRefillHistory).toContainText(completionResponse);
      await expect(completedRefillHistory).toContainText("Care team");
      const deniedRefillHistory = portalPage
        .locator(".refill-history-list li")
        .filter({ hasText: deniedRequestNote });
      await expect(deniedRefillHistory).toBeVisible({ timeout: 30_000 });
      await expect(deniedRefillHistory).toContainText("Denied");
      await expect(deniedRefillHistory).toContainText(denialResponse);
      await expect(portalPage.locator(".refill-history .hint-banner")).toContainText(
        "does not confirm that a pharmacy dispensed or delivered medication",
      );
      await portalPage.close();
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
