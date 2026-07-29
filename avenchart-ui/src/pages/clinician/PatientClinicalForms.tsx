import { useEffect, useEffectEvent, useState, type ReactNode } from "react";
import { useOutletContext } from "react-router-dom";
import {
  amendClinicalFormInstance,
  createPatientClinicalFormInstance,
  exportClinicalFormInstanceHtml,
  exportClinicalFormInstanceStructured,
  getClinicalFormCatalog,
  getClinicalFormInstance,
  getPatientClinicalFormInstances,
  previewClinicalForm,
  transitionClinicalFormInstance,
  updateClinicalFormInstance,
  type ClinicalFormDefinitionSummary,
  type ClinicalFormField,
  type ClinicalFormInstanceDetail,
  type ClinicalFormInstanceSummary,
} from "../../api/clinicalForms.ts";
import { searchEncounters, type EncounterListItem } from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { PatientOutletContext } from "./PatientShell.tsx";

type RecordValue = Record<string, unknown>;

function newIdempotencyKey(prefix: string) {
  return `${prefix}-${crypto.randomUUID()}`;
}

function formatInstant(value: string | null) {
  if (!value) return "Not recorded";
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

function textValue(value: unknown) {
  return typeof value === "string" || typeof value === "number" ? String(value) : "";
}

function numericValue(value: unknown) {
  return typeof value === "number" || typeof value === "string" ? String(value) : "";
}

function objectValue(value: unknown): RecordValue {
  return value !== null && typeof value === "object" && !Array.isArray(value)
    ? (value as RecordValue)
    : {};
}

function downloadJson(filename: string, value: unknown) {
  const url = URL.createObjectURL(new Blob([JSON.stringify(value, null, 2)], {
    type: "application/json",
  }));
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

type FieldInputProps = {
  field: ClinicalFormField;
  value: unknown;
  required: boolean;
  disabled: boolean;
  issue?: string;
  onChange: (value: unknown) => void;
};

function FieldInput({ field, value, required, disabled, issue, onChange }: FieldInputProps) {
  const inputId = `clinical-form-${field.key}`;
  const helpId = `${inputId}-help`;
  const common = {
    id: inputId,
    disabled,
    required,
    "aria-label": field.accessibilityLabel,
    "aria-describedby": field.helpText || issue ? helpId : undefined,
  };

  if (field.type === "repeat") {
    const rows = Array.isArray(value)
      ? value.filter((row): row is RecordValue => row !== null && typeof row === "object" && !Array.isArray(row))
      : [];
    const maximum = field.repeatMaximum ?? 10;
    return (
      <fieldset className="cl-fieldset" disabled={disabled}>
        <legend>{field.label}{required ? " *" : ""}</legend>
        {field.helpText ? <p id={helpId} className="cl-field-help">{field.helpText}</p> : null}
        {rows.map((row, index) => (
          <div className="cl-card" key={`${field.key}-${index}`} style={{ marginBottom: 12 }}>
            <div className="section-heading">
              <h4>Entry {index + 1}</h4>
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() => onChange(rows.filter((_, rowIndex) => rowIndex !== index))}
              >
                Remove entry
              </button>
            </div>
            {field.children.map((child) => (
              <div className="cl-form-field" key={child.key}>
                <FieldInput
                  field={child}
                  value={row[child.key]}
                  required={child.required}
                  disabled={disabled}
                  onChange={(next) => {
                    const nextRows = rows.map((current, rowIndex) =>
                      rowIndex === index ? { ...current, [child.key]: next } : current,
                    );
                    onChange(nextRows);
                  }}
                />
              </div>
            ))}
          </div>
        ))}
        <button
          className="cl-btn-secondary"
          type="button"
          disabled={disabled || rows.length >= maximum}
          onClick={() => onChange([...rows, {}])}
        >
          Add entry
        </button>
        <p className="cl-field-help">{rows.length} of {maximum} permitted entries.</p>
        {issue ? <p className="form-error" role="alert">{issue}</p> : null}
      </fieldset>
    );
  }

  let input: ReactNode;
  switch (field.type) {
    case "multiline":
      input = <textarea {...common} value={textValue(value)} maxLength={field.maxLength ?? undefined} onChange={(event) => onChange(event.target.value)} rows={4} />;
      break;
    case "integer":
    case "decimal":
      input = <input {...common} type="number" value={numericValue(value)} min={field.minimum ?? undefined} max={field.maximum ?? undefined} step={field.type === "integer" ? 1 : field.precision === null ? "any" : 10 ** -(field.precision ?? 2)} onChange={(event) => onChange(event.target.value === "" ? undefined : Number(event.target.value))} />;
      break;
    case "measurement": {
      const measurement = objectValue(value);
      input = (
        <div className="cl-inline-fields">
          <input {...common} type="number" value={numericValue(measurement.value)} min={field.minimum ?? undefined} max={field.maximum ?? undefined} step={field.precision === null ? "any" : 10 ** -(field.precision ?? 2)} onChange={(event) => onChange(event.target.value === "" ? undefined : { value: Number(event.target.value), unit: field.unit })} />
          <span aria-label={`Unit ${field.unit ?? ""}`}>{field.unit}</span>
        </div>
      );
      break;
    }
    case "date":
      input = <input {...common} type="date" value={textValue(value)} onChange={(event) => onChange(event.target.value || undefined)} />;
      break;
    case "datetime":
      input = <input {...common} type="datetime-local" value={textValue(value).slice(0, 16)} onChange={(event) => onChange(event.target.value ? new Date(event.target.value).toISOString() : undefined)} />;
      break;
    case "boolean":
      input = <input {...common} type="checkbox" checked={value === true} onChange={(event) => onChange(event.target.checked)} />;
      break;
    case "select":
    case "coded":
      input = (
        <select {...common} value={textValue(value)} onChange={(event) => onChange(event.target.value || undefined)}>
          <option value="">Select an option</option>
          {field.options.map((option) => <option key={option.code} value={option.code}>{option.display}</option>)}
        </select>
      );
      break;
    case "multiselect": {
      const selected = new Set(Array.isArray(value) ? value.filter((item): item is string => typeof item === "string") : []);
      input = (
        <div className="checkbox-list" role="group" aria-label={field.accessibilityLabel}>
          {field.options.map((option) => (
            <label key={option.code}>
              <input
                type="checkbox"
                disabled={disabled}
                checked={selected.has(option.code)}
                onChange={(event) => {
                  const next = new Set(selected);
                  if (event.target.checked) {
                    next.add(option.code);
                  } else {
                    next.delete(option.code);
                  }
                  onChange([...next]);
                }}
              />
              {option.display}
            </label>
          ))}
        </div>
      );
      break;
    }
    case "computed":
      input = <output id={inputId}>{numericValue(value) || "Calculated when its rule applies."}</output>;
      break;
    default:
      input = <input {...common} type="text" value={textValue(value)} maxLength={field.maxLength ?? undefined} onChange={(event) => onChange(event.target.value)} />;
  }

  return (
    <>
      <label htmlFor={inputId}>{field.label}{required ? " *" : ""}</label>
      {field.helpText || issue ? <p id={helpId} className="cl-field-help">{field.helpText}{issue ? ` ${issue}` : ""}</p> : null}
      {input}
    </>
  );
}

export default function PatientClinicalForms() {
  const { patient, patientId, session } = useOutletContext<PatientOutletContext>();
  const [catalog, setCatalog] = useState<ClinicalFormDefinitionSummary[]>([]);
  const [encounters, setEncounters] = useState<EncounterListItem[]>([]);
  const [encounterId, setEncounterId] = useState("");
  const [instances, setInstances] = useState<ClinicalFormInstanceSummary[]>([]);
  const [selected, setSelected] = useState<ClinicalFormInstanceDetail | null>(null);
  const [values, setValues] = useState<RecordValue>({});
  const [reason, setReason] = useState("Clinical form entry");
  const [actionReason, setActionReason] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  async function refresh(selectId?: string) {
    setLoading(true);
    setError("");
    try {
      const [loadedCatalog, loadedInstances, loadedEncounters] = await Promise.all([
        getClinicalFormCatalog(session.sessionId),
        getPatientClinicalFormInstances(session.sessionId, patientId),
        searchEncounters(session.sessionId, { patientId, limit: 100 }),
      ]);
      setCatalog(loadedCatalog.definitions);
      setInstances(loadedInstances.instances);
      setEncounters(loadedEncounters.encounters);
      setEncounterId((current) => current || (loadedEncounters.encounters[0]?.id ? String(loadedEncounters.encounters[0].id) : ""));
      const instanceId = selectId ?? selected?.instance.instanceId;
      if (instanceId) {
        const detail = await getClinicalFormInstance(session.sessionId, instanceId);
        setSelected(detail);
        setValues(detail.values);
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not load clinical forms.");
    } finally {
      setLoading(false);
    }
  }

  const refreshEvent = useEffectEvent(refresh);
  useEffect(() => {
    void refreshEvent();
  }, [patientId, session.sessionId]);

  function setFieldValue(key: string, value: unknown) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  async function startForm(definition: ClinicalFormDefinitionSummary) {
    if (reason.trim().length < 3) {
      setError("Provide a reason of at least three characters before starting a form.");
      return;
    }
    const selectedEncounterId = encounterId ? Number(encounterId) : null;
    if (definition.contextScope === "encounter" && !selectedEncounterId) {
      setError("Select a patient encounter before starting this encounter-scoped form.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      const created = await createPatientClinicalFormInstance(session.sessionId, patientId, {
        definitionId: definition.definitionId,
        encounterId: definition.contextScope === "encounter" ? selectedEncounterId : null,
        idempotencyKey: newIdempotencyKey("clinical-form"),
        reason: reason.trim(),
        values: {},
      });
      setSelected(created);
      setValues(created.values);
      await refresh(created.instance.instanceId);
      showToast("Clinical form draft started.", "success");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not start the clinical form.");
    } finally {
      setSaving(false);
    }
  }

  async function validateDraft() {
    if (!selected) return;
    setSaving(true);
    setError("");
    try {
      const evaluation = await previewClinicalForm(session.sessionId, selected.definition, values);
      setSelected((current) => current ? { ...current, validation: evaluation } : current);
      showToast(evaluation.valid ? "The form is valid." : "Review the form validation messages.", evaluation.valid ? "success" : "error");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not validate the clinical form.");
    } finally {
      setSaving(false);
    }
  }

  async function saveDraft() {
    if (!selected) return;
    if (reason.trim().length < 3) {
      setError("Provide a reason of at least three characters before saving.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      const updated = await updateClinicalFormInstance(session.sessionId, selected.instance.instanceId, selected.instance.version, values, reason.trim());
      setSelected(updated);
      setValues(updated.values);
      await refresh(updated.instance.instanceId);
      showToast("Clinical form draft saved.", "success");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not save the clinical form.");
    } finally {
      setSaving(false);
    }
  }

  async function transition(action: "finalize" | "sign" | "cosign") {
    if (!selected) return;
    if (actionReason.trim().length < 3) {
      setError("Provide a reason of at least three characters for this clinical transition.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      const updated = await transitionClinicalFormInstance(session.sessionId, selected.instance.instanceId, action, selected.instance.version, actionReason.trim());
      setSelected(updated);
      setValues(updated.values);
      setActionReason("");
      await refresh(updated.instance.instanceId);
      showToast(`Clinical form ${action === "cosign" ? "co-signed" : `${action}d`}.`, "success");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not transition the clinical form.");
    } finally {
      setSaving(false);
    }
  }

  async function amend() {
    if (!selected) return;
    if (actionReason.trim().length < 3) {
      setError("Provide a reason of at least three characters for the amendment.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      const amended = await amendClinicalFormInstance(session.sessionId, selected.instance.instanceId, selected.instance.version, actionReason.trim(), newIdempotencyKey("clinical-amendment"));
      setSelected(amended);
      setValues(amended.values);
      setActionReason("");
      await refresh(amended.instance.instanceId);
      showToast("Reasoned successor amendment started.", "success");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not create the amendment.");
    } finally {
      setSaving(false);
    }
  }

  async function downloadStructuredRecord() {
    if (!selected) return;
    setSaving(true);
    setError("");
    try {
      const exported = await exportClinicalFormInstanceStructured(
        session.sessionId,
        selected.instance.instanceId,
      );
      downloadJson(
        `${exported.instance.stableKey}-r${exported.instance.definitionRevision}-${exported.instance.instanceId}.json`,
        exported,
      );
      showToast("Revision-labeled structured clinical record downloaded.", "success");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not export the structured clinical record.");
    } finally {
      setSaving(false);
    }
  }

  async function openPrintableRecord() {
    if (!selected) return;
    setSaving(true);
    setError("");
    try {
      const html = await exportClinicalFormInstanceHtml(
        session.sessionId,
        selected.instance.instanceId,
      );
      const url = URL.createObjectURL(new Blob([html], { type: "text/html" }));
      window.open(url, "_blank", "noopener,noreferrer");
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not open the printable clinical record.");
    } finally {
      setSaving(false);
    }
  }

  const issueByField = new Map((selected?.validation.issues ?? []).map((issue) => [issue.fieldKey, issue.message]));
  const draft = selected?.instance.state === "draft";

  return (
    <section className="clinician-page" aria-labelledby="clinical-forms-heading">
      <header className="page-header">
        <div>
          <p className="eyebrow">Patient visit forms</p>
          <h1 id="clinical-forms-heading">Clinical forms for {patient.displayName}</h1>
          <p className="page-subtitle">Revision-pinned, typed clinical capture. Each finalization, signature, and amendment remains in protected history.</p>
        </div>
        <button className="cl-btn-secondary" type="button" onClick={() => void refresh()} disabled={loading || saving}>Refresh</button>
      </header>

      {error ? <div className="error-banner" role="alert">{error}</div> : null}

      <section className="cl-card" aria-labelledby="start-clinical-form-heading">
        <h2 id="start-clinical-form-heading">Start an effective form</h2>
        <div className="cl-form-field">
          <label htmlFor="clinical-form-start-reason">Reason</label>
          <input id="clinical-form-start-reason" value={reason} maxLength={500} onChange={(event) => setReason(event.target.value)} disabled={saving} />
        </div>
        <div className="cl-form-field">
          <label htmlFor="clinical-form-encounter">Encounter for encounter-scoped forms</label>
          <select id="clinical-form-encounter" value={encounterId} onChange={(event) => setEncounterId(event.target.value)} disabled={saving}>
            <option value="">Select an encounter</option>
            {encounters.map((encounter) => <option key={encounter.id} value={encounter.id}>{encounter.date} â€” {encounter.reason || `Encounter ${encounter.encounter}`}</option>)}
          </select>
          <p className="cl-field-help">Patient-scoped forms ignore this selection. Encounter-scoped forms require it.</p>
        </div>
        {catalog.length === 0 ? <p className="cl-empty-text">No effective clinical forms are currently available.</p> : (
          <div className="card-grid">
            {catalog.map((definition) => (
              <article className="cl-card" key={definition.definitionId}>
                <h3>{definition.name}</h3>
                <p>{definition.purpose}</p>
                <dl className="facts-list">
                  <div><dt>Revision</dt><dd>{definition.effectiveRevision ?? definition.latestRevision}</dd></div>
                  <div><dt>Scope</dt><dd>{definition.contextScope}</dd></div>
                  <div><dt>Signature</dt><dd>{definition.signaturePolicy}</dd></div>
                </dl>
                <button className="cl-btn-primary" type="button" disabled={saving || (definition.contextScope === "encounter" && !encounterId)} onClick={() => void startForm(definition)}>Start draft</button>
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="cl-card" aria-labelledby="clinical-form-history-heading">
        <h2 id="clinical-form-history-heading">Patient form history</h2>
        {loading ? <p>Loading clinical formsâ€¦</p> : instances.length === 0 ? <p className="cl-empty-text">No clinical form instances have been started for this patient.</p> : (
          <div className="table-scroll"><table><thead><tr><th>Form</th><th>State</th><th>Revision</th><th>Author</th><th>Updated</th><th /></tr></thead><tbody>
            {instances.map((instance) => <tr key={instance.instanceId}><td>{instance.name}</td><td>{instance.state}</td><td>{instance.definitionRevision}</td><td>{instance.author}</td><td>{formatInstant(instance.updatedAt)}</td><td><button className="cl-btn-secondary" type="button" onClick={() => void refresh(instance.instanceId)} disabled={saving}>Open</button></td></tr>)}
          </tbody></table></div>
        )}
      </section>

      {selected ? (
        <section className="cl-card" aria-labelledby="selected-clinical-form-heading">
          <div className="section-heading"><div><p className="eyebrow">{selected.instance.state}</p><h2 id="selected-clinical-form-heading">{selected.instance.name} <span className="muted">revision {selected.instance.definitionRevision}</span></h2></div><p>Author: {selected.instance.author}</p></div>
          <p>{selected.definition.purpose}</p>
          {selected.validation.issues.length > 0 ? <div className="hint-banner" role="status"><strong>Validation</strong><ul>{selected.validation.issues.map((issue) => <li key={`${issue.fieldKey}-${issue.message}`}>{issue.message}</li>)}</ul></div> : null}
          {selected.definition.sections.map((section) => <fieldset className="cl-fieldset" key={section.key}><legend>{section.title}</legend>{section.description ? <p className="cl-field-help">{section.description}</p> : null}{selected.definition.fields.filter((field) => field.sectionKey === section.key && selected.validation.visibleFields[field.key] !== false).map((field) => <div className="cl-form-field" key={field.key}><FieldInput field={field} value={values[field.key]} required={selected.validation.requiredFields[field.key] ?? field.required} disabled={!draft} issue={issueByField.get(field.key)} onChange={(value) => setFieldValue(field.key, value)} /></div>)}</fieldset>)}
          <div className="cl-form-field"><label htmlFor="clinical-form-mutation-reason">Draft save reason / transition reason</label><input id="clinical-form-mutation-reason" value={draft ? reason : actionReason} maxLength={500} disabled={saving} onChange={(event) => draft ? setReason(event.target.value) : setActionReason(event.target.value)} /></div>
          <div className="page-actions">
            <button className="cl-btn-secondary" type="button" onClick={() => void openPrintableRecord()} disabled={saving}>Open printable record</button>
            <button className="cl-btn-secondary" type="button" onClick={() => void downloadStructuredRecord()} disabled={saving}>Download structured record</button>
            {draft ? <><button className="cl-btn-secondary" type="button" onClick={() => void validateDraft()} disabled={saving}>Validate</button><button className="cl-btn-primary" type="button" onClick={() => void saveDraft()} disabled={saving}>Save draft</button><button className="cl-btn-primary" type="button" onClick={() => void transition("finalize")} disabled={saving}>Finalize</button></> : null}
            {selected.instance.state === "ready-for-signature" ? <button className="cl-btn-primary" type="button" onClick={() => void transition("sign")} disabled={saving}>Sign</button> : null}
            {selected.instance.state === "awaiting-co-sign" ? <button className="cl-btn-primary" type="button" onClick={() => void transition("cosign")} disabled={saving}>Co-sign</button> : null}
            {selected.instance.state === "signed" ? <button className="cl-btn-secondary" type="button" onClick={() => void amend()} disabled={saving}>Create amendment</button> : null}
          </div>
          <h3>Protected event history</h3>
          <div className="table-scroll"><table><thead><tr><th>Action</th><th>State</th><th>Actor</th><th>Reason</th><th>When</th></tr></thead><tbody>{selected.events.map((event) => <tr key={event.eventId}><td>{event.action}</td><td>{event.fromState ?? "â€”"} â†’ {event.toState}</td><td>{event.actor}</td><td>{event.reason}</td><td>{formatInstant(event.occurredAt)}</td></tr>)}</tbody></table></div>
          {selected.signatures.length > 0 ? <><h3>Signatures</h3><ul className="history-list">{selected.signatures.map((signature) => <li key={signature.signatureId}><strong>{signature.role}</strong> by {signature.signer} at {formatInstant(signature.signedAt)}.</li>)}</ul></> : null}
        </section>
      ) : null}
    </section>
  );
}
