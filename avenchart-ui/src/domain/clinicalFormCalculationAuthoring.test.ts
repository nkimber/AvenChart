import { describe, expect, it } from "vitest";
import type {
  ClinicalFormField,
  ClinicalFormRule,
} from "../api/clinicalForms.ts";
import {
  applyCalculationTemplate,
  appendCalculationOperand,
  calculationAuthoringIssues,
  calculationOperandFieldKeys,
  changeCalculationOperator,
  createDefaultCalculation,
  isCalculationOperandField,
  retargetCalculation,
} from "./clinicalFormCalculationAuthoring.ts";

function field(
  key: string,
  type: string,
  options: ClinicalFormField["options"] = [],
): ClinicalFormField {
  return {
    key,
    sectionKey: "main",
    label: key,
    type,
    sequence: 10,
    required: false,
    accessibilityLabel: key,
    helpText: null,
    maxLength: null,
    minimum: null,
    maximum: null,
    precision: null,
    unit: null,
    codeSystem: null,
    options,
    repeatMinimum: null,
    repeatMaximum: null,
    children: [],
    readOnly: type === "computed",
  };
}

const fields = [
  field("amount", "decimal"),
  field("quantity", "integer"),
  field("score", "select", [
    { code: "0", display: "None" },
    { code: "3", display: "High" },
  ]),
  field("label", "text"),
  field("total", "computed"),
];

function calculationRule(
  key: string,
  targetFieldKey: string,
  operandFieldKey: string,
): ClinicalFormRule {
  return {
    key,
    condition: { fieldKey: "amount", operator: "is-not-empty" },
    action: "calculate",
    targetFieldKey,
    message: null,
    calculation: {
      operator: "sum",
      operands: [{ fieldKey: operandFieldKey, constant: null }],
      precision: 2,
    },
  };
}

describe("clinical form calculation authoring", () => {
  it("offers numeric fields and bounded numeric option codes but not the target", () => {
    expect(isCalculationOperandField(fields[2])).toBe(true);
    expect(isCalculationOperandField(fields[3])).toBe(false);
    expect(calculationOperandFieldKeys(fields, "total")).toEqual([
      "amount",
      "quantity",
      "score",
    ]);
  });

  it("creates a valid sum and supplies exactly two operands for binary operators", () => {
    const sum = createDefaultCalculation(fields, "total");
    expect(sum).toEqual({
      operator: "sum",
      operands: [{ fieldKey: "amount", constant: null }],
      precision: 2,
    });

    const divided = changeCalculationOperator(
      sum,
      "divide",
      fields,
      "total",
    );
    expect(divided.operands).toEqual([
      { fieldKey: "amount", constant: null },
      { fieldKey: "quantity", constant: null },
    ]);
    expect(
      changeCalculationOperator(divided, "sum", fields, "total").operands,
    ).toHaveLength(2);
  });

  it("adds distinct available fields to a bounded sum", () => {
    const initial = createDefaultCalculation(fields, "total");
    const next = appendCalculationOperand(initial, fields, "total");
    expect(next.operands).toEqual([
      { fieldKey: "amount", constant: null },
      { fieldKey: "quantity", constant: null },
    ]);
  });

  it("applies policy-provided reusable starters with safe operands", () => {
    expect(
      applyCalculationTemplate(
        {
          key: "bounded-sum",
          title: "Bounded total",
          description: "Two-field total.",
          operator: "sum",
          operandCount: 2,
          defaultPrecision: 1,
        },
        fields,
        "total",
      ),
    ).toEqual({
      operator: "sum",
      operands: [
        { fieldKey: "amount", constant: null },
        { fieldKey: "quantity", constant: null },
      ],
      precision: 1,
    });

    expect(
      applyCalculationTemplate(
        {
          key: "ratio",
          title: "Ratio",
          description: "Divide two values.",
          operator: "divide",
          operandCount: 2,
          defaultPrecision: 3,
        },
        fields,
        "total",
      ),
    ).toEqual({
      operator: "divide",
      operands: [
        { fieldKey: "amount", constant: null },
        { fieldKey: "quantity", constant: null },
      ],
      precision: 3,
    });
  });

  it("replaces an operand when its field becomes the calculation target", () => {
    const calculation = {
      operator: "add",
      operands: [
        { fieldKey: "amount", constant: null },
        { fieldKey: "total", constant: null },
      ],
      precision: 2,
    };
    expect(
      retargetCalculation(
        calculation,
        [...fields, field("other_total", "computed")],
        "total",
      ).operands,
    ).toEqual([
      { fieldKey: "amount", constant: null },
      { fieldKey: "quantity", constant: null },
    ]);
  });

  it("reports invalid targets, operands, precision, operators, and cycles", () => {
    const invalid = calculationRule("invalid", "amount", "label");
    invalid.calculation = {
      operator: "execute",
      operands: [{ fieldKey: "label", constant: null }],
      precision: 9,
    };
    const oversized = calculationRule("oversized", "total", "amount");
    oversized.calculation = {
      operator: "sum",
      operands: [{ fieldKey: null, constant: 1e29 }],
      precision: 2,
    };
    const firstIssues = calculationAuthoringIssues(
      [invalid, oversized],
      fields,
      ["sum", "add", "subtract", "multiply", "divide"],
    );
    expect(firstIssues.map((issue) => issue.message)).toEqual(
      expect.arrayContaining([
        "Select a computed target field.",
        "Select a calculation operator allowed by server policy.",
        "This operator requires exactly two operands.",
        "Operand 1 must reference a numeric field.",
        "Operand 1 requires a supported decimal constant.",
        "Precision must be a whole number from zero to eight.",
      ]),
    );

    const cycleFields = [
      ...fields,
      field("other_total", "computed"),
    ];
    const cycleIssues = calculationAuthoringIssues(
      [
        calculationRule("first", "total", "other_total"),
        calculationRule("second", "other_total", "total"),
      ],
      cycleFields,
      ["sum", "add", "subtract", "multiply", "divide"],
    );
    expect(cycleIssues).toContainEqual({
      ruleKey: null,
      message: "Form rules cannot contain cyclic field dependencies.",
    });
  });
});
