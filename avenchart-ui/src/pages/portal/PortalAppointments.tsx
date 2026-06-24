import { useState, type FormEvent } from 'react'
import { useOutletContext } from 'react-router-dom'
import { CalendarClock, CalendarPlus, ChevronDown, ChevronUp } from 'lucide-react'
import {
  getPatientPortalAppointmentRequestOptions,
  requestPatientPortalAppointment,
  type PatientPortalAppointmentRequestOptionsResponse,
  type PatientPortalHomeAppointmentSummary,
} from '../../api.ts'
import type { PortalOutletContext } from './PortalShell.tsx'

type AsyncState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

function formatApptDate(dateStr: string) {
  const [y, m, d] = dateStr.split('-').map(Number)
  const date = new Date(y, m - 1, d)
  return {
    month: date.toLocaleString('en-US', { month: 'short' }),
    day: date.getDate(),
    weekday: date.toLocaleString('en-US', { weekday: 'short' }),
    full: date.toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' }),
  }
}

function formatTime(value?: string | null) {
  if (!value) return ''
  return value.length >= 5 ? value.slice(0, 5) : value
}

function statusClass(status?: string | null) {
  if (!status) return ''
  const s = status.toLowerCase()
  if (s.includes('cancel')) return 'appt-status-cancelled'
  if (s.includes('complet') || s.includes('check')) return 'appt-status-completed'
  if (s.includes('pend') || s.includes('request')) return 'appt-status-pending'
  return 'appt-status-scheduled'
}

function buildIcsContent(appt: PatientPortalHomeAppointmentSummary): string {
  const dtStart = `${appt.date.replace(/-/g, '')}T${(appt.startTime ?? '09:00').replace(':', '')}00`
  return [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'BEGIN:VEVENT',
    `DTSTART:${dtStart}`,
    `SUMMARY:${appt.title}`,
    `DESCRIPTION:${[appt.providerName, appt.facilityName].filter(Boolean).join(' · ')}`,
    'END:VEVENT',
    'END:VCALENDAR',
  ].join('\r\n')
}

