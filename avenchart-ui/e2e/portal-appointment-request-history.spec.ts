// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from "@axe-core/playwright";
import type { Page } from "@playwright/test";
import { expect, test } from "./support/fixtures.ts";

async function signInPortal(page: Page) {
  await page.goto("/portal/login");
  await page
    .getByLabel("Email or username")
    .fill(process.env.MODERN_UI_PORTAL_USERNAME ?? "mod-pat-0004@example.test");
  await page
    .getByLabel("Password")
    .fill(process.env.MODERN_UI_PORTAL_PASSWORD ?? "PortalPass207!");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/portal\/home$/, { timeout: 15_000 });
}

test("portal appointment history explains the durable request lifecycle", async ({
  page,
}) => {
  const appointmentId = process.env.MODERN_UI_PORTAL_REQUEST_HISTORY_ID;
  test.skip(!appointmentId, "The cleanup-backed request fixture was not supplied.");

  await signInPortal(page);
  await page.goto("/portal/appointments");

  const history = page.getByRole("region", {
    name: "Appointment request history",
  });
  const request = history
    .locator("li.portal-appointment-request-card")
    .filter({ hasText: appointmentId! });
  await expect(request).toBeVisible({ timeout: 15_000 });
  await expect(request.getByText("Cancelled", { exact: true })).toBeVisible();
  await expect(request).toContainText(
    "Submit a new request if care is still needed.",
  );
  await expect(request).toContainText("version 3");
  await expect(request).toContainText("runtime");

  await request.getByText("Lifecycle evidence (3)").click();
  await expect(request.getByText("requested", { exact: true })).toBeVisible();
  await expect(request.getByText("accepted", { exact: true })).toBeVisible();
  await expect(request.getByText("cancelled", { exact: true })).toBeVisible();
  await expect(request).toContainText("diagnostic status");

  const accessibility = await new AxeBuilder({ page })
    .include(`[aria-labelledby="appointment-request-history-title"]`)
    .withTags(["wcag2a", "wcag2aa"])
    .analyze();
  expect(
    accessibility.violations.filter((violation) =>
      ["serious", "critical"].includes(violation.impact ?? ""),
    ),
  ).toEqual([]);
});
