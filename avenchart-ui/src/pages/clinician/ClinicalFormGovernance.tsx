import { useEffect, useMemo, useState } from "react";
import {
  CircleCheckBig,
  FileCode2,
  History,
  Plus,
  ShieldAlert,
} from "lucide-react";
import {
  createClinicalFormDefinition,
  createClinicalFormRevision,
  getClinicalFormDefinition,
  getClinicalFormDefinitions,
  getClinicalFormPolicy,
  previewClinicalForm,
  transitionClinicalFormDefinition,
  type ClinicalFormDefinitionDetail,
  type ClinicalFormDefinitionSummary,
  type ClinicalFormEvaluation,
  type ClinicalFormField,
  type ClinicalFormPolicy,
  type ClinicalFormRule,
  type ClinicalFormSchema,
  type ClinicalFormSection,
} from "../../api/clinicalForms.ts";
import { showToast } from "../../components/Toast.tsx";
import {
  appendCalculationOperand,
  calculationAuthoringIssues,
  calculationOperandFieldKeys,
  calculationTargetFieldKeys,
  changeCalculationOperator,
  createDefaultCalculation,
  retargetCalculation,
} from "../../domain/clinicalFormCalculationAuthoring.ts";

type Props = { sessionId: string };

const safeField = (index = 1, sectionKey = "clinical"): ClinicalFormField => ({
  key: `field_${index}`,
  sectionKey,
  label: `Field ${index}`,
  type: "text",
  sequence: index * 10,
  required: false,
  accessibilityLabel: `Field ${index}`,
  helpText: null,
  maxLength: 240,
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
});

const emptySchema = (): ClinicalFormSchema => ({
  stableKey: "tmp.form.",
  name: "",
  purpose: "",
  contextScope: "encounter",
  owningService: "clinical_operations",
  capability: "encounters.auth_a",
  signaturePolicy: "author-only",
  sections: [
    {
      key: "clinical",
      title: "Clinical details",
      sequence: 10,
      description: "Bounded clinical facts.",
    },
  ],
  fields: [safeField()],
  rules: [],
});

function parseConditionValue(
  field: ClinicalFormField | undefined,
  rawValue: string,
): string | number | boolean {
  if (field?.type === "boolean") {
    const normalized = rawValue.trim().toLowerCase();
    if (normalized === "true") return true;
    if (normalized === "false") return false;
    return rawValue;
  }
  if (
    field &&
    ["integer", "decimal", "measurement", "computed"].includes(field.type) &&
    rawValue.trim() !== ""
  ) {
    const numericValue = Number(rawValue);
    return Number.isFinite(numericValue) ? numericValue : rawValue;
  }
  return rawValue;
}

function normalizeFieldForType(
  field: ClinicalFormField,
  type: string,
): ClinicalFormField {
  const numeric = ["integer", "decimal", "measurement", "computed"].includes(
    type,
  );
  const option = ["select", "multiselect", "coded"].includes(type);
  const repeat = type === "repeat";
  return {
    ...field,
    type,
    maxLength:
      type === "text" ? 240 : type === "multiline" ? 4000 : null,
    minimum: numeric ? (field.minimum ?? 0) : null,
    maximum: numeric ? (field.maximum ?? 100) : null,
    precision:
      type === "integer" ? 0 : numeric ? (field.precision ?? 2) : null,
    unit: type === "measurement" ? (field.unit ?? "unit") : null,
    codeSystem:
      type === "coded"
        ? (field.codeSystem ?? "local-code-system-v1")
        : option
          ? field.codeSystem
          : null,
    options: option
      ? field.options.length > 0
        ? field.options
        : [
            { code: "option_a", display: "Option A" },
            { code: "option_b", display: "Option B" },
          ]
      : [],
    repeatMinimum: repeat ? 0 : null,
    repeatMaximum: repeat ? 5 : null,
    children: repeat
      ? field.children.length > 0
        ? field.children
        : [
            {
              ...safeField(1, ""),
              key: `${field.key}_detail`,
              label: "Detail",
              accessibilityLabel: "Repeating row detail",
            },
          ]
      : [],
    readOnly: type === "computed",
    required: type === "computed" ? false : field.required,
  };
}

function actionsFor(status: string) {
  switch (status) {
    case "draft":
      return ["review", "retire"];
    case "in-review":
      return ["approve", "reject", "retire"];
    case "approved":
      return ["activate", "retire"];
    case "effective":
      return ["suspend", "retire"];
    case "suspended":
      return ["activate", "retire"];
    case "rejected":
      return ["retire"];
    default:
      return [];
  }
}

