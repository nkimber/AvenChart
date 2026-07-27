import { useEffect, useState } from "react";
import { useOutletContext } from "react-router-dom";
import {
  createPatientReferral,
  getPatientReferrals,
  updatePatientReferralStatus,
  type PatientReferral,
} from "../../api.ts";
import type { PatientOutletContext } from "./PatientShell.tsx";

export default function PatientReferrals() {
  const { session, patientId } = useOutletContext<PatientOutletContext>();
  const [referrals, setReferrals] = useState<PatientReferral[]>([]);
  const [form, setForm] = useState({
    destination: "",
    reason: "",
    externalReference: "",
    notes: "",
  });
  const [error, setError] = useState("");
  const load = () =>
    getPatientReferrals(session.sessionId, patientId)
      .then(setReferrals)
      .catch((reason) =>
        setError(
          reason instanceof Error
            ? reason.message
            : "Unable to load referrals.",
        ),
      );
  useEffect(() => {
    load();
  }, [patientId]); // eslint-disable-line react-hooks/exhaustive-deps
  async function create() {
    if (!form.destination.trim() || !form.reason.trim()) return;
    try {
      await createPatientReferral(session.sessionId, patientId, {
        destination: form.destination,
        reason: form.reason,
        externalReference: form.externalReference || undefined,
        notes: form.notes || undefined,
      });
      setForm({
        destination: "",
        reason: "",
        externalReference: "",
        notes: "",
      });
      setError("");
      load();
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : "Unable to create referral.",
      );
    }
  }
  async function transition(
    referralId: string,
    status: "sent" | "received" | "closed" | "cancelled",
  ) {
    try {
      await updatePatientReferralStatus(
        session.sessionId,
        patientId,
        referralId,
        status,
      );
      setError("");
      load();
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : "Unable to update referral.",
      );
    }
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
              <tr key={referral.id}>
                <td>{new Date(referral.requestedAt).toLocaleDateString()}</td>
                <td>{referral.destination}</td>
                <td>{referral.reason}</td>
                <td>{referral.status}</td>
                <td>
                  {referral.status === "draft" && (
                    <>
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        onClick={() => transition(referral.id, "sent")}
                      >
                        Send
                      </button>{" "}
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        onClick={() => transition(referral.id, "cancelled")}
                      >
                        Cancel
                      </button>
                    </>
                  )}
                  {referral.status === "sent" && (
                    <>
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        onClick={() => transition(referral.id, "received")}
                      >
                        Received
                      </button>{" "}
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        onClick={() => transition(referral.id, "cancelled")}
                      >
                        Cancel
                      </button>
                    </>
                  )}
                  {referral.status === "received" && (
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => transition(referral.id, "closed")}
                    >
                      Close
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {referrals.length === 0 && (
          <p className="cl-empty-text">No referrals have been recorded.</p>
        )}
      </section>
    </div>
  );
}