function downloadIcs(appt: PatientPortalHomeAppointmentSummary) {
  const blob = new Blob([buildIcsContent(appt)], { type: 'text/calendar' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `appointment-${appt.id}.ics`
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

export default function PortalAppointments() {
  const { session, home, homeLoading, refreshHome } = useOutletContext<PortalOutletContext>()
  const [requestOpen, setRequestOpen] = useState(false)
  const [optionsState, setOptionsState] = useState<
    AsyncState<PatientPortalAppointmentRequestOptionsResponse>
  >({ status: 'idle' })
  const [form, setForm] = useState({
    categoryId: '',
    providerId: '',
    facilityId: '',
    date: '',
    startTime: '',
    durationMinutes: 20,
    reason: '',
  })
  const [submitting, setSubmitting] = useState(false)
  const [result, setResult] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  function toggleRequest() {
    setRequestOpen((open) => !open)
    setResult(null)
    setError(null)
    if (optionsState.status === 'idle') {
      setOptionsState({ status: 'loading' })
      getPatientPortalAppointmentRequestOptions(session.sessionId)
        .then((data) => {
          setOptionsState({ status: 'ready', data })
          setForm({
            categoryId: data.defaults.categoryId != null ? String(data.defaults.categoryId) : '',
            providerId: data.defaults.providerId != null ? String(data.defaults.providerId) : '',
            facilityId: data.defaults.facilityId != null ? String(data.defaults.facilityId) : '',
            date: data.defaults.date,
            startTime: formatTime(data.defaults.startTime),
            durationMinutes: data.defaults.durationMinutes,
            reason: '',
          })
        })
        .catch((err) =>
          setOptionsState({
            status: 'error',
            message: err instanceof Error ? err.message : 'Could not load appointment options.',
          }),
        )
    }
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
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
          setError(res.failureReason ?? 'The appointment request was not accepted.')
          return
        }
        const appt = res.appointment
        setResult(
          `Request submitted: ${appt.title} on ${appt.date} at ${formatTime(appt.startTime)}.`,
        )
        refreshHome()
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Could not submit the request.'))
      .finally(() => setSubmitting(false))
  }

  const appointments = home?.upcomingAppointments ?? []

  return (
    <div className="portal-page">
      <section className="portal-section">
        <div className="portal-section-header">
          <h2 className="portal-section-title">Upcoming appointments</h2>
          <button
            className={`toggle-button${requestOpen ? ' toggle-button-active' : ''}`}
            type="button"
            onClick={toggleRequest}
          >
            <CalendarPlus size={15} />
            {requestOpen ? 'Close' : 'Request an appointment'}
            {requestOpen ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
          </button>
        </div>

        {/* Request form */}
        {requestOpen && (
          <div className="inline-panel" style={{ marginBottom: 20 }}>
            <h3 className="inline-panel-title">Appointment request</h3>
            {optionsState.status === 'loading' && (
              <div className="skeleton-list">
                {[0, 1].map((i) => <div key={i} className="skeleton-row" />)}
              </div>
            )}
            {optionsState.status === 'error' && (
              <div className="error-banner">{optionsState.message}</div>
            )}
            {result && <div className="hint-banner">{result}</div>}
            {optionsState.status === 'ready' && !result && (
              <form onSubmit={handleSubmit}>
                <div className="form-row">
                  <div className="field">
                    <label className="label" htmlFor="appt-cat">Visit type</label>
                    <select
                      id="appt-cat"
                      className="select"
                      value={form.categoryId}
                      onChange={(e) => setForm((f) => ({ ...f, categoryId: e.target.value }))}
                    >
                      {optionsState.data.categories.map((c) => (
                        <option key={c.id} value={c.id}>{c.name}</option>
                      ))}
                    </select>
                  </div>
                  <div className="field">
                    <label className="label" htmlFor="appt-prov">Provider</label>
                    <select
                      id="appt-prov"
                      className="select"
                      value={form.providerId}
                      onChange={(e) => setForm((f) => ({ ...f, providerId: e.target.value }))}
                    >
                      {optionsState.data.providers.map((p) => (
                        <option key={p.id} value={p.id}>{p.displayName}</option>
                      ))}
                    </select>
                  </div>
                </div>
                <div className="form-row">
                  <div className="field">
                    <label className="label" htmlFor="appt-date">Date</label>
                    <input
                      id="appt-date"
                      type="date"
                      className="input"
                      value={form.date}
                      onChange={(e) => setForm((f) => ({ ...f, date: e.target.value }))}
                      required
                    />
                  </div>
                  <div className="field">
                    <label className="label" htmlFor="appt-time">Time</label>
                    <input
                      id="appt-time"
                      type="time"
                      className="input"
                      value={form.startTime}
                      onChange={(e) => setForm((f) => ({ ...f, startTime: e.target.value }))}
                      required
                    />
                  </div>
                </div>
                <div className="field">
                  <label className="label" htmlFor="appt-reason">Reason for visit (optional)</label>
                  <textarea
                    id="appt-reason"
                    className="textarea"
                    value={form.reason}
                    onChange={(e) => setForm((f) => ({ ...f, reason: e.target.value }))}
                    rows={3}
                  />
                </div>
                {error && <div className="error-banner">{error}</div>}
                <button className="button-primary" type="submit" disabled={submitting}>
                  {submitting ? 'Sending request…' : 'Send request'}
                </button>
              </form>
            )}
          </div>
        )}

        {/* Appointment list */}
        {homeLoading ? (
          <div className="skeleton-list">
            {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 80 }} />)}
          </div>
        ) : appointments.length === 0 ? (
          <div className="empty-state">
            <div className="empty-state-icon-wrap"><CalendarClock size={28} /></div>
            <p className="empty-state-text">No upcoming appointments on file.</p>
          </div>
        ) : (
          <ul className="appt-list">
            {appointments.map((appt) => {
              const { month, day, weekday } = formatApptDate(appt.date)
              return (
                <li key={appt.id} className="appt-card">
                  <div className="appt-date-block">
                    <p className="appt-date-month">{month}</p>
                    <p className="appt-date-day">{day}</p>
                    <p className="appt-date-weekday">{weekday}</p>
                  </div>
                  <div className="appt-body">
                    <p className="appt-title">{appt.title}</p>
                    <p className="appt-meta">
                      {formatTime(appt.startTime)}
                      {appt.providerName ? ` · ${appt.providerName}` : ''}
                      {appt.facilityName ? ` · ${appt.facilityName}` : ''}
                    </p>
                    {appt.comments && (
                      <p className="appt-comments">{appt.comments}</p>
                    )}
                  </div>
                  <div className="appt-actions">
                    {appt.status && (
                      <span className={`appt-status ${statusClass(appt.status)}`}>{appt.status}</span>
                    )}
                    <button
                      className="appt-ics-button"
                      type="button"
                      title={`Add "${appt.title}" to calendar`}
                      onClick={() => downloadIcs(appt)}
                    >
                      Add to calendar
                    </button>
                  </div>
                </li>
              )
            })}
          </ul>
        )}
      </section>
    </div>
  )
}
