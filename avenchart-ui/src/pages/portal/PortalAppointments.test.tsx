import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import PortalAppointments from './PortalAppointments.tsx'
import {
  getPatientPortalAppointmentRequestOptions,
  requestPatientPortalAppointment,
} from '../../api.ts'
import { getPatientPortalAppointmentsWithRequestHistory } from '../../api/portalAppointments.ts'
import type { PortalOutletContext } from './PortalShell.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...actual,
    getPatientPortalAppointmentRequestOptions: vi.fn(),
    requestPatientPortalAppointment: vi.fn(),
  }
})

vi.mock('../../api/portalAppointments.ts', () => ({
  getPatientPortalAppointmentsWithRequestHistory: vi.fn(),
}))

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

const appointmentsWithHistory = {
  authenticated: true,
  sessionId: 'portal-session',
  username: 'patient',
  portalUsername: 'patient',
  canonicalId: 'MOD-PAT-0004',
  legacyPid: 100004,
  pubpid: 'MOD-PAT-0004',
  displayName: 'Patient Example',
  datasetId: 'gold',
  datasetVersion: '2026-07-29',
  asOfDate: '2026-07-29',
  upcomingAppointmentCount: 0,
  upcomingAppointments: [],
  pastAppointmentCount: 0,
  pastAppointments: [],
  appointmentRequestCount: 1,
  appointmentRequests: [
    {
      appointmentId: 'APPT-PORTAL-TEST',
      state: 'pending' as const,
      stateLabel: 'Pending practice review',
      stateSource: 'stored lifecycle',
      requestedAt: '2026-07-29T20:00:00Z',
      updatedAt: '2026-07-29T20:00:00Z',
      nextAction: 'The practice will review this request.',
      version: 1,
      date: '2026-08-12',
      startTime: '10:30',
      durationMinutes: 30,
      categoryId: 4,
      categoryName: 'Annual wellness',
      providerId: 9,
      providerName: 'Alex Kim',
      facilityId: 3,
      facilityName: 'North Clinic',
      title: 'Annual wellness',
      reason: 'Preventive visit',
      rawStatus: '^',
      evidenceSource: 'runtime' as const,
      events: [
        {
          eventId: '40000000-0000-0000-0000-000000000001',
          sequence: 1,
          action: 'requested',
          state: 'pending' as const,
          rawStatus: '^',
          occurredAt: '2026-07-29T20:00:00Z',
          evidenceSource: 'runtime' as const,
        },
      ],
    },
  ],
  failureReason: null,
  sessionSource: 'test',
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
    vi.mocked(
      getPatientPortalAppointmentsWithRequestHistory,
    ).mockResolvedValue(appointmentsWithHistory)
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

  it('renders durable request state, next action, and lifecycle evidence', async () => {
    const user = userEvent.setup()
    renderAppointments()

    const history = await screen.findByRole('region', {
      name: 'Appointment request history',
    })
    expect(within(history).getByText('Pending practice review')).toBeInTheDocument()
    expect(
      within(history).getByText('The practice will review this request.', {
        exact: false,
      }),
    ).toBeInTheDocument()
    expect(within(history).getByText('Alex Kim')).toBeInTheDocument()
    expect(within(history).getByText('North Clinic')).toBeInTheDocument()

    await user.click(
      within(history).getByText('Lifecycle evidence (1)'),
    )
    expect(within(history).getByText('requested')).toBeInTheDocument()
    expect(within(history).getByText(/diagnostic status/)).toBeInTheDocument()
  })
})
