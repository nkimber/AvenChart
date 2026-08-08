// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { useLocation, useOutletContext } from "react-router-dom";
import { CalendarClock, CalendarPlus } from "lucide-react";
import {
  getPatientPortalAppointmentRequestOptions,
  requestPatientPortalAppointment,
  type PatientPortalAppointmentRequestOptionsResponse,
  type PatientPortalHomeAppointmentSummary,
} from "../../api.ts";
import {
  getPatientPortalAppointmentsWithRequestHistory,
  type PatientPortalAppointmentRequestHistoryItem,
  type PatientPortalAppointmentsWithRequestHistoryResponse,
} from "../../api/portalAppointments.ts";
import type { PortalOutletContext } from "./PortalShell.tsx";
import { showToast } from "../../components/Toast.tsx";
import { AppointmentStatusBadge } from "../../components/AppointmentStatusBadge.tsx";

type AsyncState<T> =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "ready"; data: T }
  | { status: "error"; message: string };

function formatApptDate(dateStr: string) {
  const [y, m, d] = dateStr.split("-").map(Number);
  const date = new Date(y, m - 1, d);
  return {
    month: date.toLocaleString("en-US", { month: "short" }),
    day: date.getDate(),
    weekday: date.toLocaleString("en-US", { weekday: "short" }),
    full: date.toLocaleDateString("en-US", {
      weekday: "long",
      month: "long",
      day: "numeric",
      year: "numeric",
    }),
  };
}

function formatTime(value?: string | null) {
  if (!value) return "";
  return value.length >= 5 ? value.slice(0, 5) : value;
}

function formatTimestamp(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString("en-US", {
        dateStyle: "medium",
        timeStyle: "short",
      });
}

function requestStateClass(
  state: PatientPortalAppointmentRequestHistoryItem["state"],
) {
  if (state === "accepted") return "cl-badge-green";
  if (state === "pending") return "cl-badge-amber";
  if (state === "expired") return "cl-badge-muted";
  return "cl-badge-red";
}

function buildIcsContent(appt: PatientPortalHomeAppointmentSummary): string {
  const dtStart = `${appt.date.replace(/-/g, "")}T${(appt.startTime ?? "09:00").replace(":", "")}00`;
  return [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "BEGIN:VEVENT",
    `DTSTART:${dtStart}`,
    `SUMMARY:${appt.title}`,
    `DESCRIPTION:${[appt.providerName, appt.facilityName].filter(Boolean).join(" · ")}`,
    "END:VEVENT",
    "END:VCALENDAR",
  ].join("\r\n");
}

