// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import PatientTelehealthWorkspace from './PatientTelehealthWorkspace.tsx'
import {
  getPatientQueueStatus,
  getPatientReadiness,
  getPatientRequestHistory,
  fastTrackPatientRequestToQueue,
  listPatientRequests,
  type TelehealthPatientQueueStatus,
  type TelehealthReadiness,
  type TelehealthRequest,
} from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return {
    ...original,
    getPatientQueueStatus: vi.fn(),
    getPatientReadiness: vi.fn(),
    getPatientRequestHistory: vi.fn(),
    fastTrackPatientRequestToQueue: vi.fn(),
    listPatientRequests: vi.fn(),
  }
})

const request = {
  requestId: '00000000-0000-4000-8000-000000000001',
  status: 'OperationalReview',
  complaintCategory: 'migraine',
  triageOutcome: 'TelehealthEligible',
  version: 2,
  stateCode: 'GA',
  createdAt: '2026-08-31T12:00:00Z',
  updatedAt: '2026-08-31T12:00:00Z',
  readyAt: null,
  allowedActions: ['await-operational-review'],
  coverage: null,
} satisfies TelehealthRequest

const readiness = {
  requestId: request.requestId,
  requestVersion: request.version,
  status: request.status,
  patientDetails: {
    displayName: 'Synthetic Patient',
    dateOfBirth: '1990-01-01',
    email: 'synthetic@example.test',
    phone: '555-0100',
    address: '1 Synthetic Way',
    fingerprint: 'a'.repeat(64),
    missingFields: [],
  },
  clinicalSummary: {
    activeMedicationCount: 0,
    activeAllergyCount: 0,
    historyAvailable: true,
    fingerprint: 'b'.repeat(64),
  },
  coverageOptions: [{
    coverageToken: 'coverage-1',
    coverageType: 'Medical',
    provider: 'Synthetic Health',
    planName: 'Demo Plan',
    maskedPolicyNumber: '***1',
    maskedGroupNumber: '***2',
    subscriberRelationship: 'Self',
    fingerprint: 'c'.repeat(64),
  }],
  acknowledgment: {
    kind: 'Synthetic',
    packageKey: 'SYNTHETIC_ACKNOWLEDGMENT',
    packageVersion: 1,
    contentHash: 'd'.repeat(64),
    title: 'Synthetic acknowledgment',
    statements: ['Demonstration only.'],
    legalEffect: false,
  },
  blockingReasons: [],
} satisfies TelehealthReadiness

const queueStatus = {
  requestId: request.requestId,
  requestStatus: request.status,
  requestVersion: 3,
  phase: 'Reviewing',
  headline: 'Reviewing your request',
  detail: 'Synthetic practice review is pending.',
  approximateRequestsAhead: null,
  positionIsApproximate: false,
  waitEstimateAvailable: false,
  waitEstimateMessage: 'No estimate is available.',
  requestUpdatedAt: '2026-08-31T12:05:00Z',
  snapshotAt: '2026-08-31T12:05:00Z',
  refreshAfterSeconds: 5,
  realtimeAvailable: false,
  safetyActions: [],
} satisfies TelehealthPatientQueueStatus

describe('PatientTelehealthWorkspace', () => {
  beforeEach(() => {
    vi.mocked(listPatientRequests).mockResolvedValue([request])
    vi.mocked(getPatientReadiness).mockResolvedValue(readiness)
    vi.mocked(getPatientRequestHistory).mockResolvedValue({ requestId: request.requestId, entries: [] })
    vi.mocked(getPatientQueueStatus).mockResolvedValue(queueStatus)
    vi.mocked(fastTrackPatientRequestToQueue).mockResolvedValue({
      ...request,
      status: 'Queued',
      version: 100,
      readyAt: '2026-08-31T12:05:00Z',
      allowedActions: ['await-clinician', 'cancel-request'],
    })
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('keeps readiness confirmations while an unchanged workflow stage receives live status updates', async () => {
    render(<PatientTelehealthWorkspace />)

    await screen.findByRole('button', { name: 'Submit readiness for synthetic verification' })
    fireEvent.click(screen.getByLabelText(/confirm these current demographic/i))
    fireEvent.click(screen.getByLabelText(/reviewed this synthetic clinical-list summary/i))
    fireEvent.click(screen.getByLabelText(/entered synthetic demonstration data only/i))
    fireEvent.click(screen.getByLabelText(/selected and confirmed this existing synthetic coverage/i))
    fireEvent.click(screen.getByLabelText(/affirmatively accept this exact synthetic acknowledgment/i))

    const submit = screen.getByRole('button', { name: 'Submit readiness for synthetic verification' })
    expect(submit).toBeEnabled()

    await waitFor(() => expect(vi.mocked(getPatientQueueStatus)).toHaveBeenCalledTimes(1))
    await act(async () => {
      document.dispatchEvent(new Event('visibilitychange'))
    })
    await waitFor(() => expect(vi.mocked(getPatientQueueStatus)).toHaveBeenCalledTimes(2))

    expect(screen.getByLabelText(/confirm these current demographic/i)).toBeChecked()
    expect(screen.getByLabelText(/reviewed this synthetic clinical-list summary/i)).toBeChecked()
    expect(screen.getByLabelText(/entered synthetic demonstration data only/i)).toBeChecked()
    expect(screen.getByLabelText(/selected and confirmed this existing synthetic coverage/i)).toBeChecked()
    expect(screen.getByLabelText(/affirmatively accept this exact synthetic acknowledgment/i)).toBeChecked()
    expect(submit).toBeEnabled()
  })

  it('lets an operational-review request join the physician demo queue with its current version', async () => {
    let resolveQueueStatus: ((value: TelehealthPatientQueueStatus) => void) | undefined
    vi.mocked(getPatientQueueStatus).mockImplementationOnce(() => new Promise<TelehealthPatientQueueStatus>((resolve) => {
      resolveQueueStatus = resolve
    }))
    render(<PatientTelehealthWorkspace />)

    const handoff = await screen.findByRole('button', { name: 'Join physician demo queue' })
    fireEvent.click(handoff)

    await waitFor(() => expect(fastTrackPatientRequestToQueue).toHaveBeenCalledWith(request.requestId, expect.any(Number)))
    await waitFor(() => expect(screen.getAllByText('Queued').length).toBeGreaterThan(0))
    await act(async () => resolveQueueStatus?.({ ...queueStatus, requestVersion: request.version }))
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Join physician demo queue' })).not.toBeInTheDocument())
  })
})
