// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from "@axe-core/playwright";
import type { APIRequestContext, Page } from "@playwright/test";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { expect, test } from "./support/fixtures.ts";

const apiBaseUrl =
  process.env.MODERN_UI_API_BASE_URL ?? "http://127.0.0.1:5001";
const codingPatientId =
  process.env.MODERN_UI_CODING_PATIENT_ID ?? "MOD-PAT-0001";
const codingEncounter = Number(
  process.env.MODERN_UI_CODING_ENCOUNTER ?? "1000013",
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

function cleanupEncounter(encounter: number | null) {
  if (!encounter || !Number.isInteger(encounter) || encounter < 1) return;
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
      "avenchart",
      "-d",
      "avenchart",
      "-v",
      "ON_ERROR_STOP=1",
      "-c",
      `delete from encounters where encounter = ${encounter};`,
    ],
    {
      cwd: fileURLToPath(new URL("../../avenchart/", import.meta.url)),
      stdio: "pipe",
    },
  );
}

async function cleanupCodingFixtures(
  request: APIRequestContext,
  sessionId: string,
  billingLineIds: string[],
  procedureOrderIds: number[],
) {
  const headers = { "X-AvenChart-Session": sessionId };
  for (const billingLineId of billingLineIds) {
    const response = await request.delete(
      `${apiBaseUrl}/api/billing/lines/${billingLineId}`,
      { headers },
    );
    expect(response.ok()).toBe(true);
  }
  for (const orderId of procedureOrderIds) {
    const response = await request.delete(
      `${apiBaseUrl}/api/procedures/orders/${orderId}`,
      { headers },
    );
    expect(response.ok()).toBe(true);
  }
}

test("standalone encounter creation captures full visit metadata", async ({
  page,
}, testInfo) => {
  test.skip(
    testInfo.project.name !== "desktop-chromium",
    "The mutation is run once; the shared coding workspace is verified in every browser.",
  );

  const marker = `MUC-07-01 create ${Date.now()}`;
  let encounter: number | null = null;

  try {
    await signInClinician(page);
    await page.goto("/clinician/encounters/new");

    await page.getByPlaceholder("Search by name or ID…").fill("MOD-PAT-0004");
    const patientResult = page.locator(".ne-patient-result").first();
    await expect(patientResult).toBeVisible({ timeout: 20_000 });
    await patientResult.click();

    await page.getByLabel("Chief complaint / reason for visit").fill(marker);
    const provider = page.getByLabel("Provider");
    await expect(provider.locator("option")).not.toHaveCount(1, {
      timeout: 20_000,
    });
    await provider.selectOption({ index: 1 });
    await page.getByLabel("Billing facility").selectOption({ index: 1 });
    await page.getByLabel("Place of service").fill("11");
    await page.getByLabel("Sensitivity").fill("standard");
    await page.getByLabel("Referral source").fill(`${marker} referral`);
    await page.getByLabel("External reference").fill(`${marker} external`);
    await page.getByLabel("Billing note").fill(`${marker} billing`);

    const createResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        response.url() === `${apiBaseUrl}/api/encounters`,
    );
    await page
      .getByRole("button", { name: "Create encounter & continue" })
      .click();
    const createResponse = await createResponsePromise;
    expect(createResponse.ok()).toBe(true);
    const created = (await createResponse.json()) as {
      encounter: number;
      reason: string;
      providerName?: string | null;
      facilityName?: string | null;
      sensitivity?: string | null;
      referralSource?: string | null;
      externalId?: string | null;
      posCode?: number | null;
      billingNote?: string | null;
    };
    encounter = created.encounter;
    expect(created).toMatchObject({
      reason: marker,
      sensitivity: "standard",
      referralSource: `${marker} referral`,
      externalId: `${marker} external`,
      posCode: 11,
      billingNote: `${marker} billing`,
    });
    expect(created.providerName).toBeTruthy();
    expect(created.facilityName).toBeTruthy();

    await page.getByRole("button", { name: "Skip vitals" }).click();
    await page.getByRole("button", { name: "Skip note" }).click();
    await expect(
      page.getByRole("heading", { name: "Encounter created" }),
    ).toBeVisible();
    await expect(page.getByText(`Encounter #${encounter}`)).toBeVisible();
  } finally {
    cleanupEncounter(encounter);
  }
});

