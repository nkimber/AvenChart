// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState, type FormEvent } from 'react'
import {
  getAppointmentSchedulingOptions,
  isRequestCancellation,
  rescheduleAppointmentOccurrence,
  updateAppointment,
  type AppointmentListItem,
  type AppointmentOccurrenceRescheduleInput,
  type AppointmentSchedulingOptionsResponse,
  type AppointmentUpdateInput,
} from '../api.ts'
import { getAppointmentStatus, getAppointmentStatusOptions } from '../domain/appointmentStatus.ts'
import { showToast } from './Toast.tsx'

const STANDARD_DURATION_OPTIONS = [10, 15, 20, 30, 45, 60, 90]

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

type EditAppointmentDialogProps = {
  sessionId: string
  appointment: AppointmentListItem
  onClose: () => void
  onSaved: (appointment: AppointmentListItem) => void
}

function nullableId(value: string): number | null {
  return value ? Number(value) : null
}

function formFromAppointment(appointment: AppointmentListItem): AppointmentEditForm {
  return {
    title: appointment.title,
    date: appointment.date,
    startTime: appointment.startTime.slice(0, 5),
    durationMinutes: appointment.durationMinutes,
    providerId: appointment.providerId?.toString() ?? '',
    facilityId: appointment.facilityId?.toString() ?? '',
    room: appointment.room ?? '',
    comments: appointment.comments ?? '',
    status: getAppointmentStatus(appointment.status).apiValue,
  }
}

