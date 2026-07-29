import type {
  ClinicalFormCalculation,
  ClinicalFormCalculationTemplate,
  ClinicalFormField,
  ClinicalFormRule,
} from "../api/clinicalForms.ts";

export type CalculationAuthoringIssue = {
  ruleKey: string | null;
  message: string;
};

const NUMERIC_FIELD_TYPES = new Set([
  "integer",
  "decimal",
  "measurement",
  "computed",
]);
const MAX_DOTNET_DECIMAL = 7.922816251426433e28;

function hasNumericOptionCodes(field: ClinicalFormField) {
  return (
    ["select", "coded"].includes(field.type) &&
    field.options.length > 0 &&
    field.options.every((option) =>
      /^[+-]?(?:\d+(?:\.\d*)?|\.\d+)$/.test(option.code.trim()),
    )
  );
}

export function isCalculationOperandField(field: ClinicalFormField) {
  return NUMERIC_FIELD_TYPES.has(field.type) || hasNumericOptionCodes(field);
}

export function calculationTargetFieldKeys(fields: ClinicalFormField[]) {
  return fields
    .filter((field) => field.type === "computed")
    .map((field) => field.key);
}

export function calculationOperandFieldKeys(
  fields: ClinicalFormField[],
  targetFieldKey: string,
) {
  return fields
    .filter(
      (field) =>
        field.key !== targetFieldKey && isCalculationOperandField(field),
    )
    .map((field) => field.key);
}

function constantFor(operator: string, operandIndex: number) {
  return operandIndex === 1 && ["multiply", "divide"].includes(operator)
    ? 1
    : 0;
}

function defaultOperand(
  fieldKeys: string[],
  operator: string,
  operandIndex: number,
) {
  const fieldKey = fieldKeys[operandIndex];
  return fieldKey
    ? { fieldKey, constant: null }
    : {
        fieldKey: null,
        constant: constantFor(operator, operandIndex),
      };
}

export function createDefaultCalculation(
  fields: ClinicalFormField[],
  targetFieldKey: string,
  operator = "sum",
): ClinicalFormCalculation {
  const fieldKeys = calculationOperandFieldKeys(fields, targetFieldKey);
  const operandCount = operator === "sum" ? 1 : 2;
  return {
    operator,
    operands: Array.from({ length: operandCount }, (_, index) =>
      defaultOperand(fieldKeys, operator, index),
    ),
    precision: 2,
  };
}

export function changeCalculationOperator(
  calculation: ClinicalFormCalculation,
  operator: string,
  fields: ClinicalFormField[],
  targetFieldKey: string,
): ClinicalFormCalculation {
  const maximum = operator === "sum" ? 20 : 2;
  const minimum = operator === "sum" ? 1 : 2;
  const operands = calculation.operands.slice(0, maximum);
  const fieldKeys = calculationOperandFieldKeys(fields, targetFieldKey).filter(
    (fieldKey) =>
      !operands.some((operand) => operand.fieldKey === fieldKey),
  );

  while (operands.length < minimum) {
    const operandIndex = operands.length;
    const fieldKey = fieldKeys.shift();
    operands.push(
      fieldKey
        ? { fieldKey, constant: null }
        : {
            fieldKey: null,
            constant: constantFor(operator, operandIndex),
          },
    );
  }

  return { ...calculation, operator, operands };
}

export function appendCalculationOperand(
  calculation: ClinicalFormCalculation,
  fields: ClinicalFormField[],
  targetFieldKey: string,
): ClinicalFormCalculation {
  if (
    calculation.operator !== "sum" ||
    calculation.operands.length >= 20
  ) {
    return calculation;
  }

  const usedFields = new Set(
    calculation.operands
      .map((operand) => operand.fieldKey)
      .filter((fieldKey): fieldKey is string => fieldKey !== null),
  );
  const fieldKey = calculationOperandFieldKeys(
    fields,
    targetFieldKey,
  ).find((candidate) => !usedFields.has(candidate));
  return {
    ...calculation,
    operands: [
      ...calculation.operands,
      fieldKey
        ? { fieldKey, constant: null }
        : { fieldKey: null, constant: 0 },
    ],
  };
}

export function applyCalculationTemplate(
  template: ClinicalFormCalculationTemplate,
  fields: ClinicalFormField[],
  targetFieldKey: string,
): ClinicalFormCalculation {
  let calculation = createDefaultCalculation(
    fields,
    targetFieldKey,
    template.operator,
  );
  while (
    calculation.operator === "sum" &&
    calculation.operands.length < template.operandCount
  ) {
    calculation = appendCalculationOperand(
      calculation,
      fields,
      targetFieldKey,
    );
  }

  return {
    ...calculation,
    precision: template.defaultPrecision,
  };
}

