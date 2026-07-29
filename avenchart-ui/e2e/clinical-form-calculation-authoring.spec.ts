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

test.describe("FORM-02 calculation authoring", () => {
  test("guides, previews, and saves a bounded calculation", async ({
    page,
  }) => {
    const marker = `${Date.now()}${Math.random().toString(16).slice(2, 8)}`;
    const stableKey = `tmp.form.calculation.${marker}`;
    const formName = `Calculation authoring ${marker}`;
    const apiBaseUrl =
      process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
    let definitionId: string | null = null;
    let forgedDefinitionId: string | null = null;

    await signIn(page);
    const headers = {
      "X-Legacy EHR-Session": await sessionId(page),
    };

    try {
      await page.goto("/clinician/admin");
      await page
        .getByRole("button", { name: /Forms & layouts/ })
        .click();

      const governance = page.locator(".clinical-form-governance");
      await expect(
        governance.getByRole("heading", {
          name: "Governed clinical form engine",
        }),
      ).toBeVisible();
      await governance
        .getByText("Safe runtime and production blockers")
        .click();
      await expect(governance.getByText("5 calculation operators")).toBeVisible();
      await governance.getByLabel("Search", { exact: true }).fill(stableKey);
      await governance
        .getByRole("button", { name: "Apply", exact: true })
        .click();
      await expect(
        governance.locator(
          ".clinical-form-instance-list .clinical-form-instance-link",
        ),
      ).toHaveCount(0);

      await governance.getByLabel("Stable key").fill(stableKey);
      await governance.getByLabel("Name").fill(formName);
      await governance
        .getByLabel("Clinical purpose")
        .fill("Verify guided bounded calculation authoring.");

      const fields = governance.locator(".clinical-form-field-editor");
      const firstField = fields.nth(0);
      await firstField.getByLabel("Key", { exact: true }).fill("amount");
      await firstField.getByLabel("Label", { exact: true }).fill("Amount");
      await firstField
        .getByRole("combobox", { name: "Type", exact: true })
        .selectOption("decimal");

      await governance.getByRole("button", { name: "Add field" }).click();
      const secondField = fields.nth(1);
      await secondField.getByLabel("Key", { exact: true }).fill("quantity");
      await secondField.getByLabel("Label", { exact: true }).fill("Quantity");
      await secondField
        .getByRole("combobox", { name: "Type", exact: true })
        .selectOption("integer");

      await governance.getByRole("button", { name: "Add field" }).click();
      const computedField = fields.nth(2);
      await computedField.getByLabel("Key", { exact: true }).fill("total");
      await computedField
        .getByLabel("Label", { exact: true })
        .fill("Calculated total");
      await computedField
        .getByRole("combobox", { name: "Type", exact: true })
        .selectOption("computed");

      await governance.getByRole("button", { name: "Add field" }).click();
      const sourcedField = fields.nth(3);
      await sourcedField.getByLabel("Key", { exact: true }).fill("decision");
      await sourcedField
        .getByLabel("Label", { exact: true })
        .fill("Governed decision");
      await sourcedField
        .getByRole("combobox", { name: "Type", exact: true })
        .selectOption("select");
      await sourcedField
        .getByRole("combobox", { name: "Option source", exact: true })
        .selectOption("yesno:2");
      await expect(
        sourcedField.getByText(/Pinned to governed list yesno revision 2/),
      ).toBeVisible();
      const sourcedOptions = sourcedField.getByLabel(
        "Options (one code|display per line)",
      );
      await expect(sourcedOptions).toHaveValue("yes|Yes\nno|No");
      await expect(sourcedOptions).toHaveAttribute("readonly", "");

      await governance.getByRole("button", { name: "Add rule" }).click();
      const rule = governance.locator(".clinical-form-rule-editor").last();
      await rule
        .getByRole("combobox", { name: "Operator", exact: true })
        .selectOption("is-empty");
      await rule
        .getByRole("combobox", { name: "Action", exact: true })
        .selectOption("calculate");
      await expect(
        rule.getByRole("combobox", { name: "Target field", exact: true }),
      ).toHaveValue("total");
      await expect(
        rule.getByRole("combobox", {
          name: "Calculation operator",
          exact: true,
        }),
      ).toHaveValue("sum");
      await expect(
        rule.getByRole("combobox", {
          name: "Operand 1 field",
          exact: true,
        }),
      ).toHaveValue("amount");

      await rule.getByRole("button", { name: "Add operand" }).click();
      await expect(
        rule.getByRole("combobox", {
          name: "Operand 2 field",
          exact: true,
        }),
      ).toHaveValue("quantity");
      await rule
        .getByRole("combobox", {
          name: "Calculation operator",
          exact: true,
        })
        .selectOption("divide");
      await expect(
        rule.getByRole("combobox", { name: /Operand \d source/ }),
      ).toHaveCount(2);

      await rule
        .getByRole("combobox", {
          name: "Operand 1 source",
          exact: true,
        })
        .selectOption("constant");
      await rule.getByLabel("Operand 1 constant").fill("10");
      await rule
        .getByRole("combobox", {
          name: "Operand 2 source",
          exact: true,
        })
        .selectOption("constant");
      await rule.getByLabel("Operand 2 constant").fill("2");
      await rule.getByLabel("Result precision").fill("2");

      const previewResponsePromise = page.waitForResponse(
        (response) =>
          response.url().endsWith("/api/form-engine/preview") &&
          response.request().method() === "POST",
      );
      await governance
        .getByRole("button", { name: "Synthetic preview" })
        .click();
      const previewResponse = await previewResponsePromise;
      expect(previewResponse.ok()).toBeTruthy();
      const preview = (await previewResponse.json()) as {
        valid: boolean;
        values: Record<string, unknown>;
      };
      expect(preview.valid).toBe(true);
      expect(preview.values.total).toBe(5);

      await governance
        .getByLabel("Governance reason")
        .fill("Verify guided bounded calculation authoring.");
      const createResponsePromise = page.waitForResponse(
        (response) =>
          response.url().endsWith("/api/form-engine/definitions") &&
          response.request().method() === "POST",
      );
      await governance.getByRole("button", { name: "Create draft" }).click();
      const createResponse = await createResponsePromise;
      expect(createResponse.status()).toBe(201);
      const created = (await createResponse.json()) as {
        definition: { definitionId: string };
        currentRevision: {
          definition: {
            rules: Array<{
              calculation: {
                operator: string;
                operands: Array<{
                  fieldKey: string | null;
                  constant: number | null;
                }>;
                precision: number | null;
              };
            }>;
            fields: Array<{
              key: string;
              options: Array<{ code: string; display: string }>;
              optionListReference?: {
                listKey: string;
                revisionId: number;
              } | null;
            }>;
          };
        };
      };
      definitionId = created.definition.definitionId;
      expect(created.currentRevision.definition.rules[0]?.calculation).toEqual({
        operator: "divide",
        operands: [
          { fieldKey: null, constant: 10 },
          { fieldKey: null, constant: 2 },
        ],
        precision: 2,
      });
      expect(
        created.currentRevision.definition.fields.find(
          (field) => field.key === "decision",
        ),
      ).toEqual(
        expect.objectContaining({
          options: [
            { code: "yes", display: "Yes" },
            { code: "no", display: "No" },
          ],
          optionListReference: {
            listKey: "yesno",
            revisionId: 2,
          },
        }),
      );

      const forgedResponse = await page.request.post(
        `${apiBaseUrl}/api/form-engine/definitions`,
        {
          headers,
          data: {
            definition: {
              stableKey: `${stableKey}.forged`,
              name: `Forged option source ${marker}`,
              purpose: "Prove copied option values must match their source.",
              contextScope: "encounter",
              owningService: "clinical_operations",
              capability: "encounters.auth_a",
              signaturePolicy: "author-only",
              sections: [
                {
                  key: "main",
                  title: "Main",
                  sequence: 10,
                  description: null,
                },
              ],
              fields: [
                {
                  key: "decision",
                  sectionKey: "main",
                  label: "Decision",
                  type: "select",
                  sequence: 10,
                  required: false,
                  accessibilityLabel: "Decision",
                  helpText: null,
                  maxLength: null,
                  minimum: null,
                  maximum: null,
                  precision: null,
                  unit: null,
                  codeSystem: null,
                  options: [
                    { code: "yes", display: "Forged display" },
                    { code: "no", display: "No" },
                  ],
                  optionListReference: {
                    listKey: "yesno",
                    revisionId: 2,
                  },
                  repeatMinimum: null,
                  repeatMaximum: null,
                  children: [],
                  readOnly: false,
                },
              ],
              rules: [],
            },
            reason: "Reject false option-list provenance.",
          },
        },
      );
      if (forgedResponse.status() === 201) {
        forgedDefinitionId = (
          (await forgedResponse.json()) as {
            definition: { definitionId: string };
          }
        ).definition.definitionId;
      }
      expect(forgedResponse.status()).toBe(400);
      expect((await forgedResponse.json()).error).toContain(
        "do not match pinned option list yesno revision 2",
      );

      await governance
        .getByRole("button", { name: "Prepare successor" })
        .click();
      await expect(
        governance.getByRole("heading", {
          name: "Prepare successor revision",
        }),
      ).toBeVisible();
      await expect(
        governance.getByText(
          "No schema changes yet. Change at least one governed contract before creating a successor.",
        ),
      ).toBeVisible();

      const successorButton = governance.getByRole("button", {
        name: "Create successor draft",
      });
      await governance
        .getByLabel("Governance reason")
        .fill("Explain restrictive successor changes.");
      await expect(successorButton).toBeDisabled();

      const successorAmount = governance
        .locator(".clinical-form-field-editor")
        .nth(0);
      await successorAmount
        .getByRole("spinbutton", { name: "Minimum", exact: true })
        .fill("1");
      await successorAmount
        .getByLabel("Label", { exact: true })
        .fill("Revised amount");

      const impact = governance.locator(".clinical-form-change-impact");
      await expect(
        impact.getByRole("heading", { name: "Successor change impact" }),
      ).toBeVisible();
      await expect(impact.getByText("1 high review")).toBeVisible();
      await expect(impact.getByText("Field amount changed")).toBeVisible();
      await expect(
        impact.getByText(/minimum tightens from 0 to 1/),
      ).toBeVisible();
      await expect(successorButton).toBeEnabled();

      const accessibility = await new AxeBuilder({ page })
        .include(".clinical-form-governance")
        .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
        .analyze();
      expect(
        accessibility.violations.filter((violation) =>
          ["serious", "critical"].includes(violation.impact ?? ""),
        ),
      ).toEqual([]);

      const successorResponsePromise = page.waitForResponse(
        (response) =>
          response.url().endsWith(
            `/api/form-engine/definitions/${definitionId}/revisions`,
          ) && response.request().method() === "POST",
      );
      await successorButton.click();
      const successorResponse = await successorResponsePromise;
      expect(successorResponse.status()).toBe(201);
      const successor = (await successorResponse.json()) as {
        currentRevision: {
          revision: number;
          definition: {
            fields: Array<{
              key: string;
              label: string;
              minimum: number | null;
            }>;
          };
        };
      };
      expect(successor.currentRevision.revision).toBe(2);
      expect(
        successor.currentRevision.definition.fields.find(
          (field) => field.key === "amount",
        ),
      ).toEqual(
        expect.objectContaining({
          label: "Revised amount",
          minimum: 1,
        }),
      );
    } finally {
      if (forgedDefinitionId) {
        const forgedCleanup = await page.request.delete(
          `${apiBaseUrl}/api/form-engine/definitions/${forgedDefinitionId}/test-fixture`,
          { headers },
        );
        expect(forgedCleanup.status()).toBe(204);
      }
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
