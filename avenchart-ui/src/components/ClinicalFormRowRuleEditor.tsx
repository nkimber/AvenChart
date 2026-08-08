// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { Plus } from "lucide-react";
import type {
  ClinicalFormCalculation,
  ClinicalFormField,
  ClinicalFormPolicy,
  ClinicalFormRule,
} from "../api/clinicalForms.ts";
import {
  applyCalculationTemplate,
  appendCalculationOperand,
  calculationOperandFieldKeys,
  calculationTargetFieldKeys,
  changeCalculationOperator,
  retargetCalculation,
} from "../domain/clinicalFormCalculationAuthoring.ts";
import {
  appendClinicalFormRowRule,
  clinicalFormRowRuleLimit,
  setClinicalFormRowRuleAction,
  type RowRuleAuthoringIssue,
} from "../domain/clinicalFormRowRuleAuthoring.ts";

type Props = {
  field: ClinicalFormField;
  policy: ClinicalFormPolicy | null;
  issues: RowRuleAuthoringIssue[];
  onChange: (field: ClinicalFormField) => void;
};

function parseConditionValue(
  field: ClinicalFormField | undefined,
  rawValue: string,
): string | number | boolean {
  if (field?.type === "boolean") {
    if (rawValue.trim().toLowerCase() === "true") return true;
    if (rawValue.trim().toLowerCase() === "false") return false;
  }
  if (
    field &&
    ["integer", "decimal", "measurement", "computed"].includes(field.type) &&
    rawValue.trim() !== ""
  ) {
    const numeric = Number(rawValue);
    if (Number.isFinite(numeric)) return numeric;
  }
  return rawValue;
}

