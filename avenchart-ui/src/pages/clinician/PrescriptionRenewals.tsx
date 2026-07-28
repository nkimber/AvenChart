import { useCallback, useEffect, useState } from "react";
import { Link, useOutletContext, useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  CheckCircle,
  ChevronDown,
  ChevronUp,
  History,
  Pill,
  Search,
  XCircle,
} from "lucide-react";
import {
  approvePrescriptionRefillRequest,
  deactivatePrescription,
  getClinicalLists,
  getPrescriptionAuditHistory,
  refillPrescription,
  searchPatients,
  type ClinicalPrescriptionAuditHistory,
  type PatientListItem,
  type PrescriptionListItem,
  type PrescriptionRefillRequestItem,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";

type RxEntry = {
  patient: PatientListItem;
  rx: PrescriptionListItem;
  daysUntilExpiry: number | null;
};

type RequestEntry = {
  patient: PatientListItem;
  request: PrescriptionRefillRequestItem;
};

type ReadyState = {
  status: "ready";
  prescriptions: RxEntry[];
  requests: RequestEntry[];
  patientCount: number;
  totalMatches: number;
  failedPatients: number;
  datasetId?: string;
  datasetVersion?: string;
};

type AsyncState =
  | { status: "loading" }
  | ReadyState
  | { status: "error"; message: string };

type AuditState =
  | { status: "loading" }
  | { status: "ready"; history: ClinicalPrescriptionAuditHistory }
  | { status: "error"; message: string };

type QueueView = "requests" | "expiring" | "expired" | "all";

function daysUntil(dateStr?: string | null): number | null {
  if (!dateStr) return null;
  const diff = new Date(`${dateStr}T23:59:59`).getTime() - Date.now();
  return Math.ceil(diff / (1000 * 60 * 60 * 24));
}

function urgencyClass(days: number | null): string {
  if (days === null) return "rx-urgency-unknown";
  if (days < 0) return "rx-urgency-expired";
  if (days <= 7) return "rx-urgency-critical";
  if (days <= 30) return "rx-urgency-soon";
  return "rx-urgency-ok";
}

function urgencyLabel(days: number | null): string {
  if (days === null) return "No end date";
  if (days < 0) return `Ended ${Math.abs(days)}d ago`;
  if (days === 0) return "Ends today";
  return `${days}d remaining`;
}

function today() {
  return new Date().toISOString().slice(0, 10);
}

function eventLabel(action: string) {
  return action
    .split("-")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

export default function PrescriptionRenewals() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [searchParams, setSearchParams] = useSearchParams();
  const patientScope = searchParams.get("patient")?.trim() ?? "";
  const requestedView = searchParams.get("view");
  const view: QueueView =
    requestedView === "expiring" ||
    requestedView === "expired" ||
    requestedView === "all"
      ? requestedView
      : "requests";
  const [patientInput, setPatientInput] = useState(patientScope);
  const [state, setState] = useState<AsyncState>({ status: "loading" });
  const [workingKey, setWorkingKey] = useState<string | null>(null);
  const [refillTarget, setRefillTarget] = useState<string | null>(null);
  const [refillCount, setRefillCount] = useState("1");
  const [refillNote, setRefillNote] = useState("");
  const [auditTarget, setAuditTarget] = useState<string | null>(null);
  const [auditByPrescription, setAuditByPrescription] = useState<
    Record<string, AuditState>
  >({});

  useEffect(() => setPatientInput(patientScope), [patientScope]);

  const load = useCallback(async () => {
    setState({ status: "loading" });
    try {
      const patientResult = await searchPatients(session.sessionId, {
        search: patientScope || undefined,
        limit: 50,
      });
      const settled = await Promise.allSettled(
        patientResult.patients.map(async (patient) => ({
          patient,
          lists: await getClinicalLists(
            session.sessionId,
            patient.canonicalId,
          ),
        })),
      );
      const prescriptions: RxEntry[] = [];
      const requests: RequestEntry[] = [];
      let datasetId: string | undefined;
      let datasetVersion: string | undefined;
      let failedPatients = 0;

      for (const result of settled) {
        if (result.status === "rejected") {
          failedPatients += 1;
          continue;
        }
        const { patient, lists } = result.value;
        datasetId ??= lists.datasetId;
        datasetVersion ??= lists.datasetVersion;
        for (const rx of lists.prescriptions) {
          if (rx.active !== 1) continue;
          prescriptions.push({
            patient,
            rx,
            daysUntilExpiry: daysUntil(rx.endDate),
          });
        }
        for (const request of lists.prescriptionRefillRequests) {
          requests.push({ patient, request });
        }
      }

      prescriptions.sort((left, right) => {
        const leftDays = left.daysUntilExpiry ?? Number.MAX_SAFE_INTEGER;
        const rightDays = right.daysUntilExpiry ?? Number.MAX_SAFE_INTEGER;
        return (
          leftDays - rightDays ||
          left.patient.displayName.localeCompare(right.patient.displayName)
        );
      });
      requests.sort(
        (left, right) =>
          left.request.requestDate.localeCompare(right.request.requestDate) ||
          left.patient.displayName.localeCompare(right.patient.displayName),
      );
      setState({
        status: "ready",
        prescriptions,
        requests,
        patientCount: patientResult.patients.length,
        totalMatches: patientResult.totalMatches,
        failedPatients,
        datasetId,
        datasetVersion,
      });
    } catch (error) {
      setState({
        status: "error",
        message:
          error instanceof Error
            ? error.message
            : "The prescription queue could not be loaded.",
      });
    }
  }, [patientScope, session.sessionId]);

  useEffect(() => {
    void load();
  }, [load]);

  function changeView(nextView: QueueView) {
    const next = new URLSearchParams(searchParams);
    next.set("view", nextView);
    setSearchParams(next);
    setRefillTarget(null);
  }

  function submitPatientScope(event: React.FormEvent) {
    event.preventDefault();
    const next = new URLSearchParams(searchParams);
    const normalized = patientInput.trim();
    if (normalized) next.set("patient", normalized);
    else next.delete("patient");
    setSearchParams(next);
  }

  function beginRefill(key: string) {
    setRefillTarget(key);
    setRefillCount("1");
    setRefillNote("");
  }

  function readRefillCount() {
    const count = Number(refillCount);
    return Number.isInteger(count) && count > 0 && count <= 12 ? count : null;
  }

  async function handleRefill(entry: RxEntry) {
    const additionalRefills = readRefillCount();
    if (additionalRefills === null) return;
    const key = `refill-${entry.rx.id}`;
    setWorkingKey(key);
    try {
      await refillPrescription(session.sessionId, entry.rx.id, {
        refillDate: today(),
        additionalRefills,
        note: refillNote.trim() || "Authorized from prescription review queue",
      });
      showToast(
        `${additionalRefills} refill${additionalRefills === 1 ? "" : "s"} added to ${entry.rx.drug}.`,
        "success",
      );
      setRefillTarget(null);
      setAuditByPrescription({});
      await load();
    } catch {
      showToast("The refill could not be recorded. Please retry.", "error");
    } finally {
      setWorkingKey(null);
    }
  }

  async function handleApprove(entry: RequestEntry) {
    const additionalRefills = readRefillCount();
    if (additionalRefills === null) return;
    const key = `approve-${entry.request.messageId}`;
    setWorkingKey(key);
    try {
      await approvePrescriptionRefillRequest(
        session.sessionId,
        entry.request.messageId,
        {
          refillDate: today(),
          additionalRefills,
          note:
            refillNote.trim() ||
            "Portal refill request approved by prescription review queue",
        },
      );
      showToast(
        `Refill request for ${entry.request.drug} approved and reconciled with the staff mailbox.`,
        "success",
      );
      setRefillTarget(null);
      setAuditByPrescription({});
      await load();
    } catch {
      showToast(
        "The request could not be approved. It may already have been processed.",
        "error",
      );
    } finally {
      setWorkingKey(null);
    }
  }

  async function handleDeactivate(entry: RxEntry) {
    if (
      !window.confirm(
        `Discontinue ${entry.rx.drug} for ${entry.patient.displayName}?`,
      )
    )
      return;
    const key = `deactivate-${entry.rx.id}`;
    setWorkingKey(key);
    try {
      await deactivatePrescription(session.sessionId, entry.rx.id, {
        endDate: today(),
        note: "Discontinued from prescription review queue",
      });
      showToast(`${entry.rx.drug} discontinued.`, "success");
      setAuditByPrescription({});
      await load();
    } catch {
      showToast("The prescription could not be discontinued.", "error");
    } finally {
      setWorkingKey(null);
    }
  }

  async function toggleAudit(prescriptionId: string) {
    if (auditTarget === prescriptionId) {
      setAuditTarget(null);
      return;
    }
    setAuditTarget(prescriptionId);
    if (auditByPrescription[prescriptionId]) return;
    setAuditByPrescription((current) => ({
      ...current,
      [prescriptionId]: { status: "loading" },
    }));
    try {
      const history = await getPrescriptionAuditHistory(
        session.sessionId,
        prescriptionId,
      );
      setAuditByPrescription((current) => ({
        ...current,
        [prescriptionId]: { status: "ready", history },
      }));
    } catch (error) {
      setAuditByPrescription((current) => ({
        ...current,
        [prescriptionId]: {
          status: "error",
          message:
            error instanceof Error
              ? error.message
              : "Audit history could not be loaded.",
        },
      }));
    }
  }

  function renderAudit(prescriptionId: string) {
    if (auditTarget !== prescriptionId) return null;
    const audit = auditByPrescription[prescriptionId];
    return (
      <div className="rx-audit-panel">
        <h3>Prescription audit history</h3>
        {!audit || audit.status === "loading" ? (
          <p className="cl-empty-text">Loading audit history…</p>
        ) : audit.status === "error" ? (
          <div className="error-banner">{audit.message}</div>
        ) : audit.history.events.length === 0 ? (
          <p className="cl-empty-text">No audit events recorded.</p>
        ) : (
          <ol className="rx-audit-list">
            {audit.history.events.map((event) => (
              <li key={event.eventId}>
                <div>
                  <strong>{eventLabel(event.action)}</strong>
                  <span>
                    {event.occurredAt} · {event.actor}
                  </span>
                </div>
                {(event.beforeRefills !== null ||
                  event.afterRefills !== null) && (
                  <span>
                    Refills {event.beforeRefills ?? "—"} →{" "}
                    {event.afterRefills ?? "—"}
                  </span>
                )}
                {event.pharmacyName && (
                  <span>Pharmacy: {event.pharmacyName}</span>
                )}
                {event.detail && <span>{event.detail}</span>}
                {event.failureReason && (
                  <span className="rx-warning">{event.failureReason}</span>
                )}
              </li>
            ))}
          </ol>
        )}
      </div>
    );
  }

  const prescriptions = state.status === "ready" ? state.prescriptions : [];
  const visiblePrescriptions = prescriptions.filter((entry) => {
    if (view === "expired")
      return entry.daysUntilExpiry !== null && entry.daysUntilExpiry < 0;
    if (view === "expiring")
      return (
        entry.daysUntilExpiry !== null &&
        entry.daysUntilExpiry >= 0 &&
        entry.daysUntilExpiry <= 60
      );
    return view === "all";
  });
  const requestCount = state.status === "ready" ? state.requests.length : 0;

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Prescription review</h1>
          <p className="clinician-page-subtitle">
            Reconcile portal refill requests, review active prescriptions, and
            retain target audit evidence.
          </p>
        </div>
      </div>

      <div className="hint-banner">
        <strong>Local workflow boundary</strong>
        <span>
          Refill and discontinuation actions update the modernized target only.
          They do not transmit an ePrescription, perform EPCS, check formulary
          or interactions, or contact a pharmacy network.
        </span>
      </div>

      <form className="rx-scope-form cl-card" onSubmit={submitPatientScope}>
        <label htmlFor="rx-patient-scope">Patient name or ID</label>
        <div className="rx-scope-controls">
          <input
            id="rx-patient-scope"
            className="ne-input"
            value={patientInput}
            onChange={(event) => setPatientInput(event.target.value)}
            placeholder="All returned patients"
          />
          <button className="cl-btn-secondary" type="submit">
            <Search size={14} /> Apply patient scope
          </button>
          {patientScope && (
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => {
                setPatientInput("");
                const next = new URLSearchParams(searchParams);
                next.delete("patient");
                setSearchParams(next);
              }}
            >
              Clear
            </button>
          )}
        </div>
      </form>

      {state.status === "ready" && (
        <div className="rx-scan-evidence" role="status">
          <span>
            Reviewed {state.patientCount - state.failedPatients} of{" "}
            {state.patientCount} returned patient charts
          </span>
          <span>{state.totalMatches} matching patients</span>
          {state.datasetId && (
            <span>
              Dataset {state.datasetId} · {state.datasetVersion}
            </span>
          )}
          {state.totalMatches > state.patientCount && (
            <span className="rx-warning">
              Results are bounded to the first {state.patientCount} patients.
              Apply a patient scope for complete patient-level review.
            </span>
          )}
          {state.failedPatients > 0 && (
            <span className="rx-warning">
              {state.failedPatients} patient chart
              {state.failedPatients === 1 ? "" : "s"} failed to load.
            </span>
          )}
        </div>
      )}

      <div className="cl-tab-bar" aria-label="Prescription queue view">
        {(
          [
            ["requests", `Portal requests (${requestCount})`],
            ["expiring", "Ending within 60 days"],
            ["expired", "Past end date"],
            ["all", "All active"],
          ] as const
        ).map(([key, label]) => (
          <button
            key={key}
            className={`cl-tab-btn${view === key ? " cl-tab-btn-active" : ""}`}
            type="button"
            aria-pressed={view === key}
            onClick={() => changeView(key)}
          >
            {label}
          </button>
        ))}
      </div>

      {state.status === "loading" && (
        <div className="cl-card" aria-live="polite">
          <p className="cl-empty-text">Loading prescription review data…</p>
          <div className="skeleton-list" style={{ marginTop: 12 }}>
            {[0, 1, 2, 3].map((item) => (
              <div
                key={item}
                className="skeleton-row"
                style={{ height: 68 }}
              />
            ))}
          </div>
        </div>
      )}
      {state.status === "error" && (
        <div className="error-banner">
          <p>{state.message}</p>
          <button className="cl-btn-secondary" type="button" onClick={load}>
            Retry
          </button>
        </div>
      )}

      {state.status === "ready" && view === "requests" && (
        <>
          {state.requests.length === 0 ? (
            <div className="cl-card">
              <p className="cl-empty-text">
                No pending portal refill requests match this patient scope.
              </p>
            </div>
          ) : (
            <div className="rx-renew-list">
              {state.requests.map((entry) => {
                const formKey = `request-${entry.request.messageId}`;
                const busyKey = `approve-${entry.request.messageId}`;
                return (
                  <article key={formKey} className="rx-renew-item cl-card">
                    <div className="rx-renew-left">
                      <div className="rx-renew-patient">
                        <Pill size={14} />
                        <Link
                          className="rx-renew-patient-name"
                          to={`/clinician/patients/${entry.patient.canonicalId}/chart`}
                        >
                          {entry.patient.displayName}
                        </Link>
                        <span className="cl-badge cl-badge-muted">
                          {entry.patient.pubpid}
                        </span>
                      </div>
                      <p className="rx-renew-drug">{entry.request.drug}</p>
                      <p className="rx-renew-meta">
                        {[
                          entry.request.dosage,
                          entry.request.quantity
                            ? `Qty ${entry.request.quantity}`
                            : null,
                          entry.request.route,
                          `${entry.request.currentRefills} current refill${entry.request.currentRefills === 1 ? "" : "s"}`,
                        ]
                          .filter(Boolean)
                          .join(" · ")}
                      </p>
                      <p className="rx-renew-meta">
                        Requested {entry.request.requestDate} by{" "}
                        {entry.request.portalUsername}
                      </p>
                      {entry.request.patientNote && (
                        <p className="rx-patient-note">
                          Patient note: {entry.request.patientNote}
                        </p>
                      )}
                      {renderAudit(entry.request.prescriptionId)}
                    </div>
                    <div className="rx-renew-right">
                      <span className="rx-urgency-badge rx-urgency-soon">
                        {entry.request.status}
                      </span>
                      {refillTarget === formKey ? (
                        <div className="rx-refill-form">
                          <label>
                            Additional refills
                            <input
                              className="ne-input"
                              type="number"
                              min={1}
                              max={12}
                              value={refillCount}
                              onChange={(event) =>
                                setRefillCount(event.target.value)
                              }
                            />
                          </label>
                          <label>
                            Approval note
                            <input
                              className="ne-input"
                              value={refillNote}
                              onChange={(event) =>
                                setRefillNote(event.target.value)
                              }
                              maxLength={250}
                            />
                          </label>
                          <div className="rx-renew-actions">
                            <button
                              className="cl-btn-primary"
                              type="button"
                              disabled={
                                workingKey === busyKey ||
                                readRefillCount() === null
                              }
                              onClick={() => handleApprove(entry)}
                            >
                              <CheckCircle size={13} /> Approve request
                            </button>
                            <button
                              className="cl-btn-secondary"
                              type="button"
                              disabled={workingKey === busyKey}
                              onClick={() => setRefillTarget(null)}
                            >
                              Cancel
                            </button>
                          </div>
                        </div>
                      ) : (
                        <div className="rx-renew-actions">
                          <button
                            className="cl-btn-primary"
                            type="button"
                            onClick={() => beginRefill(formKey)}
                          >
                            <CheckCircle size={13} /> Review and approve
                          </button>
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            aria-expanded={
                              auditTarget === entry.request.prescriptionId
                            }
                            onClick={() =>
                              toggleAudit(entry.request.prescriptionId)
                            }
                          >
                            <History size={13} /> History{" "}
                            {auditTarget === entry.request.prescriptionId ? (
                              <ChevronUp size={13} />
                            ) : (
                              <ChevronDown size={13} />
                            )}
                          </button>
                        </div>
                      )}
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </>
      )}

      {state.status === "ready" && view !== "requests" && (
        <>
          {visiblePrescriptions.length === 0 ? (
            <div className="cl-card">
              <p className="cl-empty-text">
                No active prescriptions match this view and patient scope.
              </p>
            </div>
          ) : (
            <div className="rx-renew-list">
              {visiblePrescriptions.map((entry) => {
                const formKey = `prescription-${entry.rx.id}`;
                const refillBusyKey = `refill-${entry.rx.id}`;
                const deactivateBusyKey = `deactivate-${entry.rx.id}`;
                return (
                  <article
                    key={`${entry.patient.canonicalId}-${entry.rx.id}`}
                    className="rx-renew-item cl-card"
                  >
                    <div className="rx-renew-left">
                      <div className="rx-renew-patient">
                        <Pill size={14} />
                        <Link
                          className="rx-renew-patient-name"
                          to={`/clinician/patients/${entry.patient.canonicalId}/chart`}
                        >
                          {entry.patient.displayName}
                        </Link>
                        <span className="cl-badge cl-badge-muted">
                          {entry.patient.pubpid}
                        </span>
                      </div>
                      <p className="rx-renew-drug">{entry.rx.drug}</p>
                      <p className="rx-renew-meta">
                        {[
                          entry.rx.dosage,
                          entry.rx.quantity ? `Qty ${entry.rx.quantity}` : null,
                          entry.rx.route,
                          entry.rx.rxNormCode
                            ? `RXCUI ${entry.rx.rxNormCode}`
                            : null,
                        ]
                          .filter(Boolean)
                          .join(" · ")}
                      </p>
                      <p className="rx-renew-meta">
                        {entry.rx.refills} refill
                        {entry.rx.refills === 1 ? "" : "s"} · Started{" "}
                        {entry.rx.startDate ?? "not recorded"} · Ends{" "}
                        {entry.rx.endDate ?? "not recorded"}
                      </p>
                      {entry.rx.controlledSubstanceReviewRequired && (
                        <p className="rx-warning">
                          <AlertTriangle size={13} />{" "}
                          {entry.rx.controlledSubstanceReason ??
                            "Controlled-substance policy review is required."}
                        </p>
                      )}
                      {entry.rx.pharmacyName && (
                        <p className="rx-renew-meta">
                          Local route evidence: {entry.rx.pharmacyName}
                          {entry.rx.erxSentAt
                            ? ` · recorded ${entry.rx.erxSentAt}`
                            : ""}
                        </p>
                      )}
                      {renderAudit(entry.rx.id)}
                    </div>
                    <div className="rx-renew-right">
                      <span
                        className={`rx-urgency-badge ${urgencyClass(entry.daysUntilExpiry)}`}
                      >
                        {urgencyLabel(entry.daysUntilExpiry)}
                      </span>
                      {refillTarget === formKey ? (
                        <div className="rx-refill-form">
                          <label>
                            Additional refills
                            <input
                              className="ne-input"
                              type="number"
                              min={1}
                              max={12}
                              value={refillCount}
                              onChange={(event) =>
                                setRefillCount(event.target.value)
                              }
                            />
                          </label>
                          <label>
                            Authorization note
                            <input
                              className="ne-input"
                              value={refillNote}
                              onChange={(event) =>
                                setRefillNote(event.target.value)
                              }
                              maxLength={250}
                            />
                          </label>
                          <div className="rx-renew-actions">
                            <button
                              className="cl-btn-primary"
                              type="button"
                              disabled={
                                workingKey === refillBusyKey ||
                                readRefillCount() === null
                              }
                              onClick={() => handleRefill(entry)}
                            >
                              <CheckCircle size={13} /> Record refill
                            </button>
                            <button
                              className="cl-btn-secondary"
                              type="button"
                              disabled={workingKey === refillBusyKey}
                              onClick={() => setRefillTarget(null)}
                            >
                              Cancel
                            </button>
                          </div>
                        </div>
                      ) : (
                        <div className="rx-renew-actions">
                          <button
                            className="cl-btn-primary"
                            type="button"
                            onClick={() => beginRefill(formKey)}
                          >
                            <CheckCircle size={13} /> Add refills
                          </button>
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            disabled={workingKey === deactivateBusyKey}
                            onClick={() => handleDeactivate(entry)}
                          >
                            <XCircle size={13} /> Discontinue
                          </button>
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            aria-expanded={auditTarget === entry.rx.id}
                            onClick={() => toggleAudit(entry.rx.id)}
                          >
                            <History size={13} /> History{" "}
                            {auditTarget === entry.rx.id ? (
                              <ChevronUp size={13} />
                            ) : (
                              <ChevronDown size={13} />
                            )}
                          </button>
                        </div>
                      )}
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </>
      )}
    </div>
  );
}
