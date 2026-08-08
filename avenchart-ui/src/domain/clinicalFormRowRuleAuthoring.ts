// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import type {
  ClinicalFormField,
  ClinicalFormRule,
} from "../api/clinicalForms.ts";
import {
  calculationAuthoringIssues,
  calculationTargetFieldKeys,
  createDefaultCalculation,
} from "./clinicalFormCalculationAuthoring.ts";

export const clinicalFormRowRuleLimit = 20;

export type RowRuleAuthoringIssue = {
  repeatFieldKey: string;
  ruleKey: string | null;
  message: string;
};

export function appendClinicalFormRowRule(
  field: ClinicalFormField,
): ClinicalFormField {
  if (
    field.type !== "repeat" ||
    field.children.length < 2 ||
    (field.rowRules?.length ?? 0) >= clinicalFormRowRuleLimit
  ) {
    return field;
  }

  const rules = field.rowRules ?? [];
  const source = field.children.find((child) => child.type !== "computed")
    ?? field.children[0]!;
  const target = field.children.find(
    (child) => child.key !== source.key && child.type !== "computed",
  ) ?? source;
  const rule: ClinicalFormRule = {
    key: `${field.key}_row_rule_${rules.length + 1}`,
    condition: {
      fieldKey: source.key,
      operator: "is-not-empty",
    },
    action: "warning",
    targetFieldKey: target.key,
    message: "Review this row value.",
    calculation: null,
  };
  return { ...field, rowRules: [...rules, rule] };
}

export function setClinicalFormRowRuleAction(
  field: ClinicalFormField,
  ruleIndex: number,
  action: string,
  defaultCalculationOperator = "sum",
): ClinicalFormField {
  return {
    ...field,
    rowRules: (field.rowRules ?? []).map((rule, index) => {
      if (index !== ruleIndex) return rule;
      if (action !== "calculate") {
        return {
          ...rule,
          action,
          targetFieldKey:
            field.children.find((child) => child.type !== "computed")?.key
            ?? field.children[0]?.key
            ?? "",
          message:
            action === "warning"
              ? rule.message || "Review this row value."
              : null,
          calculation: null,
        };
      }

      const targetFieldKey =
        calculationTargetFieldKeys(field.children)[0] ?? "";
      return {
        ...rule,
        action,
        targetFieldKey,
        message: null,
        calculation: createDefaultCalculation(
          field.children,
          targetFieldKey,
          defaultCalculationOperator,
        ),
      };
    }),
  };
}

export function clinicalFormRowRuleAuthoringIssues(
  fields: ClinicalFormField[],
  supportedActions: string[],
  supportedCalculationOperators: string[],
): RowRuleAuthoringIssue[] {
  const supported = new Set(supportedActions);
  return fields
    .filter((field) => field.type === "repeat")
    .flatMap((field) => {
      const childKeys = new Set(field.children.map((child) => child.key));
      const rules = field.rowRules ?? [];
      const issues: RowRuleAuthoringIssue[] = [];
      if (rules.length > clinicalFormRowRuleLimit) {
        issues.push({
          repeatFieldKey: field.key,
          ruleKey: null,
          message: `A repeating group may contain at most ${clinicalFormRowRuleLimit} same-row rules.`,
        });
      }
      for (const rule of rules) {
        if (!childKeys.has(rule.condition.fieldKey)) {
          issues.push({
            repeatFieldKey: field.key,
            ruleKey: rule.key,
            message: "Condition must reference a sibling child in this row.",
          });
        }
        if (!childKeys.has(rule.targetFieldKey)) {
          issues.push({
            repeatFieldKey: field.key,
            ruleKey: rule.key,
            message: "Target must reference a sibling child in this row.",
          });
        }
        if (!supported.has(rule.action)) {
          issues.push({
            repeatFieldKey: field.key,
            ruleKey: rule.key,
            message: "Select a row action allowed by server policy.",
          });
        }
      }
      issues.push(
        ...calculationAuthoringIssues(
          rules,
          field.children,
          supportedCalculationOperators,
        ).map((issue) => ({
          repeatFieldKey: field.key,
          ...issue,
        })),
      );
      return issues;
    });
}
