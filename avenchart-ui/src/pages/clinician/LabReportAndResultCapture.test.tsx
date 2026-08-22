// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { createProcedureReport, type ProcedureResultsResponse } from '../../api.ts'
import LabReportAndResultCapture from './LabReportAndResultCapture.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return { ...original, createProcedureReport: vi.fn() }
})

vi.mock('../../components/Toast.tsx', () => ({ showToast: vi.fn() }))

const detail = {
  patientId: 'MOD-PAT-0001',
  pubpid: 'PUB-0001',
  patientDisplayName: 'Alex Morgan',
  counts: { orders: 1, reports: 0, results: 0, finalResults: 0 },
  orders: [],
} as unknown as ProcedureResultsResponse

const orders: ProcedureResultsResponse['orders'] = [{
  id: 7001,
  encounter: 1000013,
  providerName: 'Dr. Ada Lovelace',
  orderDate: '2026-07-29',
  orderPriority: 'routine',
  code: 'BMP',
  name: 'Basic metabolic panel',
  procedureType: 'laboratory',
  diagnosis: 'Routine monitoring',
  instructions: '',
  orderStatus: 'pending',
  specimens: [
    {
      id: 8001,
      specimenIdentifier: 'SPEC-RECEIVED',
      accessionIdentifier: 'ACC-RECEIVED',
      specimenType: 'serum',
      collectionMethod: null,
      specimenLocation: null,
      collectedDate: '2026-07-29 12:00',
      specimenCondition: null,
      comments: null,
      lifecycleStatus: 'received',
      lifecycleVersion: 2,
      lifecycleHistoryCount: 2,
    },
    {
      id: 8002,
      specimenIdentifier: 'SPEC-REJECTED',
      accessionIdentifier: 'ACC-REJECTED',
      specimenType: 'serum',
      collectionMethod: null,
      specimenLocation: null,
      collectedDate: '2026-07-29 12:00',
      specimenCondition: null,
      comments: null,
      lifecycleStatus: 'rejected',
      lifecycleVersion: 2,
      lifecycleHistoryCount: 2,
    },
  ],
  reports: [],
}]

describe('LabReportAndResultCapture', () => {
  it('binds a new report to a received specimen and never sends a free-text specimen reference', async () => {
    vi.mocked(createProcedureReport).mockResolvedValue(detail)
    const onChange = vi.fn()
    const user = userEvent.setup()

    render(<LabReportAndResultCapture sessionId="staff-session" orders={orders} onChange={onChange} />)

    await user.selectOptions(screen.getByLabelText('Order'), '7001')
    const specimenSelect = screen.getByLabelText('Received specimen / accession')
    expect(screen.getByRole('option', { name: 'ACC-RECEIVED' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: 'ACC-REJECTED' })).not.toBeInTheDocument()

    await user.selectOptions(specimenSelect, '8001')
    await user.click(screen.getByRole('button', { name: 'Save report' }))

    await waitFor(() => expect(createProcedureReport).toHaveBeenCalledWith(
      'staff-session',
      expect.objectContaining({ orderId: 7001, specimenId: 8001 }),
    ))
    expect(vi.mocked(createProcedureReport).mock.calls[0]?.[1]).not.toHaveProperty('specimenNumber')
    expect(onChange).toHaveBeenCalledWith(detail)
  })
})
