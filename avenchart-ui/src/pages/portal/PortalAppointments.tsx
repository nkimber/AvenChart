import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useLocation, useOutletContext } from 'react-router-dom'
import { CalendarClock, CalendarPlus } from 'lucide-react'
import {
  getPatientPortalAppointmentRequestOptions,
  requestPatientPortalAppointment,
  type PatientPortalAppointmentRequestOptionsResponse,
  type PatientPortalHomeAppointmentSummary,
} from '../../api.ts'
import type { PortalOutletContext } from './PortalShell.tsx'
import { showToast } from '../../components/Toast.tsx'

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
  const location = useLocation()
  const modalPanelRef = useRef<HTMLDivElement>(null)
  const [requestOpen, setRequestOpen] = useState(
    () => location.state?.openRequest === true,
  )
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

  // Focus trap + Escape key for modal (#1)
  useEffect(() => {
    if (!requestOpen) return
    // Move focus into modal
    const firstFocusable = modalPanelRef.current?.querySelector<HTMLElement>(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
    )
    firstFocusable?.focus()

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') { toggleRequest(); return }
      if (e.key !== 'Tab' || !modalPanelRef.current) return
      const focusable = Array.from(
        modalPanelRef.current.querySelectorAll<HTMLElement>(
          'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
        ),
      )
      if (focusable.length === 0) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault(); last.focus()
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault(); first.focus()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [requestOpen])

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
        const msg = `Request submitted: ${appt.title} on ${appt.date} at ${formatTime(appt.startTime)}.`
        setResult(msg)
        showToast(msg)
        refreshHome()
      })
      .catch((err) => {
        const msg = err instanceof Error ? err.message : 'Could not submit the request.'
        setError(msg)
        showToast(msg, 'error')
      })
      .finally(() => setSubmitting(false))
  }

  const appointments = home?.upcomingAppointments ?? []

  return (
    <div className="portal-page">
      {/* ─── Request appointment modal ─── */}
      {requestOpen && (
        <div className="modal-overlay" onClick={(e) => { if (e.target === e.currentTarget) toggleRequest() }}>
          <div className="modal-panel" ref={modalPanelRef} role="dialog" aria-modal="true" aria-labelledby="appt-modal-title">
            <div className="modal-header">
              <h2 id="appt-modal-title" className="modal-title">Request an appointment</h2>
              <button className="modal-close" type="button" onClick={toggleRequest} aria-label="Close">×</button>
            </div>

            {optionsState.status === 'loading' && (
              <div className="skeleton-list">
                {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" />)}
              </div>
            )}
            {optionsState.status === 'error' && (
              <div className="error-banner">{optionsState.message}</div>
            )}
            {result ? (
              <div>
                <div className="hint-banner">{result}</div>
                <button className="button-secondary" style={{ width: 'auto' }} type="button" onClick={toggleRequest}>
                  Close
                </button>
              </div>
            ) : optionsState.status === 'ready' ? (
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
                <div className="button-row">
                  <button className="button-primary" type="submit" disabled={submitting}>
                    {submitting ? 'Sending request…' : 'Send request'}
                  </button>
                  <button
                    className="button-secondary"
                    type="button"
                    onClick={toggleRequest}
                    style={{ width: 'auto', flex: 'none' }}
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
            className="toggle-button"
            type="button"
            onClick={toggleRequest}
          >
            <CalendarPlus size={15} />
            Request an appointment
          </button>
        </div>

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
