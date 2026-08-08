// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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

test.describe("SEC-03 disclosure authority", () => {
  test("enforces bounded authority, decision, revocation, history, and cleanup", async ({
    page,
  }) => {
    const marker = `TMP-DISCLOSURE-UI-${Date.now()}`;
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    let authorityId: string | null = null;

    await signIn(page);
    const staffSessionId = await sessionId(page);
    try {
      await page.goto("/clinician/patients/MOD-PAT-0001/summary");
      await expect(
        page.getByRole("heading", {
          name: "Consent, authority, and disclosure decisions",
        }),
      ).toBeVisible({ timeout: 20_000 });
      await expect(
        page.getByText("local-disclosure-authority-v1", { exact: true }),
      ).toBeVisible();
      await expect(
        page.getByText("disabled owner gated", { exact: true }),
      ).toBeVisible();

      await page.getByRole("button", { name: "Record authority" }).click();
      const authorityForm = page.getByRole("form", {
        name: "Record disclosure authority",
      });
      await authorityForm.getByLabel("Authority type").selectOption("proxy");
      await authorityForm.getByLabel("Proxy name").fill("Synthetic Proxy");
      await authorityForm
        .getByLabel("Proxy relationship")
        .fill("guardian");
      await authorityForm.getByLabel("Purpose").fill("care coordination");
      await authorityForm.getByLabel("Exact recipient").fill(marker);
      await authorityForm
        .getByLabel("Verification method")
        .selectOption("documented-authority");
      await authorityForm
        .getByLabel("Verification reference")
        .fill(marker);
      await authorityForm
        .getByRole("checkbox", { name: /Clinical summary/i })
        .check();
      await authorityForm
        .getByRole("checkbox", { name: /Documents/i })
        .check();
      await authorityForm
        .getByLabel("Why this authority is being recorded")
        .fill("Synthetic browser lifecycle proof");
      await authorityForm
        .getByRole("button", { name: "Record pending authority" })
        .click();

      const authorityList = page.getByRole("list", {
        name: "Disclosure authorities",
      });
      const authorityRow = authorityList
        .getByRole("listitem")
        .filter({ hasText: marker });
      await expect(authorityRow).toContainText("pending");
      await authorityRow.getByRole("button", { name: "Activate" }).click();
      const activateForm = page.getByRole("form", {
        name: "activate disclosure authority",
      });
      await activateForm
        .getByLabel("Reason to activate this disclosure authority")
        .fill("Verification evidence reviewed");
      await activateForm
        .getByRole("button", { name: "Confirm activate" })
        .click();
      await expect(authorityRow).toContainText("active");

      const requestForm = page.getByRole("form", {
        name: "Request disclosure decision",
      });
      const option = requestForm.locator("option").filter({ hasText: marker });
      authorityId = await option.getAttribute("value");
      expect(authorityId).toBeTruthy();
      await requestForm
        .getByLabel("Active authority")
        .selectOption(authorityId!);
      await requestForm
        .getByRole("checkbox", { name: /Documents/i })
        .uncheck();
      await requestForm
        .getByLabel("Why this disclosure decision is requested")
        .fill("Release only the bounded clinical summary");
      await requestForm
        .getByRole("button", { name: "Request decision" })
        .click();

      const requestList = page.getByRole("list", {
        name: "Disclosure decision requests",
      });
      const requestRow = requestList
        .getByRole("listitem")
        .filter({ hasText: marker });
      await expect(requestRow).toContainText("requested");
      await requestRow
        .getByRole("button", { name: "Approve decision" })
        .click();
      const approveForm = page.getByRole("form", {
        name: "approve disclosure decision",
      });
      await approveForm
        .getByLabel("Reason for this approve decision")
        .fill("Purpose, recipient, scope, and active authority match");
      await approveForm
        .getByRole("button", { name: "Confirm approve" })
        .click();
      await expect(requestRow).toContainText("approved");
      await expect(requestRow).toContainText("clinical summary");

      await requestRow.getByRole("button", { name: "History" }).click();
      const requestHistory = page.getByRole("region", {
        name: "request disclosure history",
      });
      await expect(requestHistory).toContainText("requested");
      await expect(requestHistory).toContainText("approved");
      await expect(requestHistory).toContainText("authority v1 active");
      await requestHistory
        .getByRole("button", { name: "Close history" })
        .click();

      await authorityRow.getByRole("button", { name: "History" }).click();
      const authorityHistory = page.getByRole("region", {
        name: "authority disclosure history",
      });
      await expect(authorityHistory).toContainText("created");
      await expect(authorityHistory).toContainText("activated");
      await authorityHistory
        .getByRole("button", { name: "Close history" })
        .click();

      await authorityRow.getByRole("button", { name: "Revoke" }).click();
      const revokeForm = page.getByRole("form", {
        name: "revoke disclosure authority",
      });
      await revokeForm
        .getByLabel("Reason to revoke this disclosure authority")
        .fill("Synthetic authority withdrawn");
      await revokeForm
        .getByRole("button", { name: "Confirm revoke" })
        .click();
      await expect(authorityRow).toContainText("revoked");
      await expect(
        requestForm.getByLabel("Active authority").locator(`option[value="${authorityId}"]`),
      ).toHaveCount(0);

      const accessibility = await new AxeBuilder({ page })
        .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
        .analyze();
      expect(
        accessibility.violations.filter(
          ({ impact }) => impact === "serious" || impact === "critical",
        ),
      ).toEqual([]);
    } finally {
      if (authorityId) {
        const cleanup = await page.request.delete(
          `${apiBaseUrl}/api/patients/MOD-PAT-0001/disclosure-authorities/${authorityId}/test-fixture`,
          {
            headers: { "X-AvenChart-Session": staffSessionId },
          },
        );
        expect([204, 404]).toContain(cleanup.status());
      }
    }
  });
});
