// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from "@axe-core/playwright";
import type { APIRequestContext, Page } from "@playwright/test";
import { expect, test } from "./support/fixtures.ts";

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
  await page.getByLabel("Password").press("Enter");
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

async function openSoapCard(page: Page, patientId: string, encounter: number) {
  await page.goto(`/clinician/patients/${patientId}/encounters`);
  const row = page.locator(`[data-encounter="${encounter}"]`);
  await expect(row).toBeVisible({ timeout: 20_000 });
  await row.click();
  await expect(
    page.locator('[aria-labelledby="encounter-soap-note-title"]'),
  ).toBeVisible({ timeout: 20_000 });
}

function currentVersionFrom(text: string | null) {
  const match = text?.match(/Saved version\s+(\d+)/);
  if (!match) throw new Error(`Could not parse SOAP version from '${text}'.`);
  return Number(match[1]);
}

test("SOAP draft exposes and resolves an optimistic save conflict", async ({
  page,
  request,
}, testInfo) => {
  test.skip(
    process.env.MODERN_UI_SOAP_LOCKED_MODE === "1",
    "Conflict proof runs before the locking-signature proof.",
  );
  const encounter = Number(process.env.MODERN_UI_SOAP_ENCOUNTER);
  const patientId = process.env.MODERN_UI_SOAP_PATIENT_ID;
  const marker = process.env.MODERN_UI_SOAP_MARKER;
  test.skip(
    !Number.isInteger(encounter) || !patientId || !marker,
    "The cleanup-backed SOAP fixture was not supplied.",
  );

  await signInClinician(page);
  await openSoapCard(page, patientId!, encounter);

  const soapCard = page.locator(
    '[aria-labelledby="encounter-soap-note-title"]',
  );
  const versionBadge = soapCard.getByText(/Saved version \d+/);
  const loadedVersion = currentVersionFrom(await versionBadge.textContent());
  await soapCard.getByRole("button", { name: "Edit SOAP note draft" }).click();
  const browserDraft = `${marker} browser draft ${testInfo.project.name}`;
  await soapCard.getByLabel("Subjective").fill(browserDraft);
  await expect(soapCard.getByText("Unsaved draft")).toBeVisible();
  await expect(soapCard).toContainText(
    `Based on saved SOAP version ${loadedVersion}`,
  );

  const apiSession = await createApiSession(request);
  const headers = { "X-Legacy EHR-Session": apiSession.sessionId };
  const currentResponse = await request.get(
    `${apiBaseUrl}/api/encounters/${encounter}?includeArchivedDocuments=true`,
    { headers },
  );
  expect(currentResponse.ok()).toBe(true);
  const current = (await currentResponse.json()) as {
    soapNote: {
      version: number;
      subjective?: string | null;
      objective?: string | null;
      assessment?: string | null;
      plan?: string | null;
    };
  };
  expect(current.soapNote.version).toBe(loadedVersion);
  const concurrentMarker = `${marker} concurrent ${testInfo.project.name}`;
  const concurrentResponse = await request.post(
    `${apiBaseUrl}/api/encounters/${encounter}/soap-notes`,
    {
      headers,
      data: {
        dateTime: new Date().toISOString().replace("T", " ").slice(0, 19),
        expectedVersion: loadedVersion,
        subjective: current.soapNote.subjective,
        objective: concurrentMarker,
        assessment: current.soapNote.assessment,
        plan: current.soapNote.plan,
      },
    },
  );
  expect(concurrentResponse.status()).toBe(201);

  await soapCard.getByRole("button", { name: "Save new version" }).click();
  await expect(
    soapCard.getByText("A newer SOAP version was saved"),
  ).toBeVisible();
  await expect(soapCard).toContainText(concurrentMarker);
  await expect(soapCard.getByLabel("Subjective")).toHaveValue(browserDraft);
  await soapCard
    .getByRole("button", { name: "Keep draft after review" })
    .click();
  await soapCard.getByRole("button", { name: "Save new version" }).click();

  await expect(
    soapCard.getByText(`Saved version ${loadedVersion + 2}`),
  ).toBeVisible({ timeout: 20_000 });
  await expect(soapCard).toContainText(browserDraft);
  await soapCard.getByText(/SOAP version history \(\d+\)/).click();
  await expect(soapCard).toContainText(concurrentMarker);

  const accessibility = await new AxeBuilder({ page })
    .include('[aria-labelledby="encounter-soap-note-title"]')
    .withTags(["wcag2a", "wcag2aa"])
    .analyze();
  expect(
    accessibility.violations.filter((violation) =>
      ["serious", "critical"].includes(violation.impact ?? ""),
    ),
  ).toEqual([]);
});

test("locking signature makes the SOAP editor visibly read-only", async ({
  page,
}) => {
  test.skip(
    process.env.MODERN_UI_SOAP_LOCKED_MODE !== "1",
    "Lock proof runs after the API creates its cleanup-owned signature.",
  );
  const encounter = Number(process.env.MODERN_UI_SOAP_ENCOUNTER);
  const patientId = process.env.MODERN_UI_SOAP_PATIENT_ID;
  test.skip(
    !Number.isInteger(encounter) || !patientId,
    "The cleanup-backed SOAP fixture was not supplied.",
  );

  await signInClinician(page);
  await openSoapCard(page, patientId!, encounter);

  const soapCard = page.locator(
    '[aria-labelledby="encounter-soap-note-title"]',
  );
  await expect(
    soapCard.getByText(/locked by an encounter signature/i),
  ).toBeVisible();
  await expect(
    soapCard.getByRole("button", { name: "SOAP note locked" }),
  ).toBeDisabled();

  const accessibility = await new AxeBuilder({ page })
    .include('[aria-labelledby="encounter-soap-note-title"]')
    .withTags(["wcag2a", "wcag2aa"])
    .analyze();
  expect(
    accessibility.violations.filter((violation) =>
      ["serious", "critical"].includes(violation.impact ?? ""),
    ),
  ).toEqual([]);
});
