// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useState } from "react";
import { Link, useOutletContext, useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  CheckCircle,
  ChevronDown,
  ChevronUp,
  History,
  MapPin,
  Pencil,
  Pill,
  Search,
  XCircle,
} from "lucide-react";
import {
  ApiRequestError,
  approvePrescriptionRefillRequest,
  deactivatePrescription,
  decidePrescriptionRefillRequest,
  getClinicalPharmacyDirectory,
  getClinicalLists,
  getPrescriptionAuditHistory,
  getPrescriptionRefillQueue,
  refillPrescription,
  routePrescriptionToPharmacy,
  searchPatients,
  updatePrescription,
  type ClinicalPrescriptionAuditHistory,
  type ClinicalPharmacyDirectoryResponse,
  type PatientListItem,
  type PrescriptionListItem,
  type PrescriptionRefillQueueCounts,
  type PrescriptionRefillQueueItem,
  type PrescriptionRefillDecisionInput,
  type PrescriptionUpdateInput,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";

type RxEntry = {
  patient: PatientListItem;
  rx: PrescriptionListItem;
  daysUntilExpiry: number | null;
};

type RequestEntry = {
  patient: Pick<PatientListItem, "canonicalId" | "displayName" | "pubpid">;
  request: PrescriptionRefillQueueItem;
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
  queueCounts: PrescriptionRefillQueueCounts;
  queueStatusFilter: string;
};

type AsyncState =
  | { status: "loading" }
  | ReadyState
  | { status: "error"; message: string };

type AuditState =
  | { status: "loading" }
  | { status: "ready"; history: ClinicalPrescriptionAuditHistory }
  | { status: "error"; message: string };

type PharmacyState =
  | { status: "loading" }
  | { status: "ready"; data: ClinicalPharmacyDirectoryResponse }
  | { status: "error"; message: string };

type PrescriptionEditDraft = {
  expectedVersion: string;
  startDate: string;
  dosage: string;
  quantity: string;
  doseAmount: string;
  doseUnit: string;
  frequency: string;
  durationDays: string;
  route: string;
  refills: string;
  diagnosis: string;
  note: string;
  editReason: string;
};

type QueueView = "requests" | "expiring" | "expired" | "all";
type RequestStatusView = "open" | "approved" | "denied" | "completed" | "all";

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

function refillStatusClass(status: string) {
  if (status === "approved" || status === "completed") {
    return "rx-urgency-ok";
  }
  if (status === "denied") return "rx-urgency-expired";
  return "rx-urgency-soon";
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
  const requestedRequestStatus = searchParams.get("requestStatus");
  const requestStatus: RequestStatusView =
    requestedRequestStatus === "approved" ||
    requestedRequestStatus === "denied" ||
    requestedRequestStatus === "completed" ||
    requestedRequestStatus === "all"
      ? requestedRequestStatus
      : "open";
  const [patientInput, setPatientInput] = useState(patientScope);
  const [state, setState] = useState<AsyncState>({ status: "loading" });
  const [workingKey, setWorkingKey] = useState<string | null>(null);
  const [refillTarget, setRefillTarget] = useState<string | null>(null);
  const [refillCount, setRefillCount] = useState("1");
  const [refillNote, setRefillNote] = useState("");
  const [routeTarget, setRouteTarget] = useState<string | null>(null);
  const [routePharmacyId, setRoutePharmacyId] = useState("");
  const [routeNote, setRouteNote] = useState("");
  const [editTarget, setEditTarget] = useState<string | null>(null);
  const [editDraft, setEditDraft] =
    useState<PrescriptionEditDraft | null>(null);
  const [decisionTarget, setDecisionTarget] = useState<string | null>(null);
  const [decisionAction, setDecisionAction] =
    useState<PrescriptionRefillDecisionInput["action"]>("deny");
  const [decisionResponse, setDecisionResponse] = useState("");
  const [pharmacyState, setPharmacyState] = useState<PharmacyState>({
    status: "loading",
  });
  const [auditTarget, setAuditTarget] = useState<string | null>(null);
  const [auditByPrescription, setAuditByPrescription] = useState<
    Record<string, AuditState>
  >({});

  useEffect(() => setPatientInput(patientScope), [patientScope]);

  useEffect(() => {
    getClinicalPharmacyDirectory(session.sessionId)
      .then((data) => setPharmacyState({ status: "ready", data }))
      .catch((error) =>
        setPharmacyState({
          status: "error",
          message:
            error instanceof Error
              ? error.message
              : "The local pharmacy directory could not be loaded.",
        }),
      );
  }, [session.sessionId]);

  const load = useCallback(async () => {
    setState({ status: "loading" });
    try {
      const queuePromise = getPrescriptionRefillQueue(session.sessionId, {
        status: requestStatus,
        patient: patientScope || undefined,
        limit: 200,
      });
      if (view === "requests") {
        const queue = await queuePromise;
        setState({
          status: "ready",
          prescriptions: [],
          requests: queue.requests.map((request) => ({
            patient: {
              canonicalId: request.patientId,
              displayName: request.patientDisplayName,
              pubpid: request.pubpid,
            },
            request,
          })),
          patientCount: queue.returnedCount,
          totalMatches: queue.totalMatches,
          failedPatients: 0,
          datasetId: queue.datasetId,
          datasetVersion: queue.datasetVersion,
          queueCounts: queue.counts,
          queueStatusFilter: queue.statusFilter,
        });
        return;
      }

      const [queue, patientResult] = await Promise.all([
        queuePromise,
        searchPatients(session.sessionId, {
          search: patientScope || undefined,
          limit: 50,
        }),
      ]);
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
      const requests: RequestEntry[] = queue.requests.map((request) => ({
        patient: {
          canonicalId: request.patientId,
          displayName: request.patientDisplayName,
          pubpid: request.pubpid,
        },
        request,
      }));
      let datasetId: string | undefined = queue.datasetId;
      let datasetVersion: string | undefined = queue.datasetVersion;
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
      }

      prescriptions.sort((left, right) => {
        const leftDays = left.daysUntilExpiry ?? Number.MAX_SAFE_INTEGER;
        const rightDays = right.daysUntilExpiry ?? Number.MAX_SAFE_INTEGER;
        return (
          leftDays - rightDays ||
          left.patient.displayName.localeCompare(right.patient.displayName)
        );
      });
      setState({
        status: "ready",
        prescriptions,
        requests,
        patientCount: patientResult.patients.length,
        totalMatches: patientResult.totalMatches,
        failedPatients,
        datasetId,
        datasetVersion,
        queueCounts: queue.counts,
        queueStatusFilter: queue.statusFilter,
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
  }, [patientScope, requestStatus, session.sessionId, view]);

  useEffect(() => {
    void load();
  }, [load]);

  function changeView(nextView: QueueView) {
    const next = new URLSearchParams(searchParams);
    next.set("view", nextView);
    setSearchParams(next);
    setRefillTarget(null);
    setRouteTarget(null);
    setEditTarget(null);
    setDecisionTarget(null);
  }

  function changeRequestStatus(nextStatus: RequestStatusView) {
    const next = new URLSearchParams(searchParams);
    next.set("view", "requests");
    next.set("requestStatus", nextStatus);
    setSearchParams(next);
    setRefillTarget(null);
    setDecisionTarget(null);
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
    setRouteTarget(null);
    setEditTarget(null);
    setDecisionTarget(null);
    setRefillTarget(key);
    setRefillCount("1");
    setRefillNote("");
  }

  function beginRoute(key: string, pharmacyId?: number | null) {
    setRefillTarget(null);
    setEditTarget(null);
    setDecisionTarget(null);
    setRouteTarget(key);
    setRoutePharmacyId(pharmacyId ? String(pharmacyId) : "");
    setRouteNote("");
  }

  function beginEdit(key: string, prescription: PrescriptionListItem) {
    setRefillTarget(null);
    setRouteTarget(null);
    setDecisionTarget(null);
    setEditTarget(key);
    setEditDraft({
      expectedVersion: prescription.version,
      startDate: prescription.startDate ?? "",
      dosage: prescription.dosage ?? "",
      quantity: prescription.quantity ?? "",
      doseAmount:
        prescription.doseAmount === null ||
        prescription.doseAmount === undefined
          ? ""
          : String(prescription.doseAmount),
      doseUnit: prescription.doseUnit ?? "",
      frequency: prescription.frequency ?? "",
      durationDays:
        prescription.durationDays === null ||
        prescription.durationDays === undefined
          ? ""
          : String(prescription.durationDays),
      route: prescription.route ?? "",
      refills: String(prescription.refills),
      diagnosis: prescription.diagnosis ?? "",
      note: prescription.note ?? "",
      editReason: "",
    });
  }

  function beginDecision(
    key: string,
    action: PrescriptionRefillDecisionInput["action"],
  ) {
    setRefillTarget(null);
    setDecisionTarget(key);
    setDecisionAction(action);
    setDecisionResponse("");
  }

  function updateEditField(
    field: keyof PrescriptionEditDraft,
    value: string,
  ) {
    setEditDraft((current) =>
      current ? { ...current, [field]: value } : current,
    );
  }

  function readPrescriptionUpdate(): PrescriptionUpdateInput | null {
    if (!editDraft) return null;
    const refills = Number(editDraft.refills);
    const doseAmount = editDraft.doseAmount.trim()
      ? Number(editDraft.doseAmount)
      : null;
    const durationDays = editDraft.durationDays.trim()
      ? Number(editDraft.durationDays)
      : null;
    if (
      !editDraft.expectedVersion ||
      !editDraft.startDate ||
      !editDraft.dosage.trim() ||
      !editDraft.quantity.trim() ||
      !editDraft.editReason.trim() ||
      !Number.isInteger(refills) ||
      refills < 0 ||
      refills > 12 ||
      (doseAmount !== null &&
        (!Number.isFinite(doseAmount) || doseAmount < 0)) ||
      (durationDays !== null &&
        (!Number.isInteger(durationDays) || durationDays <= 0))
    ) {
      return null;
    }
    return {
      expectedVersion: editDraft.expectedVersion,
      startDate: editDraft.startDate,
      dosage: editDraft.dosage.trim(),
      quantity: editDraft.quantity.trim(),
      doseAmount,
      doseUnit: editDraft.doseUnit.trim() || null,
      frequency: editDraft.frequency.trim() || null,
      durationDays,
      route: editDraft.route.trim() || null,
      refills,
      diagnosis: editDraft.diagnosis.trim() || null,
      note: editDraft.note.trim() || null,
      editReason: editDraft.editReason.trim(),
    };
  }

  function readRefillCount() {
    const count = Number(refillCount);
    return Number.isInteger(count) && count > 0 && count <= 12 ? count : null;
  }

  async function handleEdit(entry: RxEntry) {
    const update = readPrescriptionUpdate();
    if (!update) return;
    const key = `edit-${entry.rx.id}`;
    setWorkingKey(key);
    try {
      await updatePrescription(
        session.sessionId,
        entry.rx.id,
        update,
      );
      showToast(
        `${entry.rx.drug} updated.${
          entry.rx.pharmacyName
            ? " The prior local pharmacy route was cleared and must be recorded again."
            : ""
        }`,
        "success",
      );
      setEditTarget(null);
      setEditDraft(null);
      setAuditTarget(null);
      setAuditByPrescription({});
      await load();
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 409) {
        showToast(
          "This prescription changed in another session. Current values were reloaded; review them before editing again.",
          "error",
        );
        setEditTarget(null);
        setEditDraft(null);
        setAuditTarget(null);
        setAuditByPrescription({});
        await load();
      } else {
        showToast(
          "The prescription changes could not be saved. Review the fields and retry.",
          "error",
        );
      }
    } finally {
      setWorkingKey(null);
    }
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
      setAuditTarget(null);
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
    if (additionalRefills === null || !refillNote.trim()) return;
    const key = `approve-${entry.request.messageId}`;
    setWorkingKey(key);
    try {
      await approvePrescriptionRefillRequest(
        session.sessionId,
        entry.request.messageId,
        {
          refillDate: today(),
          additionalRefills,
          note: refillNote.trim(),
        },
      );
      showToast(
        `Refill request for ${entry.request.drug} approved and reconciled with the staff mailbox.`,
        "success",
      );
      setRefillTarget(null);
      setAuditTarget(null);
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

  async function handleDecision(entry: RequestEntry) {
    if (!decisionResponse.trim()) return;
    const key = `decision-${entry.request.messageId}`;
    setWorkingKey(key);
    try {
      const result = await decidePrescriptionRefillRequest(
        session.sessionId,
        entry.request.messageId,
        {
          action: decisionAction,
          response: decisionResponse.trim(),
        },
      );
      const label =
        result.status === "clarification-requested"
          ? "Clarification requested"
          : result.status === "denied"
            ? "Refill request denied"
            : "Refill request marked locally completed";
      showToast(`${label} for ${entry.request.drug}.`, "success");
      setDecisionTarget(null);
      setDecisionResponse("");
      setAuditTarget(null);
      setAuditByPrescription({});
      await load();
    } catch {
      showToast(
        "The refill decision could not be recorded. Reload the queue and retry.",
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
      setAuditTarget(null);
      setAuditByPrescription({});
      await load();
    } catch {
      showToast("The prescription could not be discontinued.", "error");
    } finally {
      setWorkingKey(null);
    }
  }

  async function handleRoute(entry: RxEntry) {
    const pharmacyId = Number(routePharmacyId);
    if (
      !Number.isInteger(pharmacyId) ||
      pharmacyId <= 0 ||
      !routeNote.trim()
    )
      return;
    const key = `route-${entry.rx.id}`;
    setWorkingKey(key);
    try {
      const result = await routePrescriptionToPharmacy(
        session.sessionId,
        entry.rx.id,
        {
          pharmacyId,
          sentAt: new Date().toISOString(),
          note: routeNote.trim(),
        },
      );
      setRouteTarget(null);
      setAuditTarget(null);
      setAuditByPrescription({});
      if (result.routed) {
        showToast(
          `Local pharmacy route recorded for ${entry.rx.drug}. No external transmission occurred.`,
          "success",
        );
      } else {
        showToast(
          result.failureReason ??
            "The local pharmacy route was blocked by target policy.",
          "error",
        );
      }
      await load();
    } catch {
      showToast("The local pharmacy route could not be recorded.", "error");
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
  const requestCount =
    state.status === "ready"
      ? state.queueCounts.pending +
        state.queueCounts.clarificationRequested
      : 0;

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
          Refill, discontinuation, and pharmacy-route actions update the
          modernized target only. A pharmacy route records local synthetic
          evidence; it does not transmit an ePrescription, perform EPCS, check
          formulary or interactions, or contact a pharmacy network.
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
          {view === "requests" ? (
            <>
              <span>Protected global refill queue</span>
              <span>
                {state.requests.length} returned · {state.totalMatches}{" "}
                matching {state.queueStatusFilter} request
                {state.totalMatches === 1 ? "" : "s"}
              </span>
              <span>
                {state.queueCounts.pending} pending ·{" "}
                {state.queueCounts.clarificationRequested} clarification ·{" "}
                {state.queueCounts.approved} approved ·{" "}
                {state.queueCounts.denied} denied ·{" "}
                {state.queueCounts.completed} completed
              </span>
            </>
          ) : (
            <>
              <span>
                Reviewed {state.patientCount - state.failedPatients} of{" "}
                {state.patientCount} returned patient charts
              </span>
              <span>{state.totalMatches} matching patients</span>
            </>
          )}
          {state.datasetId && (
            <span>
              Dataset {state.datasetId} · {state.datasetVersion}
            </span>
          )}
          {pharmacyState.status === "ready" && (
            <span>
              {pharmacyState.data.pharmacyCount} local pharmacies ·{" "}
              {pharmacyState.data.datasetId} ·{" "}
              {pharmacyState.data.datasetVersion}
            </span>
          )}
          {pharmacyState.status === "error" && (
            <span className="rx-warning">
              Local pharmacy directory unavailable
            </span>
          )}
          {view !== "requests" &&
            state.totalMatches > state.patientCount && (
            <span className="rx-warning">
              Results are bounded to the first {state.patientCount} patients.
              Apply a patient scope for complete patient-level review.
            </span>
          )}
          {view !== "requests" && state.failedPatients > 0 && (
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
          <div
            className="cl-tab-bar"
            aria-label="Refill request status filter"
          >
            {(
              [
                [
                  "open",
                  `Open (${state.queueCounts.pending + state.queueCounts.clarificationRequested})`,
                ],
                ["approved", `Approved (${state.queueCounts.approved})`],
                ["denied", `Denied (${state.queueCounts.denied})`],
                ["completed", `Completed (${state.queueCounts.completed})`],
                ["all", `All (${state.queueCounts.total})`],
              ] as const
            ).map(([key, label]) => (
              <button
                key={key}
                className={`cl-tab-btn${requestStatus === key ? " cl-tab-btn-active" : ""}`}
                type="button"
                aria-pressed={requestStatus === key}
                onClick={() => changeRequestStatus(key)}
              >
                {label}
              </button>
            ))}
          </div>
          {state.requests.length === 0 ? (
            <div className="cl-card">
              <p className="cl-empty-text">
                No {requestStatus} portal refill requests match this patient
                scope.
              </p>
            </div>
          ) : (
            <div className="rx-renew-list">
              {state.requests.map((entry) => {
                const formKey = `request-${entry.request.messageId}`;
                const busyKey = `approve-${entry.request.messageId}`;
                const decisionBusyKey = `decision-${entry.request.messageId}`;
                const requestIsOpen =
                  entry.request.status === "pending" ||
                  entry.request.status === "clarification-requested";
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
                      {entry.request.staffResponse && (
                        <p className="rx-staff-response">
                          Staff response: {entry.request.staffResponse}
                        </p>
                      )}
                      {renderAudit(entry.request.prescriptionId)}
                    </div>
                    <div className="rx-renew-right">
                      <span
                        className={`rx-urgency-badge ${refillStatusClass(entry.request.status)}`}
                      >
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
                            Patient-visible approval response
                            <input
                              className="ne-input"
                              value={refillNote}
                              onChange={(event) =>
                                setRefillNote(event.target.value)
                              }
                              maxLength={250}
                              required
                            />
                          </label>
                          <div className="rx-renew-actions">
                            <button
                              className="cl-btn-primary"
                              type="button"
                              disabled={
                                workingKey === busyKey ||
                                readRefillCount() === null ||
                                !refillNote.trim()
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
                      ) : decisionTarget === formKey ? (
                        <div className="rx-refill-form">
                          <label>
                            {decisionAction === "deny"
                              ? "Patient-visible denial reason"
                              : decisionAction === "request-clarification"
                                ? "Patient-visible clarification question"
                                : "Patient-visible completion note"}
                            <textarea
                              className="ne-input"
                              rows={3}
                              value={decisionResponse}
                              onChange={(event) =>
                                setDecisionResponse(event.target.value)
                              }
                              maxLength={500}
                              required
                            />
                          </label>
                          <p className="rx-renew-meta">
                            {decisionAction === "complete"
                              ? "Completion closes the local review workflow only; it does not prove pharmacy dispensing or delivery."
                              : "The response is published in the patient refill history."}
                          </p>
                          <div className="rx-renew-actions">
                            <button
                              className={
                                decisionAction === "deny"
                                  ? "cl-btn-danger"
                                  : "cl-btn-primary"
                              }
                              type="button"
                              disabled={
                                workingKey === decisionBusyKey ||
                                !decisionResponse.trim()
                              }
                              onClick={() => handleDecision(entry)}
                            >
                              {decisionAction === "deny"
                                ? "Deny request"
                                : decisionAction === "request-clarification"
                                  ? "Request clarification"
                                  : "Mark locally completed"}
                            </button>
                            <button
                              className="cl-btn-secondary"
                              type="button"
                              disabled={workingKey === decisionBusyKey}
                              onClick={() => setDecisionTarget(null)}
                            >
                              Cancel
                            </button>
                          </div>
                        </div>
                      ) : (
                        <div className="rx-renew-actions">
                          {requestIsOpen && (
                            <>
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
                                onClick={() =>
                                  beginDecision(
                                    formKey,
                                    "request-clarification",
                                  )
                                }
                              >
                                Request clarification
                              </button>
                              <button
                                className="cl-btn-secondary"
                                type="button"
                                onClick={() =>
                                  beginDecision(formKey, "deny")
                                }
                              >
                                Deny
                              </button>
                            </>
                          )}
                          {entry.request.status === "approved" && (
                            <button
                              className="cl-btn-primary"
                              type="button"
                              onClick={() =>
                                beginDecision(formKey, "complete")
                              }
                            >
                              <CheckCircle size={13} /> Mark completed
                            </button>
                          )}
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
                const editBusyKey = `edit-${entry.rx.id}`;
                const refillBusyKey = `refill-${entry.rx.id}`;
                const routeBusyKey = `route-${entry.rx.id}`;
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
                        {entry.rx.endDate ?? "not recorded"} · RX ID{" "}
                        {entry.rx.id}
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
                      {editTarget === formKey && editDraft ? (
                        <div className="rx-edit-form">
                          <p className="rx-renew-meta">
                            Medication identity remains {entry.rx.drug}
                            {entry.rx.rxNormCode
                              ? ` · RXCUI ${entry.rx.rxNormCode}`
                              : ""}
                            . Use a new prescription to change the medication
                            identity.
                          </p>
                          <div className="rx-edit-grid">
                            <label>
                              Start date
                              <input
                                className="ne-input"
                                type="date"
                                value={editDraft.startDate}
                                onChange={(event) =>
                                  updateEditField(
                                    "startDate",
                                    event.target.value,
                                  )
                                }
                                required
                              />
                            </label>
                            <label>
                              Directions
                              <input
                                className="ne-input"
                                value={editDraft.dosage}
                                onChange={(event) =>
                                  updateEditField(
                                    "dosage",
                                    event.target.value,
                                  )
                                }
                                maxLength={250}
                                required
                              />
                            </label>
                            <label>
                              Quantity
                              <input
                                className="ne-input"
                                value={editDraft.quantity}
                                onChange={(event) =>
                                  updateEditField(
                                    "quantity",
                                    event.target.value,
                                  )
                                }
                                maxLength={100}
                                required
                              />
                            </label>
                            <label>
                              Dose amount
                              <input
                                className="ne-input"
                                type="number"
                                min={0}
                                step="0.01"
                                value={editDraft.doseAmount}
                                onChange={(event) =>
                                  updateEditField(
                                    "doseAmount",
                                    event.target.value,
                                  )
                                }
                              />
                            </label>
                            <label>
                              Dose unit
                              <input
                                className="ne-input"
                                value={editDraft.doseUnit}
                                onChange={(event) =>
                                  updateEditField(
                                    "doseUnit",
                                    event.target.value,
                                  )
                                }
                                maxLength={50}
                              />
                            </label>
                            <label>
                              Frequency
                              <input
                                className="ne-input"
                                value={editDraft.frequency}
                                onChange={(event) =>
                                  updateEditField(
                                    "frequency",
                                    event.target.value,
                                  )
                                }
                                maxLength={100}
                              />
                            </label>
                            <label>
                              Duration days
                              <input
                                className="ne-input"
                                type="number"
                                min={1}
                                value={editDraft.durationDays}
                                onChange={(event) =>
                                  updateEditField(
                                    "durationDays",
                                    event.target.value,
                                  )
                                }
                              />
                            </label>
                            <label>
                              Route
                              <input
                                className="ne-input"
                                value={editDraft.route}
                                onChange={(event) =>
                                  updateEditField(
                                    "route",
                                    event.target.value,
                                  )
                                }
                                maxLength={100}
                              />
                            </label>
                            <label>
                              Authorized refills
                              <input
                                className="ne-input"
                                type="number"
                                min={0}
                                max={12}
                                value={editDraft.refills}
                                onChange={(event) =>
                                  updateEditField(
                                    "refills",
                                    event.target.value,
                                  )
                                }
                                required
                              />
                            </label>
                            <label>
                              Diagnosis
                              <input
                                className="ne-input"
                                value={editDraft.diagnosis}
                                onChange={(event) =>
                                  updateEditField(
                                    "diagnosis",
                                    event.target.value,
                                  )
                                }
                                maxLength={100}
                              />
                            </label>
                            <label className="rx-edit-wide">
                              Clinical note
                              <textarea
                                className="ne-input"
                                rows={2}
                                value={editDraft.note}
                                onChange={(event) =>
                                  updateEditField(
                                    "note",
                                    event.target.value,
                                  )
                                }
                                maxLength={1000}
                              />
                            </label>
                            <label className="rx-edit-wide">
                              Edit reason
                              <textarea
                                className="ne-input"
                                rows={2}
                                value={editDraft.editReason}
                                onChange={(event) =>
                                  updateEditField(
                                    "editReason",
                                    event.target.value,
                                  )
                                }
                                maxLength={500}
                                required
                              />
                            </label>
                          </div>
                          <p className="rx-renew-meta">
                            Saving checks the version loaded with this form. A
                            concurrent change is rejected and reloaded.
                            {entry.rx.pharmacyName
                              ? " Existing local pharmacy route evidence will be cleared and must be recorded again."
                              : ""}
                          </p>
                          <div className="rx-renew-actions">
                            <button
                              className="cl-btn-primary"
                              type="button"
                              disabled={
                                workingKey === editBusyKey ||
                                readPrescriptionUpdate() === null
                              }
                              onClick={() => handleEdit(entry)}
                            >
                              <Pencil size={13} /> Save prescription
                            </button>
                            <button
                              className="cl-btn-secondary"
                              type="button"
                              disabled={workingKey === editBusyKey}
                              onClick={() => {
                                setEditTarget(null);
                                setEditDraft(null);
                              }}
                            >
                              Cancel
                            </button>
                          </div>
                        </div>
                      ) : routeTarget === formKey ? (
                        <div className="rx-refill-form">
                          <label>
                            Local pharmacy
                            <select
                              className="ne-input"
                              value={routePharmacyId}
                              onChange={(event) =>
                                setRoutePharmacyId(event.target.value)
                              }
                              required
                            >
                              <option value="">
                                {pharmacyState.status === "loading"
                                  ? "Loading local directory…"
                                  : "Select a local pharmacy"}
                              </option>
                              {pharmacyState.status === "ready" &&
                                pharmacyState.data.pharmacies.map(
                                  (pharmacy) => (
                                    <option
                                      key={pharmacy.id}
                                      value={pharmacy.id}
                                    >
                                      {pharmacy.name}
                                      {pharmacy.ncpdp
                                        ? ` · NCPDP ${pharmacy.ncpdp}`
                                        : ""}
                                    </option>
                                  ),
                                )}
                            </select>
                          </label>
                          {pharmacyState.status === "error" && (
                            <p className="rx-warning">
                              {pharmacyState.message}
                            </p>
                          )}
                          <label>
                            Routing note
                            <input
                              className="ne-input"
                              value={routeNote}
                              onChange={(event) =>
                                setRouteNote(event.target.value)
                              }
                              maxLength={250}
                              required
                            />
                          </label>
                          <p className="rx-renew-meta">
                            Records local synthetic route evidence only. No
                            pharmacy network, eRx, or EPCS transmission occurs.
                          </p>
                          <div className="rx-renew-actions">
                            <button
                              className="cl-btn-primary"
                              type="button"
                              disabled={
                                workingKey === routeBusyKey ||
                                pharmacyState.status !== "ready" ||
                                !routePharmacyId ||
                                !routeNote.trim()
                              }
                              onClick={() => handleRoute(entry)}
                            >
                              <MapPin size={13} /> Record local route
                            </button>
                            <button
                              className="cl-btn-secondary"
                              type="button"
                              disabled={workingKey === routeBusyKey}
                              onClick={() => setRouteTarget(null)}
                            >
                              Cancel
                            </button>
                          </div>
                        </div>
                      ) : refillTarget === formKey ? (
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
                            className="cl-btn-secondary"
                            type="button"
                            disabled={
                              entry.rx.controlledSubstanceReviewRequired
                            }
                            title={
                              entry.rx.controlledSubstanceReviewRequired
                                ? "Controlled-substance policy review blocks prescription editing."
                                : undefined
                            }
                            onClick={() => beginEdit(formKey, entry.rx)}
                          >
                            <Pencil size={13} /> Edit prescription
                          </button>
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
                            disabled={
                              entry.rx.controlledSubstanceReviewRequired ||
                              pharmacyState.status !== "ready"
                            }
                            title={
                              entry.rx.controlledSubstanceReviewRequired
                                ? "Controlled-substance policy review blocks local pharmacy routing."
                                : pharmacyState.status !== "ready"
                                  ? "The local pharmacy directory is not available."
                                  : undefined
                            }
                            onClick={() =>
                              beginRoute(formKey, entry.rx.pharmacyId)
                            }
                          >
                            <MapPin size={13} />{" "}
                            {entry.rx.pharmacyName
                              ? "Change local route"
                              : "Record pharmacy"}
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
