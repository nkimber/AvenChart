// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getCriticalLabResultQueue,
  getProcedureOrderQueue,
  getProcedureReportQueue,
  type ProcedureOrderQueueResponse,
  type ProcedureReportQueueResponse,
} from '../../api.ts'
import { AppErrorBoundary } from '../../components/AppErrorBoundary.tsx'
import LabQueue from './LabQueue.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...original,
    getCriticalLabResultQueue: vi.fn(),
    getProcedureOrderQueue: vi.fn(),
    getProcedureReportQueue: vi.fn(),
  }
})

const reportQueue: ProcedureReportQueueResponse = {
  datasetId: 'test',
  datasetVersion: 'v1',
  statusFilter: 'unreviewed',
  limit: 100,
  totalReports: 0,
  reviewedReports: 0,
  unreviewedReports: 0,
  reports: [],
}

const orderQueue: ProcedureOrderQueueResponse = {
  datasetId: 'test',
  datasetVersion: 'v1',
  statusFilter: 'ready-to-send',
  limit: 100,
  totalOrders: 0,
  readyToSendOrders: 0,
  transmittedPendingOrders: 0,
  reportedOrders: 0,
  scheduledOrders: 0,
  completedOrders: 0,
  orders: [],
}

function TestOutlet() {
  return (
    <Outlet
      context={{
        session: {
          sessionId: 'staff-session',
          username: 'admin',
          displayName: 'Administrator',
          role: 'administrator',
        },
        signOut: vi.fn(),
      }}
    />
  )
}

function renderLabQueue() {
  return render(
    <AppErrorBoundary>
      <MemoryRouter initialEntries={['/clinician/labs']}>
        <Routes>
          <Route path="/clinician" element={<TestOutlet />}>
            <Route path="labs" element={<LabQueue />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AppErrorBoundary>,
  )
}

describe('LabQueue', () => {
  beforeEach(() => {
    vi.mocked(getProcedureReportQueue).mockResolvedValue(reportQueue)
    vi.mocked(getProcedureOrderQueue).mockResolvedValue(orderQueue)
    vi.mocked(getCriticalLabResultQueue).mockResolvedValue({
      totalOpen: 0,
      results: [],
    })
  })

  it('keeps an incomplete report contract inside the retryable page state', async () => {
    vi.mocked(getProcedureReportQueue).mockResolvedValueOnce({
      ...reportQueue,
      reports: undefined,
    } as unknown as ProcedureReportQueueResponse)

    renderLabQueue()

    expect(
      await screen.findByRole('alert'),
    ).toHaveTextContent(
      'The lab report queue response was incomplete. Retry the page.',
    )
    expect(
      screen.queryByRole('heading', {
        name: 'This page could not be displayed',
      }),
    ).not.toBeInTheDocument()
  })

  it('keeps an incomplete critical-result contract from crashing the queue', async () => {
    vi.mocked(getCriticalLabResultQueue).mockResolvedValueOnce({
      totalOpen: 1,
      results: undefined,
    } as unknown as Awaited<ReturnType<typeof getCriticalLabResultQueue>>)

    renderLabQueue()

    expect(
      await screen.findByRole('region', { name: 'Report review totals' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent(
      'The critical-result acknowledgement queue is unavailable.',
    )
    expect(
      screen.queryByRole('heading', {
        name: 'This page could not be displayed',
      }),
    ).not.toBeInTheDocument()
  })

  it('preserves the explicit all status when filters are applied', async () => {
    const user = userEvent.setup()
    renderLabQueue()
    await screen.findByRole('region', { name: 'Report review totals' })

    await user.selectOptions(screen.getByLabelText('Review status'), 'all')
    await user.click(screen.getByRole('button', { name: 'Apply filters' }))

    await waitFor(() =>
      expect(getProcedureReportQueue).toHaveBeenLastCalledWith(
        'staff-session',
        expect.objectContaining({ status: 'all' }),
        expect.any(AbortSignal),
      ),
    )
  })
})
