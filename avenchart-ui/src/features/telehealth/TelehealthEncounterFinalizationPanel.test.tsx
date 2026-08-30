// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TelehealthEncounterFinalizationPanel from './TelehealthEncounterFinalizationPanel.tsx'
import { finalizeTelehealthEncounter, getTelehealthFinalClinicalReview, type TelehealthFinalClinicalReviewWorkspace } from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, getTelehealthFinalClinicalReview: vi.fn(), finalizeTelehealthEncounter: vi.fn() }
})

const workspace: TelehealthFinalClinicalReviewWorkspace = {
  consultationId: 'consultation-1', asOf: '2026-08-30T16:00:00Z',
  documentation: { version: 3, hasAnyContent: true, subjectivePresent: true, objectivePresent: true, assessmentPresent: true, planPresent: true },
  safetyDisposition: { version: 2, dispositionCode: 'TreatedTelehealth', adequateEvaluationCompleted: true, followUpOwnerPresent: true, followUpTimeframePresent: true, nextStepInstructionsPresent: true, warningEscalationInstructionsPresent: true, communicationMethod: 'DiscussedDuringSyntheticConsultation', communicationCompleted: true, locationCallbackReconfirmed: false, emergencyInstructionProvided: false, emergencyHandoffStatusPresent: false, contactAttemptSummaryPresent: false },
  currentPrescriptionOrderId: null, currentReview: { reviewId: 'review-1', version: 4, documentationVersion: 3, dispositionVersion: 2, prescriptionOrderId: null, reviewedAt: '2026-08-30T16:01:00Z', contentHash: 'a'.repeat(64), legalEffect: false, encounterSignatureCreated: false, completionCreated: false, patientDeliveryCreated: false, billingCreated: false, claimCreated: false, externalDestinationContacted: false },
  reviewEnabled: true, encounterSignatureEnabled: false, completionEnabled: false, claimCreationEnabled: false, claimSubmissionEnabled: false, limitations: [],
}

describe('TelehealthEncounterFinalizationPanel', () => {
  beforeEach(() => { vi.clearAllMocks(); vi.mocked(getTelehealthFinalClinicalReview).mockResolvedValue(workspace) })
  it('requires both confirmations before the bounded synthetic lock command', async () => {
    vi.mocked(finalizeTelehealthEncounter).mockResolvedValue({ encounterSignatureId: 42, signedAt: '2026-08-30T16:02:00Z', documentationVersion: 3, dispositionVersion: 2, finalClinicalReviewVersion: 4, encounterLocked: true, legalEffect: false, completionCreated: false, patientDeliveryCreated: false, billingCreated: false, claimCreated: false, externalDestinationContacted: false, limitations: [] })
    render(<TelehealthEncounterFinalizationPanel consultationId="consultation-1" />)
    expect(await screen.findByText(/not a legal signature/i)).toBeInTheDocument()
    const button = screen.getByRole('button', { name: /record synthetic encounter lock/i })
    expect(button).toBeDisabled()
    screen.getAllByRole('checkbox').forEach((checkbox) => fireEvent.click(checkbox))
    expect(button).toBeEnabled()
    fireEvent.click(button)
    await waitFor(() => expect(finalizeTelehealthEncounter).toHaveBeenCalledWith('consultation-1', expect.objectContaining({ expectedDocumentationVersion: 3, expectedDispositionVersion: 2, expectedFinalClinicalReviewVersion: 4 })))
  })
})
