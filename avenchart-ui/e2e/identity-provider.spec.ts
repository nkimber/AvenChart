// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
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

test.describe("SEC-02 identity-provider seam", () => {
  test("renders the local adapter boundary without serious accessibility violations", async ({
    page,
  }) => {
    await signIn(page);
    await page.goto("/clinician/admin");
    await page
      .getByRole("button", { name: /^Access control \(/ })
      .click();

    const readiness = page.getByRole("region", {
      name: "Identity-provider readiness",
    });
    await expect(
      readiness.getByRole("heading", {
        name: "Identity-provider readiness",
      }),
    ).toBeVisible({ timeout: 20_000 });
    await expect(
      readiness.getByText("local-identity-adapter-v1", { exact: true }),
    ).toBeVisible();
    await expect(
      readiness.getByText("local adapter active", { exact: true }),
    ).toBeVisible();
    await expect(
      readiness.getByText("disabled owner gated", { exact: true }),
    ).toBeVisible();
    await expect(
      readiness.getByLabel("Identity readiness counts"),
    ).toContainText("7Production blockers");

    const results = await new AxeBuilder({ page })
      .include(".identity-readiness")
      .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
      .analyze();
    expect(
      results.violations.filter(
        (violation) =>
          violation.impact === "serious" ||
          violation.impact === "critical",
      ),
    ).toEqual([]);
  });
});
