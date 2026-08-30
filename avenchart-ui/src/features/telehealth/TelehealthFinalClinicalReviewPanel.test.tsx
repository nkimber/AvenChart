// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TelehealthFinalClinicalReviewPanel from './TelehealthFinalClinicalReviewPanel.tsx'
import { getTelehealthFinalClinicalReview, recordTelehealthFinalClinicalReview, type TelehealthFinalClinicalReviewWorkspace } from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, getTelehealthFinalClinicalReview: vi.fn(), recordTelehealthFinalClinicalReview: vi.fn() }
})

const workspace: TelehealthFinalClinicalReviewWorkspace = {
  consultationId: 'consultation-1', asOf: '2026-08-30T16:00:00Z',
  documentation: { version: 3, hasAnyContent: true, subjectivePresent: true, objectivePresent: true, assessmentPresent: true, planPresent: true },
  safetyDisposition: { version: 2, dispositionCode: 'TreatedTelehealth', adequateEvaluationCompleted: true, followUpOwnerPresent: true, followUpTimeframePresent: true, nextStepInstructionsPresent: true, warningEscalationInstructionsPresent: true, communicationMethod: 'DiscussedDuringSyntheticConsultation', communicationCompleted: true, locationCallbackReconfirmed: false, emergencyInstructionProvided: false, emergencyHandoffStatusPresent: false, contactAttemptSummaryPresent: false },
  currentPrescriptionOrderId: null, currentReview: null, reviewEnabled: true,
  encounterSignatureEnabled: false, completionEnabled: false, claimCreationEnabled: false, claimSubmissionEnabled: false,
  limitations: ['No signature, completion, delivery, billing, or claim is available.'],
}

describe('TelehealthFinalClinicalReviewPanel', () => {
  beforeEach(() => { vi.clearAllMocks(); vi.mocked(getTelehealthFinalClinicalReview).mockResolvedValue(workspace) })

  it('requires every acknowledgment and records only bounded synthetic evidence', async () => {
    vi.mocked(recordTelehealthFinalClinicalReview).mockResolvedValue({
      reviewId: 'review-1', version: 1, documentationVersion: 3, dispositionVersion: 2, prescriptionOrderId: null,
      reviewedAt: '2026-08-30T16:01:00Z', contentHash: 'a'.repeat(64), legalEffect: false, encounterSignatureCreated: false,
      completionCreated: false, patientDeliveryCreated: false, billingCreated: false, claimCreated: false, externalDestinationContacted: false,
    })
    render(<TelehealthFinalClinicalReviewPanel consultationId="consultation-1" />)
    expect(await screen.findByText(/not a legal encounter signature/i)).toBeInTheDocument()
    const button = screen.getByRole('button', { name: /record final clinical-review evidence/i })
    expect(button).toBeDisabled()
    screen.getAllByRole('checkbox').forEach((checkbox) => fireEvent.click(checkbox))
    expect(button).toBeEnabled()
    fireEvent.click(button)
    await waitFor(() => expect(recordTelehealthFinalClinicalReview).toHaveBeenCalledTimes(1))
    expect(await screen.findByText(/no legal, delivery, billing, or claim effect/i)).toBeInTheDocument()
    expect(recordTelehealthFinalClinicalReview).toHaveBeenCalledWith('consultation-1', expect.objectContaining({ expectedDocumentationVersion: 3, expectedDispositionVersion: 2 }), expect.any(String))
  })

  it('prevents recording when current source evidence is incomplete', async () => {
    vi.mocked(getTelehealthFinalClinicalReview).mockResolvedValue({ ...workspace, reviewEnabled: false, safetyDisposition: null })
    render(<TelehealthFinalClinicalReviewPanel consultationId="consultation-1" />)
    expect(await screen.findByText(/All four SOAP sections and a safety-disposition draft/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /record final clinical-review evidence/i })).toBeDisabled()
  })
})