export function EditAppointmentDialog({
  sessionId,
  appointment,
  onClose,
  onSaved,
}: EditAppointmentDialogProps) {
  const [form, setForm] = useState(() => formFromAppointment(appointment))
  const [schedulingOptions, setSchedulingOptions] = useState<AppointmentSchedulingOptionsResponse | null>(null)
  const [schedulingOptionsError, setSchedulingOptionsError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

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
          'Provider and facility options could not be loaded. Existing assignments can still be retained.',
        )
      })
    return () => controller.abort()
  }, [sessionId])

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape' && !saving) onClose()
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onClose, saving])

  function updateForm<T extends keyof AppointmentEditForm>(field: T, value: AppointmentEditForm[T]) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  function close() {
    if (!saving) onClose()
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)

    const appointmentFacts: AppointmentOccurrenceRescheduleInput = {
      providerId: nullableId(form.providerId),
      title: form.title,
      date: form.date,
      startTime: form.startTime,
      durationMinutes: form.durationMinutes,
      facilityId: nullableId(form.facilityId),
      billingLocationId: appointment.billingLocationId ?? null,
      categoryId: appointment.categoryId ?? null,
      room: form.room || null,
      status: form.status || null,
      comments: form.comments || null,
      expectedVersion: appointment.rowVersion,
    }

    try {
      let savedAppointment: AppointmentListItem
      if (appointment.isVirtualOccurrence) {
        savedAppointment = await rescheduleAppointmentOccurrence(
          sessionId,
          appointment.id,
          appointment.date,
          appointmentFacts,
        )
        showToast('Appointment occurrence updated.', 'success')
      } else {
        const update: AppointmentUpdateInput = {
          ...appointmentFacts,
          // Updating a record replaces its recurrence fields, so retain the
          // complete series definition while changing appointment facts.
          recurrenceType: appointment.recurrenceType,
          repeatFrequency: appointment.repeatFrequency ?? null,
          repeatUnit: appointment.repeatUnit ?? null,
          repeatOnNum: appointment.repeatOnNum ?? null,
          repeatOnDay: appointment.repeatOnDay ?? null,
          repeatOnFrequency: appointment.repeatOnFrequency ?? null,
          recurrenceDays: appointment.recurrenceDays,
          recurrenceEndDate: appointment.recurrenceEndDate ?? null,
          recurrenceExdates: appointment.recurrenceExdates,
        }
        savedAppointment = await updateAppointment(sessionId, appointment.id, update)
        showToast(appointment.isRecurringSeries ? 'Appointment series updated.' : 'Appointment updated.', 'success')
      }
      onSaved(savedAppointment)
    } catch {
      showToast('Could not update appointment.', 'error')
    } finally {
      setSaving(false)
    }
  }

  const hasProvider = !!(
    form.providerId
    && schedulingOptions?.providers.some((provider) => provider.id === Number(form.providerId))
  )
  const hasFacility = !!(
    form.facilityId
    && schedulingOptions?.facilities.some((facility) => facility.id === Number(form.facilityId))
  )
  const durationOptions = Array.from(
    new Set([...STANDARD_DURATION_OPTIONS, appointment.durationMinutes]),
  ).sort((left, right) => left - right)

  return (
    <div className="modal-overlay" onClick={(event) => { if (event.target === event.currentTarget) close() }}>
      <div className="modal-panel" role="dialog" aria-modal="true" aria-labelledby="edit-appointment-title">
        <div className="modal-header">
          <div>
            <h2 className="modal-title" id="edit-appointment-title">
              {appointment.isVirtualOccurrence ? 'Edit appointment occurrence' : 'Edit appointment'}
            </h2>
            <p className="cl-table-sub">
              {appointment.patientDisplayName} · #{appointment.pubpid}
            </p>
            {appointment.isVirtualOccurrence && (
              <p className="cl-table-sub">Changes apply only to the {appointment.date} occurrence.</p>
            )}
            {appointment.isRecurringSeries && !appointment.isVirtualOccurrence && (
              <p className="cl-table-sub">Changes apply to the recurring series.</p>
            )}
          </div>
          <button className="modal-close" type="button" onClick={close} aria-label="Close">&times;</button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="field">
            <label className="label" htmlFor="schedule-edit-appt-title">Visit type / title</label>
            <input
              id="schedule-edit-appt-title"
              className="input"
              value={form.title}
              onChange={(event) => updateForm('title', event.target.value)}
              required
            />
          </div>
          <div className="form-row">
            <div className="field">
              <label className="label" htmlFor="schedule-edit-appt-date">Date</label>
              <input
                id="schedule-edit-appt-date"
                type="date"
                className="input"
                value={form.date}
                onChange={(event) => updateForm('date', event.target.value)}
                required
              />
            </div>
            <div className="field">
              <label className="label" htmlFor="schedule-edit-appt-time">Time</label>
              <input
                id="schedule-edit-appt-time"
                type="time"
                className="input"
                value={form.startTime}
                onChange={(event) => updateForm('startTime', event.target.value)}
                required
              />
            </div>
          </div>
          <div className="form-row">
            <div className="field">
              <label className="label" htmlFor="schedule-edit-appt-provider">Provider</label>
              <select
                id="schedule-edit-appt-provider"
                className="select"
                value={form.providerId}
                onChange={(event) => updateForm('providerId', event.target.value)}
              >
                {!hasProvider && form.providerId && (
                  <option value={form.providerId}>{appointment.providerName ?? 'Current provider'}</option>
                )}
                <option value="">Keep current provider</option>
                {schedulingOptions?.providers.map((provider) => (
                  <option key={provider.id} value={provider.id}>
                    {provider.displayName}{provider.facilityName ? ` · ${provider.facilityName}` : ''}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label className="label" htmlFor="schedule-edit-appt-facility">Facility</label>
              <select
                id="schedule-edit-appt-facility"
                className="select"
                value={form.facilityId}
                onChange={(event) => updateForm('facilityId', event.target.value)}
              >
                {!hasFacility && form.facilityId && (
                  <option value={form.facilityId}>{appointment.facilityName ?? 'Current facility'}</option>
                )}
                <option value="">Keep current facility</option>
                {schedulingOptions?.facilities.map((facility) => (
                  <option key={facility.id} value={facility.id}>
                    {facility.name}{facility.code ? ` (${facility.code})` : ''}
                  </option>
                ))}
              </select>
            </div>
          </div>
          {schedulingOptionsError && <p className="cl-table-sub" role="status">{schedulingOptionsError}</p>}
          <div className="form-row">
            <div className="field">
              <label className="label" htmlFor="schedule-edit-appt-duration">Duration</label>
              <select
                id="schedule-edit-appt-duration"
                className="select"
                value={form.durationMinutes}
                onChange={(event) => updateForm('durationMinutes', Number(event.target.value))}
              >
                {durationOptions.map((duration) => (
                  <option key={duration} value={duration}>{duration} min</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label className="label" htmlFor="schedule-edit-appt-room">Room</label>
              <input
                id="schedule-edit-appt-room"
                className="input"
                value={form.room}
                onChange={(event) => updateForm('room', event.target.value)}
              />
            </div>
          </div>
          <div className="field">
            <label className="label" htmlFor="schedule-edit-appt-status">Status</label>
            <select
              id="schedule-edit-appt-status"
              className="select"
              value={form.status}
              onChange={(event) => updateForm('status', event.target.value)}
            >
              {getAppointmentStatusOptions(appointment.status).map((status) => (
                <option key={status.apiValue} value={status.apiValue}>{status.label}</option>
              ))}
            </select>
          </div>
          <div className="field">
            <label className="label" htmlFor="schedule-edit-appt-comments">Comments</label>
            <textarea
              id="schedule-edit-appt-comments"
              className="textarea"
              rows={3}
              value={form.comments}
              onChange={(event) => updateForm('comments', event.target.value)}
            />
          </div>
          <div className="button-row">
            <button className="button-primary" type="submit" disabled={saving}>
              {saving ? 'Saving…' : 'Save changes'}
            </button>
            <button
              className="button-secondary"
              type="button"
              onClick={close}
              disabled={saving}
              style={{ flex: 'none', width: 'auto' }}
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