export default function ClinicalFormGovernance({ sessionId }: Props) {
  const [policy, setPolicy] = useState<ClinicalFormPolicy | null>(null);
  const [definitions, setDefinitions] = useState<
    ClinicalFormDefinitionSummary[]
  >([]);
  const [detail, setDetail] = useState<ClinicalFormDefinitionDetail | null>(
    null,
  );
  const [schema, setSchema] = useState<ClinicalFormSchema>(emptySchema);
  const [reason, setReason] = useState("");
  const [preview, setPreview] = useState<ClinicalFormEvaluation | null>(null);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successorMode, setSuccessorMode] = useState(false);

  async function refresh(
    selectedId?: string,
    nextStatus = status,
    nextSearch = search,
  ) {
    setError(null);
    try {
      const [nextPolicy, list] = await Promise.all([
        getClinicalFormPolicy(sessionId),
        getClinicalFormDefinitions(sessionId, {
          status: nextStatus || undefined,
          search: nextSearch || undefined,
          page: 1,
          pageSize: 100,
        }),
      ]);
      setPolicy(nextPolicy);
      setDefinitions(list.definitions);
      const targetId =
        selectedId ??
        detail?.definition.definitionId ??
        list.definitions[0]?.definitionId;
      if (targetId) {
        const nextDetail = await getClinicalFormDefinition(
          sessionId,
          targetId,
        );
        setDetail(nextDetail);
      } else {
        setDetail(null);
      }
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Clinical form governance could not be loaded.",
      );
    }
  }

  // The protected staff session is the authoritative initial-load boundary.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => void refresh(), [sessionId]);

  function updateSection(
    index: number,
    patch: Partial<ClinicalFormSection>,
  ) {
    setSchema((current) => ({
      ...current,
      sections: current.sections.map((section, sectionIndex) =>
        sectionIndex === index ? { ...section, ...patch } : section,
      ),
    }));
  }

  function updateField(index: number, patch: Partial<ClinicalFormField>) {
    setSchema((current) => ({
      ...current,
      fields: current.fields.map((field, fieldIndex) =>
        fieldIndex === index ? { ...field, ...patch } : field,
      ),
    }));
  }

  function updateRule(index: number, patch: Partial<ClinicalFormRule>) {
    setSchema((current) => ({
      ...current,
      rules: current.rules.map((rule, ruleIndex) =>
        ruleIndex === index ? { ...rule, ...patch } : rule,
      ),
    }));
  }

  function updateCalculation(
    index: number,
    update: (
      calculation: NonNullable<ClinicalFormRule["calculation"]>,
      rule: ClinicalFormRule,
      fields: ClinicalFormField[],
    ) => NonNullable<ClinicalFormRule["calculation"]>,
  ) {
    setSchema((current) => ({
      ...current,
      rules: current.rules.map((rule, ruleIndex) => {
        if (ruleIndex !== index || !rule.calculation) return rule;
        return {
          ...rule,
          calculation: update(rule.calculation, rule, current.fields),
        };
      }),
    }));
  }

  function setRuleAction(index: number, action: string) {
    setSchema((current) => ({
      ...current,
      rules: current.rules.map((rule, ruleIndex) => {
        if (ruleIndex !== index) return rule;
        if (action !== "calculate") {
          return {
            ...rule,
            action,
            message:
              action === "warning"
                ? rule.message || "Review this value."
                : null,
            calculation: null,
          };
        }

        const targetFieldKey =
          calculationTargetFieldKeys(current.fields)[0] ?? "";
        const operator =
          policy?.supportedCalculationOperators[0] ?? "sum";
        return {
          ...rule,
          action,
          targetFieldKey,
          message: null,
          calculation: createDefaultCalculation(
            current.fields,
            targetFieldKey,
            operator,
          ),
        };
      }),
    }));
  }

  function setRuleTarget(index: number, targetFieldKey: string) {
    setSchema((current) => ({
      ...current,
      rules: current.rules.map((rule, ruleIndex) =>
        ruleIndex === index
          ? {
              ...rule,
              targetFieldKey,
              calculation: rule.calculation
                ? retargetCalculation(
                    rule.calculation,
                    current.fields,
                    targetFieldKey,
                  )
                : null,
            }
          : rule,
      ),
    }));
  }

  function addSection() {
    setSchema((current) => {
      const index = current.sections.length + 1;
      return {
        ...current,
        sections: [
          ...current.sections,
          {
            key: `section_${index}`,
            title: `Section ${index}`,
            sequence: index * 10,
            description: null,
          },
        ],
      };
    });
  }

  function addField() {
    setSchema((current) => ({
      ...current,
      fields: [
        ...current.fields,
        safeField(
          current.fields.length + 1,
          current.sections[0]?.key ?? "clinical",
        ),
      ],
    }));
  }

  function addRule() {
    setSchema((current) => {
      const source = current.fields[0]?.key ?? "";
      const target = current.fields[1]?.key ?? source;
      return {
        ...current,
        rules: [
          ...current.rules,
          {
            key: `rule_${current.rules.length + 1}`,
            condition: {
              fieldKey: source,
              operator: "is-not-empty",
            },
            action: "warning",
            targetFieldKey: target,
            message: "Review this value.",
            calculation: null,
          },
        ],
      };
    });
  }

  function startNew() {
    setDetail(null);
    setSchema(emptySchema());
    setReason("");
    setPreview(null);
    setSuccessorMode(false);
  }

  function startSuccessor() {
    if (!detail) return;
    setSchema(structuredClone(detail.currentRevision.definition));
    setReason("");
    setPreview(null);
    setSuccessorMode(true);
  }

  async function runPreview() {
    setBusy(true);
    try {
      const result = await previewClinicalForm(sessionId, schema, {});
      setPreview(result);
      showToast(
        result.valid
          ? "Synthetic preview passed."
          : "Synthetic preview returned expected validation findings.",
        result.valid ? "success" : "error",
      );
    } catch (caught) {
      showToast(
        caught instanceof Error
          ? caught.message
          : "The typed schema is invalid.",
        "error",
      );
    } finally {
      setBusy(false);
    }
  }

  async function saveDefinition() {
    if (!reason.trim()) return;
    setBusy(true);
    try {
      const saved =
        successorMode && detail
          ? await createClinicalFormRevision(
              sessionId,
              detail.definition.definitionId,
              schema,
              detail.definition.latestRevision,
              reason.trim(),
            )
          : await createClinicalFormDefinition(
              sessionId,
              schema,
              reason.trim(),
            );
      setDetail(saved);
      setSchema(emptySchema());
      setReason("");
      setPreview(null);
      setSuccessorMode(false);
      await refresh(saved.definition.definitionId);
      showToast(
        successorMode
          ? "Immutable successor draft created."
          : "Governed form draft created.",
        "success",
      );
    } catch (caught) {
      showToast(
        caught instanceof Error
          ? caught.message
          : "The form definition could not be saved.",
        "error",
      );
    } finally {
      setBusy(false);
    }
  }

  async function transition(action: string) {
    if (!detail || !reason.trim()) return;
    setBusy(true);
    try {
      const current = detail.currentRevision;
      const next = await transitionClinicalFormDefinition(
        sessionId,
        detail.definition.definitionId,
        action,
        current.revision,
        current.version,
        reason.trim(),
        action === "activate" ? new Date().toISOString() : null,
        null,
      );
      setDetail(next);
      setReason("");
      await refresh(next.definition.definitionId);
      showToast(`Form revision ${action} recorded.`, "success");
    } catch (caught) {
      showToast(
        caught instanceof Error
          ? caught.message
          : "The lifecycle transition failed.",
        "error",
      );
    } finally {
      setBusy(false);
    }
  }

  const currentFieldKeys = useMemo(
    () => schema.fields.map((field) => field.key),
    [schema.fields],
  );
  const computedFieldKeys = useMemo(
    () => calculationTargetFieldKeys(schema.fields),
    [schema.fields],
  );
  const calculationIssues = useMemo(
    () =>
      calculationAuthoringIssues(
        schema.rules,
        schema.fields,
        policy?.supportedCalculationOperators ?? [],
      ),
    [policy?.supportedCalculationOperators, schema.fields, schema.rules],
  );

  return (
    <section className="clinical-form-governance">
      <div className="cl-card clinical-form-governance-hero">
        <div>
          <span className="report-governance-eyebrow">
            FORM-01/02 · safe definition runtime
          </span>
          <h2 className="cl-card-title">Governed clinical form engine</h2>
          <p>
            Build typed schemas through bounded controls, preview without
            persistence, advance immutable revisions through review and
            approval, and expose only the effective revision to new patient
            instances.
          </p>
        </div>
        <div className="clinical-form-policy-boundary">
          <ShieldAlert size={22} aria-hidden="true" />
          <strong>
            {policy?.productionSignatureStandardApproved
              ? "Production signature approved"
              : "Local signature policy only"}
          </strong>
          <span>
            Script {policy?.arbitraryScriptsAllowed ? "enabled" : "blocked"} ·
            HTML {policy?.rawHtmlAllowed ? "enabled" : "blocked"} · external
            fetch {policy?.externalFetchAllowed ? "enabled" : "blocked"}
          </span>
        </div>
      </div>

      {error && (
        <div className="error-banner" role="alert">
          {error}
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => void refresh()}
          >
            Retry
          </button>
        </div>
      )}

      {policy && (
        <details className="cl-card">
          <summary>Safe runtime and production blockers</summary>
          <div className="report-policy-flags">
            <span>{policy.revision}</span>
            <span>{policy.rendererVersion}</span>
            <span>{policy.signaturePolicyRevision}</span>
            <span>{policy.supportedFieldTypes.length} field types</span>
            <span>{policy.supportedRuleActions.length} rule actions</span>
            <span>
              {policy.supportedCalculationOperators.length} calculation
              operators
            </span>
          </div>
          <p>
            Forbidden: {policy.forbiddenCapabilities.join(", ")}.
          </p>
          <ol className="report-blocker-list">
            {policy.productionBlockers.map((blocker) => (
              <li key={blocker}>{blocker}</li>
            ))}
          </ol>
        </details>
      )}

      <div className="clinical-form-governance-layout">
        <section className="cl-card">
          <div className="cl-card-header-row">
            <div>
              <h3 className="cl-card-title">
                {successorMode
                  ? "Prepare successor revision"
                  : "Guided schema authoring"}
              </h3>
              <p className="cl-empty-text">
                Every control carries a stable key, type, accessibility label,
                bounds, and section. Declarative rules can only reference this
                schema.
              </p>
            </div>
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={startNew}
            >
              New definition
            </button>
          </div>

          <div className="clinical-form-author-grid">
            <label className="cl-admin-field">
              <span>Stable key</span>
              <input
                className="ne-input"
                value={schema.stableKey}
                disabled={successorMode}
                onChange={(event) =>
                  setSchema({ ...schema, stableKey: event.target.value })
                }
              />
            </label>
            <label className="cl-admin-field">
              <span>Name</span>
              <input
                className="ne-input"
                value={schema.name}
                onChange={(event) =>
                  setSchema({ ...schema, name: event.target.value })
                }
              />
            </label>
            <label className="cl-admin-field">
              <span>Context</span>
              <select
                className="ne-input"
                value={schema.contextScope}
                onChange={(event) =>
                  setSchema({
                    ...schema,
                    contextScope: event.target.value as "patient" | "encounter",
                  })
                }
              >
                <option value="encounter">Encounter</option>
                <option value="patient">Patient</option>
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Signature policy</span>
              <select
                className="ne-input"
                value={schema.signaturePolicy}
                onChange={(event) =>
                  setSchema({
                    ...schema,
                    signaturePolicy: event.target.value as
                      | "author-only"
                      | "author-and-cosigner",
                  })
                }
              >
                <option value="author-only">Author only</option>
                <option value="author-and-cosigner">
                  Author and distinct co-signer
                </option>
              </select>
            </label>
            <label className="cl-admin-field">
              <span>Owning service</span>
              <input
                className="ne-input"
                value={schema.owningService}
                onChange={(event) =>
                  setSchema({ ...schema, owningService: event.target.value })
                }
              />
            </label>
            <label className="cl-admin-field">
              <span>Required capability</span>
              <input
                className="ne-input"
                value={schema.capability}
                onChange={(event) =>
                  setSchema({ ...schema, capability: event.target.value })
                }
              />
            </label>
            <label className="cl-admin-field clinical-form-author-wide">
              <span>Clinical purpose</span>
              <textarea
                className="ne-input"
                value={schema.purpose}
                onChange={(event) =>
                  setSchema({ ...schema, purpose: event.target.value })
                }
              />
            </label>
          </div>

          <div className="clinical-form-author-heading">
            <h4>Sections</h4>
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={addSection}
            >
              <Plus size={14} aria-hidden="true" />
              Add section
            </button>
          </div>
          {schema.sections.map((section, index) => (
            <div className="clinical-form-section-editor" key={`${index}-${section.key}`}>
              <label className="cl-admin-field">
                <span>Key</span>
                <input
                  className="ne-input"
                  value={section.key}
                  onChange={(event) =>
                    updateSection(index, { key: event.target.value })
                  }
                />
              </label>
              <label className="cl-admin-field">
                <span>Title</span>
                <input
                  className="ne-input"
                  value={section.title}
                  onChange={(event) =>
                    updateSection(index, { title: event.target.value })
                  }
                />
              </label>
              <label className="cl-admin-field">
                <span>Sequence</span>
                <input
                  className="ne-input"
                  type="number"
                  value={section.sequence}
                  onChange={(event) =>
                    updateSection(index, {
                      sequence: Number(event.target.value),
                    })
                  }
                />
              </label>
              <label className="cl-admin-field">
                <span>Description</span>
                <input
                  className="ne-input"
                  value={section.description ?? ""}
                  onChange={(event) =>
                    updateSection(index, {
                      description: event.target.value || null,
                    })
                  }
                />
              </label>
            </div>
          ))}

          <div className="clinical-form-author-heading">
            <h4>Typed fields</h4>
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={addField}
            >
              <Plus size={14} aria-hidden="true" />
              Add field
            </button>
          </div>
          <div className="clinical-form-field-editors">
            {schema.fields.map((field, index) => (
              <article className="clinical-form-field-editor" key={`${index}-${field.key}`}>
                <div className="clinical-form-field-editor-title">
                  <strong>{field.label || field.key || `Field ${index + 1}`}</strong>
                  {schema.fields.length > 1 && (
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() =>
                        setSchema((current) => ({
                          ...current,
                          fields: current.fields.filter(
                            (_, fieldIndex) => fieldIndex !== index,
                          ),
                        }))
                      }
                    >
                      Remove
                    </button>
                  )}
                </div>
                <div className="clinical-form-field-editor-grid">
                  <label className="cl-admin-field">
                    <span>Key</span>
                    <input
                      className="ne-input"
                      value={field.key}
                      onChange={(event) =>
                        updateField(index, { key: event.target.value })
                      }
                    />
                  </label>
                  <label className="cl-admin-field">
                    <span>Label</span>
                    <input
                      className="ne-input"
                      value={field.label}
                      onChange={(event) =>
                        updateField(index, {
                          label: event.target.value,
                          accessibilityLabel:
                            field.accessibilityLabel === field.label
                              ? event.target.value
                              : field.accessibilityLabel,
                        })
                      }
                    />
                  </label>
                  <label className="cl-admin-field">
                    <span>Accessibility label</span>
                    <input
                      className="ne-input"
                      value={field.accessibilityLabel}
                      onChange={(event) =>
                        updateField(index, {
                          accessibilityLabel: event.target.value,
                        })
                      }
                    />
                  </label>
                  <label className="cl-admin-field">
                    <span>Section</span>
                    <select
                      className="ne-input"
                      value={field.sectionKey}
                      onChange={(event) =>
                        updateField(index, { sectionKey: event.target.value })
                      }
                    >
                      {schema.sections.map((section) => (
                        <option key={section.key} value={section.key}>
                          {section.title || section.key}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="cl-admin-field">
                    <span>Type</span>
                    <select
                      className="ne-input"
                      value={field.type}
                      onChange={(event) =>
                        updateField(
                          index,
                          normalizeFieldForType(field, event.target.value),
                        )
                      }
                    >
                      {policy?.supportedFieldTypes.map((type) => (
                        <option key={type} value={type}>
                          {type}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="cl-admin-field">
                    <span>Sequence</span>
                    <input
                      className="ne-input"
                      type="number"
                      value={field.sequence}
                      onChange={(event) =>
                        updateField(index, {
                          sequence: Number(event.target.value),
                        })
                      }
                    />
                  </label>
                  <label className="cl-admin-active-toggle">
                    <input
                      type="checkbox"
                      checked={field.required}
                      disabled={field.readOnly}
                      onChange={(event) =>
                        updateField(index, { required: event.target.checked })
                      }
                    />
                    <span>Required</span>
                  </label>
                  <label className="cl-admin-field">
                    <span>Help text</span>
                    <input
                      className="ne-input"
                      value={field.helpText ?? ""}
                      onChange={(event) =>
                        updateField(index, {
                          helpText: event.target.value || null,
                        })
                      }
                    />
                  </label>
                  {(field.type === "text" ||
                    field.type === "multiline") && (
                    <label className="cl-admin-field">
                      <span>Maximum length</span>
                      <input
                        className="ne-input"
                        type="number"
                        value={field.maxLength ?? ""}
                        onChange={(event) =>
                          updateField(index, {
                            maxLength: event.target.value
                              ? Number(event.target.value)
                              : null,
                          })
                        }
                      />
                    </label>
                  )}
                  {[
                    "integer",
                    "decimal",
                    "measurement",
                    "computed",
                  ].includes(field.type) && (
                    <>
                      <label className="cl-admin-field">
                        <span>Minimum</span>
                        <input
                          className="ne-input"
                          type="number"
                          value={field.minimum ?? ""}
                          onChange={(event) =>
                            updateField(index, {
                              minimum: event.target.value
                                ? Number(event.target.value)
                                : null,
                            })
                          }
                        />
                      </label>
                      <label className="cl-admin-field">
                        <span>Maximum</span>
                        <input
                          className="ne-input"
                          type="number"
                          value={field.maximum ?? ""}
                          onChange={(event) =>
                            updateField(index, {
                              maximum: event.target.value
                                ? Number(event.target.value)
                                : null,
                            })
                          }
                        />
                      </label>
                      <label className="cl-admin-field">
                        <span>Precision</span>
                        <input
                          className="ne-input"
                          type="number"
                          min="0"
                          max="8"
                          value={field.precision ?? ""}
                          onChange={(event) =>
                            updateField(index, {
                              precision: event.target.value
                                ? Number(event.target.value)
                                : null,
                            })
                          }
                        />
                      </label>
                    </>
                  )}
                  {field.type === "measurement" && (
                    <label className="cl-admin-field">
                      <span>Unit</span>
                      <input
                        className="ne-input"
                        value={field.unit ?? ""}
                        onChange={(event) =>
                          updateField(index, { unit: event.target.value })
                        }
                      />
                    </label>
                  )}
                  {["select", "multiselect", "coded"].includes(field.type) && (
                    <>
                      {field.type === "coded" && (
                        <label className="cl-admin-field">
                          <span>Code system</span>
                          <input
                            className="ne-input"
                            value={field.codeSystem ?? ""}
                            onChange={(event) =>
                              updateField(index, {
                                codeSystem: event.target.value,
                              })
                            }
                          />
                        </label>
                      )}
                      <label className="cl-admin-field clinical-form-author-wide">
                        <span>Options (one code|display per line)</span>
                        <textarea
                          className="ne-input"
                          value={field.options
                            .map(
                              (option) =>
                                `${option.code}|${option.display}`,
                            )
                            .join("\n")}
                          onChange={(event) =>
                            updateField(index, {
                              options: event.target.value
                                .split("\n")
                                .map((line) => line.trim())
                                .filter(Boolean)
                                .map((line) => {
                                  const [code, ...display] = line.split("|");
                                  return {
                                    code,
                                    display: display.join("|") || code,
                                  };
                                }),
                            })
                          }
                        />
                      </label>
                    </>
                  )}
                  {field.type === "repeat" && (
                    <>
                      <label className="cl-admin-field">
                        <span>Minimum rows</span>
                        <input
                          className="ne-input"
                          type="number"
                          min="0"
                          max="20"
                          value={field.repeatMinimum ?? 0}
                          onChange={(event) =>
                            updateField(index, {
                              repeatMinimum: Number(event.target.value),
                            })
                          }
                        />
                      </label>
                      <label className="cl-admin-field">
                        <span>Maximum rows</span>
                        <input
                          className="ne-input"
                          type="number"
                          min="1"
                          max="20"
                          value={field.repeatMaximum ?? 5}
                          onChange={(event) =>
                            updateField(index, {
                              repeatMaximum: Number(event.target.value),
                            })
                          }
                        />
                      </label>
                      <p className="cl-empty-text clinical-form-author-wide">
                        The guided baseline creates one bounded text child. The
                        server validates every child type and rejects nested
                        repeats.
                      </p>
                    </>
                  )}
                </div>
              </article>
            ))}
          </div>

          <div className="clinical-form-author-heading">
            <h4>Declarative rules</h4>
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={addRule}
            >
              <Plus size={14} aria-hidden="true" />
              Add rule
            </button>
          </div>
          {schema.rules.map((rule, index) => (
            <div className="clinical-form-rule-editor" key={`${index}-${rule.key}`}>
              <label className="cl-admin-field">
                <span>Rule key</span>
                <input
                  className="ne-input"
                  value={rule.key}
                  onChange={(event) =>
                    updateRule(index, { key: event.target.value })
                  }
                />
              </label>
              <label className="cl-admin-field">
                <span>Condition field</span>
                <select
                  className="ne-input"
                  value={rule.condition.fieldKey}
                  onChange={(event) =>
                    updateRule(index, {
                      condition: {
                        ...rule.condition,
                        fieldKey: event.target.value,
                      },
                    })
                  }
                >
                  {currentFieldKeys.map((key) => (
                    <option key={key} value={key}>
                      {key}
                    </option>
                  ))}
                </select>
              </label>
              <label className="cl-admin-field">
                <span>Operator</span>
                <select
                  className="ne-input"
                  value={rule.condition.operator}
                  onChange={(event) =>
                    updateRule(index, {
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
                    <option key={operator} value={operator}>
                      {operator}
                    </option>
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
                    type={
                      [
                        "integer",
                        "decimal",
                        "measurement",
                        "computed",
                      ].includes(
                        schema.fields.find(
                          (field) =>
                            field.key === rule.condition.fieldKey,
                        )?.type ?? "",
                      )
                        ? "number"
                        : "text"
                    }
                    placeholder={
                      schema.fields.find(
                        (field) => field.key === rule.condition.fieldKey,
                      )?.type === "boolean"
                        ? "true or false"
                        : undefined
                    }
                    value={
                      typeof rule.condition.value === "string" ||
                      typeof rule.condition.value === "number" ||
                      typeof rule.condition.value === "boolean"
                        ? String(rule.condition.value)
                        : ""
                    }
                    onChange={(event) =>
                      updateRule(index, {
                        condition: {
                          ...rule.condition,
                          value: parseConditionValue(
                            schema.fields.find(
                              (field) =>
                                field.key === rule.condition.fieldKey,
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
                <span>Action</span>
                <select
                  className="ne-input"
                  value={rule.action}
                  onChange={(event) =>
                    setRuleAction(index, event.target.value)
                  }
                >
                  {policy?.supportedRuleActions.map((action) => (
                    <option key={action} value={action}>
                      {action}
                    </option>
                  ))}
                </select>
              </label>
              <label className="cl-admin-field">
                <span>Target field</span>
                <select
                  className="ne-input"
                  value={rule.targetFieldKey}
                  onChange={(event) =>
                    setRuleTarget(index, event.target.value)
                  }
                >
                  {rule.action === "calculate" &&
                    computedFieldKeys.length === 0 && (
                      <option value="">Add a computed field first</option>
                    )}
                  {(rule.action === "calculate"
                    ? computedFieldKeys
                    : currentFieldKeys
                  ).map((key) => (
                    <option key={key} value={key}>
                      {key}
                    </option>
                  ))}
                </select>
              </label>
              {rule.action === "warning" && (
                <label className="cl-admin-field">
                  <span>Warning message</span>
                  <input
                    className="ne-input"
                    value={rule.message ?? ""}
                    onChange={(event) =>
                      updateRule(index, { message: event.target.value })
                    }
                  />
                </label>
              )}
              {rule.action === "calculate" && rule.calculation && (
                <section
                  className="clinical-form-calculation-editor clinical-form-author-wide"
                  aria-label={`Calculation for ${rule.key || `rule ${index + 1}`}`}
                >
                  <div className="clinical-form-calculation-grid">
                    <label className="cl-admin-field">
                      <span>Calculation operator</span>
                      <select
                        className="ne-input"
                        value={rule.calculation.operator}
                        onChange={(event) =>
                          updateCalculation(
                            index,
                            (calculation, currentRule, fields) =>
                              changeCalculationOperator(
                                calculation,
                                event.target.value,
                                fields,
                                currentRule.targetFieldKey,
                              ),
                          )
                        }
                      >
                        {policy?.supportedCalculationOperators.map(
                          (operator) => (
                            <option key={operator} value={operator}>
                              {operator}
                            </option>
                          ),
                        )}
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
                          updateCalculation(index, (calculation) => ({
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
                    {rule.calculation.operands.map(
                      (operand, operandIndex) => {
                        const operandFieldKeys =
                          calculationOperandFieldKeys(
                            schema.fields,
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
                                  updateCalculation(
                                    index,
                                    (calculation) => ({
                                      ...calculation,
                                      operands: calculation.operands.map(
                                        (currentOperand, currentIndex) =>
                                          currentIndex === operandIndex
                                            ? event.target.value === "field"
                                              ? {
                                                  fieldKey:
                                                    operandFieldKeys[0] ??
                                                    null,
                                                  constant: null,
                                                }
                                              : {
                                                  fieldKey: null,
                                                  constant: 0,
                                                }
                                            : currentOperand,
                                      ),
                                    }),
                                  )
                                }
                              >
                                <option value="field">Numeric field</option>
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
                                    updateCalculation(
                                      index,
                                      (calculation) => ({
                                        ...calculation,
                                        operands: calculation.operands.map(
                                          (currentOperand, currentIndex) =>
                                            currentIndex === operandIndex
                                              ? {
                                                  fieldKey:
                                                    event.target.value ||
                                                    null,
                                                  constant: null,
                                                }
                                              : currentOperand,
                                        ),
                                      }),
                                    )
                                  }
                                >
                                  {operandFieldKeys.length === 0 && (
                                    <option value="">
                                      Add a numeric field first
                                    </option>
                                  )}
                                  {operandFieldKeys.map((key) => (
                                    <option key={key} value={key}>
                                      {key}
                                    </option>
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
                                    updateCalculation(
                                      index,
                                      (calculation) => ({
                                        ...calculation,
                                        operands: calculation.operands.map(
                                          (currentOperand, currentIndex) =>
                                            currentIndex === operandIndex
                                              ? {
                                                  fieldKey: null,
                                                  constant:
                                                    event.target.value === ""
                                                      ? null
                                                      : Number(
                                                          event.target.value,
                                                        ),
                                                }
                                              : currentOperand,
                                        ),
                                      }),
                                    )
                                  }
                                />
                              </label>
                            )}
                            {rule.calculation?.operator === "sum" &&
                              rule.calculation.operands.length > 1 && (
                                <button
                                  className="cl-btn-secondary"
                                  type="button"
                                  aria-label={`Remove operand ${operandIndex + 1}`}
                                  onClick={() =>
                                    updateCalculation(
                                      index,
                                      (calculation) => ({
                                        ...calculation,
                                        operands:
                                          calculation.operands.filter(
                                            (_, currentIndex) =>
                                              currentIndex !== operandIndex,
                                          ),
                                      }),
                                    )
                                  }
                                >
                                  Remove
                                </button>
                              )}
                          </div>
                        );
                      },
                    )}
                  </div>
                  {rule.calculation.operator === "sum" && (
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      disabled={rule.calculation.operands.length >= 20}
                      onClick={() =>
                        updateCalculation(
                          index,
                          (calculation, currentRule, fields) =>
                            appendCalculationOperand(
                              calculation,
                              fields,
                              currentRule.targetFieldKey,
                            ),
                        )
                      }
                    >
                      <Plus size={14} aria-hidden="true" />
                      Add operand
                    </button>
                  )}
                  {calculationIssues
                    .filter((issue) => issue.ruleKey === rule.key)
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
              )}
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() =>
                  setSchema((current) => ({
                    ...current,
                    rules: current.rules.filter(
                      (_, ruleIndex) => ruleIndex !== index,
                    ),
                  }))
                }
              >
                Remove rule
              </button>
            </div>
          ))}
          {calculationIssues
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

          <label className="cl-admin-field clinical-form-action-reason">
            <span>Governance reason</span>
            <textarea
              className="ne-input"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </label>
          <div className="clinical-form-actions">
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={busy || calculationIssues.length > 0}
              onClick={() => void runPreview()}
            >
              <FileCode2 size={15} aria-hidden="true" />
              Synthetic preview
            </button>
            <button
              className="cl-btn-primary"
              type="button"
              disabled={
                busy || !reason.trim() || calculationIssues.length > 0
              }
              onClick={() => void saveDefinition()}
            >
              {successorMode ? "Create successor draft" : "Create draft"}
            </button>
          </div>
          {preview && (
            <section className="clinical-form-validation" aria-live="polite">
              <h4>Preview result</h4>
              <p>
                {preview.valid
                  ? "The empty synthetic fixture is valid."
                  : `${preview.issues.length} validation or warning finding(s).`}
              </p>
              <ul>
                {preview.issues.map((issue, index) => (
                  <li key={`${issue.fieldKey}-${index}`}>
                    {issue.fieldKey}: {issue.message}
                  </li>
                ))}
              </ul>
            </section>
          )}
        </section>

        <aside>
          <section className="cl-card">
            <div className="cl-card-header-row">
              <h3 className="cl-card-title">Definition catalog</h3>
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={busy}
                onClick={() => void refresh()}
              >
                Refresh
              </button>
            </div>
            <div className="report-definition-filters clinical-form-governance-filters">
              <label className="cl-admin-field">
                <span>Search</span>
                <input
                  className="ne-input"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </label>
              <label className="cl-admin-field">
                <span>Status</span>
                <select
                  className="ne-input"
                  value={status}
                  onChange={(event) => setStatus(event.target.value)}
                >
                  <option value="">All</option>
                  {policy?.definitionStates.map((state) => (
                    <option key={state} value={state}>
                      {state}
                    </option>
                  ))}
                </select>
              </label>
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() => void refresh(undefined, status, search)}
              >
                Apply
              </button>
            </div>
            <div className="clinical-form-instance-list">
              {definitions.map((definition) => (
                <button
                  className={`clinical-form-instance-link${
                    detail?.definition.definitionId === definition.definitionId
                      ? " is-selected"
                      : ""
                  }`}
                  key={definition.definitionId}
                  type="button"
                  onClick={() =>
                    void getClinicalFormDefinition(
                      sessionId,
                      definition.definitionId,
                    ).then(setDetail)
                  }
                >
                  <strong>{definition.name}</strong>
                  <span>
                    {definition.stableKey} · r{definition.latestRevision} ·{" "}
                    {definition.latestStatus}
                  </span>
                  <small>
                    Effective revision {definition.effectiveRevision ?? "none"}
                  </small>
                </button>
              ))}
              {definitions.length === 0 && (
                <p className="cl-empty-text">No definitions match.</p>
              )}
            </div>
          </section>

          {detail && (
            <section className="cl-card clinical-form-definition-detail">
              <div className="cl-card-header-row">
                <div>
                  <h3 className="cl-card-title">
                    {detail.currentRevision.definition.name}
                  </h3>
                  <p className="cl-empty-text">
                    <code>{detail.definition.stableKey}</code>
                  </p>
                </div>
                <span
                  className={`report-definition-status is-${detail.currentRevision.status}`}
                >
                  {detail.currentRevision.status}
                </span>
              </div>
              <dl className="clinical-form-facts">
                <div>
                  <dt>Revision</dt>
                  <dd>{detail.currentRevision.revision}</dd>
                </div>
                <div>
                  <dt>Version</dt>
                  <dd>{detail.currentRevision.version}</dd>
                </div>
                <div>
                  <dt>Author</dt>
                  <dd>{detail.currentRevision.author}</dd>
                </div>
                <div>
                  <dt>Reviewer</dt>
                  <dd>{detail.currentRevision.reviewedBy ?? "Pending"}</dd>
                </div>
                <div>
                  <dt>Approver</dt>
                  <dd>{detail.currentRevision.approvedBy ?? "Pending"}</dd>
                </div>
                <div>
                  <dt>Schema SHA-256</dt>
                  <dd>
                    <code>{detail.currentRevision.schemaHash}</code>
                  </dd>
                </div>
              </dl>
              <label className="cl-admin-field">
                <span>Lifecycle reason</span>
                <textarea
                  className="ne-input"
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                />
              </label>
              <div className="clinical-form-actions">
                {actionsFor(detail.currentRevision.status).map((action) => (
                  <button
                    key={action}
                    className={
                      action === "activate"
                        ? "cl-btn-primary"
                        : "cl-btn-secondary"
                    }
                    type="button"
                    disabled={busy || !reason.trim()}
                    onClick={() => void transition(action)}
                  >
                    {action}
                  </button>
                ))}
                <button
                  className="cl-btn-secondary"
                  type="button"
                  disabled={busy}
                  onClick={startSuccessor}
                >
                  Prepare successor
                </button>
              </div>

              <h4>Immutable revisions</h4>
              <ul className="report-event-list">
                {detail.revisions.map((revision) => (
                  <li key={revision.revision}>
                    <div>
                      <strong>
                        Revision {revision.revision} · {revision.status}
                      </strong>
                      <span>v{revision.version}</span>
                    </div>
                    <p>
                      {revision.definition.fields.length} fields ·{" "}
                      {revision.definition.rules.length} rules ·{" "}
                      {revision.rendererVersion}
                    </p>
                    <code>{revision.schemaHash}</code>
                  </li>
                ))}
              </ul>

              <h4>
                <History size={16} aria-hidden="true" /> Lifecycle evidence
              </h4>
              <ul className="report-event-list">
                {detail.events.map((event) => (
                  <li key={event.eventId}>
                    <div>
                      <strong>
                        {event.action} · revision {event.revision}
                      </strong>
                      <span>
                        {event.actor} ·{" "}
                        {new Date(event.occurredAt).toLocaleString()}
                      </span>
                    </div>
                    <p>{event.reason}</p>
                    <code>{event.snapshotHash}</code>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </aside>
      </div>
      <div className="clinical-form-local-complete">
        <CircleCheckBig size={18} aria-hidden="true" />
        Definition previews are read-only and write no patient facts. New
        patient instances can resolve only a currently effective revision.
      </div>
    </section>
  );
}
