import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { searchAppointments, updateAppointmentStatus, type AppointmentListItem } from '../../api.ts'
import type { PatientOutletContext } from './PatientShell.tsx'
import { showToast } from '../../components/Toast.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

const STATUS_OPTIONS = ['Scheduled', 'Arrived', 'In Room', 'Checked Out', 'No Show', 'Cancelled']

function formatTime(t?: string | null) {
  if (!t) return ''
  return t.slice(0, 5)
}

export default function PatientAppointments() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [state, setState] = useState<AsyncState<AppointmentListItem[]>>({ status: 'loading' })
  const [updatingId, setUpdatingId] = useState<string | null>(null)

  function load() {
    setState({ status: 'loading' })
    searchAppointments(session.sessionId, { patientId, limit: 50 })
      .then((data) => setState({ status: 'ready', data: data.appointments }))
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  }

  useEffect(() => { load() }, [patientId]) // eslint-disable-line react-hooks/exhaustive-deps

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

  return (
    <div className="clinician-page">
      {state.status === 'loading' && (
        <div className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton-row" style={{ height: 60 }} />)}
          </div>
        </div>
      )}
      {state.status === 'error' && <div className="error-banner">{state.message}</div>}
      {state.status === 'ready' && state.data.length === 0 && (
        <div className="cl-card"><p className="cl-empty-text">No appointments on file.</p></div>
      )}
      {state.status === 'ready' && state.data.length > 0 && (
        <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
          <table className="cl-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Time</th>
                <th>Visit type</th>
                <th>Provider</th>
                <th>Facility</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {state.data.map((appt) => (
                <tr key={appt.id}>
                  <td>{appt.date}</td>
                  <td className="cl-td-time">{formatTime(appt.startTime)}</td>
                  <td>{appt.title}</td>
                  <td className="cl-td-muted">{appt.providerName ?? '—'}</td>
                  <td className="cl-td-muted">{appt.facilityName ?? '—'}</td>
                  <td>
                    <select
                      className="cl-status-select"
                      value={appt.status ?? ''}
                      disabled={updatingId === appt.id}
                      onChange={(e) => handleStatusChange(appt.id, e.target.value)}
                      aria-label={`Appointment status on ${appt.date}`}
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
