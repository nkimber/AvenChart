// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TelehealthProfessionalClaimPreparationPanel from './TelehealthProfessionalClaimPreparationPanel.tsx'
import { getTelehealthProfessionalClaimPreparation, type TelehealthProfessionalClaimPreparationWorkspace } from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, getTelehealthProfessionalClaimPreparation: vi.fn() }
})

const workspace: TelehealthProfessionalClaimPreparationWorkspace = {
  consultationId: 'consultation-1', evaluatedAt: '2026-08-30T16:00:00Z',
  currentDocumentationVersion: 2, currentDispositionVersion: 1, currentFinalClinicalReviewVersion: 1,
  currentFinalClinicalReviewRecorded: true, encounterSignatureRecorded: false,
  codingEvidenceRecorded: false, billingProviderEvidenceRecorded: false, feeScheduleEvidenceRecorded: false,
  humanBillingApprovalRecorded: false, adapterMode: 'NON_PRODUCTION', targetStandard: 'ASC_X12N_837P_005010X222A1',
  claimPreparationEnabled: false, claimSubmissionEnabled: false,
  currentPreparation: null,
  blockers: ['A governed encounter signature/finalization is required.'],
  limitations: ['This creates no claim, billing item, transaction, or gateway call.'],
}

describe('TelehealthProfessionalClaimPreparationPanel', () => {
  beforeEach(() => { vi.clearAllMocks(); vi.mocked(getTelehealthProfessionalClaimPreparation).mockResolvedValue(workspace) })

  it('shows required blockers without offering claim creation or submission', async () => {
    render(<TelehealthProfessionalClaimPreparationPanel consultationId="consultation-1" />)
    expect(await screen.findByText(/ASC_X12N_837P_005010X222A1/i)).toBeInTheDocument()
    expect(screen.getByText(/encounter signature/i)).toBeInTheDocument()
    expect(screen.getByText(/cannot create or submit a claim/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /prepare|submit|create claim/i })).not.toBeInTheDocument()
    expect(getTelehealthProfessionalClaimPreparation).toHaveBeenCalledWith('consultation-1', expect.any(AbortSignal))
  })
})
