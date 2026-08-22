// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Outlet, Route, Routes, useNavigate, useParams } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getClinicalLists,
  getProcedureReportQueue,
  searchAppointments,
  searchEncounters,
  type AppointmentSearchResponse,
  type ClinicalListsResponse,
  type EncounterSearchResponse,
  type ProcedureReportQueueResponse,
} from '../../api.ts'
import PatientTimeline from './PatientTimeline.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...original,
    getClinicalLists: vi.fn(),
    getProcedureReportQueue: vi.fn(),
    searchAppointments: vi.fn(),
    searchEncounters: vi.fn(),
  }
})

function TestOutlet() {
  const navigate = useNavigate()
  return (
    <>
      <button type="button" onClick={() => navigate('/clinician/patients/patient-b/timeline')}>
        Open Beta timeline
      </button>
      <Outlet
        context={{
          session: { sessionId: 'staff-session', username: 'clinician', displayName: 'Clinician', role: 'staff' },
          patient: {},
          patientId: 'placeholder',
          reload: vi.fn(),
          signOut: vi.fn(),
        }}
      />
    </>
  )
}

function PatientTimelineRoute() {
  const { patientId = '' } = useParams<{ patientId: string }>()
  return (
    <Outlet
      context={{
        session: { sessionId: 'staff-session', username: 'clinician', displayName: 'Clinician', role: 'staff' },
        patient: {},
        patientId,
        reload: vi.fn(),
        signOut: vi.fn(),
      }}
    />
  )
}

function renderTimeline() {
  return render(
    <MemoryRouter initialEntries={['/clinician/patients/patient-a/timeline']}>
      <Routes>
        <Route path="/clinician" element={<TestOutlet />}>
          <Route path="patients/:patientId" element={<PatientTimelineRoute />}>
            <Route path="timeline" element={<PatientTimeline />} />
          </Route>
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

function appointmentResponse(patientId: string, title: string): AppointmentSearchResponse {
  return {
    totalMatches: 1,
    appointments: [{
      id: `${patientId}-appointment`,
      patientId,
      date: '2026-08-22',
      title,
      startTime: '09:00:00',
      status: 'scheduled',
    }],
  } as AppointmentSearchResponse
}

const emptyEncounters = (): EncounterSearchResponse => ({ totalMatches: 0, encounters: [] })
const emptyLabs = (): ProcedureReportQueueResponse => ({ reports: [] }) as unknown as ProcedureReportQueueResponse
const emptyClinicalLists = (): ClinicalListsResponse => ({ prescriptions: [] }) as unknown as ClinicalListsResponse

describe('PatientTimeline', () => {
  beforeEach(() => vi.clearAllMocks())

  it('does not render an obsolete chart timeline after navigation', async () => {
    let resolveFirst!: (value: EncounterSearchResponse) => void
    let firstSignal: AbortSignal | undefined
    const firstResponse = new Promise<EncounterSearchResponse>((resolve) => { resolveFirst = resolve })
    vi.mocked(searchEncounters).mockImplementation((_sessionId, params, signal) => {
      if (params.patientId === 'patient-a') {
        firstSignal = signal
        return firstResponse
      }
      return Promise.resolve(emptyEncounters())
    })
    vi.mocked(searchAppointments).mockImplementation((_sessionId, params) => Promise.resolve(
      appointmentResponse(params.patientId ?? '', params.patientId === 'patient-a' ? 'Alpha appointment' : 'Beta appointment'),
    ))
    vi.mocked(getProcedureReportQueue).mockResolvedValue(emptyLabs())
    vi.mocked(getClinicalLists).mockResolvedValue(emptyClinicalLists())

    const user = userEvent.setup()
    renderTimeline()

    await waitFor(() => expect(searchEncounters).toHaveBeenCalledWith(
      'staff-session',
      { patientId: 'patient-a', limit: 100 },
      expect.any(AbortSignal),
    ))
    await user.click(screen.getByRole('button', { name: 'Open Beta timeline' }))
    expect(await screen.findByText('Beta appointment')).toBeInTheDocument()
    expect(firstSignal?.aborted).toBe(true)

    await act(async () => {
      resolveFirst(emptyEncounters())
      await firstResponse
    })

    await waitFor(() => expect(screen.queryByText('Alpha appointment')).not.toBeInTheDocument())
    expect(screen.getByText('Beta appointment')).toBeInTheDocument()
  })

  it('announces a loading failure and retries the current chart', async () => {
    vi.mocked(searchEncounters).mockResolvedValue(emptyEncounters())
    vi.mocked(searchAppointments)
      .mockRejectedValueOnce(new Error('Timeline service unavailable'))
      .mockResolvedValueOnce(appointmentResponse('patient-a', 'Recovered appointment'))
    vi.mocked(getProcedureReportQueue).mockResolvedValue(emptyLabs())
    vi.mocked(getClinicalLists).mockResolvedValue(emptyClinicalLists())

    const user = userEvent.setup()
    renderTimeline()

    expect(await screen.findByRole('alert')).toHaveTextContent('Timeline service unavailable')
    await user.click(screen.getByRole('button', { name: 'Retry' }))

    expect(await screen.findByText('Recovered appointment')).toBeInTheDocument()
    expect(searchAppointments).toHaveBeenCalledTimes(2)
  })
})
