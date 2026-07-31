// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from "@axe-core/playwright";
import { readFile } from "node:fs/promises";
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

test.describe("FORM-01 through FORM-04a governed clinical forms", () => {
  test("captures, signs, exports, and amends a revision-pinned form", async ({
    page,
  }) => {
    const marker = `${Date.now()}${Math.random().toString(16).slice(2, 8)}`;
    const stableKey = `tmp.form.browser.${marker}`;
    const formName = `Browser clinical form ${marker}`;
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    let definitionId: string | null = null;

    await signIn(page);
    const staffSessionId = await sessionId(page);
    const headers = { "X-Legacy EHR-Session": staffSessionId };
    const definition = {
      stableKey,
      name: formName,
      purpose:
        "Verify accessible typed capture, signature, export, and successor amendment.",
      contextScope: "encounter",
      owningService: "clinical_operations",
      capability: "encounters.auth_a",
      signaturePolicy: "author-only",
      sections: [
        {
          key: "observation",
          title: "Observation",
          sequence: 10,
          description: "Bounded browser verification.",
        },
      ],
      fields: [
        {
          key: "chief_concern",
          sectionKey: "observation",
          label: "Chief concern",
          type: "multiline",
          sequence: 10,
          required: true,
          accessibilityLabel: "Chief concern",
          helpText: "Describe the primary concern.",
          maxLength: 500,
          minimum: null,
          maximum: null,
          precision: null,
          unit: null,
          codeSystem: null,
          options: [],
          repeatMinimum: null,
          repeatMaximum: null,
          children: [],
          readOnly: false,
        },
        {
          key: "pain_score",
          sectionKey: "observation",
          label: "Pain score",
          type: "integer",
          sequence: 20,
          required: false,
          accessibilityLabel: "Pain score",
          helpText: "Optional zero to ten score.",
          maxLength: null,
          minimum: 0,
          maximum: 10,
          precision: 0,
          unit: "score",
          codeSystem: null,
          options: [],
          repeatMinimum: null,
          repeatMaximum: null,
          children: [],
          readOnly: false,
        },
        {
          key: "disposition",
          sectionKey: "observation",
          label: "Disposition",
          type: "select",
          sequence: 30,
          required: false,
          accessibilityLabel: "Disposition",
          helpText: null,
          maxLength: null,
          minimum: null,
          maximum: null,
          precision: null,
          unit: null,
          codeSystem: "local-disposition-v1",
          options: [
            { code: "routine", display: "Routine follow-up" },
            { code: "urgent", display: "Urgent follow-up" },
          ],
          repeatMinimum: null,
          repeatMaximum: null,
          children: [],
          readOnly: false,
        },
        {
          key: "escalation_note",
          sectionKey: "observation",
          label: "Escalation note",
          type: "multiline",
          sequence: 40,
          required: false,
          accessibilityLabel: "Escalation note",
          helpText: "Explain the urgent follow-up plan.",
          maxLength: 500,
          minimum: null,
          maximum: null,
          precision: null,
          unit: null,
          codeSystem: null,
          options: [],
          repeatMinimum: null,
          repeatMaximum: null,
          children: [],
          readOnly: false,
        },
      ],
      rules: [
        {
          key: "warn_high_pain",
          condition: {
            fieldKey: "pain_score",
            operator: "greater-than-or-equal",
            value: 8,
          },
          action: "warning",
          targetFieldKey: "disposition",
          message: "High pain score requires clinical attention.",
          calculation: null,
        },
        {
          key: "hide_escalation_note",
          condition: {
            fieldKey: "disposition",
            operator: "not-equals",
            value: "urgent",
          },
          action: "hide",
          targetFieldKey: "escalation_note",
          message: null,
          calculation: null,
        },
        {
          key: "show_escalation_note",
          condition: {
            fieldKey: "disposition",
            operator: "equals",
            value: "urgent",
          },
          action: "show",
          targetFieldKey: "escalation_note",
          message: null,
          calculation: null,
        },
        {
          key: "require_escalation_note",
          condition: {
            fieldKey: "disposition",
            operator: "equals",
            value: "urgent",
          },
          action: "require",
          targetFieldKey: "escalation_note",
          message: null,
          calculation: null,
        },
      ],
      localizations: [
        {
          locale: "es-US",
          name: `Formulario clínico del navegador ${marker}`,
          purpose:
            "Verificar captura tipada accesible, firma, exportación y enmienda sucesora.",
          sections: [
            {
              sectionKey: "observation",
              title: "Observación",
              description: "Verificación acotada del navegador.",
            },
          ],
          fields: [
            {
              fieldKey: "chief_concern",
              label: "Motivo principal",
              accessibilityLabel: "Motivo principal",
              helpText: "Describa el motivo principal.",
              options: [],
            },
            {
              fieldKey: "pain_score",
              label: "Puntuación de dolor",
              accessibilityLabel: "Puntuación de dolor",
              helpText: "Puntuación opcional de cero a diez.",
              options: [],
            },
            {
              fieldKey: "disposition",
              label: "Disposición",
              accessibilityLabel: "Disposición",
              helpText: null,
              options: [
                { code: "routine", display: "Seguimiento de rutina" },
                { code: "urgent", display: "Seguimiento urgente" },
              ],
            },
            {
              fieldKey: "escalation_note",
              label: "Nota de escalamiento",
              accessibilityLabel: "Nota de escalamiento",
              helpText: "Explique el plan de seguimiento urgente.",
              options: [],
            },
          ],
          rules: [
            {
              ruleKey: "warn_high_pain",
              message:
                "Una puntuación alta de dolor requiere atención clínica.",
            },
            { ruleKey: "hide_escalation_note", message: null },
            { ruleKey: "show_escalation_note", message: null },
            { ruleKey: "require_escalation_note", message: null },
          ],
        },
      ],
    };

    try {
      const createdResponse = await expectOk(
        page.request,
        "post",
        `${apiBaseUrl}/api/form-engine/definitions`,
        headers,
        {
          definition,
          reason: "Create the synthetic browser form.",
        },
      );
      const created = (await createdResponse.json()) as {
        definition: { definitionId: string };
      };
      definitionId = created.definition.definitionId;

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
            reason: `${action} the synthetic browser form.`,
            effectiveFrom: null,
            effectiveTo: null,
          },
        );
      }

      await page.goto("/clinician/patients/MOD-PAT-0001/forms");
      await expect(
        page.getByRole("heading", {
          name: /Clinical forms for /,
        }),
      ).toBeVisible({ timeout: 20_000 });
      const startSection = page.getByRole("region", {
        name: "Start an effective form",
      });
      const localeSelector = page.getByRole("combobox", {
        name: "Clinical content language",
      });
      await expect(localeSelector).toContainText("Spanish (United States)");
      await localeSelector.selectOption("es-US");
      const spanishFormName = `Formulario clínico del navegador ${marker}`;
      const spanishFormCard = startSection
        .locator("article")
        .filter({ hasText: spanishFormName });
      await expect(spanishFormCard).toContainText(
        "Verificar captura tipada accesible",
      );
      await localeSelector.selectOption("en-US");
      await startSection
        .getByLabel("Encounter for encounter-scoped forms")
        .selectOption({ index: 1 });
      await startSection
        .getByLabel("Reason")
        .fill("Start the browser clinical form.");

      const formCard = startSection
        .locator("article")
        .filter({ hasText: formName });
      await expect(formCard).toContainText("author-only");
      await formCard.getByRole("button", { name: "Start draft" }).click();

      const selected = page.locator(
        'section[aria-labelledby="selected-clinical-form-heading"]',
      );
      await expect(selected.getByRole("heading", { name: /revision 1/ })).toBeVisible({
        timeout: 20_000,
      });
      await expect(selected.getByLabel("Chief concern")).toBeEnabled({
        timeout: 20_000,
      });
      await selected
        .getByLabel("Chief concern")
        .fill("Focused browser observation");
      await selected.getByLabel("Pain score").fill("8");
      const liveGuidance = selected.getByRole("region", {
        name: "Live rule guidance",
      });
      await expect(liveGuidance).toContainText(
        "High pain score requires clinical attention.",
        { timeout: 20_000 },
      );
      await expect(liveGuidance).toContainText("Rule warn_high_pain");
      await expect(selected.getByLabel("Escalation note")).toHaveCount(0);
      await localeSelector.selectOption("es-US");
      await expect(
        selected.getByRole("heading", {
          name: new RegExp(`Formulario clínico del navegador ${marker}`),
        }),
      ).toBeVisible();
      await expect(selected.getByLabel("Motivo principal")).toHaveValue(
        "Focused browser observation",
      );
      await expect(liveGuidance).toContainText(
        "Una puntuación alta de dolor requiere atención clínica.",
      );
      await expect(selected.getByLabel("Nota de escalamiento")).toHaveCount(0);
      await localeSelector.selectOption("en-US");
      await expect(selected.getByLabel("Chief concern")).toHaveValue(
        "Focused browser observation",
      );

      await selected.getByLabel("Disposition").selectOption("urgent");
      await expect(selected.getByLabel("Escalation note")).toBeVisible({
        timeout: 20_000,
      });
      await expect(selected.getByLabel("Escalation note")).toHaveAttribute(
        "required",
        "",
      );
      await expect(liveGuidance).toContainText("Rule show_escalation_note");
      await expect(liveGuidance).toContainText("Rule require_escalation_note");
      await selected
        .getByLabel("Escalation note")
        .fill("Arrange urgent clinical reassessment.");

      const instanceListResponse = await page.request.get(
        `${apiBaseUrl}/api/form-engine/patients/MOD-PAT-0001/instances`,
        { headers },
      );
      expect(instanceListResponse.ok()).toBeTruthy();
      const instanceList = (await instanceListResponse.json()) as {
        instances: { instanceId: string; stableKey: string }[];
      };
      const currentInstance = instanceList.instances.find(
        (instance) => instance.stableKey === stableKey,
      );
      expect(currentInstance).toBeTruthy();
      const persistedDraftResponse = await page.request.get(
        `${apiBaseUrl}/api/form-engine/instances/${currentInstance?.instanceId}`,
        { headers },
      );
      expect(persistedDraftResponse.ok()).toBeTruthy();
      const persistedDraft = (await persistedDraftResponse.json()) as {
        values: Record<string, unknown>;
      };
      expect(persistedDraft.values).toEqual({});

      await selected.getByRole("button", { name: "Validate" }).click();
      await expect(selected).toContainText(
        "High pain score requires clinical attention.",
      );

      await selected
        .getByLabel("Draft save reason / transition reason")
        .fill("Save and finalize the validated browser observation.");
      await selected.getByRole("button", { name: "Save draft" }).click();
      await expect(selected.locator(".eyebrow")).toHaveText("draft");
      await selected.getByRole("button", { name: "Finalize" }).click();
      await expect(selected.locator(".eyebrow")).toHaveText(
        "ready-for-signature",
        { timeout: 20_000 },
      );

      await selected
        .getByLabel("Draft save reason / transition reason")
        .fill("Sign the browser clinical record.");
      await selected.getByRole("button", { name: "Sign" }).click();
      await expect(selected.locator(".eyebrow")).toHaveText("signed", {
        timeout: 20_000,
      });
      await expect(selected.getByText("signer by admin")).toBeVisible();

      const downloadPromise = page.waitForEvent("download");
      await selected
        .getByRole("button", { name: "Download structured record" })
        .click();
      const download = await downloadPromise;
      expect(download.suggestedFilename()).toContain(`${stableKey}-r1-`);
      const downloadPath = await download.path();
      expect(downloadPath).toBeTruthy();
      const exported = JSON.parse(
        await readFile(downloadPath as string, "utf8"),
      ) as {
        exportFormat: string;
        schemaHash: string;
        contentHash: string;
        instance: { definitionRevision: number };
        fieldDictionary: {
          fields: { reportColumn: string }[];
        };
      };
      expect(exported.exportFormat).toBe(
        "application/vnd.legacy-ehr.clinical-form+json;version=1",
      );
      expect(exported.instance.definitionRevision).toBe(1);
      expect(exported.schemaHash).toHaveLength(64);
      expect(exported.contentHash).toHaveLength(64);
      expect(exported.fieldDictionary.fields).toHaveLength(4);
      expect(
        exported.fieldDictionary.fields[0]?.reportColumn,
      ).toContain(`${stableKey}.r1.`);

      const accessibility = await new AxeBuilder({ page })
        .include(
          'section[aria-labelledby="start-clinical-form-heading"]',
        )
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

      await selected
        .getByLabel("Draft save reason / transition reason")
        .fill("Correct the signed browser record through a successor.");
      await selected
        .getByRole("button", { name: "Create amendment" })
        .click();
      await expect(selected.locator(".eyebrow")).toHaveText("draft", {
        timeout: 20_000,
      });
      await selected
        .getByLabel("Chief concern")
        .fill("Corrected browser observation");
      await selected
        .getByLabel("Draft save reason / transition reason")
        .fill("Save and finalize the corrected browser observation.");
      await selected.getByRole("button", { name: "Save draft" }).click();
      await selected.getByRole("button", { name: "Finalize" }).click();
      await expect(selected.locator(".eyebrow")).toHaveText(
        "ready-for-signature",
        { timeout: 20_000 },
      );
      await selected
        .getByLabel("Draft save reason / transition reason")
        .fill("Sign the corrected browser record.");
      await selected.getByRole("button", { name: "Sign" }).click();
      await expect(selected.locator(".eyebrow")).toHaveText("signed", {
        timeout: 20_000,
      });

      const history = page.getByRole("region", {
        name: "Patient form history",
      });
      const fixtureRows = history
        .locator("tbody tr")
        .filter({ hasText: formName });
      await expect(fixtureRows).toHaveCount(2);
      await expect(fixtureRows.filter({ hasText: "amended" })).toHaveCount(1);
      await expect(fixtureRows.filter({ hasText: "signed" })).toHaveCount(1);
    } finally {
      if (definitionId) {
        const cleanup = await page.request.delete(
          `${apiBaseUrl}/api/form-engine/definitions/${definitionId}/test-fixture`,
          { headers },
        );
        expect(cleanup.status()).toBe(204);
      }
    }
  });
});
