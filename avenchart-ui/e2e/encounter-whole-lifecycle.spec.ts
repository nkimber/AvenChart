// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from "@axe-core/playwright";
import type { APIRequestContext, Page } from "@playwright/test";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { expect, test } from "./support/fixtures.ts";

const apiBaseUrl =
  process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
const patientId = process.env.MODERN_UI_LIFECYCLE_PATIENT_ID ?? "MOD-PAT-0004";
const composeRoot = fileURLToPath(
  new URL("../../avenchart/", import.meta.url),
);

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

async function createApiSession(request: APIRequestContext) {
  const response = await request.post(`${apiBaseUrl}/api/auth/login`, {
    data: {
      username: process.env.MODERN_UI_STAFF_USERNAME ?? "admin",
      password: process.env.MODERN_UI_STAFF_PASSWORD ?? "pass",
    },
  });
  expect(response.ok()).toBe(true);
  return (await response.json()) as { sessionId: string };
}

function fixtureSql(sql: string) {
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
      "-Atc",
      sql,
    ],
    { cwd: composeRoot, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
  ).trim();
}

async function cleanupLifecycleFixture(
  request: APIRequestContext,
  sessionId: string,
  marker: string,
  encounter: number | null,
  documentId: number | null,
  billingLineIds: string[],
  procedureOrderIds: number[],
) {
  const headers = { "X-Legacy EHR-Session": sessionId };
  for (const id of billingLineIds) {
    await request.delete(`${apiBaseUrl}/api/billing/lines/${id}`, { headers });
  }
  for (const id of procedureOrderIds) {
    await request.delete(`${apiBaseUrl}/api/procedures/orders/${id}`, {
      headers,
    });
  }
  if (documentId) {
    await request.delete(`${apiBaseUrl}/api/documents/${documentId}`, {
      headers,
    });
  }
  if (!encounter) return;
  if (
    !Number.isInteger(encounter) ||
    !/^TMP-ENC-LIFECYCLE-BROWSER-[A-Za-z0-9-]+$/.test(marker)
  ) {
    throw new Error("Unsafe whole-lifecycle cleanup target.");
  }
  fixtureSql(`
delete from patient_documents
where encounter = ${encounter} and name like '${marker}%';
delete from clinical_notes
where encounter = ${encounter}
  and concat_ws(' ', subjective, objective, assessment, plan) like '${marker}%';
delete from encounter_audit_events where encounter = ${encounter};
delete from encounters
where encounter = ${encounter} and reason = '${marker} complete visit';
`);
  expect(
    fixtureSql(
      `select count(*) from encounters where encounter = ${encounter};`,
    ),
  ).toBe("0");
}

