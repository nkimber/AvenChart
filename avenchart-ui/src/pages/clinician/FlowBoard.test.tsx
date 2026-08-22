// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getAppointmentFlowBoard, updateAppointmentStatus, type FlowBoardResponse } from '../../api.ts'
import FlowBoard from './FlowBoard.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...original,
    getAppointmentFlowBoard: vi.fn(),
    updateAppointmentStatus: vi.fn(),
  }
})

function board(date: string, patientDisplayName: string): FlowBoardResponse {
  return {
    date,
    lanes: [{
      key: 'scheduled',
      label: 'Scheduled',
      items: [{
        appointmentId: `${date}-appointment`,
        rowVersion: 1,
        patientId: 'patient-1',
        patientDisplayName,
        startTime: '09:00:00',
        title: 'Office visit',
        flowStatus: 'scheduled',
      }],
    }],
  }
}

const currentDate = new Date().toISOString().slice(0, 10)

function TestOutlet() {
  return (
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
  )
}

function renderFlowBoard() {
  return render(
    <MemoryRouter initialEntries={['/clinician/flow']}>
      <Routes>
        <Route path="/clinician" element={<TestOutlet />}>
          <Route path="flow" element={<FlowBoard />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('FlowBoard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(updateAppointmentStatus).mockResolvedValue({} as never)
  })

  it('keeps an obsolete date response from replacing the selected day', async () => {
    let resolveInitial!: (value: FlowBoardResponse) => void
    let resolveSelected!: (value: FlowBoardResponse) => void
    let initialSignal: AbortSignal | undefined
    const initial = new Promise<FlowBoardResponse>((resolve) => { resolveInitial = resolve })
    const selected = new Promise<FlowBoardResponse>((resolve) => { resolveSelected = resolve })

    vi.mocked(getAppointmentFlowBoard).mockImplementation((_sessionId, date, signal) => {
      if (date === currentDate) {
        initialSignal = signal
        return initial
      }
      return selected
    })

    renderFlowBoard()

    await waitFor(() => expect(getAppointmentFlowBoard).toHaveBeenCalledTimes(1))
    fireEvent.change(screen.getByLabelText('Select flow-board date'), { target: { value: '2030-04-17' } })

    await waitFor(() => expect(getAppointmentFlowBoard).toHaveBeenCalledWith(
      'staff-session',
      '2030-04-17',
      expect.any(AbortSignal),
    ))
    expect(initialSignal?.aborted).toBe(true)

    resolveSelected(board('2030-04-17', 'Selected Day Patient'))
    expect(await screen.findByRole('heading', { name: 'Selected Day Patient' })).toBeInTheDocument()

    resolveInitial(board(currentDate, 'Obsolete Day Patient'))
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Obsolete Day Patient' })).not.toBeInTheDocument())
    expect(screen.getByRole('heading', { name: 'Selected Day Patient' })).toBeInTheDocument()
  })

  it('removes stale actions and announces a failed refresh', async () => {
    vi.mocked(getAppointmentFlowBoard)
      .mockResolvedValueOnce(board(currentDate, 'Initial Patient'))
      .mockRejectedValueOnce(new TypeError('Network unavailable'))

    renderFlowBoard()

    expect(await screen.findByRole('button', { name: 'Arrive' })).toBeEnabled()
    fireEvent.change(screen.getByLabelText('Select flow-board date'), { target: { value: '2030-04-18' } })

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the flow board. Retry the page.')
    expect(screen.queryByRole('button', { name: 'Arrive' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeEnabled()
  })
})
