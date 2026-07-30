import { useCallback, useEffect, useMemo, useState } from "react";
import { useOutletContext } from "react-router-dom";
import {
  ApiRequestError,
  createPatientReferral,
  getClinicalWorkflowAssignees,
  getPatientReferralHistory,
  getPatientReferrals,
  updatePatientReferralAssignment,
  updatePatientReferralStatus,
  type ClinicalWorkflowAssignee,
  type ClinicalWorkflowTransitionOption,
  type PatientReferral,
  type PatientReferralWorkflowEvent,
} from "../../api.ts";
import type { PatientOutletContext } from "./PatientShell.tsx";

export default function PatientReferrals() {
  const { session, patientId } = useOutletContext<PatientOutletContext>();
  const [referrals, setReferrals] = useState<PatientReferral[]>([]);
  const [assignees, setAssignees] = useState<ClinicalWorkflowAssignee[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [history, setHistory] = useState<PatientReferralWorkflowEvent[]>([]);
  const [transition, setTransition] = useState<ClinicalWorkflowTransitionOption | null>(null);
  const [transitionReason, setTransitionReason] = useState("");
  const [assignment, setAssignment] = useState({ assignedTo: "", dueAt: "", reason: "" });
  const [working, setWorking] = useState("");
  const [form, setForm] = useState({
    destination: "",
    reason: "",
    externalReference: "",
    notes: "",
    assignedTo: "",
    dueAt: "",
    workflowReason: "",
  });
  const [error, setError] = useState("");
  const selected = useMemo(() => referrals.find((item) => item.id === selectedId) ?? null, [referrals, selectedId]);
  const load = useCallback(async () => {
    try {
      const [items, roster] = await Promise.all([getPatientReferrals(session.sessionId, patientId), getClinicalWorkflowAssignees(session.sessionId)]);
      setReferrals(items);
      setAssignees(roster.assignees);
      setForm((current) => ({ ...current, assignedTo: current.assignedTo || roster.assignees.find((item) => item.username === session.username)?.username || roster.assignees[0]?.username || "" }));
      setSelectedId((current) => current && items.some((item) => item.id === current) ? current : (items[0]?.id ?? null));
    } catch (reason) {
        setError(
          reason instanceof Error
            ? reason.message
            : "Unable to load referrals.",
        );
    }
  }, [patientId, session.sessionId, session.username]);
  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    if (!selected) return;
    setAssignment({ assignedTo: selected.assignedTo, dueAt: selected.dueAt?.slice(0, 10) ?? "", reason: "" });
    setTransition(null); setTransitionReason("");
    getPatientReferralHistory(session.sessionId, patientId, selected.id).then((value) => setHistory(value.events)).catch(() => setHistory([]));
  }, [patientId, selected?.id, session.sessionId]); // eslint-disable-line react-hooks/exhaustive-deps
  async function create() {
    if (!form.destination.trim() || !form.reason.trim()) return;
    try {
      await createPatientReferral(session.sessionId, patientId, {
        destination: form.destination,
        reason: form.reason,
        externalReference: form.externalReference || undefined,
        notes: form.notes || undefined,
        assignedTo: form.assignedTo,
        dueAt: form.dueAt || undefined,
        workflowReason: form.workflowReason,
      });
      setForm({
        destination: "",
        reason: "",
        externalReference: "",
        notes: "",
        assignedTo: form.assignedTo,
        dueAt: "",
        workflowReason: "",
      });
      setError("");
      load();
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : "Unable to create referral.",
      );
    }
  }
  async function applyTransition(
    referralId: string,
    option: ClinicalWorkflowTransitionOption,
  ) {
    try {
      if (!selected || !transitionReason.trim()) { setError("A transition reason is required."); return; }
      setWorking(`transition-${referralId}`);
      const updated = await updatePatientReferralStatus(
        session.sessionId,
        patientId,
        referralId,
        { status: option.toState as "sent" | "received" | "closed" | "cancelled", expectedVersion: selected.workflowVersion, reasonCode: option.reasonCode, reason: transitionReason.trim() },
      );
      setReferrals((current) => current.map((item) => item.id === updated.id ? updated : item));
      setTransition(null); setTransitionReason("");
      setError("");
      await load();
    } catch (reason) {
      if (reason instanceof ApiRequestError && reason.status === 409) { setError("This referral changed after you opened it. The current values were reloaded."); await load(); return; }
      setError(
        reason instanceof Error ? reason.message : "Unable to update referral.",
      );
    } finally { setWorking(""); }
  }
  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Referrals</h1>
        <p className="clinician-page-subtitle">
          Track a locally documented referral from draft through closure.
          External delivery is not implied.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-inline-form">
          <label className="cl-admin-field">
            <span>Destination</span>
            <input
              className="ne-input"
              value={form.destination}
              onChange={(event) =>
                setForm({ ...form, destination: event.target.value })
              }
            />
          </label>
          <label className="cl-admin-field">
            <span>Reason</span>
            <input
              className="ne-input"
              value={form.reason}
              onChange={(event) =>
                setForm({ ...form, reason: event.target.value })
              }
            />
          </label>
          <label className="cl-admin-field">
            <span>External reference</span>
            <input
              className="ne-input"
              value={form.externalReference}
              onChange={(event) =>
                setForm({ ...form, externalReference: event.target.value })
              }
            />
          </label>
          <label className="cl-admin-field">
            <span>Notes</span>
            <input
              className="ne-input"
              value={form.notes}
              onChange={(event) =>
                setForm({ ...form, notes: event.target.value })
              }
            />
          </label>
          <label className="cl-admin-field">
            <span>Responsible staff</span>
            <select className="ne-input" value={form.assignedTo} onChange={(event) => setForm({ ...form, assignedTo: event.target.value })} required>
              <option value="">Select staff</option>
              {assignees.map((assignee) => <option key={assignee.username} value={assignee.username}>{assignee.displayName} · {assignee.role}</option>)}
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Work due date</span>
            <input className="ne-input" type="date" value={form.dueAt} onChange={(event) => setForm({ ...form, dueAt: event.target.value })} />
          </label>
          <label className="cl-admin-field">
            <span>Creation reason</span>
            <input className="ne-input" value={form.workflowReason} onChange={(event) => setForm({ ...form, workflowReason: event.target.value })} required />
          </label>
          <div className="cl-inline-form-actions">
            <button className="cl-btn-primary" type="button" onClick={create}>
              Create draft
            </button>
          </div>
        </div>
        {error && <p className="cl-error-text">{error}</p>}
      </section>
      <section className="cl-card">
        <table className="cl-table">
          <thead>
            <tr>
              <th>Requested</th>
              <th>Destination</th>
              <th>Reason</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {referrals.map((referral) => (
              <tr key={referral.id} className={selectedId === referral.id ? "is-selected" : ""}>
                <td>{new Date(referral.requestedAt).toLocaleDateString()}</td>
                <td>{referral.destination}</td>
                <td>{referral.reason}</td>
                <td>{referral.status}<br /><small>{referral.assignedDisplayName} · v{referral.workflowVersion}</small></td>
                <td>
                  <button className="cl-btn-secondary" type="button" onClick={() => setSelectedId(referral.id)}>Manage</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {referrals.length === 0 && (
          <p className="cl-empty-text">No referrals have been recorded.</p>
        )}
      </section>
      {selected && <section className="cl-card" aria-label="Referral workflow details">
        <h2 className="cl-card-title">{selected.destination} workflow</h2>
        <p className="cl-card-subtitle">Owner {selected.assignedDisplayName}; due {selected.dueAt ? new Date(selected.dueAt).toLocaleDateString(undefined, { timeZone: "UTC" }) : "not set"}; policy {selected.policyRevision}.</p>
        <div className="cl-inline-form">
          <label className="cl-admin-field"><span>New responsible staff</span><select className="ne-input" value={assignment.assignedTo} onChange={(event) => setAssignment({ ...assignment, assignedTo: event.target.value })}>{assignees.map((item) => <option key={item.username} value={item.username}>{item.displayName}</option>)}</select></label>
          <label className="cl-admin-field"><span>Due date</span><input className="ne-input" type="date" value={assignment.dueAt} onChange={(event) => setAssignment({ ...assignment, dueAt: event.target.value })} /></label>
          <label className="cl-admin-field"><span>Handoff reason</span><input className="ne-input" value={assignment.reason} onChange={(event) => setAssignment({ ...assignment, reason: event.target.value })} /></label>
          <div className="cl-inline-form-actions"><button className="cl-btn-secondary" type="button" disabled={!assignment.reason.trim() || working === `assignment-${selected.id}`} onClick={async () => { try { setWorking(`assignment-${selected.id}`); const updated = await updatePatientReferralAssignment(session.sessionId, patientId, selected.id, { assignedTo: assignment.assignedTo, dueAt: assignment.dueAt || undefined, expectedVersion: selected.workflowVersion, reasonCode: "responsibility-transfer", reason: assignment.reason.trim() }); setReferrals((current) => current.map((item) => item.id === updated.id ? updated : item)); setAssignment({ ...assignment, reason: "" }); await load(); } catch (reason) { setError(reason instanceof Error ? reason.message : "Unable to update responsibility."); } finally { setWorking(""); } }}>Save responsibility</button></div>
        </div>
        <div className="cl-inline-form">
          {selected.availableTransitions.map((option) => <button key={`${option.action}-${option.toState}`} className={transition?.toState === option.toState ? "cl-btn-primary" : "cl-btn-secondary"} type="button" onClick={() => { setTransition(option); setTransitionReason(""); }}>{option.label}</button>)}
          {transition && <><label className="cl-admin-field"><span>{transition.label} reason</span><input className="ne-input" value={transitionReason} onChange={(event) => setTransitionReason(event.target.value)} /></label><div className="cl-inline-form-actions"><button className="cl-btn-primary" type="button" disabled={!transitionReason.trim() || working === `transition-${selected.id}`} onClick={() => applyTransition(selected.id, transition)}>Confirm {transition.label}</button></div></>}
        </div>
        <h3>Immutable history</h3>
        <ul>{history.map((event) => <li key={event.eventId}>v{event.workflowVersion} {event.action}: {event.reason} — {event.actor}</li>)}</ul>
      </section>}
    </div>
  );
}
