// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useMemo, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Bell, Check, Clock3, RotateCcw, Send } from "lucide-react";
import {
  dispatchAppointmentReminder,
  getAppointmentReminderDispatchHistory,
  getAppointmentReminderTemplates,
  getAppointmentWaitlist,
  retryAppointmentReminderDispatch,
  searchAppointments,
  updateAppointmentStatus,
  updatePatientMessageStatus,
  type AppointmentListItem,
  type AppointmentReminderDispatchHistoryResponse,
  type AppointmentReminderTemplateCatalogResponse,
  type AppointmentWaitlistResponse,
} from "../../api.ts";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
import { showToast } from "../../components/Toast.tsx";

type OperationsState =
  | { status: "loading" }
  | {
      status: "ready";
      waitlist: AppointmentWaitlistResponse;
      appointments: AppointmentListItem[];
      templates: AppointmentReminderTemplateCatalogResponse;
      history: AppointmentReminderDispatchHistoryResponse;
    }
  | { status: "error"; message: string };

function formatTime(value?: string | null) {
  return value ? value.slice(0, 5) : "";
}

export default function SchedulingOperations() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [state, setState] = useState<OperationsState>({ status: "loading" });
  const [tab, setTab] = useState<"waitlist" | "reminders">("waitlist");
  const [workingId, setWorkingId] = useState<string | null>(null);
  const [templateId, setTemplateId] = useState("");

  function load() {
    setState({ status: "loading" });
    Promise.all([
      getAppointmentWaitlist(session.sessionId),
      searchAppointments(session.sessionId, { limit: 100 }),
      getAppointmentReminderTemplates(session.sessionId),
      getAppointmentReminderDispatchHistory(session.sessionId),
    ])
      .then(([waitlist, appointments, templates, history]) => {
        setTemplateId(
          (current) =>
            current ||
            templates.templates.find((template) => template.isDefault)
              ?.templateId ||
            templates.templates[0]?.templateId ||
            "",
        );
        setState({
          status: "ready",
          waitlist,
          appointments: appointments.appointments,
          templates,
          history,
        });
      })
      .catch((error) =>
        setState({
          status: "error",
          message:
            error instanceof Error
              ? error.message
              : "Could not load scheduling operations.",
        }),
      );
  }

  // This route starts an external data request when the clinician session changes.
  useEffect(() => {
    load();
  }, [session.sessionId]); // eslint-disable-line react-hooks/exhaustive-deps

  async function promoteWaitlist(appointmentId: string) {
    setWorkingId(appointmentId);
    try {
      await updateAppointmentStatus(session.sessionId, appointmentId, "~");
      showToast("Waitlist request promoted to pending.", "success");
      load();
    } catch {
      showToast("Could not promote waitlist request.", "error");
    } finally {
      setWorkingId(null);
    }
  }

  async function deferWaitlist(reminderId: string, reason?: string | null) {
    if (!window.confirm("Defer this request for scheduling follow-up?")) return;
    setWorkingId(reminderId);
    try {
      await updatePatientMessageStatus(session.sessionId, reminderId, {
        status: "Deferred",
        body: `${reason || "Appointment request"}\n\nDeferred by scheduling staff for follow-up.`,
      });
      showToast("Waitlist request deferred.", "success");
      load();
    } catch {
      showToast("Could not defer waitlist request.", "error");
    } finally {
      setWorkingId(null);
    }
  }

  async function dispatchReminder(appointmentId: string) {
    setWorkingId(appointmentId);
    try {
      await dispatchAppointmentReminder(
        session.sessionId,
        appointmentId,
        templateId,
      );
      showToast("Reminder queued for dispatch.", "success");
      load();
    } catch {
      showToast("Could not dispatch appointment reminder.", "error");
    } finally {
      setWorkingId(null);
    }
  }

  async function retryReminder(appointmentId: string) {
    setWorkingId(appointmentId);
    try {
      await retryAppointmentReminderDispatch(session.sessionId, appointmentId);
      showToast("Reminder retry queued.", "success");
      load();
    } catch {
      showToast("Could not retry appointment reminder.", "error");
    } finally {
      setWorkingId(null);
    }
  }

  const dueAppointments = useMemo(
    () =>
      state.status === "ready"
        ? state.appointments.filter((appointment) => appointment.reminderDue)
        : [],
    [state],
  );
  const latestByAppointment = useMemo(() => {
    const result = new Map<
      string,
      AppointmentReminderDispatchHistoryResponse["entries"][number]
    >();
    if (state.status === "ready")
      state.history.entries.forEach((entry) => {
        if (!result.has(entry.appointmentId))
          result.set(entry.appointmentId, entry);
      });
    return result;
  }, [state]);

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Scheduling operations</h1>
          <p className="clinician-page-subtitle">
            Promote patient requests and manage due appointment reminders.
          </p>
        </div>
      </div>

      <div
        className="cl-tab-bar"
        role="tablist"
        aria-label="Scheduling operations"
      >
        <button
          className={`cl-tab-btn${tab === "waitlist" ? " cl-tab-btn-active" : ""}`}
          type="button"
          role="tab"
          aria-selected={tab === "waitlist"}
          onClick={() => setTab("waitlist")}
        >
          <Clock3 size={15} /> Waitlist
        </button>
        <button
          className={`cl-tab-btn${tab === "reminders" ? " cl-tab-btn-active" : ""}`}
          type="button"
          role="tab"
          aria-selected={tab === "reminders"}
          onClick={() => setTab("reminders")}
        >
          <Bell size={15} /> Reminders
        </button>
      </div>

      {state.status === "loading" && (
        <section className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2].map((index) => (
              <div
                key={index}
                className="skeleton-row"
                style={{ height: 72 }}
              />
            ))}
          </div>
        </section>
      )}
      {state.status === "error" && (
        <div className="error-banner">{state.message}</div>
      )}

      {state.status === "ready" && tab === "waitlist" && (
        <section className="cl-card">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">Waiting requests</h2>
              <p className="cl-table-sub">
                {state.waitlist.totalWaiting} waiting as of{" "}
                {state.waitlist.asOfDate}
              </p>
            </div>
          </div>
          {state.waitlist.items.length === 0 ? (
            <p className="cl-empty-text">No waiting appointment requests.</p>
          ) : (
            <div className="scheduling-operations-list">
              {state.waitlist.items.map((item) => (
                <article
                  className="scheduling-operation-item"
                  key={item.appointmentId}
                >
                  <div>
                    <strong>{item.patientDisplayName}</strong>
                    <p>
                      {item.pubpid} · {item.title}
                    </p>
                    <p>
                      {item.date} · {formatTime(item.startTime)}–
                      {formatTime(item.endTime)} ·{" "}
                      {item.providerName ?? "Unassigned provider"} ·{" "}
                      {item.facilityName ?? "Unassigned facility"}
                    </p>
                    {item.reason && <p>{item.reason}</p>}
                  </div>
                  <div className="scheduling-operation-actions">
                    <span className="cl-badge cl-badge-amber">
                      {item.priority}
                    </span>
                    <button
                      className="cl-btn-primary"
                      type="button"
                      disabled={workingId === item.appointmentId}
                      onClick={() => promoteWaitlist(item.appointmentId)}
                    >
                      <Check size={14} /> Promote pending
                    </button>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      disabled={
                        !item.reminderId || workingId === item.reminderId
                      }
                      onClick={() =>
                        item.reminderId &&
                        deferWaitlist(item.reminderId, item.reason)
                      }
                    >
                      Defer
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      )}

      {state.status === "ready" && tab === "reminders" && (
        <section className="cl-card">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">Due reminders</h2>
              <p className="cl-table-sub">
                Local dispatch evidence only; no external delivery provider is
                connected.
              </p>
            </div>
          </div>
          <label className="field" style={{ maxWidth: 360 }}>
            <span className="label">Dispatch template</span>
            <select
              className="select"
              value={templateId}
              onChange={(event) => setTemplateId(event.target.value)}
            >
              {state.templates.templates.map((template) => (
                <option key={template.templateId} value={template.templateId}>
                  {template.name} · {template.channel}
                </option>
              ))}
            </select>
          </label>
          {dueAppointments.length === 0 ? (
            <p className="cl-empty-text">No reminders are currently due.</p>
          ) : (
            <div className="scheduling-operations-list">
              {dueAppointments.map((appointment) => {
                const previous = latestByAppointment.get(appointment.id);
                return (
                  <article
                    className="scheduling-operation-item"
                    key={appointment.id}
                  >
                    <div>
                      <strong>{appointment.patientDisplayName}</strong>
                      <p>
                        {appointment.date} · {formatTime(appointment.startTime)}{" "}
                        · {appointment.title}
                      </p>
                      <p>
                        {appointment.reminderChannel} ·{" "}
                        {appointment.reminderLeadDays ?? 0}-day lead ·{" "}
                        {appointment.reminderStatus}
                      </p>
                      {previous && (
                        <p>
                          Latest: {previous.dispatchStatus} (
                          {previous.templateName})
                          {previous.retryAttempt
                            ? ` · retry ${previous.retryAttempt}`
                            : ""}
                        </p>
                      )}
                    </div>
                    <div className="scheduling-operation-actions">
                      <button
                        className="cl-btn-primary"
                        type="button"
                        disabled={workingId === appointment.id}
                        onClick={() => dispatchReminder(appointment.id)}
                      >
                        <Send size={14} /> Dispatch
                      </button>
                      {previous && (
                        <button
                          className="cl-btn-secondary"
                          type="button"
                          disabled={workingId === appointment.id}
                          onClick={() => retryReminder(appointment.id)}
                        >
                          <RotateCcw size={14} /> Retry
                        </button>
                      )}
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      )}
    </div>
  );
}
