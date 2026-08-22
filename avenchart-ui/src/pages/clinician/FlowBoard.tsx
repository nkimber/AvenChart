// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useRef, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { getAppointmentFlowBoard, updateAppointmentStatus, type FlowBoardResponse } from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import { getAppointmentStatus } from '../../domain/appointmentStatus.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

function today() {
  return new Date().toISOString().slice(0, 10)
}

export default function FlowBoard() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [date, setDate] = useState(today)
  const [board, setBoard] = useState<FlowBoardResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [updating, setUpdating] = useState<string | null>(null)
  const activeRequest = useRef<AbortController | null>(null)

  const load = useCallback(async (selectedDate = date) => {
    activeRequest.current?.abort()
    const controller = new AbortController()
    activeRequest.current = controller
    setLoading(true)
    setBoard(null)
    setError(null)
    try {
      const data = await getAppointmentFlowBoard(session.sessionId, selectedDate, controller.signal)
      if (controller.signal.aborted || activeRequest.current !== controller) return
      if (data.date !== selectedDate) throw new Error('The flow board response did not match the selected date.')
      setBoard(data)
      setLoading(false)
    } catch {
      if (controller.signal.aborted || activeRequest.current !== controller) return
      setBoard(null)
      setError('Could not load the flow board. Retry the page.')
      setLoading(false)
    }
  }, [date, session.sessionId])

  useEffect(() => {
    void load()
    return () => activeRequest.current?.abort()
  }, [load])

  async function advance(appointmentId: string, status: string) {
    if (updating) return
    setUpdating(appointmentId)
    try {
      await updateAppointmentStatus(session.sessionId, appointmentId, status)
      showToast(`Appointment marked ${getAppointmentStatus(status).label.toLowerCase()}.`, 'success')
      await load()
    } catch {
      showToast('Could not update appointment status.', 'error')
    } finally {
      setUpdating(null)
    }
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Flow board</h1>
          <p className="clinician-page-subtitle">Room and appointment progress for the selected day.</p>
        </div>
        <input
          className="cl-date-input"
          type="date"
          value={date}
          onChange={(event) => {
            setDate(event.target.value)
          }}
          aria-label="Select flow-board date"
        />
      </div>

      {error && (
        <div className="error-banner" role="alert">
          <p>{error}</p>
          <button className="cl-btn-secondary" type="button" onClick={() => { void load() }}>
            Retry
          </button>
        </div>
      )}
      {loading && !error && (
        <div className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2].map((item) => <div key={item} className="skeleton-row" style={{ height: 70 }} />)}
          </div>
        </div>
      )}
      {board && (
        <div className="flow-board-grid">
          {board.lanes.map((lane) => (
            <section key={lane.key} className="flow-lane" aria-labelledby={`flow-lane-${lane.key}`}>
              <div className="flow-lane-heading">
                <h2 id={`flow-lane-${lane.key}`}>{lane.label}</h2>
                <span aria-label={`${lane.items.length} appointments`}>{lane.items.length}</span>
              </div>
              {lane.items.map((item) => (
                <article key={item.appointmentId} className="flow-card">
                  <p className="flow-time">
                    {item.startTime.slice(0, 5)}{item.room ? ` / Room ${item.room}` : ''}
                  </p>
                  <h3>{item.patientDisplayName}</h3>
                  <p>{item.title}</p>
                  <small>{item.providerName ?? item.facilityName ?? 'Unassigned'}</small>
                  {['scheduled', 'arrived', 'in-room'].includes(lane.key) && (
                    <div className="flow-actions">
                      {lane.key === 'scheduled' && (
                        <button className="cl-btn-secondary" type="button" disabled={updating === item.appointmentId} onClick={() => advance(item.appointmentId, '@')}>
                          Arrive
                        </button>
                      )}
                      {lane.key === 'arrived' && (
                        <button className="cl-btn-primary" type="button" disabled={updating === item.appointmentId} onClick={() => advance(item.appointmentId, '>')}>
                          Room
                        </button>
                      )}
                      {lane.key === 'in-room' && (
                        <button className="cl-btn-primary" type="button" disabled={updating === item.appointmentId} onClick={() => advance(item.appointmentId, '<')}>
                          Complete
                        </button>
                      )}
                    </div>
                  )}
                </article>
              ))}
              {lane.items.length === 0 && <p className="cl-empty-text">No appointments.</p>}
            </section>
          ))}
        </div>
      )}
    </div>
  )
}
