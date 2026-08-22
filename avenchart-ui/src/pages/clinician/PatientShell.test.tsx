// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Outlet, Route, Routes, useNavigate } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiRequestError, getPatientChartSummary, type PatientChartSummary } from '../../api.ts'
import PatientShell from './PatientShell.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...original,
    getPatientChartSummary: vi.fn(),
  }
})

function patient(canonicalId: string, displayName: string): PatientChartSummary {
  return {
    canonicalId,
    legacyPid: 1,
    pubpid: canonicalId,
    administrationVersion: 1,
    displayName,
    firstName: displayName.split(' ')[0],
    lastName: displayName.split(' ')[1],
    dateOfBirth: '1980-01-01',
    age: 50,
    portalEnabled: false,
    registrationDate: '2026-01-01',
    lifecycleStatus: 'active',
    insurance: [],
    duplicateCandidates: [],
    counts: {} as PatientChartSummary['counts'],
  }
}

function ClinicianTestOutlet() {
  const navigate = useNavigate()
  return (
    <>
      <button type="button" onClick={() => navigate('/clinician/patients/patient-b/summary')}>
        Open Beta patient
      </button>
      <Outlet
        context={{
          session: {
            sessionId: 'staff-session',
            username: 'clinician',
            displayName: 'Clinician',
            role: 'staff',
          },
          signOut: vi.fn(),
        }}
      />
    </>
  )
}

function renderPatientShell(initialPath = '/clinician/patients/patient-a/summary') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/clinician" element={<ClinicianTestOutlet />}>
          <Route path="patients/:patientId" element={<PatientShell />}>
            <Route path="summary" element={<p>Patient summary tab</p>} />
          </Route>
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('PatientShell', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('does not render an obsolete patient response after chart navigation', async () => {
    let resolveA!: (value: PatientChartSummary) => void
    let resolveB!: (value: PatientChartSummary) => void
    let firstSignal: AbortSignal | undefined
    const patientA = new Promise<PatientChartSummary>((resolve) => { resolveA = resolve })
    const patientB = new Promise<PatientChartSummary>((resolve) => { resolveB = resolve })

    vi.mocked(getPatientChartSummary).mockImplementation((_sessionId, patientId, signal) => {
      if (patientId === 'patient-a') {
        firstSignal = signal
        return patientA
      }
      return patientB
    })

    const user = userEvent.setup()
    renderPatientShell()

    await waitFor(() => expect(getPatientChartSummary).toHaveBeenCalledWith(
      'staff-session',
      'patient-a',
      expect.any(AbortSignal),
    ))
    await user.click(screen.getByRole('button', { name: 'Open Beta patient' }))
    await waitFor(() => expect(getPatientChartSummary).toHaveBeenCalledWith(
      'staff-session',
      'patient-b',
      expect.any(AbortSignal),
    ))
    expect(firstSignal?.aborted).toBe(true)

    resolveB(patient('patient-b', 'Beta Patient'))
    expect(await screen.findByRole('heading', { name: 'Beta Patient' })).toBeInTheDocument()

    resolveA(patient('patient-a', 'Alpha Patient'))
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Alpha Patient' })).not.toBeInTheDocument())
    expect(screen.getByRole('heading', { name: 'Beta Patient' })).toBeInTheDocument()
  })

  it('redirects a merged chart link to its surviving patient chart', async () => {
    vi.mocked(getPatientChartSummary).mockImplementation((_sessionId, patientId) => {
      if (patientId === 'merged-patient') {
        return Promise.reject(new ApiRequestError(
          'This chart is no longer independently available.',
          410,
          { targetPatientId: 'surviving-patient' },
        ))
      }
      return Promise.resolve(patient('surviving-patient', 'Surviving Patient'))
    })

    renderPatientShell('/clinician/patients/merged-patient/summary')

    expect(await screen.findByRole('heading', { name: 'Surviving Patient' })).toBeInTheDocument()
    expect(getPatientChartSummary).toHaveBeenCalledWith(
      'staff-session',
      'surviving-patient',
      expect.any(AbortSignal),
    )
  })
})
