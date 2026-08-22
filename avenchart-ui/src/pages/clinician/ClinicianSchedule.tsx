// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useRef, useState } from 'react'
import { useNavigate, useOutletContext, useSearchParams } from 'react-router-dom'
import { CalendarPlus, ChevronLeft, ChevronRight, Pencil } from 'lucide-react'
import { searchAppointments, updateAppointmentStatus, type AppointmentListItem } from '../../api.ts'
import { getAppointmentStatus, getAppointmentStatusOptions } from '../../domain/appointmentStatus.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'
import { showToast } from '../../components/Toast.tsx'
import { NewAppointmentDialog } from '../../components/NewAppointmentDialog.tsx'
import { EditAppointmentDialog } from '../../components/EditAppointmentDialog.tsx'

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


export default function ClinicianSchedule() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const [selectedDate, setSelectedDate] = useState(() => {
    const requestedDate = searchParams.get('date')
    return requestedDate && /^\d{4}-\d{2}-\d{2}$/.test(requestedDate)
      ? requestedDate
      : isoDate(new Date())
  })
  const [apptState, setApptState] = useState<AsyncState<AppointmentListItem[]>>({ status: 'loading' })
  const appointmentRequestEpoch = useRef(0)
  const selectedDateRef = useRef(selectedDate)
  selectedDateRef.current = selectedDate
  const [updatingId, setUpdatingId] = useState<string | null>(null)
  const [newAppointmentOpen, setNewAppointmentOpen] = useState(false)
  const [editingAppointment, setEditingAppointment] = useState<AppointmentListItem | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    load(selectedDate, controller.signal)
    return () => controller.abort()
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedDate, session.sessionId])

  function load(date = selectedDateRef.current, signal?: AbortSignal) {
    const requestEpoch = ++appointmentRequestEpoch.current
    setApptState({ status: 'loading' })
    searchAppointments(session.sessionId, { fromDate: date, toDate: date, limit: 50 }, signal)
      .then((data) => {
        if (!signal?.aborted && requestEpoch === appointmentRequestEpoch.current) {
          setApptState({ status: 'ready', data: data.appointments })
        }
      })
      .catch((err) => {
        if (!signal?.aborted && requestEpoch === appointmentRequestEpoch.current) {
          setApptState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' })
        }
      })
  }

  function changeDay(delta: number) {
    const d = new Date(selectedDate + 'T00:00:00')
    selectDate(isoDate(addDays(d, delta)))
  }

  function selectDate(date: string) {
    setSelectedDate(date)
    setSearchParams({ date }, { replace: true })
  }

  async function handleStatusChange(apptId: string, status: string) {
    const requestDate = selectedDateRef.current
    setUpdatingId(apptId)
    try {
      await updateAppointmentStatus(session.sessionId, apptId, status)
      showToast(`Status updated to "${getAppointmentStatus(status).label}"`)
      if (selectedDateRef.current === requestDate) {
        load(requestDate)
      }
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
      {newAppointmentOpen && (
        <NewAppointmentDialog
          sessionId={session.sessionId}
          initialDate={selectedDate}
          onClose={() => setNewAppointmentOpen(false)}
          onCreated={() => {
            setNewAppointmentOpen(false)
            load()
          }}
        />
      )}

      {editingAppointment && (
        <EditAppointmentDialog
          sessionId={session.sessionId}
          appointment={editingAppointment}
          onClose={() => setEditingAppointment(null)}
          onSaved={() => {
            setEditingAppointment(null)
            load()
          }}
        />
      )}

      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Schedule</h1>
          <p className="clinician-page-subtitle">
            {dateLabel}
            {isToday ? ' (Today)' : ''}
            {apptState.status === 'ready'
              ? ` · ${apptState.data.length} appointments on this date`
              : ''}
          </p>
        </div>
        <div className="clinician-header-actions">
          <button className="cl-btn-primary" type="button" onClick={() => setNewAppointmentOpen(true)}>
            <CalendarPlus size={14} /> New appointment
          </button>
          <input
            type="date"
            className="cl-date-input"
            value={selectedDate}
            onChange={(e) => selectDate(e.target.value)}
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
          onClick={() => selectDate(isoDate(new Date()))}
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
        <div className="error-banner" role="alert">
          <p>{apptState.message}</p>
          <button className="cl-btn-secondary" type="button" onClick={() => load(selectedDate)}>
            Retry
          </button>
        </div>
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
                <th aria-label="Actions" />
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
                      value={getAppointmentStatus(appt.status).apiValue}
                      disabled={updatingId === appt.id}
                      onChange={(e) => handleStatusChange(appt.id, e.target.value)}
                      aria-label={`Status for ${appt.patientDisplayName}`}
                    >
                      {getAppointmentStatusOptions(appt.status).map((status) => (
                        <option key={status.apiValue} value={status.apiValue}>{status.label}</option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      disabled={updatingId === appt.id}
                      onClick={() => setEditingAppointment(appt)}
                      aria-label={`Edit appointment for ${appt.patientDisplayName} at ${formatTime(appt.startTime)}`}
                    >
                      <Pencil size={13} aria-hidden="true" /> Edit
                    </button>
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
