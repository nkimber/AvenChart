// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Ban, CheckCircle2, Mail, Phone, Plus, Tag } from "lucide-react";
import {
  addRecallActivity,
  createRecall,
  closeRecall,
  getRecallActivity,
  getRecalls,
  type RecallActivityItem,
  type RecallItem,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
export default function RecallBoard() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [items, setItems] = useState<RecallItem[]>([]);
  const [showClosed, setShowClosed] = useState(false);
  const [activity, setActivity] = useState<
    Record<string, RecallActivityItem[]>
  >({});
  const [form, setForm] = useState({
    patientId: "",
    recallDate: new Date().toISOString().slice(0, 10),
    reason: "",
  });
  const load = async () => {
    try {
      setItems(await getRecalls(session.sessionId, showClosed));
    } catch {
      showToast("Could not load recalls.", "error");
    }
  };
  const loadActivity = async (id: string) => {
    try {
      const history = await getRecallActivity(session.sessionId, id);
      setActivity((a) => ({ ...a, [id]: history }));
    } catch {
      showToast("Could not load recall activity.", "error");
    }
  };
  const loadOnMount = useEffectEvent(load);
  useEffect(() => {
    void loadOnMount();
  }, [session.sessionId, showClosed]);
  const save = async () => {
    try {
      await createRecall(session.sessionId, form);
      setForm({ ...form, patientId: "", reason: "" });
      await load();
      showToast("Recall created.", "success");
    } catch {
      showToast("Recall could not be created.", "error");
    }
  };
  const record = async (
    id: string,
    activityType: "phone" | "postcard" | "label",
  ) => {
    const note = window.prompt(`Optional note for ${activityType} outreach:`);
    if (note === null) return;
    try {
      await addRecallActivity(session.sessionId, id, { activityType, note });
      await loadActivity(id);
      showToast("Recall activity recorded.", "success");
    } catch {
      showToast("Recall activity could not be recorded.", "error");
    }
  };
  const close = async (id: string, status: "completed" | "cancelled") => {
    const action = status === "completed" ? "complete" : "cancel";
    const reason = window.prompt(`Reason to ${action} this recall:`);
    if (reason === null) return;
    if (reason.trim().length < 10) {
      showToast("Enter a closure reason of at least 10 characters.", "error");
      return;
    }
    try {
      await closeRecall(session.sessionId, id, { status, reason: reason.trim() });
      await load();
      showToast(`Recall ${status}.`, "success");
    } catch {
      showToast(`Recall could not be ${status}.`, "error");
    }
  };
  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Recall Board</h1>
        <p className="clinician-page-subtitle">
          Track follow-up recalls and evidence outreach without implying
          external delivery.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-admin-form-grid">
          <label className="cl-admin-field">
            <span>Patient ID</span>
            <input
              className="ne-input"
              value={form.patientId}
              onChange={(e) => setForm({ ...form, patientId: e.target.value })}
            />
          </label>
          <label className="cl-admin-field">
            <span>Recall date</span>
            <input
              className="ne-input"
              type="date"
              value={form.recallDate}
              onChange={(e) => setForm({ ...form, recallDate: e.target.value })}
            />
          </label>
          <label className="cl-admin-field">
            <span>Reason</span>
            <input
              className="ne-input"
              value={form.reason}
              onChange={(e) => setForm({ ...form, reason: e.target.value })}
            />
          </label>
        </div>
        <button
          className="cl-btn-primary"
          disabled={!form.patientId || !form.reason}
          onClick={() => void save()}
        >
          <Plus size={15} /> Create recall
        </button>
      </section>
      <section className="cl-card">
        <label className="cl-admin-field" style={{ marginBottom: 12 }}>
          <span>Recall status</span>
          <select
            className="ne-input"
            value={showClosed ? "all" : "active"}
            onChange={(event) => setShowClosed(event.target.value === "all")}
          >
            <option value="active">Active recalls</option>
            <option value="all">All recalls, including closed</option>
          </select>
        </label>
        <table className="cl-table">
          <thead>
            <tr>
              <th>Due</th>
              <th>Patient</th>
              <th>Reason</th>
              <th>Outreach evidence</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.map((x) => (
              <tr key={x.id}>
                <td>{x.recallDate}</td>
                <td>
                  {x.patientName}
                  <p className="cl-table-sub">{x.patientId}</p>
                </td>
                <td>
                  {x.reason}
                  <p className="cl-table-sub">{x.status}</p>
                  {x.closedAt && (
                    <p className="cl-table-sub">
                      {x.status} by {x.closedBy ?? "unknown"} ·{" "}
                      {new Date(x.closedAt).toLocaleString()}
                      {x.closureReason ? `: ${x.closureReason}` : ""}
                    </p>
                  )}
                </td>
                <td>
                  <div className="cl-actions">
                    {x.status === "active" && <>
                    <button
                      className="cl-btn-secondary"
                      onClick={() => void record(x.id, "phone")}
                    >
                      <Phone size={14} /> Phone
                    </button>
                    <button
                      className="cl-btn-secondary"
                      onClick={() => void record(x.id, "postcard")}
                    >
                      <Mail size={14} /> Postcard
                    </button>
                    <button
                      className="cl-btn-secondary"
                      onClick={() => void record(x.id, "label")}
                    >
                      <Tag size={14} /> Label
                    </button>
                    <button
                      className="cl-btn-secondary"
                      onClick={() => void loadActivity(x.id)}
                    >
                      History
                    </button>
                    </>}
                    {x.status !== "active" && (
                      <button
                        className="cl-btn-secondary"
                        onClick={() => void loadActivity(x.id)}
                      >
                        History
                      </button>
                    )}
                  </div>
                  {activity[x.id]?.map((a) => (
                    <p className="cl-table-sub" key={a.id}>
                      {a.activityType} ·{" "}
                      {new Date(a.recordedAt).toLocaleString()}
                      {a.note ? `: ${a.note}` : ""}
                    </p>
                  ))}
                </td>
                <td>
                  {x.status === "active" && (
                    <div className="cl-actions">
                      <button
                        className="cl-btn-secondary"
                        onClick={() => void close(x.id, "completed")}
                      >
                        <CheckCircle2 size={14} /> Complete
                      </button>
                      <button
                        className="cl-btn-secondary"
                        onClick={() => void close(x.id, "cancelled")}
                      >
                        <Ban size={14} /> Cancel
                      </button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
