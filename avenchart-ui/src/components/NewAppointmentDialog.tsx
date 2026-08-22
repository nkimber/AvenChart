// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Search, UserRoundCheck } from 'lucide-react'
import {
  ApiRequestError,
  createAppointment,
  getAppointmentSchedulingOptions,
  isRequestCancellation,
  searchPatients,
  validateAppointmentAvailability,
  type AppointmentAvailabilityValidationResponse,
  type AppointmentListItem,
  type AppointmentSchedulingOptionsResponse,
  type PatientListItem,
} from '../api.ts'
import { showToast } from './Toast.tsx'

const DURATION_OPTIONS = [10, 15, 20, 30, 45, 60, 90]

export type AppointmentPatient = Pick<PatientListItem, 'canonicalId' | 'displayName' | 'pubpid' | 'dateOfBirth'>

type PatientSearchState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; patients: PatientListItem[]; total: number }
  | { status: 'error'; message: string }

type NewAppointmentDialogProps = {
  sessionId: string
  initialDate: string
  patient?: AppointmentPatient
  onClose: () => void
  onCreated: (appointment: AppointmentListItem) => void
}

function nullableId(value: string): number | null {
  return value ? Number(value) : null
}

function formatTime(value?: string | null) {
  return value ? value.slice(0, 5) : ''
}

