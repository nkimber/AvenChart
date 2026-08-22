// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Outlet, Route, Routes, useNavigate, useParams } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getPatientMessages, type PatientMessagesResponse } from '../../api.ts'
import PatientMessages from './PatientMessages.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...original,
    getPatientMessages: vi.fn(),
  }
})

function TestOutlet() {
  const navigate = useNavigate()
  return (
    <>
      <button type="button" onClick={() => navigate('/clinician/patients/patient-b/messages')}>
        Open Beta messages
      </button>
      <Outlet
        context={{
          session: {
            sessionId: 'staff-session',
            username: 'clinician',
            displayName: 'Clinician',
            role: 'staff',
          },
          patient: {},
          patientId: 'placeholder',
          reload: vi.fn(),
          signOut: vi.fn(),
        }}
      />
    </>
  )
}

function PatientMessageRoute() {
  const { patientId = '' } = useParams<{ patientId: string }>()
  return (
    <Outlet
      context={{
        session: {
          sessionId: 'staff-session',
          username: 'clinician',
          displayName: 'Clinician',
          role: 'staff',
        },
        patient: {},
        patientId,
        reload: vi.fn(),
        signOut: vi.fn(),
      }}
    />
  )
}

function renderMessages() {
  return render(
    <MemoryRouter initialEntries={['/clinician/patients/patient-a/messages']}>
      <Routes>
        <Route path="/clinician" element={<TestOutlet />}>
          <Route path="patients/:patientId" element={<PatientMessageRoute />}>
            <Route path="messages" element={<PatientMessages />} />
          </Route>
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

function messages(patientId: string, title: string): PatientMessagesResponse {
  return {
    patientId,
    patientDisplayName: patientId,
    portalEnabled: true,
    messages: [{
      id: `${patientId}-message`,
      title,
      body: `${title} body`,
      status: 'New',
      deleted: 0,
      assignmentVersion: 1,
    }],
  }
}

describe('PatientMessages', () => {
  beforeEach(() => vi.clearAllMocks())

  it('does not show an obsolete chart response after navigating to another patient', async () => {
    let resolveFirst!: (value: PatientMessagesResponse) => void
    let firstSignal: AbortSignal | undefined
    const firstResponse = new Promise<PatientMessagesResponse>((resolve) => { resolveFirst = resolve })
    vi.mocked(getPatientMessages).mockImplementation((_sessionId, patientId, signal) => {
      if (patientId === 'patient-a') {
        firstSignal = signal
        return firstResponse
      }
      return Promise.resolve(messages('patient-b', 'Beta message'))
    })
    const user = userEvent.setup()
    renderMessages()

    await waitFor(() => expect(getPatientMessages).toHaveBeenCalledWith('staff-session', 'patient-a', expect.any(AbortSignal)))
    await user.click(screen.getByRole('button', { name: 'Open Beta messages' }))
    expect(await screen.findByText('Beta message')).toBeInTheDocument()
    expect(firstSignal?.aborted).toBe(true)

    await act(async () => {
      resolveFirst(messages('patient-a', 'Alpha message'))
      await firstResponse
    })

    await waitFor(() => expect(screen.queryByText('Alpha message')).not.toBeInTheDocument())
    expect(screen.getByText('Beta message')).toBeInTheDocument()
  })
})