export function retargetCalculation(
  calculation: ClinicalFormCalculation,
  fields: ClinicalFormField[],
  targetFieldKey: string,
): ClinicalFormCalculation {
  const candidates = calculationOperandFieldKeys(fields, targetFieldKey);
  const usedFields = new Set(
    calculation.operands
      .map((operand) => operand.fieldKey)
      .filter(
        (fieldKey): fieldKey is string =>
          fieldKey !== null &&
          fieldKey !== targetFieldKey &&
          candidates.includes(fieldKey),
      ),
  );
  return {
    ...calculation,
    operands: calculation.operands.map((operand, index) => {
      if (operand.fieldKey !== targetFieldKey) return operand;
      const replacement = candidates.find(
        (candidate) => !usedFields.has(candidate),
      );
      if (replacement) {
        usedFields.add(replacement);
        return { fieldKey: replacement, constant: null };
      }
      return {
        fieldKey: null,
        constant: constantFor(calculation.operator, index),
      };
    }),
  };
}

function hasDependencyCycle(rules: ClinicalFormRule[]) {
  const graph = new Map<string, Set<string>>();
  const addEdge = (source: string, target: string) => {
    const targets = graph.get(source) ?? new Set<string>();
    targets.add(target);
    graph.set(source, targets);
  };

  for (const rule of rules) {
    addEdge(rule.condition.fieldKey, rule.targetFieldKey);
    for (const operand of rule.calculation?.operands ?? []) {
      if (operand.fieldKey) addEdge(operand.fieldKey, rule.targetFieldKey);
    }
  }

  const visiting = new Set<string>();
  const visited = new Set<string>();
  const visit = (node: string): boolean => {
    if (visited.has(node)) return false;
    if (visiting.has(node)) return true;
    visiting.add(node);
    for (const target of graph.get(node) ?? []) {
      if (visit(target)) return true;
    }
    visiting.delete(node);
    visited.add(node);
    return false;
  };

  return [...graph.keys()].some(visit);
}

export function calculationAuthoringIssues(
  rules: ClinicalFormRule[],
  fields: ClinicalFormField[],
  supportedOperators: string[],
): CalculationAuthoringIssue[] {
  const fieldMap = new Map(fields.map((field) => [field.key, field]));
  const supported = new Set(supportedOperators);
  const issues: CalculationAuthoringIssue[] = [];

  for (const rule of rules.filter((candidate) => candidate.action === "calculate")) {
    const target = fieldMap.get(rule.targetFieldKey);
    if (target?.type !== "computed") {
      issues.push({
        ruleKey: rule.key,
        message: "Select a computed target field.",
      });
    }

    const calculation = rule.calculation;
    if (!calculation) {
      issues.push({
        ruleKey: rule.key,
        message: "Add a bounded calculation.",
      });
      continue;
    }

    if (!supported.has(calculation.operator)) {
      issues.push({
        ruleKey: rule.key,
        message: "Select a calculation operator allowed by server policy.",
      });
    }

    const expectedCount =
      calculation.operator === "sum"
        ? calculation.operands.length >= 1 &&
          calculation.operands.length <= 20
        : calculation.operands.length === 2;
    if (!expectedCount) {
      issues.push({
        ruleKey: rule.key,
        message:
          calculation.operator === "sum"
            ? "Sum requires one to twenty operands."
            : "This operator requires exactly two operands.",
      });
    }

    calculation.operands.forEach((operand, index) => {
      const hasField = Boolean(operand.fieldKey);
      const hasConstant = operand.constant !== null;
      if (hasField === hasConstant) {
        issues.push({
          ruleKey: rule.key,
          message: `Operand ${index + 1} must use one field or one constant.`,
        });
        return;
      }

      if (operand.fieldKey) {
        const field = fieldMap.get(operand.fieldKey);
        if (!field || !isCalculationOperandField(field)) {
          issues.push({
            ruleKey: rule.key,
            message: `Operand ${index + 1} must reference a numeric field.`,
          });
        } else if (operand.fieldKey === rule.targetFieldKey) {
          issues.push({
            ruleKey: rule.key,
            message: `Operand ${index + 1} cannot reference its target field.`,
          });
        }
      } else if (
        operand.constant === null ||
        !Number.isFinite(operand.constant) ||
        Math.abs(operand.constant) > MAX_DOTNET_DECIMAL
      ) {
        issues.push({
          ruleKey: rule.key,
          message: `Operand ${index + 1} requires a supported decimal constant.`,
        });
      }
    });

    if (
      calculation.precision !== null &&
      (!Number.isInteger(calculation.precision) ||
        calculation.precision < 0 ||
        calculation.precision > 8)
    ) {
      issues.push({
        ruleKey: rule.key,
        message: "Precision must be a whole number from zero to eight.",
      });
    }
  }

  if (hasDependencyCycle(rules)) {
    issues.push({
      ruleKey: null,
      message: "Form rules cannot contain cyclic field dependencies.",
    });
  }

  return issues;
}