test("create-to-restore encounter package remains authoritative and immutable", async ({
  page,
  request,
}, testInfo) => {
  test.setTimeout(180_000);
  const marker = `TMP-ENC-LIFECYCLE-BROWSER-${Date.now()}-${testInfo.project.name}`;
  const diagnosisCode = `Z71.${String(
    [
      "desktop-chromium",
      "mobile-chromium",
      "desktop-firefox",
      "desktop-webkit",
    ].indexOf(testInfo.project.name) + 1,
  ).padStart(2, "0")}`;
  const chargeCode = `8${Date.now().toString().slice(-4)}`;
  const billingLineIds: string[] = [];
  const procedureOrderIds: number[] = [];
  let encounter: number | null = null;
  let documentId: number | null = null;
  const apiSession = await createApiSession(request);

  try {
    await signInClinician(page);
    await page.goto("/clinician/encounters/new");
    await page.getByLabel("Patient").fill(patientId);
    const patient = page.locator(".ne-patient-result").first();
    await expect(patient).toBeVisible({ timeout: 20_000 });
    await patient.click();
    await page
      .getByLabel("Chief complaint / reason for visit")
      .fill(`${marker} complete visit`);
    await page.getByLabel("Provider").selectOption({ index: 1 });
    await page.getByLabel("Billing facility").selectOption({ index: 1 });
    await page.getByLabel("Place of service").fill("11");

    const createResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        response.url() === `${apiBaseUrl}/api/encounters`,
    );
    await page
      .getByRole("button", { name: "Create encounter & continue" })
      .click();
    const createResponse = await createResponsePromise;
    expect(createResponse.status()).toBe(201);
    encounter = ((await createResponse.json()) as { encounter: number })
      .encounter;

    const vitalInputs = page.locator(".ne-vitals-grid input");
    await vitalInputs.nth(0).fill("118");
    await vitalInputs.nth(1).fill("76");
    await vitalInputs.nth(2).fill("72");
    await page.getByRole("button", { name: "Save vitals & continue" }).click();

    const soapInputs = page.locator(".ne-soap-textarea");
    await soapInputs.nth(0).fill(`${marker} subjective`);
    await soapInputs.nth(1).fill(`${marker} objective`);
    await soapInputs.nth(2).fill(`${marker} assessment`);
    await soapInputs.nth(3).fill(`${marker} plan`);
    await page.getByRole("button", { name: "Save note & finish" }).click();
    await expect(
      page.getByRole("heading", { name: "Encounter created" }),
    ).toBeVisible({ timeout: 20_000 });
    await page.getByRole("button", { name: "View in chart" }).click();

    const encounterRow = page.locator(`[data-encounter="${encounter}"]`);
    await expect(encounterRow).toBeVisible({ timeout: 20_000 });
    await encounterRow.click();
    await expect(
      page.locator('[aria-labelledby="encounter-soap-note-title"]'),
    ).toContainText(`${marker} subjective`, { timeout: 20_000 });

    const coding = page.locator(
      `[aria-labelledby="encounter-coding-title-${encounter}"]`,
    );
    await coding.getByRole("button", { name: "Add diagnosis" }).click();
    await coding.getByLabel("ICD-10 diagnosis code").fill(diagnosisCode);
    await coding.getByLabel("Description").fill(`${marker} diagnosis`);
    const diagnosisResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        response.url() === `${apiBaseUrl}/api/billing/lines`,
    );
    await coding.getByRole("button", { name: "Link to encounter" }).click();
    const diagnosisResponse = await diagnosisResponsePromise;
    billingLineIds.push(
      ((await diagnosisResponse.json()) as { id: string }).id,
    );

    await coding.getByRole("button", { name: "Add charge" }).click();
    await coding.getByLabel("Billing code").fill(chargeCode);
    await coding.getByLabel("Description").fill(`${marker} charge`);
    await coding.getByLabel("Fee").fill("95");
    await coding.getByLabel("Units").fill("1");
    await coding.getByLabel("Supporting diagnosis").fill(diagnosisCode);
    const chargeResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        response.url() === `${apiBaseUrl}/api/billing/lines`,
    );
    await coding.getByRole("button", { name: "Link to encounter" }).click();
    const chargeResponse = await chargeResponsePromise;
    billingLineIds.push(((await chargeResponse.json()) as { id: string }).id);

    await coding.getByRole("button", { name: "Add procedure order" }).click();
    await coding.getByLabel("Catalog procedure").selectOption({ index: 1 });
    await coding.getByLabel("Priority").selectOption("urgent");
    await coding.getByLabel("Supporting diagnosis").fill(diagnosisCode);
    await coding
      .getByLabel("Clinical instructions")
      .fill(`${marker} procedure`);
    const orderResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        response.url() === `${apiBaseUrl}/api/procedures/orders`,
    );
    await coding.getByRole("button", { name: "Create linked order" }).click();
    const orderResponse = await orderResponsePromise;
    procedureOrderIds.push(((await orderResponse.json()) as { id: number }).id);

    const attachments = page.locator(
      '[aria-labelledby="encounter-attachments-title"]',
    );
    await attachments
      .getByRole("button", { name: "Add encounter attachment" })
      .click();
    await attachments.getByRole("button", { name: "Text note" }).click();
    await attachments.getByLabel("Name").fill(`${marker} attachment`);
    await attachments
      .getByLabel("Attachment text")
      .fill(`${marker} protected attachment`);
    await attachments
      .getByLabel("Filing note")
      .fill(`${marker} filing evidence`);
    const documentResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        response.url() === `${apiBaseUrl}/api/documents`,
    );
    await attachments.getByRole("button", { name: "File attachment" }).click();
    const documentResponse = await documentResponsePromise;
    documentId = ((await documentResponse.json()) as { id: number }).id;
    await expect(attachments).toContainText(`${marker} attachment`, {
      timeout: 20_000,
    });

    const signatures = page.locator(
      '[aria-labelledby="encounter-signatures-title"]',
    );
    await signatures.getByRole("button", { name: "Record signature" }).click();
    await signatures.getByLabel("Direct SOAP changes").selectOption("locked");
    page.once("dialog", (dialog) => dialog.accept());
    await signatures
      .getByRole("button", { name: "Record signature", exact: true })
      .last()
      .click();
    await expect(signatures).toContainText("admin", { timeout: 20_000 });
    await expect(signatures).toContainText("Locked");

    await signatures
      .getByRole("button", { name: "Add signed amendment" })
      .click();
    await signatures
      .getByLabel("Correction or amendment")
      .fill(`${marker} signed correction`);
    page.once("dialog", (dialog) => dialog.accept());
    await signatures
      .getByRole("button", { name: "Append signed amendment" })
      .click();
    await expect(signatures).toContainText(`${marker} signed correction`, {
      timeout: 20_000,
    });
    await expect(
      page
        .locator('[aria-labelledby="encounter-soap-note-title"]')
        .getByText(/locked by an encounter signature/i),
    ).toBeVisible();

    await page.getByRole("button", { name: "Archive encounter" }).click();
    await page
      .getByLabel("Archive reason")
      .fill(`${marker} archive complete package`);
    await page
      .locator("form.cl-inline-edit-form")
      .getByRole("button", { name: "Archive encounter" })
      .click();
    await expect(encounterRow).not.toBeVisible({ timeout: 20_000 });

    await page.getByRole("button", { name: "Show archived" }).click();
    const archivedRow = page.locator(`[data-encounter="${encounter}"]`);
    await expect(archivedRow).toBeVisible({ timeout: 20_000 });
    await archivedRow.click();
    await expect(
      page.getByRole("button", { name: "Restore encounter" }),
    ).toBeVisible({ timeout: 20_000 });
    await expect(signatures).toContainText(`${marker} signed correction`);
    await expect(attachments).toContainText(`${marker} attachment`);
    await expect(coding).toContainText(`${marker} charge`);

    await page.getByRole("button", { name: "Restore encounter" }).click();
    await page
      .getByLabel("Restore reason")
      .fill(`${marker} restore complete package`);
    await page
      .locator("form.cl-inline-edit-form")
      .getByRole("button", { name: "Restore encounter" })
      .click();

    const restoredRow = page.locator(`[data-encounter="${encounter}"]`);
    await expect(restoredRow).toBeVisible({ timeout: 20_000 });
    await restoredRow.click();
    await expect(signatures).toContainText(`${marker} signed correction`);
    await expect(attachments).toContainText(`${marker} attachment`);
    await expect(coding).toContainText(`${marker} diagnosis`);

    const accessibility = await new AxeBuilder({ page })
      .include('[aria-labelledby="encounter-signatures-title"]')
      .include('[aria-labelledby="encounter-attachments-title"]')
      .include(`[aria-labelledby="encounter-coding-title-${encounter}"]`)
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();
    expect(
      accessibility.violations.filter((violation) =>
        ["serious", "critical"].includes(violation.impact ?? ""),
      ),
    ).toEqual([]);
  } finally {
    await cleanupLifecycleFixture(
      request,
      apiSession.sessionId,
      marker,
      encounter,
      documentId,
      billingLineIds,
      procedureOrderIds,
    );
  }
});