export default function ClinicalFormRowRuleEditor({
  field,
  policy,
  issues,
  onChange,
}: Props) {
  const rules = field.rowRules ?? [];
  const childKeys = field.children.map((child) => child.key);
  const computedKeys = calculationTargetFieldKeys(field.children);

  const updateRule = (ruleIndex: number, patch: Partial<ClinicalFormRule>) =>
    onChange({
      ...field,
      rowRules: rules.map((rule, index) =>
        index === ruleIndex ? { ...rule, ...patch } : rule,
      ),
    });

  const updateCalculation = (
    ruleIndex: number,
    update: (
      calculation: ClinicalFormCalculation,
      rule: ClinicalFormRule,
    ) => ClinicalFormCalculation,
  ) =>
    onChange({
      ...field,
      rowRules: rules.map((rule, index) =>
        index === ruleIndex && rule.calculation
          ? {
              ...rule,
              calculation: update(rule.calculation, rule),
            }
          : rule,
      ),
    });

  return (
    <section
      className="clinical-form-row-rule-designer clinical-form-author-wide"
      aria-label={`Same-row rules for ${field.label}`}
    >
      <div className="clinical-form-author-heading">
        <div>
          <h5>Same-row rules and calculations</h5>
          <p className="cl-empty-text">
            Each rule can reference sibling children in its own row only.
            Cross-row lookup, aggregation, nested repeats, and scripts remain
            blocked.
          </p>
        </div>
        <button
          className="cl-btn-secondary"
          type="button"
          disabled={
            field.children.length < 2 ||
            rules.length >= clinicalFormRowRuleLimit
          }
          onClick={() => onChange(appendClinicalFormRowRule(field))}
        >
          <Plus size={14} aria-hidden="true" />
          Add row rule
        </button>
      </div>

      {rules.map((rule, ruleIndex) => (
        <article
          className="clinical-form-rule-editor"
          key={`${ruleIndex}-${rule.key}`}
        >
          <label className="cl-admin-field">
            <span>Row rule key</span>
            <input
              className="ne-input"
              value={rule.key}
              onChange={(event) =>
                updateRule(ruleIndex, { key: event.target.value })
              }
            />
          </label>
          <label className="cl-admin-field">
            <span>Sibling condition field</span>
            <select
              className="ne-input"
              value={rule.condition.fieldKey}
              onChange={(event) =>
                updateRule(ruleIndex, {
                  condition: {
                    ...rule.condition,
                    fieldKey: event.target.value,
                  },
                })
              }
            >
              {childKeys.map((key) => (
                <option key={key} value={key}>{key}</option>
              ))}
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Condition operator</span>
            <select
              className="ne-input"
              value={rule.condition.operator}
              onChange={(event) =>
                updateRule(ruleIndex, {
                  condition: {
                    ...rule.condition,
                    operator: event.target.value,
                    value: ["is-empty", "is-not-empty"].includes(
                      event.target.value,
                    )
                      ? undefined
                      : rule.condition.value,
                  },
                })
              }
            >
              {policy?.supportedConditionOperators.map((operator) => (
                <option key={operator} value={operator}>{operator}</option>
              ))}
            </select>
          </label>
          {!["is-empty", "is-not-empty"].includes(
            rule.condition.operator,
          ) && (
            <label className="cl-admin-field">
              <span>Condition value</span>
              <input
                className="ne-input"
                value={
                  typeof rule.condition.value === "string" ||
                  typeof rule.condition.value === "number" ||
                  typeof rule.condition.value === "boolean"
                    ? String(rule.condition.value)
                    : ""
                }
                onChange={(event) =>
                  updateRule(ruleIndex, {
                    condition: {
                      ...rule.condition,
                      value: parseConditionValue(
                        field.children.find(
                          (child) =>
                            child.key === rule.condition.fieldKey,
                        ),
                        event.target.value,
                      ),
                    },
                  })
                }
              />
            </label>
          )}
          <label className="cl-admin-field">
            <span>Row action</span>
            <select
              className="ne-input"
              value={rule.action}
              onChange={(event) =>
                onChange(
                  setClinicalFormRowRuleAction(
                    field,
                    ruleIndex,
                    event.target.value,
                    policy?.supportedCalculationOperators[0] ?? "sum",
                  ),
                )
              }
            >
              {policy?.supportedRuleActions.map((action) => (
                <option key={action} value={action}>{action}</option>
              ))}
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Sibling target field</span>
            <select
              className="ne-input"
              value={rule.targetFieldKey}
              onChange={(event) =>
                updateRule(ruleIndex, {
                  targetFieldKey: event.target.value,
                  calculation: rule.calculation
                    ? retargetCalculation(
                        rule.calculation,
                        field.children,
                        event.target.value,
                      )
                    : null,
                })
              }
            >
              {rule.action === "calculate" && computedKeys.length === 0 ? (
                <option value="">Add a computed child first</option>
              ) : null}
              {(rule.action === "calculate" ? computedKeys : childKeys).map(
                (key) => <option key={key} value={key}>{key}</option>,
              )}
            </select>
          </label>
          {rule.action === "warning" ? (
            <label className="cl-admin-field">
              <span>Row warning message</span>
              <input
                className="ne-input"
                value={rule.message ?? ""}
                onChange={(event) =>
                  updateRule(ruleIndex, { message: event.target.value })
                }
              />
            </label>
          ) : null}

          {rule.action === "calculate" && rule.calculation ? (
            <section
              className="clinical-form-calculation-editor clinical-form-author-wide"
              aria-label={`Same-row calculation for ${rule.key}`}
            >
              <label className="cl-admin-field">
                <span>Reusable calculation starter</span>
                <select
                  className="ne-input"
                  value=""
                  onChange={(event) => {
                    const template =
                      policy?.supportedCalculationTemplates.find(
                        (candidate) =>
                          candidate.key === event.target.value,
                      );
                    if (!template) return;
                    updateCalculation(ruleIndex, () =>
                      applyCalculationTemplate(
                        template,
                        field.children,
                        rule.targetFieldKey,
                      ),
                    );
                  }}
                >
                  <option value="">Choose a policy starter</option>
                  {policy?.supportedCalculationTemplates.map((template) => (
                    <option key={template.key} value={template.key}>
                      {template.title} — {template.description}
                    </option>
                  ))}
                </select>
              </label>
              <div className="clinical-form-calculation-grid">
                <label className="cl-admin-field">
                  <span>Calculation operator</span>
                  <select
                    className="ne-input"
                    value={rule.calculation.operator}
                    onChange={(event) =>
                      updateCalculation(ruleIndex, (calculation) =>
                        changeCalculationOperator(
                          calculation,
                          event.target.value,
                          field.children,
                          rule.targetFieldKey,
                        ),
                      )
                    }
                  >
                    {policy?.supportedCalculationOperators.map((operator) => (
                      <option key={operator} value={operator}>{operator}</option>
                    ))}
                  </select>
                </label>
                <label className="cl-admin-field">
                  <span>Result precision</span>
                  <input
                    className="ne-input"
                    type="number"
                    min="0"
                    max="8"
                    step="1"
                    value={rule.calculation.precision ?? ""}
                    onChange={(event) =>
                      updateCalculation(ruleIndex, (calculation) => ({
                        ...calculation,
                        precision:
                          event.target.value === ""
                            ? null
                            : Number(event.target.value),
                      }))
                    }
                  />
                </label>
              </div>
              <div className="clinical-form-calculation-operands">
                {rule.calculation.operands.map((operand, operandIndex) => {
                  const operandFieldKeys = calculationOperandFieldKeys(
                    field.children,
                    rule.targetFieldKey,
                  );
                  const sourceKind =
                    operand.fieldKey !== null ? "field" : "constant";
                  return (
                    <div
                      className="clinical-form-calculation-operand"
                      key={operandIndex}
                    >
                      <label className="cl-admin-field">
                        <span>Operand {operandIndex + 1} source</span>
                        <select
                          className="ne-input"
                          value={sourceKind}
                          onChange={(event) =>
                            updateCalculation(ruleIndex, (calculation) => ({
                              ...calculation,
                              operands: calculation.operands.map(
                                (current, index) =>
                                  index === operandIndex
                                    ? event.target.value === "field"
                                      ? {
                                          fieldKey:
                                            operandFieldKeys[0] ?? null,
                                          constant: null,
                                        }
                                      : { fieldKey: null, constant: 0 }
                                    : current,
                              ),
                            }))
                          }
                        >
                          <option value="field">Sibling numeric field</option>
                          <option value="constant">Constant</option>
                        </select>
                      </label>
                      {sourceKind === "field" ? (
                        <label className="cl-admin-field">
                          <span>Operand {operandIndex + 1} field</span>
                          <select
                            className="ne-input"
                            value={operand.fieldKey ?? ""}
                            onChange={(event) =>
                              updateCalculation(ruleIndex, (calculation) => ({
                                ...calculation,
                                operands: calculation.operands.map(
                                  (current, index) =>
                                    index === operandIndex
                                      ? {
                                          fieldKey:
                                            event.target.value || null,
                                          constant: null,
                                        }
                                      : current,
                                ),
                              }))
                            }
                          >
                            {operandFieldKeys.length === 0 ? (
                              <option value="">Add a numeric child first</option>
                            ) : null}
                            {operandFieldKeys.map((key) => (
                              <option key={key} value={key}>{key}</option>
                            ))}
                          </select>
                        </label>
                      ) : (
                        <label className="cl-admin-field">
                          <span>Operand {operandIndex + 1} constant</span>
                          <input
                            className="ne-input"
                            type="number"
                            step="any"
                            value={operand.constant ?? ""}
                            onChange={(event) =>
                              updateCalculation(ruleIndex, (calculation) => ({
                                ...calculation,
                                operands: calculation.operands.map(
                                  (current, index) =>
                                    index === operandIndex
                                      ? {
                                          fieldKey: null,
                                          constant:
                                            event.target.value === ""
                                              ? null
                                              : Number(event.target.value),
                                        }
                                      : current,
                                ),
                              }))
                            }
                          />
                        </label>
                      )}
                      {rule.calculation?.operator === "sum" &&
                      rule.calculation.operands.length > 1 ? (
                        <button
                          className="cl-btn-secondary"
                          type="button"
                          aria-label={`Remove row operand ${operandIndex + 1}`}
                          onClick={() =>
                            updateCalculation(ruleIndex, (calculation) => ({
                              ...calculation,
                              operands: calculation.operands.filter(
                                (_, index) => index !== operandIndex,
                              ),
                            }))
                          }
                        >
                          Remove
                        </button>
                      ) : null}
                    </div>
                  );
                })}
              </div>
              {rule.calculation.operator === "sum" ? (
                <button
                  className="cl-btn-secondary"
                  type="button"
                  disabled={rule.calculation.operands.length >= 20}
                  onClick={() =>
                    updateCalculation(ruleIndex, (calculation) =>
                      appendCalculationOperand(
                        calculation,
                        field.children,
                        rule.targetFieldKey,
                      ),
                    )
                  }
                >
                  <Plus size={14} aria-hidden="true" />
                  Add operand
                </button>
              ) : null}
            </section>
          ) : null}

          {issues
            .filter((issue) => issue.ruleKey === rule.key)
            .map((issue) => (
              <p
                className="clinical-form-calculation-issue clinical-form-author-wide"
                role="alert"
                key={issue.message}
              >
                {issue.message}
              </p>
            ))}
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() =>
              onChange({
                ...field,
                rowRules: rules.filter((_, index) => index !== ruleIndex),
              })
            }
          >
            Remove row rule
          </button>
        </article>
      ))}

      {issues
        .filter((issue) => issue.ruleKey === null)
        .map((issue) => (
          <p
            className="clinical-form-calculation-issue"
            role="alert"
            key={issue.message}
          >
            {issue.message}
          </p>
        ))}
    </section>
  );
}
