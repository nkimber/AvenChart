import { useEffect, useState } from 'react'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { searchAppointments, updateAppointmentStatus, type AppointmentListItem } from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'
import { showToast } from '../../components/Toast.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

function isoDate(d: Date) {
  return d.toISOString().slice(0, 10)
}

function addDays(d: Date, n: number) {
  const r = new Date(d)
  r.setDate(r.getDate() + n)
  return r
}

function formatTime(t?: string | null) {
  if (!t) return ''
  return t.slice(0, 5)
}


const STATUS_OPTIONS = ['Scheduled', 'Arrived', 'In Room', 'Checked Out', 'No Show', 'Cancelled']

export default function ClinicianSchedule() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const [selectedDate, setSelectedDate] = useState(() => isoDate(new Date()))
  const [apptState, setApptState] = useState<AsyncState<AppointmentListItem[]>>({ status: 'loading' })
  const [updatingId, setUpdatingId] = useState<string | null>(null)

  useEffect(() => {
    load()
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedDate])

  function load() {
    setApptState({ status: 'loading' })
    searchAppointments(session.sessionId, { fromDate: selectedDate, toDate: selectedDate, limit: 50 })
      .then((data) => setApptState({ status: 'ready', data: data.appointments }))
      .catch((err) => setApptState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  }

  function changeDay(delta: number) {
    const d = new Date(selectedDate + 'T00:00:00')
    setSelectedDate(isoDate(addDays(d, delta)))
  }

  async function handleStatusChange(apptId: string, status: string) {
    setUpdatingId(apptId)
    try {
      await updateAppointmentStatus(session.sessionId, apptId, status)
      showToast(`Status updated to "${status}"`)
      load()
    } catch {
      showToast('Could not update status.', 'error')
    } finally {
      setUpdatingId(null)
    }
  }

  const dateLabel = new Date(selectedDate + 'T00:00:00').toLocaleDateString('en-US', {
    weekday: 'long', month: 'long', day: 'numeric', year: 'numeric',
  })
  const isToday = selectedDate === isoDate(new Date())

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Schedule</h1>
          <p className="clinician-page-subtitle">{dateLabel}{isToday ? ' (Today)' : ''}</p>
        </div>
        <div className="clinician-header-actions">
          <input
            type="date"
            className="cl-date-input"
            value={selectedDate}
            onChange={(e) => setSelectedDate(e.target.value)}
            aria-label="Select date"
          />
        </div>
      </div>

      {/* Day nav */}
      <div className="cl-day-nav">
        <button className="cl-day-nav-btn" type="button" onClick={() => changeDay(-1)} aria-label="Previous day">
          <ChevronLeft size={16} />
          Previous
        </button>
        <button
          className={`cl-day-nav-btn${isToday ? ' cl-day-nav-btn-active' : ''}`}
          type="button"
          onClick={() => setSelectedDate(isoDate(new Date()))}
        >
          Today
        </button>
        <button className="cl-day-nav-btn" type="button" onClick={() => changeDay(1)} aria-label="Next day">
          Next
          <ChevronRight size={16} />
        </button>
      </div>

      {apptState.status === 'loading' && (
        <section className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2, 3, 4].map((i) => <div key={i} className="skeleton-row" style={{ height: 64 }} />)}
          </div>
        </section>
      )}
      {apptState.status === 'error' && (
        <div className="error-banner">{apptState.message}</div>
      )}
      {apptState.status === 'ready' && apptState.data.length === 0 && (
        <section className="cl-card">
          <p className="cl-empty-text">No appointments for this date.</p>
        </section>
      )}
      {apptState.status === 'ready' && apptState.data.length > 0 && (
        <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
          <table className="cl-table">
            <thead>
              <tr>
                <th>Time</th>
                <th>Patient</th>
                <th>Visit type</th>
                <th>Provider</th>
                <th>Room</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {apptState.data.map((appt) => (
                <tr key={appt.id}>
                  <td className="cl-td-time">{formatTime(appt.startTime)}</td>
                  <td>
                    <button
                      className="cl-table-link"
                      type="button"
                      onClick={() => navigate(`/clinician/patients/${appt.patientId}`)}
                    >
                      {appt.patientDisplayName}
                    </button>
                    <p className="cl-table-sub">{appt.pubpid}</p>
                  </td>
                  <td>{appt.title}</td>
                  <td className="cl-td-muted">{appt.providerName ?? '—'}</td>
                  <td className="cl-td-muted">{appt.room ?? '—'}</td>
                  <td>
                    <select
                      className="cl-status-select"
                      value={appt.status ?? ''}
                      disabled={updatingId === appt.id}
                      onChange={(e) => handleStatusChange(appt.id, e.target.value)}
                      aria-label={`Status for ${appt.patientDisplayName}`}
                    >
                      {!appt.status && <option value="">—</option>}
                      {STATUS_OPTIONS.map((s) => (
                        <option key={s} value={s}>{s}</option>
                      ))}
                    </select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
    </div>
  )
}
