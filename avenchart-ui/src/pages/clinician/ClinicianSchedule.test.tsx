// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getAppointmentSchedulingOptions,
  rescheduleAppointmentOccurrence,
  searchAppointments,
  updateAppointment,
  type AppointmentListItem,
} from '../../api.ts'
import ClinicianSchedule from './ClinicianSchedule.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...original,
    getAppointmentSchedulingOptions: vi.fn(),
    rescheduleAppointmentOccurrence: vi.fn(),
    searchAppointments: vi.fn(),
    updateAppointment: vi.fn(),
  }
})

const appointment: AppointmentListItem = {
  id: 'appointment-1',
  seriesRootId: 'appointment-1',
  isRecurringSeries: false,
  isVirtualOccurrence: false,
  patientId: 'patient-1',
  patientDisplayName: 'Alex Rivera',
  pubpid: 'P0001',
  date: '2030-04-17',
  startTime: '09:00:00',
  durationMinutes: 20,
  title: 'Office visit',
  status: '-',
  room: '101',
  providerId: 12,
  providerName: 'Dr. Morgan',
  facilityId: 3,
  facilityName: 'Main Clinic',
  comments: 'Bring medication list',
  recurrenceType: 0,
  recurrenceDays: [],
  recurrenceExdates: [],
  reminderDue: false,
  reminderStatus: 'not-due',
  reminderChannel: 'none',
}

function TestOutlet() {
  return (
    <Outlet
      context={{
        session: {
          sessionId: 'staff-session',
          username: 'scheduler',
          displayName: 'Scheduler',
          role: 'staff',
        },
        signOut: vi.fn(),
      }}
    />
  )
}

function renderSchedule() {
  return render(
    <MemoryRouter initialEntries={['/clinician/schedule?date=2030-04-17']}>
      <Routes>
        <Route path="/clinician" element={<TestOutlet />}>
          <Route path="schedule" element={<ClinicianSchedule />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('ClinicianSchedule appointment editing', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(searchAppointments).mockResolvedValue({ totalMatches: 1, appointments: [appointment] })
    vi.mocked(getAppointmentSchedulingOptions).mockResolvedValue({
      providers: [{ id: 12, displayName: 'Dr. Morgan', facilityId: 3, facilityName: 'Main Clinic' }],
      facilities: [{ id: 3, name: 'Main Clinic', code: 'MAIN' }],
    })
    vi.mocked(updateAppointment).mockResolvedValue(appointment)
    vi.mocked(rescheduleAppointmentOccurrence).mockResolvedValue(appointment)
  })

  it('edits an appointment from its schedule row and refreshes the day', async () => {
    const user = userEvent.setup()
    renderSchedule()

    await user.click(await screen.findByRole('button', { name: 'Edit appointment for Alex Rivera at 09:00' }))
    const dialog = screen.getByRole('dialog', { name: 'Edit appointment' })

    const title = within(dialog).getByLabelText('Visit type / title')
    await user.clear(title)
    await user.type(title, 'Annual wellness visit')
    await user.clear(within(dialog).getByLabelText('Room'))
    await user.type(within(dialog).getByLabelText('Room'), '204')
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(updateAppointment).toHaveBeenCalledWith(
      'staff-session',
      'appointment-1',
      expect.objectContaining({
        title: 'Annual wellness visit',
        date: '2030-04-17',
        startTime: '09:00',
        room: '204',
        recurrenceType: 0,
      }),
    ))
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Edit appointment' })).not.toBeInTheDocument())
    expect(searchAppointments).toHaveBeenCalledTimes(2)
  })

  it('edits a virtual recurring occurrence without changing the full series', async () => {
    const recurringOccurrence: AppointmentListItem = {
      ...appointment,
      id: 'appointment-1::2030-04-17',
      isRecurringSeries: true,
      isVirtualOccurrence: true,
      occurrenceNumber: 3,
      recurrenceType: 1,
      repeatFrequency: 1,
      repeatUnit: 2,
      recurrenceEndDate: '2030-12-31',
    }
    vi.mocked(searchAppointments).mockResolvedValue({ totalMatches: 1, appointments: [recurringOccurrence] })
    vi.mocked(rescheduleAppointmentOccurrence).mockResolvedValue(recurringOccurrence)
    const user = userEvent.setup()
    renderSchedule()

    await user.click(await screen.findByRole('button', { name: 'Edit appointment for Alex Rivera at 09:00' }))
    const dialog = screen.getByRole('dialog', { name: 'Edit appointment occurrence' })
    expect(within(dialog).getByText('Changes apply only to the 2030-04-17 occurrence.')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(rescheduleAppointmentOccurrence).toHaveBeenCalledWith(
      'staff-session',
      'appointment-1::2030-04-17',
      '2030-04-17',
      expect.objectContaining({ title: 'Office visit', date: '2030-04-17' }),
    ))
    expect(updateAppointment).not.toHaveBeenCalled()
  })
})
