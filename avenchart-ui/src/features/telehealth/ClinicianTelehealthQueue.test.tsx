// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import ClinicianTelehealthQueue from './ClinicianTelehealthQueue.tsx'
import { getClinicianActiveWork, getTelehealthConsultationWorkspace, listClinicianQueue, type TelehealthClinicianActiveWork, type TelehealthConsultationWorkspace } from './api.ts'

vi.mock('./api.ts', async (original) => ({
  ...await original<typeof import('./api.ts')>(),
  getClinicianActiveWork: vi.fn(), getTelehealthConsultationWorkspace: vi.fn(), listClinicianQueue: vi.fn(),
}))
vi.mock('./TelehealthConversationPanel.tsx', () => ({ default: () => null }))
vi.mock('./TelehealthPharmacyChoicePanel.tsx', () => ({ default: () => null }))
vi.mock('./TelehealthPrescriptionPreparationPanel.tsx', () => ({ default: () => null }))
vi.mock('./TelehealthSafetyDispositionPanel.tsx', () => ({ default: () => null }))
vi.mock('./TelehealthFinalClinicalReviewPanel.tsx', () => ({ default: () => null }))
vi.mock('./TelehealthEncounterFinalizationPanel.tsx', () => ({ default: () => null }))
vi.mock('./TelehealthProfessionalClaimPreparationPanel.tsx', () => ({ default: () => null }))
vi.mock('./TelehealthCompletionPrerequisitesPanel.tsx', () => ({ default: () => null }))
vi.mock('./TelehealthSyntheticVisitClosurePanel.tsx', () => ({ default: () => <p>Recovered closure action</p> }))

const activeWork = {
  shift: { shiftId: 'shift', status: 'Busy', facilityId: 10, clinicianStaffId: 1, startedAt: '2026-09-12T12:00:00Z', version: 2 },
  reservation: { reservationId: 'reservation', requestId: 'request', queueEntryId: 'queue', shiftId: 'shift', clinicianStaffId: 1, reservedAt: '2026-09-12T12:00:00Z', leaseExpiresAt: '2026-09-12T12:05:00Z', status: 'Released', requestVersion: 8, applicantOriginated: false },
  consultationId: 'consultation',
} satisfies TelehealthClinicianActiveWork
const workspace = {
  consultationId: 'consultation', consultationStatus: 'InConsultation', consultationVersion: 1,
  mediaEndedAt: null, modality: 'SYNTHETIC_VIDEO', startedAt: '2026-09-12T12:01:00Z', asOf: '2026-09-12T12:01:00Z', readOnly: true,
  patient: { displayName: 'Synthetic Patient', dateOfBirth: '1990-01-01', age: 36, recordedSex: null, callbackPhone: '555-0100' },
  visit: { patientLocationState: 'GA', complaintCategory: 'sleep', complaintSummary: 'Synthetic sleep demo', symptomDuration: '1-3-days', triageOutcome: 'TelehealthEligible' },
  allergies: [], medications: [], problems: [],
  documentation: { version: 1, savedAt: null, savedBy: null, isLocked: false, isSigned: false, isFinal: false, subjective: 'Saved synthetic history', objective: null, assessment: null, plan: null },
  documentationEnabled: true, prescribingEnabled: false, claimsEnabled: false, completionEnabled: false, limitations: [],
} satisfies TelehealthConsultationWorkspace

beforeEach(() => {
  vi.mocked(listClinicianQueue).mockResolvedValue([])
  vi.mocked(getClinicianActiveWork).mockResolvedValue(activeWork)
  vi.mocked(getTelehealthConsultationWorkspace).mockResolvedValue(workspace)
})
afterEach(() => { vi.useRealTimers(); vi.clearAllMocks() })

it('resumes a consultation and does not overwrite unsaved notes on the ten-second queue poll', async () => {
  vi.useFakeTimers()
  render(<ClinicianTelehealthQueue />)
  await act(async () => { await vi.advanceTimersByTimeAsync(0) })
  expect(screen.getByLabelText('Subjective')).toHaveValue('Saved synthetic history')
  fireEvent.change(screen.getByLabelText('Subjective'), { target: { value: 'Unsaved synthetic history' } })
  await act(async () => { await vi.advanceTimersByTimeAsync(10_000) })
  expect(getClinicianActiveWork).toHaveBeenCalledTimes(2)
  expect(getTelehealthConsultationWorkspace).toHaveBeenCalledTimes(1)
  expect(screen.getByLabelText('Subjective')).toHaveValue('Unsaved synthetic history')
  expect(screen.getByRole('button', { name: 'Save unsigned draft' })).toBeEnabled()
})

it('restores saved wrap-up and the closure action after an encounter lock', async () => {
  vi.mocked(getClinicianActiveWork).mockResolvedValue({ ...activeWork, shift: { ...activeWork.shift, status: 'WrapUp' } })
  vi.mocked(getTelehealthConsultationWorkspace).mockResolvedValue({ ...workspace, consultationStatus: 'WrapUp', documentation: { ...workspace.documentation, isLocked: true } })
  render(<ClinicianTelehealthQueue />)
  await screen.findByText('Recovered closure action')
  expect(screen.getByLabelText('Subjective')).toBeDisabled()
  expect(screen.queryByText(/Reservation lease expires/)).not.toBeInTheDocument()
})

it('preserves the open visit and typed draft if the queue refresh fails', async () => {
  render(<ClinicianTelehealthQueue />)
  await screen.findByDisplayValue('Saved synthetic history')
  fireEvent.change(screen.getByLabelText('Subjective'), { target: { value: 'Retain this draft' } })
  vi.mocked(getClinicianActiveWork).mockRejectedValueOnce(new Error('Queue temporarily unavailable'))
  fireEvent.click(screen.getByRole('button', { name: /^Refresh$/ }))
  await screen.findByText('Queue temporarily unavailable')
  expect(screen.getByLabelText('Subjective')).toHaveValue('Retain this draft')
})
