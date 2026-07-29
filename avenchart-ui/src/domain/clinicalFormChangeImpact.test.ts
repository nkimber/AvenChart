import { describe, expect, it } from "vitest";
import type {
  ClinicalFormField,
  ClinicalFormSchema,
} from "../api/clinicalForms.ts";
import { describeClinicalFormChangeImpact } from "./clinicalFormChangeImpact.ts";

function field(
  key: string,
  type = "text",
  required = false,
): ClinicalFormField {
  return {
    key,
    sectionKey: "main",
    label: key,
    type,
    sequence: 10,
    required,
    accessibilityLabel: key,
    helpText: null,
    maxLength: type === "text" ? 240 : null,
    minimum: ["integer", "decimal", "computed"].includes(type) ? 0 : null,
    maximum: ["integer", "decimal", "computed"].includes(type) ? 100 : null,
    precision: type === "integer" ? 0 : type === "decimal" ? 2 : null,
    unit: null,
    codeSystem: null,
    options: [],
    repeatMinimum: null,
    repeatMaximum: null,
    children: [],
    readOnly: type === "computed",
  };
}

function schema(): ClinicalFormSchema {
  return {
    stableKey: "tmp.form.impact",
    name: "Impact fixture",
    purpose: "Verify successor impact guidance.",
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
    fields: [field("amount", "decimal")],
    rules: [],
  };
}

describe("clinical form successor change impact", () => {
  it("reports an unchanged candidate without invented findings", () => {
    const current = schema();
    expect(
      describeClinicalFormChangeImpact(current, structuredClone(current)),
    ).toEqual({
      items: [],
      highCount: 0,
      reviewCount: 0,
      lowCount: 0,
    });
  });

  it("groups restrictive bounds and presentation changes by field", () => {
    const current = schema();
    const candidate = structuredClone(current);
    candidate.fields[0]!.minimum = 1;
    candidate.fields[0]!.label = "Revised amount";
    candidate.fields[0]!.accessibilityLabel = "Revised amount";

    const impact = describeClinicalFormChangeImpact(current, candidate);
    expect(impact.highCount).toBe(1);
    expect(impact.items).toContainEqual(
      expect.objectContaining({
        key: "field:amount:changed",
        severity: "high",
        title: "Field amount changed",
        description: expect.stringContaining(
          "minimum tightens from 0 to 1",
        ),
      }),
    );
    expect(impact.items[0]?.description).toContain(
      "label, accessibility label, or help text changes",
    );
  });

  it("distinguishes additive optional fields from required fields and removals", () => {
    const current = schema();
    const candidate = structuredClone(current);
    candidate.fields = [
      field("optional_note"),
      field("required_score", "integer", true),
    ];

    const impact = describeClinicalFormChangeImpact(current, candidate);
    expect(impact.lowCount).toBe(1);
    expect(impact.highCount).toBe(2);
    expect(impact.items.map((item) => item.key)).toEqual([
      "field:amount:removed",
      "field:optional_note:added",
      "field:required_score:added",
    ]);
  });

  it("explains metadata, option, and rule contract changes", () => {
    const current = schema();
    current.fields[0] = {
      ...field("severity", "select"),
      optionListReference: { listKey: "yesno", revisionId: 2 },
      options: [
        { code: "low", display: "Low" },
        { code: "high", display: "High" },
      ],
    };
    current.rules = [
      {
        key: "show_details",
        condition: {
          fieldKey: "severity",
          operator: "equals",
          value: "high",
        },
        action: "show",
        targetFieldKey: "severity",
        message: null,
        calculation: null,
      },
    ];
    const candidate = structuredClone(current);
    candidate.signaturePolicy = "author-and-cosigner";
    candidate.fields[0]!.options = [
      { code: "low", display: "Low severity" },
      { code: "medium", display: "Medium" },
    ];
    candidate.fields[0]!.optionListReference = {
      listKey: "state",
      revisionId: 60,
    };
    candidate.rules[0]!.action = "require";

    const impact = describeClinicalFormChangeImpact(current, candidate);
    expect(impact.highCount).toBe(3);
    expect(impact.items).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          key: "metadata:signaturePolicy",
          severity: "high",
        }),
        expect.objectContaining({
          key: "field:severity:changed",
          description: expect.stringContaining("removes option codes high"),
        }),
        expect.objectContaining({
          key: "field:severity:changed",
          description: expect.stringContaining(
            "option source changes from yesno revision 2 to state revision 60",
          ),
        }),
        expect.objectContaining({
          key: "rule:show_details:changed",
          description: expect.stringContaining(
            "action changes from show to require",
          ),
        }),
      ]),
    );
  });
});
