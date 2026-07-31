// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useMemo, useState } from "react";
import {
  ClipboardPlus,
  Plus,
  ReceiptText,
  TestTubeDiagonal,
} from "lucide-react";
import {
  createProcedureOrder,
  getEncounterDetail,
  getProcedureOrderCatalog,
  type EncounterDetail,
  type ProcedureOrderCatalogItem,
} from "../../api.ts";
import {
  asEncounterCodingDetail,
  createEncounterBillingLine,
} from "../../api/encounterCoding.ts";
import { showToast } from "../../components/Toast.tsx";

type BillingMode = "diagnosis" | "charge";

type BillingDraft = {
  mode: BillingMode;
  codeType: string;
  code: string;
  description: string;
  modifier: string;
  fee: string;
  units: string;
  diagnosis: string;
};

type OrderDraft = {
  catalogItemId: string;
  priority: string;
  diagnosis: string;
  instructions: string;
};

const BLANK_BILLING_DRAFT: BillingDraft = {
  mode: "charge",
  codeType: "CPT",
  code: "",
  description: "",
  modifier: "",
  fee: "",
  units: "1",
  diagnosis: "",
};

const BLANK_ORDER_DRAFT: OrderDraft = {
  catalogItemId: "",
  priority: "routine",
  diagnosis: "",
  instructions: "",
};

function normalizeCode(value: string) {
  return value.trim().toUpperCase();
}

function validCode(value: string) {
  return /^[A-Z0-9][A-Z0-9.-]{1,19}$/.test(normalizeCode(value));
}

function displayMoney(value?: number | null) {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(value ?? 0);
}

