// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TelehealthCompletionPrerequisitesPanel from './TelehealthCompletionPrerequisitesPanel.tsx'
import { getTelehealthCompletionPrerequisites, type TelehealthCompletionPrerequisites } from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, getTelehealthCompletionPrerequisites: vi.fn() }
})

const review: TelehealthCompletionPrerequisites = {
  consultationId: 'consultation-1',
  consultationStatus: 'MediaEnded',
  requestStatus: 'WrapUp',
  shiftStatus: 'WrapUp',
  appointmentStatus: '>',
  asOf: '2026-08-27T15:00:00Z',
  documentation: {
    version: 0,
    hasAnyContent: false,
    subjectivePresent: false,
    objectivePresent: false,
    assessmentPresent: false,
    planPresent: false,
  },
  safetyDisposition: null,
  pharmacyChoice: null,
  currentFinalClinicalReview: null,
  structuralEvidencePresent: false,
  productBlockers: [
    'DOCUMENTATION_DRAFT_MISSING',
    'SAFETY_DISPOSITION_DRAFT_MISSING',
    'FINAL_CLINICAL_REVIEW_NOT_RECORDED',
    'SIGNATURE_FINALIZATION_NOT_IMPLEMENTED',
    'ATOMIC_DOWNSTREAM_OWNERSHIP_NOT_IMPLEMENTED',
  ],
  signingEnabled: false,
  completionEnabled: false,
  patientDeliveryEnabled: false,
  downstreamCreationEnabled: false,
  limitations: ['Field presence is structural evidence only.'],
}

describe('TelehealthCompletionPrerequisitesPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTelehealthCompletionPrerequisites).mockResolvedValue(review)
  })

  it('shows missing structural drafts, optional pharmacy, and disabled consequential capabilities', async () => {
    render(<TelehealthCompletionPrerequisitesPanel consultationId="consultation-1" />)

    expect(await screen.findByText('No safety-disposition draft recorded.')).toBeInTheDocument()
    expect(screen.getByText(/None recorded.*optional.*not a blocker/i)).toBeInTheDocument()
    expect(screen.getByText('documentation draft missing')).toBeInTheDocument()
    expect(screen.getByText(/signing, encounter completion, patient delivery, and downstream creation/i)).toBeInTheDocument()
    expect(screen.getByText(/This is not a clinical readiness result/i)).toBeInTheDocument()
  })

  it('does not present complete structural drafts as clinical readiness or enable completion', async () => {
    vi.mocked(getTelehealthCompletionPrerequisites).mockResolvedValue({
      ...review,
      documentation: { version: 2, hasAnyContent: true, subjectivePresent: true, objectivePresent: false, assessmentPresent: true, planPresent: true },
      safetyDisposition: {
        version: 1,
        dispositionCode: 'TreatedTelehealth',
        adequateEvaluationCompleted: true,
        followUpOwnerPresent: true,
        followUpTimeframePresent: true,
        nextStepInstructionsPresent: true,
        warningEscalationInstructionsPresent: true,
        communicationMethod: 'DiscussedDuringSyntheticConsultation',
        communicationCompleted: true,
        locationCallbackReconfirmed: false,
        emergencyInstructionProvided: false,
        emergencyHandoffStatusPresent: false,
        contactAttemptSummaryPresent: false,
      },
      pharmacyChoice: { version: 1, patientChoiceConfirmed: true },
      structuralEvidencePresent: true,
      productBlockers: review.productBlockers.slice(2),
    })

    render(<TelehealthCompletionPrerequisitesPanel consultationId="consultation-1" />)

    expect(await screen.findByText(/Treated telehealth.*version 1/i)).toBeInTheDocument()
    expect(screen.getByText(/Structural drafts recorded: yes/i)).toBeInTheDocument()
    expect(screen.getByText(/does not judge clinical completeness/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /sign|complete|finalize|deliver/i })).not.toBeInTheDocument()
  })

  it('focuses a load failure and supports an explicit read-only retry', async () => {
    vi.mocked(getTelehealthCompletionPrerequisites)
      .mockRejectedValueOnce(new Error('Synthetic review unavailable.'))
      .mockResolvedValueOnce(review)
    render(<TelehealthCompletionPrerequisitesPanel consultationId="consultation-1" />)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveFocus()
    fireEvent.click(screen.getByRole('button', { name: 'Reload review' }))
    await waitFor(() => expect(getTelehealthCompletionPrerequisites).toHaveBeenCalledTimes(2))
    expect(await screen.findByText(/No signing or completion action occurred/i)).toBeInTheDocument()
  })
})
