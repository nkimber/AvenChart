// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from "@axe-core/playwright";
import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

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

async function expectOk(
  request: APIRequestContext,
  method: "post" | "delete",
  url: string,
  headers: Record<string, string>,
  data?: unknown,
) {
  const response = await request[method](url, { headers, data });
  expect(
    response.ok(),
    `${method.toUpperCase()} ${url}: ${await response.text()}`,
  ).toBeTruthy();
  return response;
}

test.describe("FORM-02 same-row repeat rules", () => {
  test("isolates sibling rules and renders computed children per row", async ({
    page,
  }) => {
    const marker = `${Date.now()}${Math.random().toString(16).slice(2, 8)}`;
    const stableKey = `tmp.form.row_rules.${marker}`;
    const formName = `Same-row form ${marker}`;
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    let definitionId: string | null = null;

    await signIn(page);
    const headers = {
      "X-Legacy EHR-Session": await sessionId(page),
    };
    const child = (
      key: string,
      label: string,
      type: string,
      sequence: number,
    ) => ({
      key,
      sectionKey: "",
      label,
      type,
      sequence,
      required: false,
      accessibilityLabel: label,
      helpText: null,
      maxLength: type === "text" ? 200 : null,
      minimum: ["integer", "decimal", "computed"].includes(type) ? 0 : null,
      maximum: ["integer", "decimal", "computed"].includes(type) ? 1000 : null,
      precision: type === "integer" ? 0 : ["decimal", "computed"].includes(type) ? 2 : null,
      unit: null,
      codeSystem: null,
      options: [],
      optionListReference: null,
      repeatMinimum: null,
      repeatMaximum: null,
      children: [],
      readOnly: type === "computed",
      rowRules: null,
    });
    const definition = {
      stableKey,
      name: formName,
      purpose:
        "Verify row-isolated sibling rules, computed outputs, and validation.",
      contextScope: "encounter",
      owningService: "clinical_operations",
      capability: "encounters.auth_a",
      signaturePolicy: "author-only",
      sections: [
        {
          key: "main",
          title: "Line item details",
          sequence: 10,
          description: "Each line is evaluated independently.",
        },
      ],
      fields: [
        {
          key: "line_items",
          sectionKey: "main",
          label: "Line items",
          type: "repeat",
          sequence: 10,
          required: false,
          accessibilityLabel: "Line items",
          helpText: "Add one or two bounded line items.",
          maxLength: null,
          minimum: null,
          maximum: null,
          precision: null,
          unit: null,
          codeSystem: null,
          options: [],
          optionListReference: null,
          repeatMinimum: 1,
          repeatMaximum: 2,
          children: [
            child("quantity", "Quantity", "integer", 10),
            child("unit_price", "Unit price", "decimal", 20),
            child("line_detail", "Line detail", "text", 30),
            child("line_total", "Line total", "computed", 40),
          ],
          readOnly: false,
          rowRules: [
            {
              key: "calculate_line_total",
              condition: {
                fieldKey: "quantity",
                operator: "is-not-empty",
              },
              action: "calculate",
              targetFieldKey: "line_total",
              message: null,
              calculation: {
                operator: "multiply",
                operands: [
                  { fieldKey: "quantity", constant: null },
                  { fieldKey: "unit_price", constant: null },
                ],
                precision: 2,
              },
            },
            {
              key: "require_large_line_detail",
              condition: {
                fieldKey: "quantity",
                operator: "greater-than-or-equal",
                value: 5,
              },
              action: "require",
              targetFieldKey: "line_detail",
              message: null,
              calculation: null,
            },
            {
              key: "hide_small_line_detail",
              condition: {
                fieldKey: "quantity",
                operator: "less-than",
                value: 5,
              },
              action: "hide",
              targetFieldKey: "line_detail",
              message: null,
              calculation: null,
            },
          ],
        },
      ],
      rules: [],
      localizations: null,
    };

    try {
      const createdResponse = await expectOk(
        page.request,
        "post",
        `${apiBaseUrl}/api/form-engine/definitions`,
        headers,
        {
          definition,
          reason: "Create the same-row browser fixture.",
        },
      );
      definitionId = (
        (await createdResponse.json()) as {
          definition: { definitionId: string };
        }
      ).definition.definitionId;

      for (const [action, expectedVersion] of [
        ["review", 0],
        ["approve", 1],
        ["activate", 2],
      ] as const) {
        await expectOk(
          page.request,
          "post",
          `${apiBaseUrl}/api/form-engine/definitions/${definitionId}/${action}`,
          headers,
          {
            revision: 1,
            expectedVersion,
            reason: `${action} the same-row browser fixture.`,
            effectiveFrom: null,
            effectiveTo: null,
          },
        );
      }

      await page.goto("/clinician/patients/MOD-PAT-0001/forms");
      const start = page.getByRole("region", {
        name: "Start an effective form",
      });
      await start
        .getByLabel("Encounter for encounter-scoped forms")
        .selectOption({ index: 1 });
      await start.getByLabel("Reason").fill("Start same-row browser proof.");
      await start
        .locator("article")
        .filter({ hasText: formName })
        .getByRole("button", { name: "Start draft" })
        .click();

      const selected = page.locator(
        'section[aria-labelledby="selected-clinical-form-heading"]',
      );
      const repeat = selected.getByRole("group", { name: "Line items" });
      await expect(repeat).toBeVisible({ timeout: 20_000 });
      await repeat.getByRole("button", { name: "Add entry" }).click();
      await repeat.getByRole("button", { name: "Add entry" }).click();
      const entries = repeat.locator(":scope > .cl-card");
      await expect(entries).toHaveCount(2);

      await entries.nth(0).getByLabel("Quantity").fill("5");
      await entries.nth(0).getByLabel("Unit price").fill("2");
      await expect(entries.nth(0).getByText("10", { exact: true })).toBeVisible({
        timeout: 20_000,
      });
      await expect(entries.nth(0).getByLabel("Line detail")).toHaveAttribute(
        "required",
        "",
      );
      await entries
        .nth(0)
        .getByLabel("Line detail")
        .fill("Large row explanation.");

      await entries.nth(1).getByLabel("Quantity").fill("1");
      await entries.nth(1).getByLabel("Unit price").fill("3");
      await expect(entries.nth(1).getByText("3", { exact: true })).toBeVisible({
        timeout: 20_000,
      });
      await expect(entries.nth(1).getByLabel("Line detail")).toHaveCount(0);

      const guidance = selected.getByRole("region", {
        name: "Live rule guidance",
      });
      await expect(guidance).toContainText("Row 1 of line_items");
      await expect(guidance).toContainText("Row 2 of line_items");
      await expect(guidance).toContainText("Rule calculate_line_total");
      await expect(guidance).toContainText("Rule require_large_line_detail");
      await expect(guidance).toContainText("Rule hide_small_line_detail");

      const validationResponsePromise = page.waitForResponse(
        (response) =>
          response.url().endsWith("/api/form-engine/preview") &&
          response.request().method() === "POST",
      );
      await selected.getByRole("button", { name: "Validate" }).click();
      const validationResponse = await validationResponsePromise;
      expect(validationResponse.ok()).toBeTruthy();
      expect(
        ((await validationResponse.json()) as { valid: boolean }).valid,
      ).toBe(true);
      const accessibility = await new AxeBuilder({ page })
        .include(
          'section[aria-labelledby="selected-clinical-form-heading"]',
        )
        .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
        .analyze();
      expect(
        accessibility.violations.filter((violation) =>
          ["serious", "critical"].includes(violation.impact ?? ""),
        ),
      ).toEqual([]);
    } finally {
      if (definitionId) {
        await expectOk(
          page.request,
          "delete",
          `${apiBaseUrl}/api/form-engine/definitions/${definitionId}/test-fixture`,
          headers,
        );
      }
    }
  });
});