test("encounter workspace links diagnosis, charge, and procedure evidence", async ({
  page,
  request,
}, testInfo) => {
  test.skip(
    !Number.isInteger(codingEncounter),
    "A valid encounter number is required.",
  );
  const projectNumber = [
    "desktop-chromium",
    "mobile-chromium",
    "desktop-firefox",
    "desktop-webkit",
  ].indexOf(testInfo.project.name);
  const suffix = `${Math.max(projectNumber, 0)}${Date.now().toString().slice(-5)}`;
  const diagnosisCode = `Z71.${suffix.slice(-2)}`;
  const chargeCode = `9${suffix.slice(-4)}`;
  const marker = `MUC-07-01 ${testInfo.project.name} ${Date.now()}`;
  const billingLineIds: string[] = [];
  const procedureOrderIds: number[] = [];
  const apiSession = await createApiSession(request);

  try {
    await signInClinician(page);
    await page.goto(`/clinician/patients/${codingPatientId}/encounters`);
    const encounterRow = page.locator(`[data-encounter="${codingEncounter}"]`);
    await expect(encounterRow).toBeVisible({ timeout: 20_000 });
    await encounterRow.click();

    const workspace = page.locator(
      `[aria-labelledby="encounter-coding-title-${codingEncounter}"]`,
    );
    await expect(workspace).toBeVisible({ timeout: 20_000 });
    await expect(workspace).toContainText(`encounter #${codingEncounter}`);

    await workspace.getByRole("button", { name: "Add diagnosis" }).click();
    await workspace.getByLabel("ICD-10 diagnosis code").fill(diagnosisCode);
    await workspace.getByLabel("Description").fill(`${marker} diagnosis`);
    const diagnosisResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        response.url() === `${apiBaseUrl}/api/billing/lines`,
    );
    await workspace.getByRole("button", { name: "Link to encounter" }).click();
    const diagnosisResponse = await diagnosisResponsePromise;
    expect(diagnosisResponse.ok()).toBe(true);
    billingLineIds.push(
      ((await diagnosisResponse.json()) as { id: string }).id,
    );
    await expect(workspace).toContainText(`${marker} diagnosis`, {
      timeout: 20_000,
    });

    await workspace.getByRole("button", { name: "Add charge" }).click();
    await workspace.getByLabel("Billing code").fill(chargeCode);
    await workspace.getByLabel("Description").fill(`${marker} charge`);
    await workspace.getByLabel("Fee").fill("125.50");
    await workspace.getByLabel("Units").fill("2");
    await workspace.getByLabel("Supporting diagnosis").fill(diagnosisCode);
    const chargeResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        response.url() === `${apiBaseUrl}/api/billing/lines`,
    );
    await workspace.getByRole("button", { name: "Link to encounter" }).click();
    const chargeResponse = await chargeResponsePromise;
    expect(chargeResponse.ok()).toBe(true);
    billingLineIds.push(((await chargeResponse.json()) as { id: string }).id);
    await expect(workspace).toContainText(`${marker} charge`, {
      timeout: 20_000,
    });
    await expect(workspace).toContainText("$251.00");

    await workspace
      .getByRole("button", { name: "Add procedure order" })
      .click();
    await workspace.getByLabel("Catalog procedure").selectOption({ index: 1 });
    await workspace.getByLabel("Priority").selectOption("urgent");
    await workspace.getByLabel("Supporting diagnosis").fill(diagnosisCode);
    await workspace
      .getByLabel("Clinical instructions")
      .fill(`${marker} instructions`);
    const orderResponsePromise = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" &&
        response.url() === `${apiBaseUrl}/api/procedures/orders`,
    );
    await workspace
      .getByRole("button", { name: "Create linked order" })
      .click();
    const orderResponse = await orderResponsePromise;
    expect(orderResponse.ok()).toBe(true);
    procedureOrderIds.push(((await orderResponse.json()) as { id: number }).id);

    await expect(workspace).toContainText("urgent", { timeout: 20_000 });
    await expect(workspace).toContainText("2 billing links");
    await expect(workspace).toContainText("1 procedure link");

    const accessibility = await new AxeBuilder({ page })
      .include(`[aria-labelledby="encounter-coding-title-${codingEncounter}"]`)
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();
    expect(
      accessibility.violations.filter((violation) =>
        ["serious", "critical"].includes(violation.impact ?? ""),
      ),
    ).toEqual([]);
  } finally {
    await cleanupCodingFixtures(
      request,
      apiSession.sessionId,
      billingLineIds,
      procedureOrderIds,
    );
  }
});
