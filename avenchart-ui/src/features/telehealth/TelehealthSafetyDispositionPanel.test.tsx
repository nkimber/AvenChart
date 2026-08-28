// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TelehealthSafetyDispositionPanel from './TelehealthSafetyDispositionPanel.tsx'
import {
  getTelehealthSafetyDispositionDraft,
  recordTelehealthSafetyDispositionDraft,
  type TelehealthSafetyDispositionDraft,
  type TelehealthSafetyDispositionWorkspace,
} from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, getTelehealthSafetyDispositionDraft: vi.fn(), recordTelehealthSafetyDispositionDraft: vi.fn() }
})

const draft: TelehealthSafetyDispositionDraft = {
  version: 1,
  dispositionCode: 'TreatedTelehealth',
  adequateEvaluationCompleted: true,
  followUpOwner: 'Patient',
  followUpTimeframe: 'within two synthetic days',
  nextStepInstructions: 'Physician-authored synthetic next step.',
  warningEscalationInstructions: 'Physician-authored synthetic warning and escalation instruction.',
  communicationMethod: 'DiscussedDuringSyntheticConsultation',
  communicationCompleted: true,
  locationCallbackReconfirmed: false,
  emergencyInstructionProvided: false,
  emergencyHandoffStatus: null,
  contactAttemptSummary: null,
  recordedAt: '2026-08-27T13:00:00Z',
  legalEffect: false,
  signed: false,
  finalized: false,
  patientDelivered: false,
}

const workspace: TelehealthSafetyDispositionWorkspace = {
  consultationId: 'consultation-1',
  consultationStatus: 'MediaEnded',
  asOf: '2026-08-27T13:00:00Z',
  dispositions: [
    { code: 'TreatedTelehealth', label: 'Treated by telehealth', requiresAdequateEvaluation: true, requiresLocationCallbackReconfirmation: false, requiresEmergencyFacts: false, requiresContactAttemptSummary: false },
    { code: 'EmergencyTransferRecommended', label: 'Emergency transfer recommended', requiresAdequateEvaluation: true, requiresLocationCallbackReconfirmation: true, requiresEmergencyFacts: true, requiresContactAttemptSummary: false },
  ],
  followUpOwners: ['Patient', 'EmergencyServices'],
  communicationMethods: ['DiscussedDuringSyntheticConsultation', 'NotYetCommunicated'],
  emergencyHandoffStatuses: ['RecommendedOnly', 'UnableToConfirm'],
  currentDraft: null,
  signingEnabled: false,
  patientDeliveryEnabled: false,
  completionEnabled: false,
  limitations: ['No patient delivery or external handoff is created.'],
}

describe('TelehealthSafetyDispositionPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTelehealthSafetyDispositionDraft).mockResolvedValue(workspace)
    vi.mocked(recordTelehealthSafetyDispositionDraft).mockResolvedValue(draft)
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('00000000-0000-4000-8000-000000008888')
  })

  it('has no clinical defaults and records only complete physician-authored draft content', async () => {
    render(<TelehealthSafetyDispositionPanel consultationId="consultation-1" />)
    expect(await screen.findByText('No safety-disposition draft has been recorded.')).toBeInTheDocument()
    expect(screen.getByLabelText('Disposition')).toHaveValue('')
    fillTreatedDraft()
    fireEvent.click(screen.getByRole('button', { name: 'Record safety draft' }))

    await waitFor(() => expect(recordTelehealthSafetyDispositionDraft).toHaveBeenCalledWith(
      'consultation-1',
      expect.objectContaining({
        expectedVersion: 0,
        dispositionCode: 'TreatedTelehealth',
        nextStepInstructions: draft.nextStepInstructions,
        warningEscalationInstructions: draft.warningEscalationInstructions,
        syntheticDataConfirmed: true,
      }),
      '00000000-0000-4000-8000-000000008888',
    ))
    expect(await screen.findByText(/not signed, finalized, or delivered/i)).toBeInTheDocument()
    expect(screen.getByText(/Signed: no.*Patient delivered: no.*Legal effect: no/i)).toBeInTheDocument()
  })

  it('reveals and requires explicit emergency facts without claiming external verification', async () => {
    render(<TelehealthSafetyDispositionPanel consultationId="consultation-1" />)
    await screen.findByText('No safety-disposition draft has been recorded.')
    fireEvent.change(screen.getByLabelText('Disposition'), { target: { value: 'EmergencyTransferRecommended' } })
    expect(screen.getByRole('group', { name: 'Emergency draft facts' })).toBeInTheDocument()
    expect(screen.getByText(/has not verified any external connection or transfer/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Record safety draft' })).toBeDisabled()
  })

  it('focuses an ambiguous failure and preserves content plus retry identity', async () => {
    vi.mocked(recordTelehealthSafetyDispositionDraft)
      .mockRejectedValueOnce(new Error('Synthetic disposition service unavailable.'))
      .mockResolvedValueOnce(draft)
    render(<TelehealthSafetyDispositionPanel consultationId="consultation-1" />)
    await screen.findByText('No safety-disposition draft has been recorded.')
    fillTreatedDraft()
    const save = screen.getByRole('button', { name: 'Record safety draft' })
    fireEvent.click(save)
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveFocus()
    expect(screen.getByLabelText('Physician-authored next-step instructions')).toHaveValue(draft.nextStepInstructions)
    expect(screen.getByLabelText(/synthetic demonstration data only/i)).toBeChecked()
    fireEvent.click(save)
    await waitFor(() => expect(recordTelehealthSafetyDispositionDraft).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordTelehealthSafetyDispositionDraft).mock.calls[0][2])
      .toBe(vi.mocked(recordTelehealthSafetyDispositionDraft).mock.calls[1][2])
  })
})

function fillTreatedDraft() {
  fireEvent.change(screen.getByLabelText('Disposition'), { target: { value: 'TreatedTelehealth' } })
  fireEvent.click(screen.getByLabelText(/available evaluation was adequate/i))
  fireEvent.change(screen.getByLabelText('Follow-up owner'), { target: { value: 'Patient' } })
  fireEvent.change(screen.getByLabelText('Physician-authored follow-up timeframe'), { target: { value: draft.followUpTimeframe } })
  fireEvent.change(screen.getByLabelText('Physician-authored next-step instructions'), { target: { value: draft.nextStepInstructions } })
  fireEvent.change(screen.getByLabelText('Physician-authored warning signs and escalation instructions'), { target: { value: draft.warningEscalationInstructions } })
  fireEvent.change(screen.getByLabelText('Communication method'), { target: { value: 'DiscussedDuringSyntheticConsultation' } })
  fireEvent.click(screen.getByLabelText(/completed this selected synthetic communication method/i))
  fireEvent.click(screen.getByLabelText(/synthetic demonstration data only/i))
}