function downloadIcs(appt: PatientPortalHomeAppointmentSummary) {
  const blob = new Blob([buildIcsContent(appt)], { type: "text/calendar" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `appointment-${appt.id}.ics`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

function AppointmentCard({
  appointment,
  allowCalendar,
}: {
  appointment: PatientPortalHomeAppointmentSummary;
  allowCalendar: boolean;
}) {
  const { month, day, weekday } = formatApptDate(appointment.date);
  const portalRequest = appointment.id.startsWith("APPT-PORTAL-");
  return (
    <li className="appt-card">
      <div className="appt-date-block">
        <p className="appt-date-month">{month}</p>
        <p className="appt-date-day">{day}</p>
        <p className="appt-date-weekday">{weekday}</p>
      </div>
      <div className="appt-body">
        <p className="appt-title">{appointment.title}</p>
        <p className="appt-meta">
          {formatTime(appointment.startTime)}
          {appointment.providerName ? ` · ${appointment.providerName}` : ""}
          {appointment.facilityName ? ` · ${appointment.facilityName}` : ""}
        </p>
        {portalRequest && (
          <p className="appt-request-origin">
            Submitted through the patient portal · request {appointment.id}
          </p>
        )}
        {appointment.comments && (
          <p className="appt-comments">{appointment.comments}</p>
        )}
      </div>
      <div className="appt-actions">
        <AppointmentStatusBadge value={appointment.status} />
        {allowCalendar && (
          <button
            className="appt-ics-button"
            type="button"
            title={`Add "${appointment.title}" to calendar`}
            onClick={() => downloadIcs(appointment)}
          >
            Add to calendar
          </button>
        )}
      </div>
    </li>
  );
}

export default function PortalAppointments() {
  const { session, home, homeLoading, refreshHome } =
    useOutletContext<PortalOutletContext>();
  const location = useLocation();
  const modalPanelRef = useRef<HTMLDivElement>(null);
  const requestButtonRef = useRef<HTMLButtonElement>(null);
  const [requestOpen, setRequestOpen] = useState(
    () => location.state?.openRequest === true,
  );
  const [appointmentsState, setAppointmentsState] = useState<
    AsyncState<PatientPortalAppointmentsWithRequestHistoryResponse>
  >({ status: "loading" });
  const [optionsState, setOptionsState] = useState<
    AsyncState<PatientPortalAppointmentRequestOptionsResponse>
  >({ status: "idle" });
  const [form, setForm] = useState({
    categoryId: "",
    providerId: "",
    facilityId: "",
    date: "",
    startTime: "",
    durationMinutes: 20,
    reason: "",
  });
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadAppointments = useCallback(async () => {
    setAppointmentsState({ status: "loading" });
    try {
      const data = await getPatientPortalAppointmentsWithRequestHistory(
        session.sessionId,
      );
      setAppointmentsState({ status: "ready", data });
    } catch (caught) {
      setAppointmentsState({
        status: "error",
        message:
          caught instanceof Error
            ? caught.message
            : "Could not load appointment history.",
      });
    }
  }, [session.sessionId]);

  const loadOptions = useCallback(async () => {
    setOptionsState({ status: "loading" });
    try {
      const data = await getPatientPortalAppointmentRequestOptions(
        session.sessionId,
      );
      setOptionsState({ status: "ready", data });
      setForm((current) => ({
        ...current,
        categoryId:
          data.defaults.categoryId != null
            ? String(data.defaults.categoryId)
            : "",
        providerId:
          data.defaults.providerId != null
            ? String(data.defaults.providerId)
            : "",
        facilityId:
          data.defaults.facilityId != null
            ? String(data.defaults.facilityId)
            : "",
        date: data.defaults.date,
        startTime: formatTime(data.defaults.startTime),
        durationMinutes: data.defaults.durationMinutes,
      }));
    } catch (caught) {
      setOptionsState({
        status: "error",
        message:
          caught instanceof Error
            ? caught.message
            : "Could not load appointment options.",
      });
    }
  }, [session.sessionId]);

  function closeRequest() {
    setRequestOpen(false);
    setResult(null);
    setError(null);
  }

  useEffect(() => {
    void loadAppointments();
  }, [loadAppointments]);

  useEffect(() => {
    if (!requestOpen) return;
    const previouslyFocused = document.activeElement;
    const requestButton = requestButtonRef.current;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const firstFocusable = modalPanelRef.current?.querySelector<HTMLElement>(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
    );
    firstFocusable?.focus();

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        closeRequest();
        return;
      }
      if (e.key !== "Tab" || !modalPanelRef.current) return;
      const focusable = Array.from(
        modalPanelRef.current.querySelectorAll<HTMLElement>(
          'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
        ),
      );
      if (focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = previousOverflow;
      if (previouslyFocused instanceof HTMLElement) {
        previouslyFocused.focus();
      } else {
        requestButton?.focus();
      }
    };
  }, [requestOpen]);

  useEffect(() => {
    if (requestOpen && optionsState.status === "idle") {
      void loadOptions();
    }
  }, [loadOptions, optionsState.status, requestOpen]);

  function toggleRequest() {
    if (requestOpen) {
      closeRequest();
    } else {
      setResult(null);
      setError(null);
      setRequestOpen(true);
    }
  }

  function retryOptions() {
    void loadOptions();
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    requestPatientPortalAppointment(session.sessionId, {
      categoryId: form.categoryId ? Number(form.categoryId) : undefined,
      providerId: form.providerId ? Number(form.providerId) : undefined,
      facilityId: form.facilityId ? Number(form.facilityId) : undefined,
      date: form.date,
      startTime: form.startTime,
      durationMinutes: form.durationMinutes,
      reason: form.reason || undefined,
    })
      .then((res) => {
        if (!res.created || !res.appointment) {
          setError(
            res.failureReason ?? "The appointment request was not accepted.",
          );
          return;
        }
        const appt = res.appointment;
        const msg = `Request submitted: ${appt.title} on ${appt.date} at ${formatTime(appt.startTime)}.`;
        setResult(msg);
        showToast(msg);
        refreshHome();
        void loadAppointments();
      })
      .catch((err) => {
        const msg =
          err instanceof Error ? err.message : "Could not submit the request.";
        setError(msg);
        showToast(msg, "error");
      })
      .finally(() => setSubmitting(false));
  }

  const appointments =
    appointmentsState.status === "ready"
      ? appointmentsState.data.upcomingAppointments
      : (home?.upcomingAppointments ?? []);
  const pastAppointments =
    appointmentsState.status === "ready"
      ? appointmentsState.data.pastAppointments
      : [];
  const appointmentRequests =
    appointmentsState.status === "ready"
      ? appointmentsState.data.appointmentRequests
      : [];
  const selectedProvider =
    optionsState.status === "ready"
      ? optionsState.data.providers.find(
          (provider) => String(provider.id) === form.providerId,
        )
      : undefined;
  const selectedFacility =
    optionsState.status === "ready"
      ? optionsState.data.facilities.find(
          (facility) => String(facility.id) === form.facilityId,
        )
      : undefined;

  return (
    <div className="portal-page">
      {/* ─── Request appointment modal ─── */}
      {requestOpen && (
        <div
          className="modal-overlay"
          onClick={(e) => {
            if (e.target === e.currentTarget) toggleRequest();
          }}
        >
          <div
            className="modal-panel"
            ref={modalPanelRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="appt-modal-title"
          >
            <div className="modal-header">
              <h2 id="appt-modal-title" className="modal-title">
                Request an appointment
              </h2>
              <button
                className="modal-close"
                type="button"
                onClick={toggleRequest}
                aria-label="Close"
              >
                ×
              </button>
            </div>

            {optionsState.status === "loading" && (
              <div className="skeleton-list">
                {[0, 1, 2].map((i) => (
                  <div key={i} className="skeleton-row" />
                ))}
              </div>
            )}
            {optionsState.status === "error" && (
              <div>
                <div className="error-banner" role="alert">
                  {optionsState.message}
                </div>
                <button
                  className="button-secondary"
                  type="button"
                  onClick={retryOptions}
                >
                  Retry options
                </button>
              </div>
            )}
            {result ? (
              <div>
                <div className="hint-banner">{result}</div>
                <button
                  className="button-secondary"
                  style={{ width: "auto" }}
                  type="button"
                  onClick={toggleRequest}
                >
                  Close
                </button>
              </div>
            ) : optionsState.status === "ready" ? (
              <form onSubmit={handleSubmit}>
                <div className="form-row">
                  <div className="field">
                    <label className="label" htmlFor="appt-cat">
                      Visit type
                    </label>
                    <select
                      id="appt-cat"
                      className="select"
                      value={form.categoryId}
                      onChange={(e) =>
                        setForm((f) => ({ ...f, categoryId: e.target.value }))
                      }
                      required
                    >
                      <option value="" disabled>
                        Select a visit type
                      </option>
                      {optionsState.data.categories.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="field">
                    <label className="label" htmlFor="appt-prov">
                      Provider
                    </label>
                    <select
                      id="appt-prov"
                      className="select"
                      value={form.providerId}
                      onChange={(e) =>
                        setForm((f) => ({ ...f, providerId: e.target.value }))
                      }
                      required
                    >
                      <option value="" disabled>
                        Select a provider
                      </option>
                      {optionsState.data.providers.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.displayName} (provider #{p.id})
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
                <div className="field">
                  <label className="label" htmlFor="appt-facility">
                    Facility
                  </label>
                  <select
                    id="appt-facility"
                    className="select"
                    value={form.facilityId}
                    onChange={(e) =>
                      setForm((f) => ({ ...f, facilityId: e.target.value }))
                    }
                    required
                  >
                    <option value="" disabled>
                      Select a facility
                    </option>
                    {optionsState.data.facilities.map((facility) => (
                      <option key={facility.id} value={facility.id}>
                        {facility.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-row">
                  <div className="field">
                    <label className="label" htmlFor="appt-date">
                      Date
                    </label>
                    <input
                      id="appt-date"
                      type="date"
                      className="input"
                      value={form.date}
                      onChange={(e) =>
                        setForm((f) => ({ ...f, date: e.target.value }))
                      }
                      required
                    />
                  </div>
                  <div className="field">
                    <label className="label" htmlFor="appt-time">
                      Time
                    </label>
                    <input
                      id="appt-time"
                      type="time"
                      className="input"
                      value={form.startTime}
                      onChange={(e) =>
                        setForm((f) => ({ ...f, startTime: e.target.value }))
                      }
                      required
                    />
                  </div>
                </div>
                <div className="field">
                  <label className="label" htmlFor="appt-reason">
                    Reason for visit (optional)
                  </label>
                  <textarea
                    id="appt-reason"
                    className="textarea"
                    value={form.reason}
                    onChange={(e) =>
                      setForm((f) => ({ ...f, reason: e.target.value }))
                    }
                    rows={3}
                  />
                </div>
                {selectedProvider &&
                  selectedFacility &&
                  form.date &&
                  form.startTime && (
                    <div className="hint-banner" role="status">
                      Requesting{" "}
                      {optionsState.data.categories.find(
                        (category) => String(category.id) === form.categoryId,
                      )?.name ?? "a visit"}{" "}
                      with {selectedProvider.displayName} (provider #
                      {selectedProvider.id}) at {selectedFacility.name} on{" "}
                      {formatApptDate(form.date).full} at{" "}
                      {formatTime(form.startTime)}.
                    </div>
                  )}
                {error && <div className="error-banner">{error}</div>}
                <div className="button-row">
                  <button
                    className="button-primary"
                    type="submit"
                    disabled={submitting}
                  >
                    {submitting ? "Sending request…" : "Send request"}
                  </button>
                  <button
                    className="button-secondary"
                    type="button"
                    onClick={toggleRequest}
                    style={{ width: "auto", flex: "none" }}
                  >
                    Cancel
                  </button>
                </div>
              </form>
            ) : null}
          </div>
        </div>
      )}

      <section className="portal-section">
        <div className="portal-section-header">
          <h2 className="portal-section-title">Upcoming appointments</h2>
          <button
            ref={requestButtonRef}
            className="toggle-button"
            type="button"
            onClick={toggleRequest}
          >
            <CalendarPlus size={15} />
            Request an appointment
          </button>
        </div>

        {appointmentsState.status === "error" && (
          <div className="error-banner" role="alert">
            {appointmentsState.message}
            <button
              className="cl-link"
              type="button"
              onClick={() => void loadAppointments()}
            >
              Retry history
            </button>
          </div>
        )}

        {/* Appointment list */}
        {homeLoading && appointmentsState.status === "loading" ? (
          <div className="skeleton-list">
            {[0, 1, 2].map((i) => (
              <div key={i} className="skeleton-row" style={{ height: 80 }} />
            ))}
          </div>
        ) : appointments.length === 0 ? (
          <div className="empty-state">
            <div className="empty-state-icon-wrap">
              <CalendarClock size={28} />
            </div>
            <p className="empty-state-text">
              No upcoming appointments on file.
            </p>
          </div>
        ) : (
          <ul className="appt-list">
            {appointments.map((appointment) => (
              <AppointmentCard
                appointment={appointment}
                allowCalendar
                key={appointment.id}
              />
            ))}
          </ul>
        )}
      </section>

      <section
        className="portal-section"
        aria-labelledby="appointment-request-history-title"
      >
        <div className="portal-section-header">
          <div>
            <h2
              className="portal-section-title"
              id="appointment-request-history-title"
            >
              Appointment request history
            </h2>
            <p className="portal-section-subtitle">
              Durable request state, timing, and the next available action.
            </p>
          </div>
          {appointmentsState.status === "ready" && (
            <span className="cl-badge cl-badge-muted">
              {appointmentRequests.length} of{" "}
              {appointmentsState.data.appointmentRequestCount}
            </span>
          )}
        </div>

        {appointmentsState.status === "loading" ? (
          <div className="skeleton-list">
            {[0, 1].map((item) => (
              <div className="skeleton-row" key={item} style={{ height: 120 }} />
            ))}
          </div>
        ) : appointmentsState.status === "error" ? (
          <div className="empty-state">
            <div className="empty-state-icon-wrap">
              <CalendarClock size={28} />
            </div>
            <p className="empty-state-text">
              Appointment request history is temporarily unavailable.
            </p>
            <button
              className="toggle-button"
              type="button"
              onClick={() => void loadAppointments()}
            >
              Retry
            </button>
          </div>
        ) : appointmentRequests.length === 0 ? (
          <div className="empty-state">
            <div className="empty-state-icon-wrap">
              <CalendarClock size={28} />
            </div>
            <p className="empty-state-text">
              No appointment requests are on file.
            </p>
          </div>
        ) : (
          <ol className="portal-appointment-request-list">
            {appointmentRequests.map((request) => (
              <li
                className="portal-appointment-request-card"
                key={request.appointmentId}
              >
                <div className="portal-appointment-request-heading">
                  <div>
                    <h3>{request.title}</h3>
                    <p>
                      Requested {formatTimestamp(request.requestedAt)} · request{" "}
                      <code>{request.appointmentId}</code> · version{" "}
                      {request.version}
                    </p>
                  </div>
                  <span
                    className={`cl-badge ${requestStateClass(request.state)}`}
                  >
                    {request.stateLabel}
                  </span>
                </div>
                <dl className="portal-appointment-request-facts">
                  <div>
                    <dt>Requested visit</dt>
                    <dd>
                      {formatApptDate(request.date).full} at{" "}
                      {formatTime(request.startTime)}
                    </dd>
                  </div>
                  <div>
                    <dt>Provider</dt>
                    <dd>{request.providerName ?? "Not recorded"}</dd>
                  </div>
                  <div>
                    <dt>Facility</dt>
                    <dd>{request.facilityName ?? "Not recorded"}</dd>
                  </div>
                  <div>
                    <dt>Last changed</dt>
                    <dd>{formatTimestamp(request.updatedAt)}</dd>
                  </div>
                </dl>
                {request.reason && (
                  <p className="portal-appointment-request-reason">
                    <strong>Reason:</strong> {request.reason}
                  </p>
                )}
                <p className="portal-appointment-request-next">
                  <strong>Next action:</strong> {request.nextAction}
                </p>
                {request.state === "expired" && (
                  <p className="portal-appointment-request-derived">
                    Expiry is {request.stateSource}; no scheduler event is
                    fabricated.
                  </p>
                )}
                {request.evidenceSource === "migration-backfill" && (
                  <p className="portal-appointment-request-derived">
                    This pre-existing local request was discovered during the
                    lifecycle migration; its earlier transition timing is not
                    reconstructed.
                  </p>
                )}
                <details className="portal-appointment-request-events">
                  <summary>
                    Lifecycle evidence ({request.events.length})
                  </summary>
                  <ol>
                    {request.events.map((event) => (
                      <li key={event.eventId}>
                        <strong>{event.action}</strong> · {event.state} ·{" "}
                        {formatTimestamp(event.occurredAt)}
                        <span>
                          Version {event.sequence} · source{" "}
                          {event.evidenceSource} · diagnostic status{" "}
                          <code>{event.rawStatus}</code>
                        </span>
                      </li>
                    ))}
                  </ol>
                </details>
              </li>
            ))}
          </ol>
        )}
      </section>

      <section className="portal-section">
        <div className="portal-section-header">
          <div>
            <h2 className="portal-section-title">Appointment history</h2>
            <p className="portal-section-subtitle">
              Past scheduled appointments and their recorded status.
            </p>
          </div>
          {appointmentsState.status === "ready" && (
            <span className="cl-badge cl-badge-muted">
              {appointmentsState.data.pastAppointmentCount} past
            </span>
          )}
        </div>
        {appointmentsState.status === "loading" ? (
          <div className="skeleton-list">
            {[0, 1].map((item) => (
              <div className="skeleton-row" key={item} style={{ height: 80 }} />
            ))}
          </div>
        ) : appointmentsState.status === "error" ? (
          <div className="empty-state">
            <div className="empty-state-icon-wrap">
              <CalendarClock size={28} />
            </div>
            <p className="empty-state-text">
              Appointment history is temporarily unavailable.
            </p>
            <button
              className="toggle-button"
              type="button"
              onClick={() => void loadAppointments()}
            >
              Retry
            </button>
          </div>
        ) : pastAppointments.length === 0 ? (
          <div className="empty-state">
            <div className="empty-state-icon-wrap">
              <CalendarClock size={28} />
            </div>
            <p className="empty-state-text">
              No past appointments are on file.
            </p>
          </div>
        ) : (
          <ul className="appt-list">
            {pastAppointments.map((appointment) => (
              <AppointmentCard
                appointment={appointment}
                allowCalendar={false}
                key={appointment.id}
              />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
