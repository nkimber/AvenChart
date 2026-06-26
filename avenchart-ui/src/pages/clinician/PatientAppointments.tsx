import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { CalendarPlus } from 'lucide-react'
import { createAppointment, searchAppointments, updateAppointmentStatus, type AppointmentListItem } from '../../api.ts'
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

function todayStr() {
  return new Date().toISOString().slice(0, 10)
}

export default function PatientAppointments() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [state, setState] = useState<AsyncState<AppointmentListItem[]>>({ status: 'loading' })
  const [updatingId, setUpdatingId] = useState<string | null>(null)
  const [newApptOpen, setNewApptOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [apptForm, setApptForm] = useState({
    title: 'Office visit',
    date: todayStr(),
    startTime: '09:00',
    durationMinutes: 20,
    comments: '',
  })

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

  async function handleCreateAppointment(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    try {
      await createAppointment(session.sessionId, { patientId, ...apptForm })
      showToast('Appointment created.', 'success')
      setNewApptOpen(false)
      load()
    } catch {
      showToast('Could not create appointment.', 'error')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="clinician-page">
      {/* New appointment modal */}
      {newApptOpen && (
        <div className="modal-overlay" onClick={(e) => { if (e.target === e.currentTarget) setNewApptOpen(false) }}>
          <div className="modal-panel" role="dialog" aria-modal="true" aria-label="New appointment">
            <div className="modal-header">
              <h2 className="modal-title">New appointment</h2>
              <button className="modal-close" type="button" onClick={() => setNewApptOpen(false)} aria-label="Close">×</button>
            </div>
            <form onSubmit={handleCreateAppointment}>
              <div className="field">
                <label className="label" htmlFor="appt-title">Visit type / title</label>
                <input id="appt-title" className="input" value={apptForm.title} onChange={(e) => setApptForm((f) => ({ ...f, title: e.target.value }))} required />
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="appt-date">Date</label>
                  <input id="appt-date" type="date" className="input" value={apptForm.date} onChange={(e) => setApptForm((f) => ({ ...f, date: e.target.value }))} required />
                </div>
                <div className="field">
                  <label className="label" htmlFor="appt-time">Time</label>
                  <input id="appt-time" type="time" className="input" value={apptForm.startTime} onChange={(e) => setApptForm((f) => ({ ...f, startTime: e.target.value }))} required />
                </div>
              </div>
              <div className="field">
                <label className="label" htmlFor="appt-dur">Duration (minutes)</label>
                <select id="appt-dur" className="select" value={apptForm.durationMinutes} onChange={(e) => setApptForm((f) => ({ ...f, durationMinutes: Number(e.target.value) }))}>
                  {[10, 15, 20, 30, 45, 60, 90].map((d) => <option key={d} value={d}>{d} min</option>)}
                </select>
              </div>
              <div className="field">
                <label className="label" htmlFor="appt-comments">Comments (optional)</label>
                <textarea id="appt-comments" className="textarea" rows={2} value={apptForm.comments} onChange={(e) => setApptForm((f) => ({ ...f, comments: e.target.value }))} />
              </div>
              <div className="button-row">
                <button className="button-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Create appointment'}</button>
                <button className="button-secondary" type="button" onClick={() => setNewApptOpen(false)} style={{ flex: 'none', width: 'auto' }}>Cancel</button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
        <button className="cl-btn-primary" type="button" onClick={() => setNewApptOpen(true)}>
          <CalendarPlus size={14} /> New appointment
        </button>
      </div>

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