export default function EncounterCodingPanel({
  sessionId,
  detail,
  onDetailChange,
}: {
  sessionId: string;
  detail: EncounterDetail;
  onDetailChange: (detail: EncounterDetail) => void;
}) {
  const codingDetail = asEncounterCodingDetail(detail);
  const [billingOpen, setBillingOpen] = useState(false);
  const [orderOpen, setOrderOpen] = useState(false);
  const [billingDraft, setBillingDraft] = useState(BLANK_BILLING_DRAFT);
  const [orderDraft, setOrderDraft] = useState(BLANK_ORDER_DRAFT);
  const [catalog, setCatalog] = useState<ProcedureOrderCatalogItem[]>([]);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [billingError, setBillingError] = useState<string | null>(null);
  const [orderError, setOrderError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const activeOrderItems = useMemo(
    () =>
      catalog.filter(
        (item) => item.active && item.itemType === "ord" && Boolean(item.code),
      ),
    [catalog],
  );

  useEffect(() => {
    let cancelled = false;
    getProcedureOrderCatalog(sessionId)
      .then((response) => {
        if (!cancelled) setCatalog(response.items);
      })
      .catch(() => {
        if (!cancelled) {
          setCatalogError(
            "The procedure catalog is unavailable. Existing linked orders remain visible.",
          );
        }
      });
    return () => {
      cancelled = true;
    };
  }, [sessionId]);

  useEffect(() => {
    setBillingOpen(false);
    setOrderOpen(false);
    setBillingDraft(BLANK_BILLING_DRAFT);
    setOrderDraft(BLANK_ORDER_DRAFT);
    setBillingError(null);
    setOrderError(null);
  }, [detail.encounter]);

  useEffect(() => {
    const firstDiagnosis = detail.diagnosisCodes[0]?.code ?? "";
    setBillingDraft((current) =>
      current.diagnosis ? current : { ...current, diagnosis: firstDiagnosis },
    );
    setOrderDraft((current) =>
      current.diagnosis ? current : { ...current, diagnosis: firstDiagnosis },
    );
  }, [detail.diagnosisCodes]);

  async function refreshEncounter() {
    const refreshed = await getEncounterDetail(
      sessionId,
      detail.encounter,
      undefined,
      true,
    );
    onDetailChange(refreshed);
  }

  function openBilling(mode: BillingMode) {
    const firstDiagnosis = detail.diagnosisCodes[0]?.code ?? "";
    setBillingDraft({
      ...BLANK_BILLING_DRAFT,
      mode,
      codeType: mode === "diagnosis" ? "ICD10" : "CPT",
      diagnosis: mode === "charge" ? firstDiagnosis : "",
      fee: mode === "diagnosis" ? "0" : "",
    });
    setBillingError(null);
    setBillingOpen(true);
    setOrderOpen(false);
  }

  async function saveBillingLine(event: React.FormEvent) {
    event.preventDefault();
    const code = normalizeCode(billingDraft.code);
    const description = billingDraft.description.trim();
    const fee =
      billingDraft.mode === "diagnosis" ? 0 : Number(billingDraft.fee);
    const units =
      billingDraft.mode === "diagnosis" ? 1 : Number(billingDraft.units);
    const diagnosis =
      billingDraft.mode === "diagnosis"
        ? ""
        : normalizeCode(billingDraft.diagnosis);

    if (!validCode(code)) {
      setBillingError(
        "Enter a code containing 2–20 letters, numbers, periods, or hyphens.",
      );
      return;
    }
    if (!description) {
      setBillingError("Enter a description for the diagnosis or charge.");
      return;
    }
    if (!Number.isFinite(fee) || fee < 0) {
      setBillingError("Fee must be zero or a positive amount.");
      return;
    }
    if (!Number.isInteger(units) || units < 1 || units > 999) {
      setBillingError("Units must be a whole number from 1 through 999.");
      return;
    }
    if (diagnosis && !validCode(diagnosis)) {
      setBillingError("The supporting diagnosis code is not valid.");
      return;
    }

    setSaving(true);
    setBillingError(null);
    try {
      await createEncounterBillingLine(sessionId, {
        patientId: detail.patientId,
        providerId: null,
        encounter: detail.encounter,
        billingDate: detail.date,
        codeType:
          billingDraft.mode === "diagnosis" ? "ICD10" : billingDraft.codeType,
        code,
        modifier: billingDraft.modifier.trim() || null,
        codeText: description,
        fee,
        units,
        justify: diagnosis,
      });
      await refreshEncounter();
      setBillingOpen(false);
      setBillingDraft(BLANK_BILLING_DRAFT);
      showToast(
        billingDraft.mode === "diagnosis"
          ? "Diagnosis linked to the encounter."
          : "Billing charge linked to the encounter.",
        "success",
      );
    } catch (error) {
      setBillingError(
        error instanceof Error
          ? error.message
          : "The billing line could not be saved.",
      );
    } finally {
      setSaving(false);
    }
  }

  async function saveProcedureOrder(event: React.FormEvent) {
    event.preventDefault();
    const selectedItem = activeOrderItems.find(
      (item) => item.id === Number(orderDraft.catalogItemId),
    );
    const diagnosis = normalizeCode(orderDraft.diagnosis);
    if (!selectedItem?.code) {
      setOrderError("Select an active procedure from the governed catalog.");
      return;
    }
    if (!validCode(diagnosis)) {
      setOrderError("Enter a valid supporting diagnosis code.");
      return;
    }

    setSaving(true);
    setOrderError(null);
    try {
      await createProcedureOrder(sessionId, {
        patientId: detail.patientId,
        providerId: null,
        labId: selectedItem.labId ?? null,
        encounterId: detail.encounter,
        dateOrdered: detail.date,
        priority: orderDraft.priority,
        status: "pending",
        procedureCode: selectedItem.code,
        procedureName: selectedItem.name,
        procedureType: selectedItem.procedureTypeName ?? "laboratory",
        diagnosis,
        instructions: orderDraft.instructions.trim(),
      });
      await refreshEncounter();
      setOrderOpen(false);
      setOrderDraft(BLANK_ORDER_DRAFT);
      showToast("Procedure order linked to the encounter.", "success");
    } catch (error) {
      setOrderError(
        error instanceof Error
          ? error.message
          : "The procedure order could not be saved.",
      );
    } finally {
      setSaving(false);
    }
  }

  const titleId = `encounter-coding-title-${detail.encounter}`;

  return (
    <section
      className="cl-card encounter-coding-workspace"
      aria-labelledby={titleId}
    >
      <div className="cl-card-header encounter-coding-header">
        <div>
          <h2 className="cl-card-title" id={titleId}>
            <ClipboardPlus size={16} /> Diagnosis, billing &amp; orders
          </h2>
          <p className="cl-empty-text">
            Every item below is linked to encounter #{detail.encounter}.
          </p>
        </div>
        <div className="cl-inline-form-actions">
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => openBilling("diagnosis")}
            disabled={saving}
          >
            <Plus size={14} /> Add diagnosis
          </button>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => openBilling("charge")}
            disabled={saving}
          >
            <ReceiptText size={14} /> Add charge
          </button>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => {
              setOrderOpen(true);
              setBillingOpen(false);
              setOrderError(null);
            }}
            disabled={saving || activeOrderItems.length === 0}
          >
            <TestTubeDiagonal size={14} /> Add procedure order
          </button>
        </div>
      </div>

      {catalogError && (
        <p className="encounter-coding-notice" role="status">
          {catalogError}
        </p>
      )}

      <datalist id={`encounter-diagnoses-${detail.encounter}`}>
        {detail.diagnosisCodes.map((diagnosis) => (
          <option key={diagnosis.code} value={diagnosis.code}>
            {diagnosis.description ?? diagnosis.code}
          </option>
        ))}
      </datalist>

      {billingOpen && (
        <form
          className="encounter-coding-form"
          onSubmit={saveBillingLine}
          aria-label={
            billingDraft.mode === "diagnosis"
              ? "Add encounter diagnosis"
              : "Add encounter charge"
          }
        >
          <h3>
            {billingDraft.mode === "diagnosis"
              ? "Add diagnosis evidence"
              : "Add billing charge"}
          </h3>
          {billingError && <p role="alert">{billingError}</p>}
          <div className="encounter-coding-fields">
            {billingDraft.mode === "charge" && (
              <label>
                <span>Code type</span>
                <select
                  value={billingDraft.codeType}
                  onChange={(event) =>
                    setBillingDraft((current) => ({
                      ...current,
                      codeType: event.target.value,
                    }))
                  }
                >
                  <option value="CPT">CPT</option>
                  <option value="HCPCS">HCPCS</option>
                  <option value="REV">Revenue</option>
                </select>
              </label>
            )}
            <label>
              <span>
                {billingDraft.mode === "diagnosis"
                  ? "ICD-10 diagnosis code"
                  : "Billing code"}
              </span>
              <input
                value={billingDraft.code}
                onChange={(event) =>
                  setBillingDraft((current) => ({
                    ...current,
                    code: event.target.value,
                  }))
                }
                autoCapitalize="characters"
                required
              />
            </label>
            <label className="encounter-coding-field-wide">
              <span>Description</span>
              <input
                value={billingDraft.description}
                onChange={(event) =>
                  setBillingDraft((current) => ({
                    ...current,
                    description: event.target.value,
                  }))
                }
                required
              />
            </label>
            {billingDraft.mode === "charge" && (
              <>
                <label>
                  <span>Modifier</span>
                  <input
                    value={billingDraft.modifier}
                    onChange={(event) =>
                      setBillingDraft((current) => ({
                        ...current,
                        modifier: event.target.value,
                      }))
                    }
                  />
                </label>
                <label>
                  <span>Fee</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={billingDraft.fee}
                    onChange={(event) =>
                      setBillingDraft((current) => ({
                        ...current,
                        fee: event.target.value,
                      }))
                    }
                    required
                  />
                </label>
                <label>
                  <span>Units</span>
                  <input
                    type="number"
                    min="1"
                    max="999"
                    step="1"
                    value={billingDraft.units}
                    onChange={(event) =>
                      setBillingDraft((current) => ({
                        ...current,
                        units: event.target.value,
                      }))
                    }
                    required
                  />
                </label>
                <label>
                  <span>Supporting diagnosis</span>
                  <input
                    value={billingDraft.diagnosis}
                    onChange={(event) =>
                      setBillingDraft((current) => ({
                        ...current,
                        diagnosis: event.target.value,
                      }))
                    }
                    list={`encounter-diagnoses-${detail.encounter}`}
                    autoCapitalize="characters"
                  />
                </label>
              </>
            )}
          </div>
          <div className="cl-inline-form-actions">
            <button className="cl-btn-primary" type="submit" disabled={saving}>
              {saving ? "Saving…" : "Link to encounter"}
            </button>
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => setBillingOpen(false)}
              disabled={saving}
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      {orderOpen && (
        <form
          className="encounter-coding-form"
          onSubmit={saveProcedureOrder}
          aria-label="Add encounter procedure order"
        >
          <h3>Add procedure order</h3>
          {orderError && <p role="alert">{orderError}</p>}
          <div className="encounter-coding-fields">
            <label className="encounter-coding-field-wide">
              <span>Catalog procedure</span>
              <select
                value={orderDraft.catalogItemId}
                onChange={(event) =>
                  setOrderDraft((current) => ({
                    ...current,
                    catalogItemId: event.target.value,
                  }))
                }
                required
              >
                <option value="">Select an active procedure</option>
                {activeOrderItems.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.code} — {item.name}
                    {item.labName ? ` — ${item.labName}` : ""}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>Priority</span>
              <select
                value={orderDraft.priority}
                onChange={(event) =>
                  setOrderDraft((current) => ({
                    ...current,
                    priority: event.target.value,
                  }))
                }
              >
                <option value="routine">Routine</option>
                <option value="urgent">Urgent</option>
                <option value="stat">STAT</option>
              </select>
            </label>
            <label>
              <span>Supporting diagnosis</span>
              <input
                value={orderDraft.diagnosis}
                onChange={(event) =>
                  setOrderDraft((current) => ({
                    ...current,
                    diagnosis: event.target.value,
                  }))
                }
                list={`encounter-diagnoses-${detail.encounter}`}
                autoCapitalize="characters"
                required
              />
            </label>
            <label className="encounter-coding-field-wide">
              <span>Clinical instructions</span>
              <textarea
                rows={3}
                value={orderDraft.instructions}
                onChange={(event) =>
                  setOrderDraft((current) => ({
                    ...current,
                    instructions: event.target.value,
                  }))
                }
              />
            </label>
          </div>
          <div className="cl-inline-form-actions">
            <button className="cl-btn-primary" type="submit" disabled={saving}>
              {saving ? "Saving…" : "Create linked order"}
            </button>
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => setOrderOpen(false)}
              disabled={saving}
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      <div className="encounter-coding-grid">
        <div>
          <h3>Diagnosis evidence ({detail.diagnosisCodes.length})</h3>
          {detail.diagnosisCodes.length === 0 ? (
            <p className="cl-empty-text">
              No diagnosis evidence is linked to this encounter.
            </p>
          ) : (
            <ul className="encounter-coding-list">
              {detail.diagnosisCodes.map((diagnosis) => (
                <li key={diagnosis.code}>
                  <strong>{diagnosis.code}</strong>
                  <span>
                    {diagnosis.description ?? "Description unavailable"}
                  </span>
                  <small>
                    {diagnosis.sources.join(" · ")}
                    {diagnosis.billingLineCount > 0
                      ? ` · ${diagnosis.billingLineCount} billing link${diagnosis.billingLineCount === 1 ? "" : "s"}`
                      : ""}
                    {diagnosis.procedureOrderCount > 0
                      ? ` · ${diagnosis.procedureOrderCount} procedure link${diagnosis.procedureOrderCount === 1 ? "" : "s"}`
                      : ""}
                  </small>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div>
          <h3>Billing lines ({codingDetail.billingLines.length})</h3>
          {codingDetail.billingLines.length === 0 ? (
            <p className="cl-empty-text">
              No billing lines are linked to this encounter.
            </p>
          ) : (
            <ul className="encounter-coding-list">
              {codingDetail.billingLines.map((line) => (
                <li key={line.id}>
                  <div className="encounter-coding-row-heading">
                    <strong>
                      {line.codeType} {line.code}
                      {line.modifier ? `-${line.modifier}` : ""}
                    </strong>
                    <span>{displayMoney((line.fee ?? 0) * line.units)}</span>
                  </div>
                  <span>{line.codeText ?? "Description unavailable"}</span>
                  <small>
                    {line.units} unit{line.units === 1 ? "" : "s"}
                    {line.justify ? ` · diagnosis ${line.justify}` : ""}
                    {line.activity === 0 ? " · inactive" : ""}
                    {line.billed === 1 ? " · billed" : " · unbilled"}
                  </small>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div>
          <h3>Procedure orders ({codingDetail.procedureOrders.length})</h3>
          {codingDetail.procedureOrders.length === 0 ? (
            <p className="cl-empty-text">
              No procedure orders are linked to this encounter.
            </p>
          ) : (
            <ul className="encounter-coding-list">
              {codingDetail.procedureOrders.map((order) => (
                <li key={order.id}>
                  <div className="encounter-coding-row-heading">
                    <strong>
                      {order.code ?? "No code"} — {order.name ?? "Procedure"}
                    </strong>
                    <span>{order.orderStatus ?? "unknown"}</span>
                  </div>
                  <span>
                    {order.orderPriority ?? "routine"} · {order.procedureType}
                  </span>
                  <small>
                    Ordered {order.orderDate}
                    {order.diagnosis ? ` · diagnosis ${order.diagnosis}` : ""}
                    {order.providerName ? ` · ${order.providerName}` : ""}
                  </small>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </section>
  );
}
