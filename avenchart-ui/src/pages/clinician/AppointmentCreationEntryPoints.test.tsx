// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  createAppointment,
  getAppointmentSchedulingOptions,
  searchAppointments,
  searchPatients,
  validateAppointmentAvailability,
  type AppointmentListItem,
  type PatientListItem,
} from '../../api.ts'
import ClinicianCalendar from './ClinicianCalendar.tsx'
import ClinicianSchedule from './ClinicianSchedule.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...original,
    createAppointment: vi.fn(),
    getAppointmentSchedulingOptions: vi.fn(),
    searchAppointments: vi.fn(),
    searchPatients: vi.fn(),
    validateAppointmentAvailability: vi.fn(),
  }
})

const patient: PatientListItem = {
  canonicalId: 'patient-1',
  legacyPid: 1,
  pubpid: 'P0001',
  displayName: 'Alex Rivera',
  firstName: 'Alex',
  lastName: 'Rivera',
  dateOfBirth: '1985-03-14',
  age: 41,
  counts: {
    appointments: 0,
    encounters: 0,
    prescriptions: 0,
    billingItems: 0,
    labOrders: 0,
    messages: 0,
    problems: 0,
    allergies: 0,
    medications: 0,
  },
}

const createdAppointment: AppointmentListItem = {
  id: 'appointment-1',
  rowVersion: 1,
  seriesRootId: 'appointment-1',
  isRecurringSeries: false,
  isVirtualOccurrence: false,
  patientId: patient.canonicalId,
  patientDisplayName: patient.displayName,
  pubpid: patient.pubpid,
  date: '2030-04-17',
  startTime: '09:00',
  durationMinutes: 20,
  title: 'Office visit',
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

function renderPage(path: string, childPath: string, element: React.ReactNode) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/clinician" element={<TestOutlet />}>
          <Route path={childPath} element={element} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

function localIsoDate(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

describe('appointment creation entry points', () => {
  beforeEach(() => {
    vi.mocked(searchAppointments).mockResolvedValue({ totalMatches: 0, appointments: [] })
    vi.mocked(getAppointmentSchedulingOptions).mockResolvedValue({ providers: [], facilities: [] })
    vi.mocked(searchPatients).mockResolvedValue({ totalMatches: 1, patients: [patient] })
    vi.mocked(validateAppointmentAvailability).mockResolvedValue({
      available: true,
      validationStatus: 'available',
      date: '2030-04-17',
      startTime: '09:00',
      endTime: '09:20',
      durationMinutes: 20,
      patientKnown: true,
      providerAvailable: true,
      facilityAvailable: true,
      withinBusinessHours: true,
      conflictCount: 0,
      conflicts: [],
      messages: [],
    })
    vi.mocked(createAppointment).mockResolvedValue(createdAppointment)
  })

  it('creates an appointment from Schedule with its selected date prefilled', async () => {
    const user = userEvent.setup()
    renderPage('/clinician/schedule?date=2030-04-17', 'schedule', <ClinicianSchedule />)

    await screen.findByText('No appointments for this date.')
    await user.click(screen.getByRole('button', { name: 'New appointment' }))

    const dialog = screen.getByRole('dialog', { name: 'New appointment' })
    expect(within(dialog).getByLabelText('Date')).toHaveValue('2030-04-17')

    await user.type(within(dialog).getByLabelText('Search patients for appointment'), 'Alex')
    await user.click(within(dialog).getByRole('button', { name: 'Search' }))
    await user.click(await within(dialog).findByRole('button', { name: /Alex Rivera/ }))
    await user.click(within(dialog).getByRole('button', { name: 'Create appointment' }))

    await waitFor(() => expect(createAppointment).toHaveBeenCalledWith(
      'staff-session',
      expect.objectContaining({ patientId: 'patient-1', date: '2030-04-17' }),
    ))
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'New appointment' })).not.toBeInTheDocument())
    expect(searchAppointments).toHaveBeenCalledTimes(2)
  })

  it('offers appointment creation after selecting an empty Calendar day', async () => {
    const user = userEvent.setup()
    const now = new Date()
    const selectedDate = localIsoDate(new Date(now.getFullYear(), now.getMonth(), 15))
    renderPage('/clinician/calendar', 'calendar', <ClinicianCalendar />)

    await user.click(await screen.findByRole('button', { name: selectedDate }))
    expect(screen.getByText('No appointments scheduled.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'New appointment' }))
    const dialog = screen.getByRole('dialog', { name: 'New appointment' })
    expect(within(dialog).getByLabelText('Date')).toHaveValue(selectedDate)
    expect(within(dialog).getByLabelText('Search patients for appointment')).toBeInTheDocument()
  })
})
