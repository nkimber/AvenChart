import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import PortalAppointments from './PortalAppointments.tsx'
import {
  getPatientPortalAppointmentRequestOptions,
  requestPatientPortalAppointment,
} from '../../api.ts'
import type { PortalOutletContext } from './PortalShell.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...actual,
    getPatientPortalAppointmentRequestOptions: vi.fn(),
    requestPatientPortalAppointment: vi.fn(),
  }
})

const appointmentOptions = {
  authenticated: true,
  categories: [{ id: 4, name: 'Annual wellness', durationMinutes: 30 }],
  providers: [
    { id: 7, displayName: 'Alex Kim' },
    { id: 9, displayName: 'Alex Kim' },
  ],
  facilities: [
    { id: 2, name: 'Downtown Clinic' },
    { id: 3, name: 'North Clinic' },
  ],
  defaults: {
    categoryId: 4,
    providerId: 9,
    facilityId: 3,
    durationMinutes: 30,
    date: '2026-08-12',
    startTime: '10:30:00',
  },
}

const context: PortalOutletContext = {
  session: {
    sessionId: 'portal-session',
    username: 'patient',
    portalUsername: 'patient',
    displayName: 'Patient Example',
  },
  home: null,
  homeLoading: false,
  markReadOptimistic: vi.fn(),
  refreshHome: vi.fn(async () => undefined),
  signOut: vi.fn(async () => undefined),
}

function renderAppointments(openRequest = false) {
  return render(
    <MemoryRouter
      initialEntries={[
        {
          pathname: '/portal/appointments',
          state: openRequest ? { openRequest: true } : undefined,
        },
      ]}
    >
      <Routes>
        <Route element={<Outlet context={context} />}>
          <Route path="/portal/appointments" element={<PortalAppointments />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('PortalAppointments request flow', () => {
  beforeEach(() => {
    vi.mocked(getPatientPortalAppointmentRequestOptions).mockResolvedValue(
      appointmentOptions,
    )
    vi.mocked(requestPatientPortalAppointment).mockReset()
  })

  afterEach(() => {
    vi.clearAllMocks()
    document.body.style.overflow = ''
  })

  it('loads request options when opened from the dashboard and disambiguates choices', async () => {
    renderAppointments(true)

    const provider = await screen.findByLabelText('Provider')
    expect(
      within(provider).getByRole('option', { name: 'Alex Kim (provider #7)' }),
    ).toBeInTheDocument()
    expect(
      within(provider).getByRole('option', { name: 'Alex Kim (provider #9)' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Facility')).toHaveValue('3')
    expect(
      screen.getByText(
        /requesting annual wellness with alex kim \(provider #9\) at north clinic/i,
      ),
    ).toBeInTheDocument()
  })

  it('traps the modal and restores focus after Escape', async () => {
    const user = userEvent.setup()
    renderAppointments()

    const trigger = screen.getByRole('button', { name: 'Request an appointment' })
    await user.click(trigger)
    await screen.findByLabelText('Provider')

    expect(document.body.style.overflow).toBe('hidden')
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(trigger).toHaveFocus()
    expect(document.body.style.overflow).toBe('')
  })
})
