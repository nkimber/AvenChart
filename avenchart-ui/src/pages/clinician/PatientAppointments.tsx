import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { CalendarPlus, Pencil } from 'lucide-react'
import {
  createAppointment,
  deleteAppointment,
  getAppointmentSchedulingOptions,
  rescheduleAppointmentOccurrence,
  restoreAppointmentOccurrence,
  searchAppointments,
  updateAppointment,
  updateAppointmentStatus,
  type AppointmentListItem,
  type AppointmentOccurrenceRescheduleInput,
  type AppointmentSchedulingOptionsResponse,
  type AppointmentUpdateInput,
} from '../../api.ts'
import type { PatientOutletContext } from './PatientShell.tsx'
import { showToast } from '../../components/Toast.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

type AppointmentEditForm = {
  title: string
  date: string
  startTime: string
  durationMinutes: number
  providerId: string
  facilityId: string
  room: string
  comments: string
  status: string
}

const STATUS_OPTIONS = ['Scheduled', 'Arrived', 'In Room', 'Checked Out', 'No Show', 'Cancelled']
const DURATION_OPTIONS = [10, 15, 20, 30, 45, 60, 90]

function formatTime(t?: string | null) {
  return t ? t.slice(0, 5) : ''
}

function todayStr() {
  return new Date().toISOString().slice(0, 10)
}

function formFromAppointment(appointment: AppointmentListItem): AppointmentEditForm {
  return {
    title: appointment.title,
    date: appointment.date,
    startTime: formatTime(appointment.startTime),
    durationMinutes: appointment.durationMinutes,
    providerId: appointment.providerId?.toString() ?? '',
    facilityId: appointment.facilityId?.toString() ?? '',
    room: appointment.room ?? '',
    comments: appointment.comments ?? '',
    status: appointment.status ?? '',
  }
}

function nullableId(value: string): number | null {
  return value ? Number(value) : null
}

function isCancelledStatus(status?: string | null) {
  return status?.toLowerCase().includes('cancel') ?? false
}

