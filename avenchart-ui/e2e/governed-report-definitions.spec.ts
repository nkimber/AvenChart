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

test.describe("REP-01 governed report definitions", () => {
  test("governs an immutable active definition and successor", async ({
    page,
  }) => {
    const marker = `tmp-report-ui-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    let definitionId: string | null = null;

    await signIn(page);
    const staffSessionId = await sessionId(page);
    const headers = { "X-Legacy EHR-Session": staffSessionId };

    try {
      await page.goto("/clinician/reports");
      await expect(
        page.getByRole("heading", { name: "Governed report catalog" }),
      ).toBeVisible({ timeout: 20_000 });
      await expect(page.getByText("local-report-definition-v2")).toBeVisible();
      await expect(page.getByText("Raw SQL: rejected")).toBeVisible();
      await page
        .getByText(/Production blockers \(8\)/)
        .click();
      await expect(page.locator(".report-blocker-list li")).toHaveCount(8);

      const editor = page.locator("#report-definition-editor");
      await editor.getByLabel("Stable key").fill(marker);
      await editor.getByLabel("Title").fill("Browser appointment governance");
      await editor
        .getByLabel("Permitted purpose")
        .fill(
          "Verify the browser lifecycle for a bounded governed report definition.",
        );
      await editor
        .getByLabel("Curated family")
        .selectOption("appointments");
      await editor.getByLabel("Sensitivity").selectOption("restricted");
      await editor
        .getByLabel("Declared row policy")
        .selectOption("facility-scoped");
      await editor.getByLabel("Retention days").fill("30");
      await editor
        .getByLabel("Governance reason")
        .fill("Create the synthetic browser governance fixture.");
      await editor
        .getByRole("button", { name: "Create draft definition" })
        .click();

      const detail = page.locator(".report-definition-detail");
      await expect(detail).toBeVisible({ timeout: 20_000 });
      await expect(detail).toContainText(marker);
      await expect(
        detail.locator(".report-definition-facts").getByText("draft", {
          exact: true,
        }),
      ).toBeVisible();
      await expect(detail.locator(".report-contract-list")).toHaveCount(4);

      const listResponse = await page.request.get(
        `${apiBaseUrl}/api/reports/definitions?search=${encodeURIComponent(marker)}&page=1&pageSize=10`,
        { headers },
      );
      expect(listResponse.ok()).toBeTruthy();
      const listed = (await listResponse.json()) as {
        definitions: { definitionId: string }[];
      };
      definitionId = listed.definitions[0]?.definitionId ?? null;
      expect(definitionId).toBeTruthy();

      const transition = async (action: string, reason: string) => {
        await detail.getByLabel("Lifecycle reason").fill(reason);
        await detail
          .getByRole("button", { name: action, exact: true })
          .click();
        await expect(
          detail.locator(".report-definition-facts").getByText(
            action === "review"
              ? "reviewed"
              : action === "approve"
                ? "approved"
                : action === "activate"
                  ? "active"
                  : action === "retire"
                    ? "retired"
                    : action,
            { exact: true },
          ),
        ).toBeVisible({ timeout: 20_000 });
      };

      await transition(
        "review",
        "Owner reviewed the browser metric dictionary and purpose.",
      );
      await transition(
        "approve",
        "Approve the browser definition for controlled local activation.",
      );
      await transition(
        "activate",
        "Activate the approved browser definition in the local catalog.",
      );

      const catalogSection = page
        .getByRole("heading", { name: "Active accessible catalog" })
        .locator("xpath=ancestor::section");
      const catalog = catalogSection
        .getByRole("region", { name: "Active governed report catalog" });
      await expect(catalog).toContainText(marker);
      await expect(catalog).toContainText("v1");

      await detail
        .getByRole("button", { name: "Prepare successor" })
        .click();
      await expect(editor.getByLabel("Stable key")).toBeDisabled();
      await editor
        .getByLabel("Title")
        .fill("Browser appointment governance v2");
      await editor
        .getByLabel("Declared row policy")
        .selectOption("patient-assigned");
      await editor.getByLabel("Retention days").fill("45");
      await editor
        .getByLabel("Governance reason")
        .fill("Create an immutable browser successor revision.");
      await editor
        .getByRole("button", { name: "Create successor revision" })
        .click();

      await expect(
        detail.locator(".report-definition-facts").getByText("draft", {
          exact: true,
        }),
      ).toBeVisible({ timeout: 20_000 });
      await expect(
        detail.getByRole("region", {
          name: "Report definition revisions",
        }).locator("tbody tr"),
      ).toHaveCount(2);
      await expect(catalog).toContainText("v1");

      await transition(
        "review",
        "Owner reviewed the successor browser dictionary and purpose.",
      );
      await transition(
        "approve",
        "Approve the successor browser definition for activation.",
      );
      await transition(
        "activate",
        "Activate successor and preserve prior immutable meaning.",
      );
      await expect(catalog).toContainText("v2");
      await expect(
        detail
          .getByRole("region", { name: "Report definition revisions" })
          .locator("tbody tr")
          .nth(1),
      ).toContainText("suspended");

      const accessibility = await new AxeBuilder({ page })
        .include(".report-governance-hero")
        .include("#report-definition-editor")
        .include(".report-definition-detail")
        .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
        .analyze();
      expect(
        accessibility.violations.filter((violation) =>
          ["serious", "critical"].includes(violation.impact ?? ""),
        ),
      ).toEqual([]);

      await transition(
        "retire",
        "Retire the completed browser governance fixture safely.",
      );
      await expect(catalogSection).not.toContainText(marker);
      await expect(detail).toContainText("Immutable events (10)");
    } finally {
      if (definitionId) {
        const cleanup = await page.request.delete(
          `${apiBaseUrl}/api/reports/definitions/${definitionId}/test-fixture`,
          { headers },
        );
        expect(cleanup.status()).toBe(204);
      }
    }
  });
});
