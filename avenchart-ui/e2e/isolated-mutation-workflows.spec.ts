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
