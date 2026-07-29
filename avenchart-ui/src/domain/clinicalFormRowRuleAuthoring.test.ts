import { describe, expect, it } from "vitest";
import {
  createSafeClinicalFormField,
  normalizeClinicalFormFieldType,
} from "./clinicalFormRepeatAuthoring.ts";
import {
  appendClinicalFormRowRule,
  clinicalFormRowRuleAuthoringIssues,
  setClinicalFormRowRuleAction,
} from "./clinicalFormRowRuleAuthoring.ts";

function repeatField() {
  const repeat = normalizeClinicalFormFieldType(
    createSafeClinicalFormField(1),
    "repeat",
  );
  repeat.children = [
    {
      ...normalizeClinicalFormFieldType(
        createSafeClinicalFormField(1, ""),
        "decimal",
      ),
      key: "quantity",
    },
    {
      ...normalizeClinicalFormFieldType(
        createSafeClinicalFormField(2, ""),
        "decimal",
      ),
      key: "unit_price",
    },
    {
      ...normalizeClinicalFormFieldType(
        createSafeClinicalFormField(3, ""),
        "computed",
      ),
      key: "row_total",
    },
  ];
  return repeat;
}

describe("clinical form same-row rule authoring", () => {
  it("creates a sibling-only warning and converts it to a bounded calculation", () => {
    const withWarning = appendClinicalFormRowRule(repeatField());
    expect(withWarning.rowRules).toEqual([
      expect.objectContaining({
        condition: expect.objectContaining({ fieldKey: "quantity" }),
        action: "warning",
        targetFieldKey: "unit_price",
      }),
    ]);

    const withCalculation = setClinicalFormRowRuleAction(
      withWarning,
      0,
      "calculate",
      "multiply",
    );
    expect(withCalculation.rowRules?.[0]).toMatchObject({
      action: "calculate",
      targetFieldKey: "row_total",
      calculation: {
        operator: "multiply",
        operands: [
          { fieldKey: "quantity", constant: null },
          { fieldKey: "unit_price", constant: null },
        ],
      },
    });
    expect(
      clinicalFormRowRuleAuthoringIssues(
        [withCalculation],
        ["show", "hide", "require", "warning", "calculate"],
        ["sum", "add", "subtract", "multiply", "divide"],
      ),
    ).toEqual([]);
  });

  it("reports cross-row-style unknown keys and calculation cycles", () => {
    const repeat = repeatField();
    repeat.rowRules = [
      {
        key: "bad_reference",
        condition: {
          fieldKey: "other_row.quantity",
          operator: "is-not-empty",
        },
        action: "warning",
        targetFieldKey: "missing_child",
        message: "Blocked.",
        calculation: null,
      },
      {
        key: "cycle",
        condition: { fieldKey: "row_total", operator: "is-not-empty" },
        action: "calculate",
        targetFieldKey: "row_total",
        message: null,
        calculation: {
          operator: "sum",
          operands: [{ fieldKey: "quantity", constant: null }],
          precision: 2,
        },
      },
    ];

    expect(
      clinicalFormRowRuleAuthoringIssues(
        [repeat],
        ["warning", "calculate"],
        ["sum"],
      ).map((issue) => issue.message),
    ).toEqual(
      expect.arrayContaining([
        "Condition must reference a sibling child in this row.",
        "Target must reference a sibling child in this row.",
        "Form rules cannot contain cyclic field dependencies.",
      ]),
    );
  });
});
