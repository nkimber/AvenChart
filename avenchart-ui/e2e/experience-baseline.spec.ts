// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "./support/fixtures.ts";

test.describe("UX-01 experience baseline", () => {
  test("exposes local evidence and keeps owner decisions and analytics gated", async ({
    page,
  }) => {
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

    await page.goto("/clinician/experience");
    await expect(
      page.getByRole("heading", { name: "Experience baseline" }),
    ).toBeVisible();
    await expect(
      page.getByText("local-experience-baseline-v1", { exact: true }),
    ).toBeVisible();
    await expect(page.getByText("Collection off")).toBeVisible();
    await expect(page.getByText("13", { exact: true }).first()).toBeVisible();

    await page.getByLabel("Category").selectOption("performance");
    await page.getByLabel("Lifecycle state").selectOption("owner-gated");
    await expect(
      page.getByRole("heading", { name: "Critical-task runtime budgets" }),
    ).toBeVisible();

    await page
      .getByRole("button", {
        name: /Create and review encounter documentation/i,
      })
      .click();
    await expect(
      page.getByRole("heading", {
        name: "Create and review encounter documentation",
      }),
    ).toBeVisible();
    await expect(page.getByText("Safety critical").last()).toBeVisible();

    await page
      .getByText("Forbidden analytics properties", { exact: true })
      .click();
    await expect(page.getByText("patientId", { exact: true })).toBeVisible();
    await expect(page.getByText("sessionId", { exact: true })).toBeVisible();

    const accessibility = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
      .analyze();
    expect(
      accessibility.violations.filter(
        ({ impact }) => impact === "serious" || impact === "critical",
      ),
    ).toEqual([]);
  });
});