export function NewAppointmentDialog({
  sessionId,
  initialDate,
  patient,
  onClose,
  onCreated,
}: NewAppointmentDialogProps) {
  const searchInputRef = useRef<HTMLInputElement>(null)
  const [selectedPatient, setSelectedPatient] = useState<AppointmentPatient | null>(patient ?? null)
  const [patientQuery, setPatientQuery] = useState('')
  const [patientSearch, setPatientSearch] = useState<PatientSearchState>({ status: 'idle' })
  const [schedulingOptions, setSchedulingOptions] = useState<AppointmentSchedulingOptionsResponse | null>(null)
  const [schedulingOptionsError, setSchedulingOptionsError] = useState<string | null>(null)
  const [availability, setAvailability] = useState<AppointmentAvailabilityValidationResponse | null>(null)
  const [checkingAvailability, setCheckingAvailability] = useState(false)
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState({
    title: 'Office visit',
    date: initialDate,
    startTime: '09:00',
    durationMinutes: 20,
    providerId: '',
    facilityId: '',
    room: '',
    comments: '',
  })

  useEffect(() => {
    const controller = new AbortController()
    getAppointmentSchedulingOptions(sessionId, controller.signal)
      .then((options) => {
        setSchedulingOptions(options)
        setSchedulingOptionsError(null)
      })
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        setSchedulingOptionsError(
          'Provider and facility options could not be loaded. Patient defaults can still be used.',
        )
      })
    return () => controller.abort()
  }, [sessionId])

  useEffect(() => {
    if (!patient) searchInputRef.current?.focus()
  }, [patient])

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape' && !saving) onClose()
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onClose, saving])

  function updateForm<T extends keyof typeof form>(field: T, value: (typeof form)[T]) {
    setAvailability(null)
    setForm((current) => ({ ...current, [field]: value }))
  }

  function close() {
    if (!saving) onClose()
  }

  async function handlePatientSearch(event: FormEvent) {
    event.preventDefault()
    const query = patientQuery.trim()
    if (!query) return

    setPatientSearch({ status: 'loading' })
    try {
      const result = await searchPatients(sessionId, { search: query, limit: 10 })
      setPatientSearch({ status: 'ready', patients: result.patients, total: result.totalMatches })
    } catch (error) {
      setPatientSearch({
        status: 'error',
        message: error instanceof Error ? error.message : 'Patient search failed.',
      })
    }
  }

  function selectPatient(result: PatientListItem) {
    setSelectedPatient(result)
    setPatientSearch({ status: 'idle' })
    setPatientQuery('')
    setAvailability(null)
  }

  async function validateAvailability() {
    if (!selectedPatient) return null
    return validateAppointmentAvailability(sessionId, {
      patientId: selectedPatient.canonicalId,
      providerId: nullableId(form.providerId),
      facilityId: nullableId(form.facilityId),
      date: form.date,
      startTime: form.startTime,
      durationMinutes: form.durationMinutes,
      room: form.room || null,
    })
  }

  async function checkAvailability() {
    if (!selectedPatient) {
      showToast('Select a patient before checking availability.', 'error')
      return
    }

    setCheckingAvailability(true)
    try {
      setAvailability(await validateAvailability())
    } catch {
      showToast('Could not validate appointment availability.', 'error')
    } finally {
      setCheckingAvailability(false)
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!selectedPatient) {
      showToast('Select a patient before creating the appointment.', 'error')
      searchInputRef.current?.focus()
      return
    }

    setSaving(true)
    try {
      const validation = await validateAvailability()
      if (!validation) return
      setAvailability(validation)
      if (!validation.available) {
        showToast('Choose an available appointment time before saving.', 'error')
        return
      }

      const appointment = await createAppointment(sessionId, {
        patientId: selectedPatient.canonicalId,
        providerId: nullableId(form.providerId),
        facilityId: nullableId(form.facilityId),
        title: form.title,
        date: form.date,
        startTime: form.startTime,
        durationMinutes: form.durationMinutes,
        room: form.room || null,
        comments: form.comments || null,
        enforceConflictPolicy: true,
      })
      showToast('Appointment created.', 'success')
      onCreated(appointment)
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 409) {
        try {
          setAvailability(await validateAvailability())
        } catch {
          // The conflict response is still actionable even if refresh fails.
        }
        showToast('This time was just taken. Review the refreshed availability before saving.', 'error')
      } else {
        showToast('Could not create appointment.', 'error')
      }
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="modal-overlay" onClick={(event) => { if (event.target === event.currentTarget) close() }}>
      <div className="modal-panel appointment-create-dialog" role="dialog" aria-modal="true" aria-labelledby="new-appointment-title">
        <div className="modal-header">
          <h2 className="modal-title" id="new-appointment-title">New appointment</h2>
          <button className="modal-close" type="button" onClick={close} aria-label="Close">&times;</button>
        </div>

        <div className="field">
            <span className="label">Patient</span>
            {selectedPatient ? (
              <div className="appointment-patient-selection">
                <UserRoundCheck size={20} aria-hidden="true" />
                <div>
                  <strong>{selectedPatient.displayName}</strong>
                  <p>{selectedPatient.dateOfBirth} · #{selectedPatient.pubpid}</p>
                </div>
                {!patient && (
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => {
                      setSelectedPatient(null)
                      setAvailability(null)
                      window.setTimeout(() => searchInputRef.current?.focus(), 0)
                    }}
                  >
                    Change
                  </button>
                )}
              </div>
            ) : (
              <div className="appointment-patient-picker">
                <form className="appointment-patient-search" onSubmit={handlePatientSearch}>
                  <div className="cl-search-input-wrap">
                    <Search size={16} className="cl-search-icon" aria-hidden="true" />
                    <input
                      ref={searchInputRef}
                      className="cl-search-input"
                      type="search"
                      value={patientQuery}
                      onChange={(event) => setPatientQuery(event.target.value)}
                      placeholder="Name, DOB, chart #, or email"
                      aria-label="Search patients for appointment"
                      required
                    />
                  </div>
                  <button className="cl-btn-secondary" type="submit" disabled={patientSearch.status === 'loading'}>
                    {patientSearch.status === 'loading' ? 'Searching…' : 'Search'}
                  </button>
                </form>
                {patientSearch.status === 'error' && <p className="error-banner" role="alert">{patientSearch.message}</p>}
                {patientSearch.status === 'ready' && patientSearch.patients.length === 0 && (
                  <p className="cl-empty-text" role="status">No patients found.</p>
                )}
                {patientSearch.status === 'ready' && patientSearch.patients.length > 0 && (
                  <div className="appointment-patient-results">
                    <p className="cl-table-sub">
                      {patientSearch.total} patient{patientSearch.total === 1 ? '' : 's'} found
                    </p>
                    <ul aria-label="Patient search results">
                      {patientSearch.patients.map((result) => (
                        <li key={result.canonicalId}>
                          <button type="button" onClick={() => selectPatient(result)}>
                            <strong>{result.displayName}</strong>
                            <span>{result.dateOfBirth} · #{result.pubpid}</span>
                          </button>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            )}
        </div>

        <form onSubmit={handleSubmit}>
          <div className="field">
            <label className="label" htmlFor="new-appt-title-input">Visit type / title</label>
            <input id="new-appt-title-input" className="input" value={form.title} onChange={(event) => updateForm('title', event.target.value)} required />
          </div>
          <div className="form-row">
            <div className="field">
              <label className="label" htmlFor="new-appt-date">Date</label>
              <input id="new-appt-date" type="date" className="input" value={form.date} onChange={(event) => updateForm('date', event.target.value)} required />
            </div>
            <div className="field">
              <label className="label" htmlFor="new-appt-time">Time</label>
              <input id="new-appt-time" type="time" className="input" value={form.startTime} onChange={(event) => updateForm('startTime', event.target.value)} required />
            </div>
          </div>
          <div className="form-row">
            <div className="field">
              <label className="label" htmlFor="new-appt-provider">Provider</label>
              <select id="new-appt-provider" className="select" value={form.providerId} onChange={(event) => updateForm('providerId', event.target.value)}>
                <option value="">Use patient default</option>
                {schedulingOptions?.providers.map((providerOption) => (
                  <option key={providerOption.id} value={providerOption.id}>
                    {providerOption.displayName}{providerOption.facilityName ? ` · ${providerOption.facilityName}` : ''}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label className="label" htmlFor="new-appt-facility">Facility</label>
              <select id="new-appt-facility" className="select" value={form.facilityId} onChange={(event) => updateForm('facilityId', event.target.value)}>
                <option value="">Use patient default</option>
                {schedulingOptions?.facilities.map((facilityOption) => (
                  <option key={facilityOption.id} value={facilityOption.id}>
                    {facilityOption.name}{facilityOption.code ? ` (${facilityOption.code})` : ''}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="field">
            <label className="label" htmlFor="new-appt-duration">Duration (minutes)</label>
            <select id="new-appt-duration" className="select" value={form.durationMinutes} onChange={(event) => updateForm('durationMinutes', Number(event.target.value))}>
              {DURATION_OPTIONS.map((duration) => <option key={duration} value={duration}>{duration} min</option>)}
            </select>
          </div>
          <div className="field">
            <label className="label" htmlFor="new-appt-room">Room (optional)</label>
            <input id="new-appt-room" className="input" value={form.room} onChange={(event) => updateForm('room', event.target.value)} />
          </div>
          <div className="field">
            <label className="label" htmlFor="new-appt-comments">Comments (optional)</label>
            <textarea id="new-appt-comments" className="textarea" rows={2} value={form.comments} onChange={(event) => updateForm('comments', event.target.value)} />
          </div>
          {schedulingOptionsError && <p className="cl-table-sub" role="status">{schedulingOptionsError}</p>}
          <button className="cl-btn-secondary" type="button" onClick={() => void checkAvailability()} disabled={!selectedPatient || checkingAvailability || saving}>
            {checkingAvailability ? 'Checking…' : 'Check availability'}
          </button>
          {availability && (
            <div className={availability.available ? 'hint-banner' : 'error-banner'} role="status" style={{ marginTop: 12 }}>
              <strong>{availability.available ? 'Time available' : 'Time unavailable'}</strong>
              {availability.messages.map((message) => <p key={message}>{message}</p>)}
              {availability.conflicts.map((conflict) => (
                <p key={`${conflict.appointmentId}-${conflict.conflictType}`}>
                  {conflict.conflictType}: {conflict.patientDisplayName} — {formatTime(conflict.startTime)}–{formatTime(conflict.endTime)} ({conflict.title})
                </p>
              ))}
            </div>
          )}
          <div className="button-row">
            <button className="button-primary" type="submit" disabled={!selectedPatient || saving}>
              {saving ? 'Saving…' : 'Create appointment'}
            </button>
            <button className="button-secondary" type="button" onClick={close} disabled={saving} style={{ flex: 'none', width: 'auto' }}>Cancel</button>
          </div>
        </form>
      </div>
    </div>
  )
}