export default function PatientAppointments() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [state, setState] = useState<AsyncState<AppointmentListItem[]>>({ status: 'loading' })
  const [updatingId, setUpdatingId] = useState<string | null>(null)
  const [newApptOpen, setNewApptOpen] = useState(false)
  const [editingAppointment, setEditingAppointment] = useState<AppointmentListItem | null>(null)
  const [reschedulingAppointment, setReschedulingAppointment] = useState<AppointmentListItem | null>(null)
  const [schedulingOptions, setSchedulingOptions] = useState<AppointmentSchedulingOptionsResponse | null>(null)
  const [schedulingOptionsError, setSchedulingOptionsError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [apptForm, setApptForm] = useState({
    title: 'Office visit',
    date: todayStr(),
    startTime: '09:00',
    durationMinutes: 20,
    comments: '',
  })
  const [editForm, setEditForm] = useState<AppointmentEditForm | null>(null)
  const [rescheduleForm, setRescheduleForm] = useState<AppointmentEditForm | null>(null)
  const [restoringDate, setRestoringDate] = useState<string | null>(null)

  function load() {
    setState({ status: 'loading' })
    searchAppointments(session.sessionId, { patientId, limit: 50 })
      .then((data) => setState({ status: 'ready', data: data.appointments }))
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  }

  // Switching charts must immediately replace the appointment list with a fresh request.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { load() }, [patientId]) // eslint-disable-line react-hooks/exhaustive-deps

  async function handleStatusChange(apptId: string, status: string) {
    if (status === 'Cancelled') {
      await cancelAppointment(apptId)
      return
    }
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

  async function handleCreateAppointment(event: React.FormEvent) {
    event.preventDefault()
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

  function openEditor(appointment: AppointmentListItem) {
    setEditingAppointment(appointment)
    setEditForm(formFromAppointment(appointment))
    if (schedulingOptions || schedulingOptionsError) return

    getAppointmentSchedulingOptions(session.sessionId)
      .then(setSchedulingOptions)
      .catch(() => setSchedulingOptionsError('Provider and facility options could not be loaded. Existing assignments can still be retained.'))
  }

  function openRescheduler(appointment: AppointmentListItem) {
    setReschedulingAppointment(appointment)
    setRescheduleForm(formFromAppointment(appointment))
    if (schedulingOptions || schedulingOptionsError) return

    getAppointmentSchedulingOptions(session.sessionId)
      .then(setSchedulingOptions)
      .catch(() => setSchedulingOptionsError('Provider and facility options could not be loaded. Existing assignments can still be retained.'))
  }

  function closeEditor() {
    if (saving) return
    setEditingAppointment(null)
    setEditForm(null)
  }

  function closeRescheduler() {
    if (saving) return
    setReschedulingAppointment(null)
    setRescheduleForm(null)
  }

  async function handleAppointmentUpdate(event: React.FormEvent) {
    event.preventDefault()
    if (!editingAppointment || !editForm) return

    const update: AppointmentUpdateInput = {
      providerId: nullableId(editForm.providerId),
      title: editForm.title,
      date: editForm.date,
      startTime: editForm.startTime,
      durationMinutes: editForm.durationMinutes,
      facilityId: nullableId(editForm.facilityId),
      billingLocationId: editingAppointment.billingLocationId ?? null,
      categoryId: editingAppointment.categoryId ?? null,
      room: editForm.room || null,
      status: editForm.status || null,
      comments: editForm.comments || null,
      // The API treats omitted recurrence values as a replacement. Preserve the
      // complete existing series configuration while changing appointment facts.
      recurrenceType: editingAppointment.recurrenceType,
      repeatFrequency: editingAppointment.repeatFrequency ?? null,
      repeatUnit: editingAppointment.repeatUnit ?? null,
      repeatOnNum: editingAppointment.repeatOnNum ?? null,
      repeatOnDay: editingAppointment.repeatOnDay ?? null,
      repeatOnFrequency: editingAppointment.repeatOnFrequency ?? null,
      recurrenceDays: editingAppointment.recurrenceDays,
      recurrenceEndDate: editingAppointment.recurrenceEndDate ?? null,
      recurrenceExdates: editingAppointment.recurrenceExdates,
    }

    setSaving(true)
    try {
      await updateAppointment(session.sessionId, editingAppointment.id, update)
      showToast(editingAppointment.isRecurringSeries ? 'Appointment series updated.' : 'Appointment updated.', 'success')
      setEditingAppointment(null)
      setEditForm(null)
      load()
    } catch {
      showToast('Could not update appointment.', 'error')
    } finally {
      setSaving(false)
    }
  }

  async function handleOccurrenceReschedule(event: React.FormEvent) {
    event.preventDefault()
    if (!reschedulingAppointment || !rescheduleForm) return

    const update: AppointmentOccurrenceRescheduleInput = {
      providerId: nullableId(rescheduleForm.providerId),
      title: rescheduleForm.title,
      date: rescheduleForm.date,
      startTime: rescheduleForm.startTime,
      durationMinutes: rescheduleForm.durationMinutes,
      facilityId: nullableId(rescheduleForm.facilityId),
      billingLocationId: reschedulingAppointment.billingLocationId ?? null,
      categoryId: reschedulingAppointment.categoryId ?? null,
      room: rescheduleForm.room || null,
      status: rescheduleForm.status || null,
      comments: rescheduleForm.comments || null,
    }

    setSaving(true)
    try {
      await rescheduleAppointmentOccurrence(session.sessionId, reschedulingAppointment.id, reschedulingAppointment.date, update)
      showToast(`Occurrence on ${reschedulingAppointment.date} rescheduled.`, 'success')
      setReschedulingAppointment(null)
      setRescheduleForm(null)
      load()
    } catch {
      showToast('Could not reschedule this occurrence.', 'error')
    } finally {
      setSaving(false)
    }
  }

  async function handleOccurrenceRestore(appointment: AppointmentListItem, occurrenceDate: string) {
    setRestoringDate(occurrenceDate)
    try {
      await restoreAppointmentOccurrence(session.sessionId, appointment.id, occurrenceDate)
      showToast(`Occurrence on ${occurrenceDate} restored.`, 'success')
      setEditingAppointment(null)
      setEditForm(null)
      load()
    } catch {
      showToast('Could not restore this occurrence.', 'error')
    } finally {
      setRestoringDate(null)
    }
  }

  async function cancelAppointment(id: string) {
    if (!window.confirm('Cancel this appointment?')) return
    setUpdatingId(id)
    try {
      await updateAppointmentStatus(session.sessionId, id, 'Cancelled')
      showToast('Appointment cancelled.', 'success')
      load()
    } catch {
      showToast('Could not cancel appointment.', 'error')
    } finally {
      setUpdatingId(null)
    }
  }

  async function restoreAppointment(id: string) {
    setUpdatingId(id)
    try {
      await updateAppointmentStatus(session.sessionId, id, 'Scheduled')
      showToast('Appointment restored to scheduled.', 'success')
      load()
    } catch {
      showToast('Could not restore appointment.', 'error')
    } finally {
      setUpdatingId(null)
    }
  }

  async function deleteAppointmentPermanently(id: string) {
    if (!window.confirm('Permanently delete this appointment? This cannot be undone.')) return
    setUpdatingId(id)
    try {
      await deleteAppointment(session.sessionId, id)
      showToast('Appointment deleted.', 'success')
      load()
    } catch {
      showToast('Could not delete appointment.', 'error')
    } finally {
      setUpdatingId(null)
    }
  }

  const editorHasProvider = !!(
    editForm?.providerId
    && schedulingOptions?.providers.some((provider) => provider.id === Number(editForm.providerId))
  )
  const editorHasFacility = !!(
    editForm?.facilityId
    && schedulingOptions?.facilities.some((facility) => facility.id === Number(editForm.facilityId))
  )
  const reschedulerHasProvider = !!(
    rescheduleForm?.providerId
    && schedulingOptions?.providers.some((provider) => provider.id === Number(rescheduleForm.providerId))
  )
  const reschedulerHasFacility = !!(
    rescheduleForm?.facilityId
    && schedulingOptions?.facilities.some((facility) => facility.id === Number(rescheduleForm.facilityId))
  )

  return (
    <div className="clinician-page">
      {newApptOpen && (
        <div className="modal-overlay" onClick={(event) => { if (event.target === event.currentTarget) setNewApptOpen(false) }}>
          <div className="modal-panel" role="dialog" aria-modal="true" aria-label="New appointment">
            <div className="modal-header">
              <h2 className="modal-title">New appointment</h2>
              <button className="modal-close" type="button" onClick={() => setNewApptOpen(false)} aria-label="Close">&times;</button>
            </div>
            <form onSubmit={handleCreateAppointment}>
              <div className="field">
                <label className="label" htmlFor="appt-title">Visit type / title</label>
                <input id="appt-title" className="input" value={apptForm.title} onChange={(event) => setApptForm((form) => ({ ...form, title: event.target.value }))} required />
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="appt-date">Date</label>
                  <input id="appt-date" type="date" className="input" value={apptForm.date} onChange={(event) => setApptForm((form) => ({ ...form, date: event.target.value }))} required />
                </div>
                <div className="field">
                  <label className="label" htmlFor="appt-time">Time</label>
                  <input id="appt-time" type="time" className="input" value={apptForm.startTime} onChange={(event) => setApptForm((form) => ({ ...form, startTime: event.target.value }))} required />
                </div>
              </div>
              <div className="field">
                <label className="label" htmlFor="appt-dur">Duration (minutes)</label>
                <select id="appt-dur" className="select" value={apptForm.durationMinutes} onChange={(event) => setApptForm((form) => ({ ...form, durationMinutes: Number(event.target.value) }))}>
                  {DURATION_OPTIONS.map((duration) => <option key={duration} value={duration}>{duration} min</option>)}
                </select>
              </div>
              <div className="field">
                <label className="label" htmlFor="appt-comments">Comments (optional)</label>
                <textarea id="appt-comments" className="textarea" rows={2} value={apptForm.comments} onChange={(event) => setApptForm((form) => ({ ...form, comments: event.target.value }))} />
              </div>
              <div className="button-row">
                <button className="button-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Create appointment'}</button>
                <button className="button-secondary" type="button" onClick={() => setNewApptOpen(false)} style={{ flex: 'none', width: 'auto' }}>Cancel</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {editingAppointment && editForm && (
        <div className="modal-overlay" onClick={(event) => { if (event.target === event.currentTarget) closeEditor() }}>
          <div className="modal-panel" role="dialog" aria-modal="true" aria-label="Edit appointment">
            <div className="modal-header">
              <div>
                <h2 className="modal-title">Edit appointment</h2>
                {editingAppointment.isRecurringSeries && <p className="cl-table-sub">Changes apply to the series. Reschedule one occurrence separately.</p>}
              </div>
              <button className="modal-close" type="button" onClick={closeEditor} aria-label="Close">&times;</button>
            </div>
            <form onSubmit={handleAppointmentUpdate}>
              <div className="field">
                <label className="label" htmlFor="edit-appt-title">Visit type / title</label>
                <input id="edit-appt-title" className="input" value={editForm.title} onChange={(event) => setEditForm((form) => form && ({ ...form, title: event.target.value }))} required />
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="edit-appt-date">Date</label>
                  <input id="edit-appt-date" type="date" className="input" value={editForm.date} onChange={(event) => setEditForm((form) => form && ({ ...form, date: event.target.value }))} required />
                </div>
                <div className="field">
                  <label className="label" htmlFor="edit-appt-time">Time</label>
                  <input id="edit-appt-time" type="time" className="input" value={editForm.startTime} onChange={(event) => setEditForm((form) => form && ({ ...form, startTime: event.target.value }))} required />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="edit-appt-provider">Provider</label>
                  <select id="edit-appt-provider" className="select" value={editForm.providerId} onChange={(event) => setEditForm((form) => form && ({ ...form, providerId: event.target.value }))}>
                    {!editorHasProvider && editForm.providerId && <option value={editForm.providerId}>{editingAppointment.providerName ?? 'Current provider'}</option>}
                    <option value="">Keep current provider</option>
                    {schedulingOptions?.providers.map((provider) => <option key={provider.id} value={provider.id}>{provider.displayName}{provider.facilityName ? ` · ${provider.facilityName}` : ''}</option>)}
                  </select>
                </div>
                <div className="field">
                  <label className="label" htmlFor="edit-appt-facility">Facility</label>
                  <select id="edit-appt-facility" className="select" value={editForm.facilityId} onChange={(event) => setEditForm((form) => form && ({ ...form, facilityId: event.target.value }))}>
                    {!editorHasFacility && editForm.facilityId && <option value={editForm.facilityId}>{editingAppointment.facilityName ?? 'Current facility'}</option>}
                    <option value="">Keep current facility</option>
                    {schedulingOptions?.facilities.map((facility) => <option key={facility.id} value={facility.id}>{facility.name}{facility.code ? ` (${facility.code})` : ''}</option>)}
                  </select>
                </div>
              </div>
              {schedulingOptionsError && <p className="cl-table-sub" role="status">{schedulingOptionsError}</p>}
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="edit-appt-duration">Duration</label>
                  <select id="edit-appt-duration" className="select" value={editForm.durationMinutes} onChange={(event) => setEditForm((form) => form && ({ ...form, durationMinutes: Number(event.target.value) }))}>
                    {DURATION_OPTIONS.map((duration) => <option key={duration} value={duration}>{duration} min</option>)}
                  </select>
                </div>
                <div className="field">
                  <label className="label" htmlFor="edit-appt-room">Room</label>
                  <input id="edit-appt-room" className="input" value={editForm.room} onChange={(event) => setEditForm((form) => form && ({ ...form, room: event.target.value }))} />
                </div>
              </div>
              <div className="field">
                <label className="label" htmlFor="edit-appt-status">Status</label>
                <select id="edit-appt-status" className="select" value={editForm.status} onChange={(event) => setEditForm((form) => form && ({ ...form, status: event.target.value }))}>
                  {!editForm.status && <option value="">Keep current status</option>}
                  {STATUS_OPTIONS.map((status) => <option key={status} value={status}>{status}</option>)}
                </select>
              </div>
              <div className="field">
                <label className="label" htmlFor="edit-appt-comments">Comments</label>
                <textarea id="edit-appt-comments" className="textarea" rows={3} value={editForm.comments} onChange={(event) => setEditForm((form) => form && ({ ...form, comments: event.target.value }))} />
              </div>
              {editingAppointment.isRecurringSeries && editingAppointment.recurrenceExdates.length > 0 && (
                <div className="field">
                  <span className="label">Skipped occurrences</span>
                  <p className="cl-table-sub">Restore a previously cancelled occurrence to its original time.</p>
                  <div className="button-row" style={{ justifyContent: 'flex-start', flexWrap: 'wrap' }}>
                    {editingAppointment.recurrenceExdates.map((occurrenceDate) => (
                      <button key={occurrenceDate} className="cl-btn-secondary" type="button" disabled={saving || restoringDate === occurrenceDate} onClick={() => handleOccurrenceRestore(editingAppointment, occurrenceDate)}>
                        {restoringDate === occurrenceDate ? 'Restoring…' : `Restore ${occurrenceDate}`}
                      </button>
                    ))}
                  </div>
                </div>
              )}
              <div className="button-row">
                <button className="button-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save changes'}</button>
                <button className="button-secondary" type="button" onClick={closeEditor} disabled={saving} style={{ flex: 'none', width: 'auto' }}>Cancel</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {reschedulingAppointment && rescheduleForm && (
        <div className="modal-overlay" onClick={(event) => { if (event.target === event.currentTarget) closeRescheduler() }}>
          <div className="modal-panel" role="dialog" aria-modal="true" aria-label="Reschedule appointment occurrence">
            <div className="modal-header">
              <div>
                <h2 className="modal-title">Reschedule one occurrence</h2>
                <p className="cl-table-sub">Only the {reschedulingAppointment.date} occurrence moves. The recurring series stays unchanged.</p>
              </div>
              <button className="modal-close" type="button" onClick={closeRescheduler} aria-label="Close">&times;</button>
            </div>
            <form onSubmit={handleOccurrenceReschedule}>
              <div className="field">
                <label className="label" htmlFor="reschedule-appt-title">Visit type / title</label>
                <input id="reschedule-appt-title" className="input" value={rescheduleForm.title} onChange={(event) => setRescheduleForm((form) => form && ({ ...form, title: event.target.value }))} required />
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="reschedule-appt-date">New date</label>
                  <input id="reschedule-appt-date" type="date" className="input" value={rescheduleForm.date} onChange={(event) => setRescheduleForm((form) => form && ({ ...form, date: event.target.value }))} required />
                </div>
                <div className="field">
                  <label className="label" htmlFor="reschedule-appt-time">New time</label>
                  <input id="reschedule-appt-time" type="time" className="input" value={rescheduleForm.startTime} onChange={(event) => setRescheduleForm((form) => form && ({ ...form, startTime: event.target.value }))} required />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="reschedule-appt-provider">Provider</label>
                  <select id="reschedule-appt-provider" className="select" value={rescheduleForm.providerId} onChange={(event) => setRescheduleForm((form) => form && ({ ...form, providerId: event.target.value }))}>
                    {!reschedulerHasProvider && rescheduleForm.providerId && <option value={rescheduleForm.providerId}>{reschedulingAppointment.providerName ?? 'Current provider'}</option>}
                    <option value="">Keep current provider</option>
                    {schedulingOptions?.providers.map((provider) => <option key={provider.id} value={provider.id}>{provider.displayName}{provider.facilityName ? ` · ${provider.facilityName}` : ''}</option>)}
                  </select>
                </div>
                <div className="field">
                  <label className="label" htmlFor="reschedule-appt-facility">Facility</label>
                  <select id="reschedule-appt-facility" className="select" value={rescheduleForm.facilityId} onChange={(event) => setRescheduleForm((form) => form && ({ ...form, facilityId: event.target.value }))}>
                    {!reschedulerHasFacility && rescheduleForm.facilityId && <option value={rescheduleForm.facilityId}>{reschedulingAppointment.facilityName ?? 'Current facility'}</option>}
                    <option value="">Keep current facility</option>
                    {schedulingOptions?.facilities.map((facility) => <option key={facility.id} value={facility.id}>{facility.name}{facility.code ? ` (${facility.code})` : ''}</option>)}
                  </select>
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="reschedule-appt-duration">Duration</label>
                  <select id="reschedule-appt-duration" className="select" value={rescheduleForm.durationMinutes} onChange={(event) => setRescheduleForm((form) => form && ({ ...form, durationMinutes: Number(event.target.value) }))}>
                    {DURATION_OPTIONS.map((duration) => <option key={duration} value={duration}>{duration} min</option>)}
                  </select>
                </div>
                <div className="field">
                  <label className="label" htmlFor="reschedule-appt-room">Room</label>
                  <input id="reschedule-appt-room" className="input" value={rescheduleForm.room} onChange={(event) => setRescheduleForm((form) => form && ({ ...form, room: event.target.value }))} />
                </div>
              </div>
              <div className="field">
                <label className="label" htmlFor="reschedule-appt-comments">Comments</label>
                <textarea id="reschedule-appt-comments" className="textarea" rows={3} value={rescheduleForm.comments} onChange={(event) => setRescheduleForm((form) => form && ({ ...form, comments: event.target.value }))} />
              </div>
              <div className="button-row">
                <button className="button-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Reschedule occurrence'}</button>
                <button className="button-secondary" type="button" onClick={closeRescheduler} disabled={saving} style={{ flex: 'none', width: 'auto' }}>Cancel</button>
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

      {state.status === 'loading' && <div className="cl-card"><div className="skeleton-list">{[0, 1, 2, 3].map((index) => <div key={index} className="skeleton-row" style={{ height: 60 }} />)}</div></div>}
      {state.status === 'error' && <div className="error-banner">{state.message}</div>}
      {state.status === 'ready' && state.data.length === 0 && <div className="cl-card"><p className="cl-empty-text">No appointments on file.</p></div>}
      {state.status === 'ready' && state.data.length > 0 && (
        <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
          <table className="cl-table">
            <thead><tr><th>Date</th><th>Time</th><th>Visit type</th><th>Provider</th><th>Facility</th><th>Status</th><th aria-label="Actions" /></tr></thead>
            <tbody>
              {state.data.map((appointment) => (
                <tr key={appointment.id}>
                  <td>{appointment.date}</td>
                  <td className="cl-td-time">{formatTime(appointment.startTime)}</td>
                  <td>{appointment.title}</td>
                  <td className="cl-td-muted">{appointment.providerName ?? '—'}</td>
                  <td className="cl-td-muted">{appointment.facilityName ?? '—'}</td>
                  <td>
                    <select className="cl-status-select" value={appointment.status ?? ''} disabled={updatingId === appointment.id} onChange={(event) => handleStatusChange(appointment.id, event.target.value)} aria-label={`Appointment status on ${appointment.date}`}>
                      {!appointment.status && <option value="">—</option>}
                      {STATUS_OPTIONS.map((status) => <option key={status} value={status}>{status}</option>)}
                    </select>
                  </td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    <button className="cl-btn-secondary" type="button" disabled={updatingId === appointment.id} onClick={() => openEditor(appointment)} aria-label={`Edit appointment on ${appointment.date}`}><Pencil size={13} /> Edit</button>
                    {appointment.isRecurringSeries && <button className="cl-btn-secondary" type="button" disabled={updatingId === appointment.id} onClick={() => openRescheduler(appointment)} style={{ marginLeft: 6 }}>Reschedule</button>}
                    {isCancelledStatus(appointment.status)
                      ? <button className="cl-btn-secondary" type="button" disabled={updatingId === appointment.id} onClick={() => restoreAppointment(appointment.id)} style={{ marginLeft: 6 }}>Restore</button>
                      : <button className="cl-btn-secondary" type="button" disabled={updatingId === appointment.id} onClick={() => cancelAppointment(appointment.id)} style={{ marginLeft: 6 }}>Cancel</button>}
                    <button className="cl-btn-secondary" type="button" disabled={updatingId === appointment.id} onClick={() => deleteAppointmentPermanently(appointment.id)} style={{ marginLeft: 6 }}>Delete</button>
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
